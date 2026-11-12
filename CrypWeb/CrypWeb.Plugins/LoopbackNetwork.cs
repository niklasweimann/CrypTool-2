// Networking for the network plugins (NetworkSender, NetworkReceiver) in the browser.
//
// A browser page cannot open UDP or TCP sockets. The CT2 templates with network components send and receive
// within the same workspace (sender -> receiver on 127.0.0.1), so these classes connect components in memory:
// a port number identifies the receiving end, the IP address is ignored. Sending to a port nobody listens on
// fails like a refused connection.
//
// The types are declared in the global namespace, so plugin code that writes "UdpClient" or "TcpClient" (with
// "using System.Net.Sockets;") gets them: types of the global namespace are found before the types of imported
// namespaces. The same technique as ManagedCrypto.cs.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

/// <summary>The in-memory "network": open UDP ports and listening TCP servers, by port number.</summary>
internal static class LoopbackNetwork
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<int, UdpClient> UdpPorts = new Dictionary<int, UdpClient>();
    private static readonly Dictionary<int, TcpListener> TcpPorts = new Dictionary<int, TcpListener>();
    private static int _nextEphemeralPort = 49152;

    internal static readonly IPAddress Loopback = IPAddress.Loopback;

    internal static int NextEphemeralPort() => Interlocked.Increment(ref _nextEphemeralPort);

    internal static void BindUdp(int port, UdpClient client)
    {
        lock (Gate)
        {
            if (UdpPorts.ContainsKey(port))
            {
                throw new LoopbackSocketException((int)SocketError.AddressAlreadyInUse);
            }
            UdpPorts[port] = client;
        }
    }

    internal static void UnbindUdp(int port, UdpClient client)
    {
        lock (Gate)
        {
            if (UdpPorts.TryGetValue(port, out UdpClient bound) && ReferenceEquals(bound, client)) UdpPorts.Remove(port);
        }
    }

    internal static UdpClient FindUdp(int port)
    {
        lock (Gate) return UdpPorts.GetValueOrDefault(port);
    }

    internal static void Listen(int port, TcpListener listener)
    {
        lock (Gate)
        {
            if (TcpPorts.ContainsKey(port))
            {
                throw new LoopbackSocketException((int)SocketError.AddressAlreadyInUse);
            }
            TcpPorts[port] = listener;
        }
    }

    internal static void StopListening(int port, TcpListener listener)
    {
        lock (Gate)
        {
            if (TcpPorts.TryGetValue(port, out TcpListener bound) && ReferenceEquals(bound, listener)) TcpPorts.Remove(port);
        }
    }

    internal static TcpListener FindListener(int port)
    {
        lock (Gate) return TcpPorts.GetValueOrDefault(port);
    }
}

/// <summary>
/// A socket error with the Windows error number in <see cref="ErrorCode"/>. Plugins compare it with Windows numbers
/// (10004 = interrupted when a socket is closed during a blocking call); the system SocketException reports the
/// platform's own number there on Linux, macOS and in the browser.
/// </summary>
internal sealed class LoopbackSocketException : SocketException
{
    internal LoopbackSocketException(int errorCode) : base(errorCode) { }

    public override int ErrorCode => (int)SocketErrorCode;
}

/// <summary>Result of an asynchronous loopback operation (completed when its value is known).</summary>
internal sealed class LoopbackAsyncResult : IAsyncResult
{
    private readonly ManualResetEvent _done = new ManualResetEvent(false);

    internal LoopbackAsyncResult(object state) => AsyncState = state;

    public object AsyncState { get; }

    public WaitHandle AsyncWaitHandle => _done;

    public bool CompletedSynchronously { get; internal set; }

    public bool IsCompleted { get; private set; }

    internal object Value { get; private set; }

    internal Exception Error { get; private set; }

    internal void Complete(object value, Exception error, AsyncCallback callback)
    {
        Value = value;
        Error = error;
        IsCompleted = true;
        _done.Set();
        callback?.Invoke(this);
    }

    internal object End()
    {
        // callers may have disposed AsyncWaitHandle already ("using (result.AsyncWaitHandle)"), so only wait when needed
        if (!IsCompleted) _done.WaitOne();
        if (Error != null) throw Error;
        return Value;
    }
}

/// <summary>A UDP endpoint; datagrams to its port arrive in its queue.</summary>
public class UdpClient : IDisposable
{
    private readonly BlockingCollection<(byte[] Data, IPEndPoint From)> _inbox = new BlockingCollection<(byte[], IPEndPoint)>();
    private int _port;

    public UdpClient() { }

    public UdpClient(int port) => Bind(port);

    public UdpClient(IPEndPoint localEndPoint) => Bind(localEndPoint.Port);

    private void Bind(int port)
    {
        _port = port == 0 ? LoopbackNetwork.NextEphemeralPort() : port;
        LoopbackNetwork.BindUdp(_port, this);
    }

