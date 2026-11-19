using CrypTool.PluginBase;
using CrypTool.PluginBase.Miscellaneous;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProtocolVisualization
{
    /// <summary>
    /// Shows two protocols as a message sequence between Alice and Bob, with every number calculated: the Diffie-Hellman key exchange with
    /// fresh exponents for every session (perfect forward secrecy), and a digital signature (hash, RSA signature, verification).
    /// </summary>
    [Author("Niklas Weimann", "ct2@nkls.nz", "", "https://niklas-weimann.de")]
    [PluginInfo("ProtocolVisualization.Properties.Resources", "PluginCaption", "PluginTooltip", "ProtocolVisualization/DetailedDescription/doc.xml", "ProtocolVisualization/icon.png")]
    [ComponentCategory(ComponentCategory.Protocols)]
    public class ProtocolVisualization : ICrypComponent
    {
        private ProtocolVisualizationSettings _settings = new ProtocolVisualizationSettings();
        private ProtocolVisualizationPresentation _presentation;

        [PropertyInfo(Direction.InputData, "ParametersCaption", "ParametersTooltip", true)]
        public string Parameters { get; set; }

        [PropertyInfo(Direction.InputData, "MessageCaption", "MessageTooltip", false)]
        public string Message { get; set; }

        [PropertyInfo(Direction.InputData, "ReceivedCaption", "ReceivedTooltip", false)]
        public string Received { get; set; }

        [PropertyInfo(Direction.OutputData, "ResultCaption", "ResultTooltip", false)]
        public string Result { get; set; }

        public ISettings Settings
        {
            get => _settings;
            set => _settings = (ProtocolVisualizationSettings)value;
        }

        public UserControl Presentation
        {
            get
            {
                if (_presentation == null) { _presentation = new ProtocolVisualizationPresentation(); }
                return _presentation;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event GuiLogNotificationEventHandler OnGuiLogNotificationOccured;
        public event StatusChangedEventHandler OnPluginStatusChanged;
        public event PluginProgressChangedEventHandler OnPluginProgressChanged;

        public void Execute()
        {
            var view = (ProtocolVisualizationPresentation)Presentation;
            if (Parameters == null)
            {
                GuiLogMessage("The parameters are needed.", NotificationLevel.Error);
                view.ShowMessage("The parameters are needed.");
                return;
            }

            try
            {
                if (_settings.Protocol == 2)
                {
                    Calculations.AuthenticationResult authentication = Calculations.Authentication(Parameters);
                    Result = authentication.Accepted ? "ok" : "failed";
                    view.ShowAuthentication(authentication);
                }
                else if (_settings.Protocol == 5)
                {
                    Calculations.ZeroKnowledgeResult zk = Calculations.ZeroKnowledge(Parameters);
                    Result = zk.HonestAccepted ? "ok" : "failed";
                    view.ShowZeroKnowledge(zk);
                }
                else if (_settings.Protocol == 4)
                {
                    Calculations.TlsResult tls = Calculations.Tls(Parameters);
                    Result = tls.Verified ? "ok" : "failed";
                    view.ShowTls(tls);
                }
                else if (_settings.Protocol == 3)
                {
                    Calculations.SmimeResult smime = Calculations.Smime(Parameters, Message ?? "");
                    Result = smime.Valid ? "valid" : "invalid";
                    view.ShowSmime(smime);
                }
                else if (_settings.Protocol == 1)
                {
                    Calculations.SignatureResult signature = Calculations.Signature(Parameters, Message ?? "", Received ?? Message ?? "");
                    Result = signature.Valid ? "valid" : "invalid";
                    view.ShowSignature(signature);
                }
                else
                {
                    Calculations.ExchangeResult exchange = Calculations.Exchange(Parameters);
                    Result = string.Join(",", exchange.Sessions.Select(s => s.Key.ToString()));
                    view.ShowExchange(exchange);
                }
                OnPropertyChanged("Result");
            }
            catch (ArgumentException ex)
            {
                GuiLogMessage(ex.Message, NotificationLevel.Error);
                view.ShowMessage(ex.Message);
            }
        }

        public void Stop()
        {
        }

        public void Initialize()
        {
        }

        public void PreExecution()
        {
            Parameters = Message = Received = Result = null;
        }

        public void PostExecution()
        {
            Parameters = Message = Received = Result = null;
        }

        public void Dispose()
        {
            Parameters = Message = Received = Result = null;
        }

        private void OnPropertyChanged(string name)
        {
            EventsHelper.PropertyChanged(PropertyChanged, this, new PropertyChangedEventArgs(name));
        }

        private void GuiLogMessage(string message, NotificationLevel level)
        {
            EventsHelper.GuiLogMessage(OnGuiLogNotificationOccured, this, message, level);
        }
    }

    /// <summary>The calculations of the protocols, free of plugin infrastructure; invalid parameters throw ArgumentException.</summary>
    public static class Calculations
    {
        private static BigInteger[] Numbers(string parameters, int minimum, string usage)
        {
            var numbers = new List<BigInteger>();
            foreach (string token in parameters.Split(new[] { ' ', ',', ';', '\n', '\r', '\t', '=' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length == 0 || !(char.IsDigit(token[0]) || token[0] == '-')) { continue; }
                if (!BigInteger.TryParse(token, out BigInteger number)) { throw new ArgumentException("Not a number: " + token); }
                numbers.Add(number);
            }
            if (numbers.Count < minimum) { throw new ArgumentException("The parameters are " + usage + "."); }
            return numbers.ToArray();
        }

        private static BigInteger Inverse(BigInteger a, BigInteger n)
        {
            BigInteger t = 0, newT = 1, r = n, newR = ((a % n) + n) % n;
            while (!newR.IsZero)
            {
                BigInteger q = r / newR;
                BigInteger tmp = t - q * newT; t = newT; newT = tmp;
                tmp = r - q * newR; r = newR; newR = tmp;
            }
            if (r != 1) { throw new ArgumentException(a + " has no inverse modulo " + n + "."); }
            return ((t % n) + n) % n;
        }

        /// <summary>Miller-Rabin test with the first twelve primes as bases; exact for numbers below 3.3 * 10^24.</summary>
        private static bool IsPrime(BigInteger n)
        {
            if (n < 2) { return false; }
            foreach (int p in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                if (n == p) { return true; }
                if (n % p == 0) { return false; }
            }
            BigInteger d = n - 1;
            int s = 0;
            while (d.IsEven) { d >>= 1; s++; }
            foreach (int a in new[] { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 })
            {
                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) { continue; }
                bool composite = true;
                for (int r = 1; r < s && composite; r++)
                {
                    x = x * x % n;
                    if (x == n - 1) { composite = false; }
                }
                if (composite) { return false; }
            }
            return true;
        }

        // ---------------------------------------------------------------- Diffie-Hellman

        public sealed class Session
        {
            public BigInteger A, B;          // the secret exponents of Alice and Bob
            public BigInteger PublicA, PublicB;
            public BigInteger Key;           // g^(ab) mod p, as computed by Alice and by Bob
            public BigInteger KeyAlice, KeyBob;
            public BigInteger? Found;        // the exponent that an eavesdropper finds by trying (small p only)
        }

        public sealed class ExchangeResult
        {
            public BigInteger P, G;
            public List<Session> Sessions = new List<Session>();
        }

        /// <summary>Parameters: p g a1 b1 [a2 b2 ...]: the prime, the generator, and the secret exponents of Alice and Bob for each session.</summary>
        public static ExchangeResult Exchange(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 4, "p, g, and then the secret exponents of Alice and Bob for each session: p g a1 b1 [a2 b2 ...]");
            var result = new ExchangeResult { P = v[0], G = v[1] };
            if (result.P < 5 || !IsPrime(result.P) || result.G < 2 || result.G >= result.P - 1) { throw new ArgumentException("p must be a prime number of at least 5 and g a number from 2 to p-2."); }
            for (int i = 2; i + 1 < v.Length && result.Sessions.Count < 3; i += 2)
            {
                var s = new Session { A = v[i], B = v[i + 1] };
                if (s.A < 1 || s.B < 1) { throw new ArgumentException("The exponents must be positive."); }
                s.PublicA = BigInteger.ModPow(result.G, s.A, result.P);
                s.PublicB = BigInteger.ModPow(result.G, s.B, result.P);
                s.KeyAlice = BigInteger.ModPow(s.PublicB, s.A, result.P);
                s.KeyBob = BigInteger.ModPow(s.PublicA, s.B, result.P);
                s.Key = s.KeyAlice;
                if (result.P < 2000000)
                {
                    // the eavesdropper knows p, g and A: tries the exponents one after the other
                    BigInteger x = 1;
                    for (long e = 1; e < (long)result.P; e++)
                    {
                        x = x * result.G % result.P;
                        if (x == s.PublicA) { s.Found = e; break; }
                    }
                }
                result.Sessions.Add(s);
            }
            return result;
        }


        // ---------------------------------------------------------------- authentication in a network

        public sealed class AuthenticationResult
        {
            public string Password, Nonce1, Nonce2;
            public string PasswordHash;                 // SHA-256 of the password
            public string Response1, Response2;         // HMAC-SHA-256 of the nonces with the password as key
            public bool Accepted;                       // the genuine response to the first challenge is accepted
            public bool ReplayAccepted;                 // the response to the first challenge, replayed against the second one
        }

        public static string Hmac(string key, string message)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "");
            }
        }

        /// <summary>Parameters: password nonce1 nonce2 (three words): the shared password and the two challenges that the server sends in two logins.</summary>
        public static AuthenticationResult Authentication(string parameters)
        {
            string[] words = parameters.Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 3) { throw new ArgumentException("The parameters are password, nonce1 and nonce2: the password and the two challenges of the server, as three words."); }
            var r = new AuthenticationResult { Password = words[0], Nonce1 = words[1], Nonce2 = words[2] };
            r.PasswordHash = Sha256(r.Password);
            r.Response1 = Hmac(r.Password, r.Nonce1);
            r.Response2 = Hmac(r.Password, r.Nonce2);
            r.Accepted = r.Response1 == Hmac(r.Password, r.Nonce1);
            r.ReplayAccepted = r.Response1 == r.Response2;
            return r;
        }

        // ---------------------------------------------------------------- zero-knowledge proof (Fiat-Shamir)

        public sealed class ZkRound
        {
            public BigInteger R, X, Challenge, Y, Left, Right;   // honest prover: x = r^2, y = r * s^c, check y^2 = x * v^c
            public bool Accepted;
            public BigInteger CheatX, CheatY;                    // cheater without s, who bets on the challenge 0: x = r^2, y = r
            public bool CheatAccepted;
        }

        public sealed class ZeroKnowledgeResult
        {
            public BigInteger N, S, V;
            public List<ZkRound> Rounds = new List<ZkRound>();
            public bool HonestAccepted;
            public int CheaterCaughtInRound;                     // 0: the cheater got through all rounds
        }

        /// <summary>
        /// Parameters: p q s r1 c1 [r2 c2 ...]: two primes, the secret s of the prover and per round the random number r of the prover and the challenge c (0 or 1) of the verifier.
        /// Fiat-Shamir identification: n = p * q, public v = s^2 mod n; in every round the prover sends x = r^2, the verifier asks c, the prover answers y = r * s^c, and the verifier
        /// checks y^2 = x * v^c (mod n). A cheater who does not know s can only answer one of the two challenges.
        /// </summary>
        public static ZeroKnowledgeResult ZeroKnowledge(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 5, "p q s r1 c1 [r2 c2 ...]: two primes, the secret, and for every round the random number of the prover and the challenge (0 or 1)");
            if ((v.Length - 3) % 2 != 0) { throw new ArgumentException("Every round needs a random number r and a challenge c."); }
            if (!IsPrime(v[0]) || !IsPrime(v[1]) || v[0] == v[1]) { throw new ArgumentException("p and q must be two different primes."); }
            var result = new ZeroKnowledgeResult { N = v[0] * v[1], S = v[2] };
            if (result.S < 2 || result.S >= result.N || BigInteger.GreatestCommonDivisor(result.S, result.N) != 1) { throw new ArgumentException("The secret s must be between 2 and n - 1 and coprime to n = " + result.N + "."); }
            result.V = result.S * result.S % result.N;
            result.HonestAccepted = true;
            for (int i = 3; i + 1 < v.Length; i += 2)
            {
                var round = new ZkRound { R = v[i], Challenge = v[i + 1] };
                if (round.R < 1 || round.R >= result.N || BigInteger.GreatestCommonDivisor(round.R, result.N) != 1) { throw new ArgumentException("The random number r must be between 1 and n - 1 and coprime to n."); }
                if (round.Challenge != 0 && round.Challenge != 1) { throw new ArgumentException("The challenge must be 0 or 1."); }
                round.X = round.R * round.R % result.N;
                round.Y = round.Challenge == 0 ? round.R : round.R * result.S % result.N;
                round.Left = round.Y * round.Y % result.N;
                round.Right = round.Challenge == 0 ? round.X : round.X * result.V % result.N;
                round.Accepted = round.Left == round.Right;
                result.HonestAccepted &= round.Accepted;
                round.CheatX = round.X;
                round.CheatY = round.R;
                BigInteger cheatRight = round.Challenge == 0 ? round.X : round.X * result.V % result.N;
                round.CheatAccepted = round.CheatY * round.CheatY % result.N == cheatRight;
                if (!round.CheatAccepted && result.CheaterCaughtInRound == 0) { result.CheaterCaughtInRound = result.Rounds.Count + 1; }
                result.Rounds.Add(round);
            }
            return result;
        }

        // ---------------------------------------------------------------- TLS 1.3 handshake

        public sealed class TlsResult
        {
            public BigInteger P, G, A, B, PublicA, PublicB, Shared;
            public string ClientRandom, ServerRandom;
            public string HandshakeSecret, ClientHandshakeKey, ServerHandshakeKey, ServerFinished, ClientFinished;
            public string ClientKey, ClientIv, ServerKey, ServerIv;
            public bool Verified, ClientVerified;
            public BigInteger EveFound;                 // the exponent that Eve finds by trial (0: not tried, the prime is too large)
            public bool EveHasKeys;
        }

        public static byte[] HkdfExtract(byte[] salt, byte[] ikm)
        {
            using (var hmac = new HMACSHA256(salt)) { return hmac.ComputeHash(ikm); }
        }

        /// <summary>HKDF-Expand of RFC 5869 with HMAC-SHA-256.</summary>
        public static byte[] HkdfExpand(byte[] prk, byte[] info, int length)
        {
            var result = new List<byte>();
            byte[] previous = new byte[0];
            using (var hmac = new HMACSHA256(prk))
            {
                for (byte counter = 1; result.Count < length; counter++)
                {
                    previous = hmac.ComputeHash(previous.Concat(info).Concat(new[] { counter }).ToArray());
                    result.AddRange(previous);
                }
            }
            return result.Take(length).ToArray();
        }

        /// <summary>HKDF-Expand-Label of TLS 1.3 (RFC 8446, section 7.1).</summary>
        public static byte[] ExpandLabel(byte[] secret, string label, byte[] context, int length)
        {
            byte[] full = Encoding.ASCII.GetBytes("tls13 " + label);
            var info = new List<byte> { (byte)(length >> 8), (byte)length, (byte)full.Length };
            info.AddRange(full);
            info.Add((byte)context.Length);
            info.AddRange(context);
            return HkdfExpand(secret, info.ToArray(), length);
        }

        public static byte[] DeriveSecret(byte[] secret, string label, byte[] transcriptHash) => ExpandLabel(secret, label, transcriptHash, 32);

        private static byte[] Sha256Bytes(byte[] data)
        {
            using (var sha = SHA256.Create()) { return sha.ComputeHash(data); }
        }

        private static string Hex(byte[] data) => BitConverter.ToString(data).Replace("-", "");

        /// <summary>
        /// Parameters: p g a b: prime, generator and the secret exponents of client and server for the key exchange (a small Diffie-Hellman group instead of
        /// an elliptic curve, so that the numbers can be followed). The key schedule is the one of TLS 1.3 (RFC 8446): HKDF with SHA-256, the transcript hash
        /// of the messages, handshake traffic keys, Finished messages (HMAC over the transcript) and the application traffic keys.
        /// </summary>
        public static TlsResult Tls(string parameters)
        {
            BigInteger[] v = Numbers(parameters, 4, "p g a b: prime, generator and the secret exponents of client and server");
            var r = new TlsResult { P = v[0], G = v[1], A = v[2], B = v[3] };
            if (!IsPrime(r.P)) { throw new ArgumentException("p must be a prime."); }
            if (r.G < 2 || r.G >= r.P) { throw new ArgumentException("g must be at least 2 and smaller than p."); }
            if (r.A < 1 || r.A >= r.P || r.B < 1 || r.B >= r.P) { throw new ArgumentException("The secret exponents must be at least 1 and smaller than p."); }
            r.PublicA = BigInteger.ModPow(r.G, r.A, r.P);
            r.PublicB = BigInteger.ModPow(r.G, r.B, r.P);
            r.Shared = BigInteger.ModPow(r.PublicB, r.A, r.P);
            r.ClientRandom = Hex(Sha256Bytes(Encoding.UTF8.GetBytes("client random " + parameters)));
            r.ServerRandom = Hex(Sha256Bytes(Encoding.UTF8.GetBytes("server random " + parameters)));

            byte[] zero = new byte[32], empty = Sha256Bytes(new byte[0]);
            byte[] clientHello = Encoding.UTF8.GetBytes("ClientHello " + r.ClientRandom + " key_share " + r.PublicA);
            byte[] serverHello = Encoding.UTF8.GetBytes("ServerHello " + r.ServerRandom + " key_share " + r.PublicB);
            byte[] hello = Sha256Bytes(clientHello.Concat(serverHello).ToArray());
            byte[] z = r.Shared.ToByteArray().Reverse().SkipWhile(x => x == 0).ToArray();

            byte[] early = HkdfExtract(zero, zero);
            byte[] handshake = HkdfExtract(DeriveSecret(early, "derived", empty), z);
            byte[] clientHs = DeriveSecret(handshake, "c hs traffic", hello), serverHs = DeriveSecret(handshake, "s hs traffic", hello);
            r.HandshakeSecret = Hex(handshake);
            r.ClientHandshakeKey = Hex(clientHs);
            r.ServerHandshakeKey = Hex(serverHs);

            // the server proves that it holds the handshake keys: HMAC over the transcript with the finished key
            byte[] serverFlight = Sha256Bytes(hello.Concat(Encoding.UTF8.GetBytes("EncryptedExtensions Certificate CertificateVerify")).ToArray());
            byte[] serverFinished = new HMACSHA256(ExpandLabel(serverHs, "finished", new byte[0], 32)).ComputeHash(serverFlight);
            r.ServerFinished = Hex(serverFinished);
            byte[] clientCheck = new HMACSHA256(ExpandLabel(serverHs, "finished", new byte[0], 32)).ComputeHash(serverFlight);
            r.Verified = clientCheck.SequenceEqual(serverFinished);
            byte[] clientFlight = Sha256Bytes(serverFlight.Concat(serverFinished).ToArray());
            byte[] clientFinished = new HMACSHA256(ExpandLabel(clientHs, "finished", new byte[0], 32)).ComputeHash(clientFlight);
            r.ClientFinished = Hex(clientFinished);
            r.ClientVerified = new HMACSHA256(ExpandLabel(clientHs, "finished", new byte[0], 32)).ComputeHash(clientFlight).SequenceEqual(clientFinished);

            byte[] master = HkdfExtract(DeriveSecret(handshake, "derived", empty), zero);
            byte[] clientAp = DeriveSecret(master, "c ap traffic", clientFlight), serverAp = DeriveSecret(master, "s ap traffic", clientFlight);
            r.ClientKey = Hex(ExpandLabel(clientAp, "key", new byte[0], 16));
            r.ClientIv = Hex(ExpandLabel(clientAp, "iv", new byte[0], 12));
            r.ServerKey = Hex(ExpandLabel(serverAp, "key", new byte[0], 16));
            r.ServerIv = Hex(ExpandLabel(serverAp, "iv", new byte[0], 12));

            // an eavesdropper who sees p, g, A and B: with a small prime the exponent is found by trial, and then every key follows
            if (r.P < 2000000)
            {
                BigInteger x = 1;
                for (BigInteger e = 1; e < r.P; e++)
                {
                    x = x * r.G % r.P;
                    if (x == r.PublicA) { r.EveFound = e; break; }
                }
                r.EveHasKeys = r.EveFound > 0 && BigInteger.ModPow(r.PublicB, r.EveFound, r.P) == r.Shared;
            }
            return r;
        }

        // ---------------------------------------------------------------- S/MIME

        public sealed class SmimeResult
        {
            public BigInteger NAlice, EAlice, DAlice, NBob, EBob, DBob;
            public string Message, Hash, SessionKey, Cipher;
            public BigInteger Reduced, Signature, Recovered;
            public List<BigInteger> WrappedKey = new List<BigInteger>();
            public string Decrypted;
            public bool Valid;
        }

        /// <summary>
        /// Parameters: pA qA eA pB qB eB: the RSA keys of Alice (signing) and Bob (encryption) as two primes and a public exponent each; n of Bob must exceed 255.
        /// Alice signs the hash of the message, encrypts message and signature with a session key (AES-128-CBC) and encrypts the session key for Bob with his RSA key
        /// (byte by byte only because the example key is so small); Bob reverses the steps and verifies the signature.
        /// </summary>
        public static SmimeResult Smime(string parameters, string message)
        {
            BigInteger[] v = Numbers(parameters, 6, "pA qA eA pB qB eB: two primes and the public exponent for Alice's signature key, and the same for Bob's encryption key");
            var r = new SmimeResult { Message = message };
            Func<BigInteger, BigInteger, BigInteger, BigInteger[]> key = (p, q, e) =>
            {
                if (!IsPrime(p) || !IsPrime(q) || p == q) { throw new ArgumentException("p and q must be two different primes."); }
                BigInteger phi = (p - 1) * (q - 1);
                if (e < 2 || e >= phi || BigInteger.GreatestCommonDivisor(e, phi) != 1) { throw new ArgumentException("e must be coprime to phi(n) = " + phi + " and smaller than it."); }
                return new[] { p * q, e, Inverse(e, phi) };
            };
            BigInteger[] alice = key(v[0], v[1], v[2]), bob = key(v[3], v[4], v[5]);
            r.NAlice = alice[0]; r.EAlice = alice[1]; r.DAlice = alice[2];
            r.NBob = bob[0]; r.EBob = bob[1]; r.DBob = bob[2];
            if (r.NBob < 256) { throw new ArgumentException("The modulus of Bob's key must be at least 256, because the session key is encrypted byte by byte."); }

            // Alice: sign, then encrypt message and signature with the session key, then wrap the session key for Bob
            r.Hash = Sha256(message);
            r.Reduced = Reduce(r.Hash, r.NAlice);
            r.Signature = BigInteger.ModPow(r.Reduced, r.DAlice, r.NAlice);
            byte[] sessionKey;
            using (var sha = SHA256.Create()) { sessionKey = sha.ComputeHash(Encoding.UTF8.GetBytes("session key for " + message)).Take(16).ToArray(); }
            r.SessionKey = BitConverter.ToString(sessionKey).Replace("-", "");
            byte[] cipher = AesCbc(sessionKey, Encoding.UTF8.GetBytes(message + "|" + r.Signature), true);
            r.Cipher = BitConverter.ToString(cipher).Replace("-", "");
            foreach (byte b in sessionKey) { r.WrappedKey.Add(BigInteger.ModPow(b, r.EBob, r.NBob)); }

            // Bob: unwrap the session key, decrypt, verify the signature
            byte[] unwrapped = r.WrappedKey.Select(c => (byte)BigInteger.ModPow(c, r.DBob, r.NBob)).ToArray();
            string plain = Encoding.UTF8.GetString(AesCbc(unwrapped, cipher, false));
            int bar = plain.LastIndexOf('|');
            r.Decrypted = bar < 0 ? plain : plain.Substring(0, bar);
            BigInteger.TryParse(bar < 0 ? "0" : plain.Substring(bar + 1), out BigInteger signature);
            r.Recovered = BigInteger.ModPow(signature, r.EAlice, r.NAlice);
            r.Valid = r.Recovered == Reduce(Sha256(r.Decrypted), r.NAlice);
            return r;
        }

        private static byte[] AesCbc(byte[] key, byte[] data, bool encrypt)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = new byte[16];   // a fixed IV is only acceptable because every message has its own session key
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor())
                {
                    return transform.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        // ---------------------------------------------------------------- signature

        public sealed class SignatureResult
        {
            public BigInteger P, Q, N, Phi, E, D;
            public string Message, Received;
            public string Hash, ReceivedHash;                 // SHA-256 as hexadecimal text
            public BigInteger Reduced, Signature, Recovered, ReceivedReduced;
            public bool Valid;
            public bool Tampered;
        }

        public static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
            }
        }

        public static BigInteger Sha256Reduced(string text, BigInteger n) => Reduce(Sha256(text), n);

        private static BigInteger Reduce(string hash, BigInteger n)
        {
            return BigInteger.Parse("0" + hash, System.Globalization.NumberStyles.HexNumber) % n;
        }

        /// <summary>
        /// Parameters: p q e (two primes and the public exponent of an RSA key). The message is hashed with SHA-256, the hash value (reduced modulo n,
        /// which is only needed because the keys of the example are small) is signed with the private key; the receiver checks the received message.
        /// </summary>
        public static SignatureResult Signature(string parameters, string message, string received)
        {
            BigInteger[] v = Numbers(parameters, 3, "p, q and e: two prime numbers and the public exponent of an RSA key");
            var r = new SignatureResult { P = v[0], Q = v[1], E = v[2], Message = message, Received = received };
            if (!IsPrime(r.P) || !IsPrime(r.Q) || r.P == r.Q) { throw new ArgumentException("p and q must be two different primes."); }
            r.N = r.P * r.Q;
            r.Phi = (r.P - 1) * (r.Q - 1);
            if (r.E < 2 || r.E >= r.Phi || BigInteger.GreatestCommonDivisor(r.E, r.Phi) != 1) { throw new ArgumentException("e must be coprime to phi(n) = " + r.Phi + " and smaller than it."); }
            r.D = Inverse(r.E, r.Phi);
            r.Hash = Sha256(message);
            r.Reduced = Reduce(r.Hash, r.N);
            r.Signature = BigInteger.ModPow(r.Reduced, r.D, r.N);
            r.ReceivedHash = Sha256(received);
            r.ReceivedReduced = Reduce(r.ReceivedHash, r.N);
            r.Recovered = BigInteger.ModPow(r.Signature, r.E, r.N);
            r.Valid = r.Recovered == r.ReceivedReduced;
            r.Tampered = received != message;
            return r;
        }
    }

    /// <summary>The pictures, built from plain WPF elements in code.</summary>
    public class ProtocolVisualizationPresentation : UserControl
    {
        private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(225, 232, 245));
        private static readonly Brush AliceBrush = new SolidColorBrush(Color.FromRgb(255, 240, 215));
        private static readonly Brush BobBrush = new SolidColorBrush(Color.FromRgb(215, 235, 255));
        private static readonly Brush GoodBrush = new SolidColorBrush(Color.FromRgb(160, 215, 160));
        private static readonly Brush BadBrush = new SolidColorBrush(Color.FromRgb(240, 160, 160));
        private static readonly Brush EveBrush = new SolidColorBrush(Color.FromRgb(240, 215, 240));

        private readonly ScrollViewer _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private StackPanel _panel;

        public ProtocolVisualizationPresentation()
        {
            Content = _scroll;
            Background = Brushes.White;
            _scroll.Content = new TextBlock { Text = Properties.Resources.WaitingText, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) };
        }

        private static TextBlock Text(string text, double size = 12, bool bold = false, bool mono = false)
        {
            var block = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return block;
        }

        private void Show(Action build)
        {
            Action show = () =>
            {
                _panel = new StackPanel { Margin = new Thickness(10) };
                build();
                _scroll.Content = _panel;
            };
            if (Dispatcher.CheckAccess()) { show(); } else { Dispatcher.BeginInvoke(show); }
        }

        private void Add(UIElement element) => _panel.Children.Add(element);

        public void ShowMessage(string message)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ErrorTitle, 16, true));
                Add(Text(message, 13));
                Add(Text(Environment.NewLine + Properties.Resources.ParameterExamples, 12, false, true));
            });
        }

        private static Border Box(string text, Brush background, double width, bool bold = false, bool mono = true)
        {
            var block = new TextBlock { Text = text, FontSize = 12, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 3, 6, 3) };
            if (mono) { block.FontFamily = new FontFamily("Consolas"); }
            return new Border { Width = width, Background = background, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5), Child = block };
        }

        /// <summary>One line of the sequence: what Alice calculates, the message between them (an arrow), what Bob calculates.</summary>
        private void Row(string alice, string arrow, string bob, bool toBob = true, Brush arrowBrush = null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            row.Children.Add(Box(alice, alice.Length > 0 ? AliceBrush : Brushes.White, 270));
            row.Children.Add(Box(arrow.Length > 0 ? (toBob ? arrow + "  \u2192" : "\u2190  " + arrow) : "", arrowBrush ?? (arrow.Length > 0 ? EveBrush : Brushes.White), 250, true));
            row.Children.Add(Box(bob, bob.Length > 0 ? BobBrush : Brushes.White, 270));
            Add(row);
        }

        private void Heading(string left, string middle, string right)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 2) };
            row.Children.Add(Box(left, HeaderBrush, 270, true, false));
            row.Children.Add(Box(middle, HeaderBrush, 250, true, false));
            row.Children.Add(Box(right, HeaderBrush, 270, true, false));
            Add(row);
        }

        // ------------------------------------------------------------------ Diffie-Hellman

        public void ShowExchange(Calculations.ExchangeResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ExchangeTitle, 16, true));
                Add(Text(string.Format(Properties.Resources.ExchangeExplanation, r.P, r.G), 12));
                int number = 0;
                foreach (var s in r.Sessions)
                {
                    number++;
                    Add(Text(string.Format(Properties.Resources.SessionTitle, number), 14, true));
                    Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                    Row(string.Format(Properties.Resources.ChooseSecret, "a", s.A), "", string.Format(Properties.Resources.ChooseSecret, "b", s.B));
                    Row("A = g^a mod p = " + r.G + "^" + s.A + " mod " + r.P + " = " + s.PublicA, "A = " + s.PublicA, "");
                    Row("", "B = " + s.PublicB, "B = g^b mod p = " + r.G + "^" + s.B + " mod " + r.P + " = " + s.PublicB, false);
                    Row("K = B^a mod p = " + s.PublicB + "^" + s.A + " mod " + r.P + " = " + s.KeyAlice, "", "K = A^b mod p = " + s.PublicA + "^" + s.B + " mod " + r.P + " = " + s.KeyBob);
                    var key = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                    key.Children.Add(Box(string.Format(Properties.Resources.SameKey, s.Key), GoodBrush, 790, true, false));
                    Add(key);
                    var eve = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                    eve.Children.Add(Box(s.Found.HasValue
                        ? string.Format(Properties.Resources.EveFound, r.P, r.G, s.PublicA, s.Found.Value, BigInteger.ModPow(s.PublicB, s.Found.Value, r.P))
                        : Properties.Resources.EveFails, EveBrush, 790, false, false));
                    Add(eve);
                }
                if (r.Sessions.Count > 1)
                {
                    bool different = r.Sessions.Select(s => s.Key).Distinct().Count() == r.Sessions.Count;
                    Add(Text(Environment.NewLine + (different ? Properties.Resources.ForwardSecrecyDifferent : Properties.Resources.ForwardSecrecySame), 13, true));
                }
                Add(Text(Properties.Resources.ForwardSecrecyExplanation, 12));
            });
        }


        // ------------------------------------------------------------------ authentication

        private static string Short(string hex) => hex.Length > 32 ? hex.Substring(0, 32) + "..." : hex;

        public void ShowAuthentication(Calculations.AuthenticationResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.AuthenticationTitle, 16, true));
                Add(Text(Properties.Resources.AuthenticationExplanation, 12));

                Add(Text(Properties.Resources.AuthPlainTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("", "login: alice, password: " + r.Password, "password is " + r.Password + ": accepted");
                Add(EveLine(string.Format(Properties.Resources.AuthPlainEve, r.Password), BadBrush));

                Add(Text(Properties.Resources.AuthHashTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("H = SHA-256(password)\n" + Short(r.PasswordHash), "login: alice, H = " + Short(r.PasswordHash), "compares H with the stored value: accepted");
                Add(EveLine(Properties.Resources.AuthHashEve, BadBrush));

                Add(Text(Properties.Resources.AuthChallengeTitle, 14, true));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row("", string.Format(Properties.Resources.AuthChallenge, r.Nonce1), "chooses the challenge " + r.Nonce1, false);
                Row("R = HMAC(password, " + r.Nonce1 + ")\n" + Short(r.Response1), "R = " + Short(r.Response1), "computes the same value from the stored password: " + (r.Accepted ? "accepted" : "rejected"));
                Row("", string.Format(Properties.Resources.AuthChallenge, r.Nonce2), "next login, new challenge " + r.Nonce2, false);
                Row("Eve sends the old response\n" + Short(r.Response1), "R = " + Short(r.Response1), "expects HMAC(password, " + r.Nonce2 + ")\n" + Short(r.Response2) + "\n" + (r.ReplayAccepted ? "the same: replay accepted" : "different: rejected"), true, r.ReplayAccepted ? BadBrush : GoodBrush);
                Add(EveLine(r.ReplayAccepted ? Properties.Resources.AuthSameNonce : Properties.Resources.AuthChallengeEve, r.ReplayAccepted ? BadBrush : GoodBrush));
            });
        }

        private static StackPanel EveLine(string text, Brush brush)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            line.Children.Add(Box(text, brush, 790, false, false));
            return line;
        }

        // ------------------------------------------------------------------ zero-knowledge proof

        public void ShowZeroKnowledge(Calculations.ZeroKnowledgeResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.ZkTitle, 16, true));
                Add(Text(Properties.Resources.ZkExplanation, 12));
                Heading("Peggy (prover)", Properties.Resources.OpenChannel, "Victor (verifier)");
                Row("secret s = " + r.S, "n = " + r.N + ", v = " + r.V, "knows n and v = s^2 mod n = " + r.V + ", not s");
                int number = 0;
                foreach (var round in r.Rounds)
                {
                    number++;
                    Add(Text(string.Format(Properties.Resources.ZkRound, number), 13, true));
                    Row("r = " + round.R + "\nx = r^2 mod n = " + round.X, "x = " + round.X, "");
                    Row("", "c = " + round.Challenge, "chooses the challenge c = " + round.Challenge, false);
                    Row("y = r * s^c mod n = " + round.Y, "y = " + round.Y, "y^2 mod n = " + round.Left + "\nx * v^c mod n = " + round.Right + "\n" + (round.Accepted ? "equal: accepted" : "different: rejected"), true, round.Accepted ? GoodBrush : BadBrush);
                }
                Add(Text(Properties.Resources.ZkCheaterTitle, 14, true));
                Add(Text(Properties.Resources.ZkCheaterText, 12));
                foreach (var round in r.Rounds.Take(r.CheaterCaughtInRound == 0 ? r.Rounds.Count : r.CheaterCaughtInRound))
                {
                    Add(EveLine(string.Format(Properties.Resources.ZkCheaterRound, r.Rounds.IndexOf(round) + 1, round.Challenge, round.CheatAccepted ? Properties.Resources.ZkPassed : Properties.Resources.ZkCaught), round.CheatAccepted ? BadBrush : GoodBrush));
                }
                Add(EveLine(r.CheaterCaughtInRound == 0 ? string.Format(Properties.Resources.ZkCheaterSurvived, r.Rounds.Count, (Math.Pow(0.5, r.Rounds.Count)).ToString("0.######")) : string.Format(Properties.Resources.ZkCheaterEnded, r.CheaterCaughtInRound), r.CheaterCaughtInRound == 0 ? BadBrush : GoodBrush));
                Add(Text(Environment.NewLine + Properties.Resources.ZkNote, 12));
            });
        }

        // ------------------------------------------------------------------ TLS 1.3 handshake

        public void ShowTls(Calculations.TlsResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.TlsTitle, 16, true));
                Add(Text(Properties.Resources.TlsExplanation, 12));
                Heading("Alice (client)", Properties.Resources.OpenChannel, "Bob (server)");
                Row(string.Format(Properties.Resources.ChooseSecret, "a", r.A), "", string.Format(Properties.Resources.ChooseSecret, "b", r.B));
                Row("1. ClientHello\nrandom " + Short(r.ClientRandom) + "\nkey_share A = g^a mod p = " + r.PublicA, "ClientHello, A = " + r.PublicA, "");
                Row("", "ServerHello, B = " + r.PublicB, "2. ServerHello\nrandom " + Short(r.ServerRandom) + "\nkey_share B = g^b mod p = " + r.PublicB, false);
                Row("3. Z = B^a mod p = " + r.Shared + "\nHandshake secret = HKDF(Z)\n" + Short(r.HandshakeSecret), "", "3. Z = A^b mod p = " + r.Shared + "\nthe same handshake secret");
                Row("", "", "4. handshake traffic keys from the secret and the hash of ClientHello and ServerHello:\nclient " + Short(r.ClientHandshakeKey) + "\nserver " + Short(r.ServerHandshakeKey));
                Row("", "encrypted: EncryptedExtensions, Certificate, CertificateVerify, Finished\n" + Short(r.ServerFinished), "5. Finished = HMAC(finished key, hash of the messages so far)", false);
                Row("6. checks the Finished value: " + (r.Verified ? "correct" : "wrong"), "", "");
                Row("7. Finished\n" + Short(r.ClientFinished), "encrypted: Finished", "8. checks it: " + (r.ClientVerified ? "correct" : "wrong"));
                Row("9. application keys\nkey " + r.ClientKey + "\niv " + r.ClientIv, "", "9. the same\nkey " + r.ServerKey + "\niv " + r.ServerIv);
                var verdict = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                verdict.Children.Add(Box(r.Verified && r.ClientVerified ? Properties.Resources.TlsOk : Properties.Resources.TlsFailed, r.Verified && r.ClientVerified ? GoodBrush : BadBrush, 790, true, false));
                Add(verdict);
                Add(EveLine(r.EveFound > 0 ? string.Format(Properties.Resources.TlsEveFound, r.EveFound) : Properties.Resources.TlsEveNotTried, r.EveFound > 0 ? BadBrush : GoodBrush));
                Add(Text(Environment.NewLine + Properties.Resources.TlsNote, 12));
            });
        }

        // ------------------------------------------------------------------ S/MIME

        public void ShowSmime(Calculations.SmimeResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.SmimeTitle, 16, true));
                Add(Text(Properties.Resources.SmimeExplanation, 12));
                Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                Row(Properties.Resources.Message + ": \"" + r.Message + "\"", "", "");
                Row("1. h = SHA-256(" + Properties.Resources.Message + ") mod nA = " + r.Reduced + "\ns = h^dA mod nA = " + r.Signature, "", "");
                Row("2. K = " + r.SessionKey + "\n(" + Properties.Resources.SessionKeyNote + ")", "", "");
                Row("3. AES-128-CBC(K, message + signature)\n" + Short(r.Cipher), "", "");
                Row("4. K^eB mod nB, byte by byte\n" + string.Join(" ", r.WrappedKey.Take(6)) + " ...", "", "");
                Row("", string.Join(" ", r.WrappedKey.Take(3)) + " ...\n" + Short(r.Cipher), "", true);
                Row("", "", "5. K^dB mod nB: " + r.SessionKey);
                Row("", "", "6. AES decryption: \"" + r.Decrypted + "\" and s = " + r.Signature);
                Row("", "", "7. s^eA mod nA = " + r.Recovered + "\nSHA-256 of the message mod nA = " + Reduce256(r));
                var verdict = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                verdict.Children.Add(Box(r.Valid ? Properties.Resources.SmimeValid : Properties.Resources.SmimeInvalid, r.Valid ? GoodBrush : BadBrush, 790, true, false));
                Add(verdict);
                Add(Text(Environment.NewLine + Properties.Resources.SmimeNote, 12));
            });
        }

        private static string Reduce256(Calculations.SmimeResult r) => Calculations.Sha256Reduced(r.Decrypted, r.NAlice).ToString();

        // ------------------------------------------------------------------ signature

        public void ShowSignature(Calculations.SignatureResult r)
        {
            Show(() =>
            {
                Add(Text(Properties.Resources.SignatureTitle, 16, true));
                Add(Text(string.Format(Properties.Resources.SignatureExplanation, r.N, r.E, r.D), 12));
                Heading("Alice", Properties.Resources.OpenChannel, "Bob");
                Row(Properties.Resources.Message + ": \"" + r.Message + "\"", "", "");
                Row("H = SHA-256(" + Properties.Resources.Message + ")\n" + r.Hash, "", "");
                Row("h = H mod n = " + r.Reduced, "", "");
                Row("s = h^d mod n = " + r.Reduced + "^" + r.D + " mod " + r.N + " = " + r.Signature, "", "");
                Row("", Properties.Resources.Message + (r.Tampered ? " (" + Properties.Resources.Changed + ")" : "") + ", s = " + r.Signature, "", true, r.Tampered ? BadBrush : null);
                Row("", "", Properties.Resources.Received + ": \"" + r.Received + "\"");
                Row("", "", "H' = SHA-256(" + Properties.Resources.Received + ")\n" + r.ReceivedHash);
                Row("", "", "h' = H' mod n = " + r.ReceivedReduced);
                Row("", "", "s^e mod n = " + r.Signature + "^" + r.E + " mod " + r.N + " = " + r.Recovered);
                var verdict = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                verdict.Children.Add(Box(r.Valid
                    ? string.Format(Properties.Resources.SignatureValid, r.Recovered)
                    : string.Format(Properties.Resources.SignatureInvalid, r.Recovered, r.ReceivedReduced), r.Valid ? GoodBrush : BadBrush, 790, true, false));
                Add(verdict);
                if (r.N < BigInteger.Pow(2, 64)) { Add(Text(Environment.NewLine + Properties.Resources.SmallKeyNote, 12)); }
            });
        }
    }
}
