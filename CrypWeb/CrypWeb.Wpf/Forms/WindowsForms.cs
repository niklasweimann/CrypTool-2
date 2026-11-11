using System.Drawing;

namespace System.Windows.Forms
{
    /// <summary>
    /// The part of Windows Forms that WPF presentations embed through <see cref="Integration.WindowsFormsHost"/>.
    /// Only the web browser control is used by plugins (PKCS1 shows its help pages in it); it is drawn as an iframe.
    /// </summary>
    public class Control : IDisposable
    {
        public DockStyle Dock { get; set; }

        public bool Visible { get; set; } = true;

        public event EventHandler SizeChanged;

        protected void RaiseSizeChanged() => SizeChanged?.Invoke(this, EventArgs.Empty);

        public virtual void Dispose() { }
    }

    public enum DockStyle { None, Top, Bottom, Left, Right, Fill }

    /// <summary>A browser frame showing HTML text (<see cref="DocumentText"/>) or an address (<see cref="Url"/>).</summary>
    public class WebBrowser : Control
    {
        private string _documentText;
        private Uri _url;

        /// <summary>The HTML shown (drawn as the srcdoc of a sandboxed iframe).</summary>
        public string DocumentText
        {
            get => _documentText;
            set
            {
                _documentText = value;
                _url = null;
                RenderVersion.Bump();
                DocumentCompleted?.Invoke(this, new WebBrowserDocumentCompletedEventArgs(null));
            }
        }

        public Uri Url
        {
            get => _url;
            set
            {
                _url = value;
                _documentText = null;
                RenderVersion.Bump();
            }
        }

        public void Navigate(string address) => Url = new Uri(address, UriKind.RelativeOrAbsolute);

        public void Navigate(Uri address) => Url = address;

        public bool ScrollBarsEnabled { get; set; } = true;

        public bool IsWebBrowserContextMenuEnabled { get; set; }

        public bool AllowWebBrowserDrop { get; set; }

        public bool WebBrowserShortcutsEnabled { get; set; }

        /// <summary>The loaded document; its measurements are not known here and read as empty.</summary>
        public HtmlDocument Document { get; } = new HtmlDocument();

        public event WebBrowserNavigatingEventHandler Navigating;

        public event WebBrowserDocumentCompletedEventHandler DocumentCompleted;

        internal void RaiseNavigating(Uri url) => Navigating?.Invoke(this, new WebBrowserNavigatingEventArgs(url, null));
    }

    public class HtmlDocument
    {
        public HtmlElement Body { get; } = new HtmlElement();
    }

    public class HtmlElement
    {
        public Rectangle ClientRectangle => Rectangle.Empty;

        public Rectangle ScrollRectangle => Rectangle.Empty;
    }

    public class WebBrowserNavigatingEventArgs : ComponentModel.CancelEventArgs
    {
        public WebBrowserNavigatingEventArgs(Uri url, string targetFrameName)
        {
            Url = url;
            TargetFrameName = targetFrameName;
        }

        public Uri Url { get; }

        public string TargetFrameName { get; }
    }

    public delegate void WebBrowserNavigatingEventHandler(object sender, WebBrowserNavigatingEventArgs e);

    public class WebBrowserDocumentCompletedEventArgs : EventArgs
    {
        public WebBrowserDocumentCompletedEventArgs(Uri url) => Url = url;

        public Uri Url { get; }
    }

    public delegate void WebBrowserDocumentCompletedEventHandler(object sender, WebBrowserDocumentCompletedEventArgs e);
}

namespace System.Windows.Forms.Integration
{
    /// <summary>Hosts a Windows Forms control in a WPF tree; the renderer draws a hosted <see cref="WebBrowser"/>.</summary>
    [Markup.ContentProperty(nameof(Child))]
    public class WindowsFormsHost : FrameworkElement
    {
        private Control _child;

        public Control Child
        {
            get => _child;
            set
            {
                _child = value;
                RenderVersion.Bump();
            }
        }
    }
}