    private IPEndPoint LocalEndPoint
    {
        get
        {
            if (_port == 0) Bind(0);    // a sending client gets a port on its first datagram, as in the OS
            return new IPEndPoint(LoopbackNetwork.Loopback, _port);
        }
    }

    public int Send(byte[] datagram, int bytes, IPEndPoint endPoint)
    {
        UdpClient receiver = LoopbackNetwork.FindUdp(endPoint.Port);
        byte[] copy = new byte[bytes];
        Array.Copy(datagram, copy, bytes);
        // UDP: a datagram to a port nobody listens on is simply lost
        if (receiver != null && !receiver._inbox.IsAddingCompleted) receiver._inbox.Add((copy, LocalEndPoint));
        return bytes;
    }

    public int Send(byte[] datagram, int bytes, string hostname, int port) => Send(datagram, bytes, new IPEndPoint(LoopbackNetwork.Loopback, port));

    /// <summary>Waits for the next datagram; when the client is closed meanwhile, fails like an interrupted socket call.</summary>
    public byte[] Receive(ref IPEndPoint remoteEP)
    {
        try
        {
            var (data, from) = _inbox.Take();
            remoteEP = from;
            return data;
        }
        catch (InvalidOperationException)
        {
            throw new LoopbackSocketException((int)SocketError.Interrupted);     // 10004, as when a blocking call is cancelled
        }
    }

    public void Close() => Dispose();

    public void Dispose()
    {
        _inbox.CompleteAdding();
        if (_port != 0) LoopbackNetwork.UnbindUdp(_port, this);
    }
}

/// <summary>One end of a loopback TCP connection; bytes written by the peer arrive in its queue.</summary>
public class Socket : IDisposable
{
    private readonly BlockingCollection<byte[]> _inbox = new BlockingCollection<byte[]>();
    private byte[] _pending;
    private int _pendingOffset;

    internal Socket(IPEndPoint local, IPEndPoint remote)
    {
        LocalEndPoint = local;
        RemoteEndPoint = remote;
    }

    internal Socket Peer { get; set; }

    public EndPoint LocalEndPoint { get; }

    public EndPoint RemoteEndPoint { get; }

    public bool Connected => !_inbox.IsAddingCompleted;

    internal void Deliver(byte[] data)
    {
        if (!_inbox.IsAddingCompleted) _inbox.Add(data);
    }

    /// <summary>Reads what arrived (waits for data); 0 when the connection was closed.</summary>
    public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (_pending == null)
        {
            if (!_inbox.TryTake(out _pending, Timeout.Infinite)) return 0;
            _pendingOffset = 0;
        }
        int count = Math.Min(size, _pending.Length - _pendingOffset);
        Array.Copy(_pending, _pendingOffset, buffer, offset, count);
        _pendingOffset += count;
        if (_pendingOffset >= _pending.Length) _pending = null;
        return count;
    }

    public int Receive(byte[] buffer) => Receive(buffer, 0, buffer.Length, SocketFlags.None);

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags, AsyncCallback callback, object state)
    {
        var result = new LoopbackAsyncResult(state);
        Task.Run(() =>
        {
            try { result.Complete(Receive(buffer, offset, size, socketFlags), null, callback); }
            catch (Exception error) { result.Complete(0, error, callback); }
        });
        return result;
    }

    public int EndReceive(IAsyncResult asyncResult) => (int)((LoopbackAsyncResult)asyncResult).End();

    public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        if (Peer == null || !Connected) throw new LoopbackSocketException((int)SocketError.NotConnected);
        byte[] copy = new byte[size];
        Array.Copy(buffer, offset, copy, 0, size);
        Peer.Deliver(copy);
        return size;
    }

    public int Send(byte[] buffer) => Send(buffer, 0, buffer.Length, SocketFlags.None);

    public void Shutdown(SocketShutdown how) => Close();

    public void Close()
    {
        _inbox.CompleteAdding();
        Peer?._inbox.CompleteAdding();      // the other end reads 0: connection closed
    }

    public void Dispose() => Close();
}

/// <summary>A stream over a loopback TCP connection.</summary>
public class NetworkStream : System.IO.Stream
{
    private readonly Socket _socket;

    internal NetworkStream(Socket socket) => _socket = socket;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public bool DataAvailable => false;

    public override void Write(byte[] buffer, int offset, int count) => _socket.Send(buffer, offset, count, SocketFlags.None);

    public override int Read(byte[] buffer, int offset, int count) => _socket.Receive(buffer, offset, count, SocketFlags.None);

    public override void Flush() { }

    public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}

/// <summary>The client end of a loopback TCP connection.</summary>
public class TcpClient : IDisposable
{
    public TcpClient() { }

    internal TcpClient(Socket socket) => Client = socket;

    public Socket Client { get; private set; }

    public bool Connected => Client?.Connected == true;

    public void Connect(IPEndPoint remoteEP) => Connect(remoteEP.Address, remoteEP.Port);

    public void Connect(string hostname, int port) => Connect(LoopbackNetwork.Loopback, port);

    /// <summary>Connects to the listener on the port; fails like a refused connection when there is none.</summary>
    public void Connect(IPAddress address, int port)
    {
        TcpListener listener = LoopbackNetwork.FindListener(port);
        if (listener == null)
        {
            throw new LoopbackSocketException((int)SocketError.ConnectionRefused);
        }
        var local = new IPEndPoint(LoopbackNetwork.Loopback, LoopbackNetwork.NextEphemeralPort());
        var remote = new IPEndPoint(LoopbackNetwork.Loopback, port);
        var clientEnd = new Socket(local, remote);
        var serverEnd = new Socket(remote, local);
        clientEnd.Peer = serverEnd;
        serverEnd.Peer = clientEnd;
        Client = clientEnd;
        listener.Accept(new TcpClient(serverEnd));
    }

    public IAsyncResult BeginConnect(IPAddress address, int port, AsyncCallback requestCallback, object state)
    {
        var result = new LoopbackAsyncResult(state) { CompletedSynchronously = true };
        try
        {
            Connect(address, port);
            result.Complete(null, null, requestCallback);
        }
        catch (Exception error)
        {
            result.Complete(null, error, requestCallback);
        }
        return result;
    }

    public IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state) =>
        BeginConnect(LoopbackNetwork.Loopback, port, requestCallback, state);

    public void EndConnect(IAsyncResult asyncResult) => ((LoopbackAsyncResult)asyncResult).End();

    public NetworkStream GetStream()
    {
        if (Client == null) throw new InvalidOperationException("The client is not connected.");
        return new NetworkStream(Client);
    }

    public void Close() => Dispose();

    public void Dispose() => Client?.Close();
}

/// <summary>A loopback TCP server: connections to its port are handed to BeginAcceptTcpClient callbacks.</summary>
public class TcpListener
{
    private readonly int _port;
    private readonly BlockingCollection<TcpClient> _pending = new BlockingCollection<TcpClient>();
    private bool _started;

    public TcpListener(IPEndPoint localEP) => _port = localEP.Port;

    public TcpListener(IPAddress localaddr, int port) => _port = port;

    public EndPoint LocalEndpoint => new IPEndPoint(LoopbackNetwork.Loopback, _port);

    public void Start() => Start(int.MaxValue);

    public void Start(int backlog)
    {
        LoopbackNetwork.Listen(_port, this);
        _started = true;
    }

    public void Stop()
    {
        if (!_started) return;
        _started = false;
        LoopbackNetwork.StopListening(_port, this);
        _pending.CompleteAdding();
    }

    internal void Accept(TcpClient serverSide) => _pending.Add(serverSide);

    public TcpClient AcceptTcpClient()
    {
        try { return _pending.Take(); }
        catch (InvalidOperationException) { throw new LoopbackSocketException((int)SocketError.Interrupted); }
    }

    public IAsyncResult BeginAcceptTcpClient(AsyncCallback callback, object state)
    {
        var result = new LoopbackAsyncResult(state);
        Task.Run(() =>
        {
            try { result.Complete(AcceptTcpClient(), null, callback); }
            catch (Exception error) { result.Complete(null, error, callback); }
        });
        return result;
    }

    public TcpClient EndAcceptTcpClient(IAsyncResult asyncResult) => (TcpClient)((LoopbackAsyncResult)asyncResult).End();
}

/// <summary>Name resolution in the browser: every host name is this machine, the in-memory loopback.</summary>
public static class Dns
{
    public static IPAddress[] GetHostAddresses(string hostNameOrAddress)
    {
        if (IPAddress.TryParse(hostNameOrAddress, out IPAddress address)) return new[] { address };
        return new[] { LoopbackNetwork.Loopback };
    }

    public static string GetHostName() => "localhost";
}

/// <summary>The network interfaces the browser "has": only the loopback interface (127.0.0.1).</summary>
public class NetworkInterface
{
    public static NetworkInterface[] GetAllNetworkInterfaces() => new[] { new NetworkInterface() };

    public string Name => "Loopback";

    public IPInterfaceProperties GetIPProperties() => new IPInterfaceProperties();
}

public class IPInterfaceProperties
{
    public UnicastIPAddressInformationCollection UnicastAddresses { get; } =
        new UnicastIPAddressInformationCollection { new UnicastIPAddressInformation(LoopbackNetwork.Loopback) };
}

public class UnicastIPAddressInformationCollection : List<UnicastIPAddressInformation> { }

public class UnicastIPAddressInformation
{
    internal UnicastIPAddressInformation(IPAddress address) => Address = address;

    public IPAddress Address { get; }
}
