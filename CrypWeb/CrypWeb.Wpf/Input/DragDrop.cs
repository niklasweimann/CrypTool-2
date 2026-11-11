using System.Collections.Generic;

namespace System.Windows
{
    [Flags]
    public enum DragDropEffects { None = 0, Copy = 1, Move = 2, Link = 4, Scroll = int.MinValue, All = Copy | Move | Link | Scroll }

    [Flags]
    public enum DragDropKeyStates { None = 0, LeftMouseButton = 1, RightMouseButton = 2, ShiftKey = 4, ControlKey = 8, MiddleMouseButton = 16, AltKey = 32 }

    public static class DataFormats
    {
        public const string Text = "Text";
        public const string UnicodeText = "UnicodeText";
        public const string StringFormat = "System.String";
        public const string FileDrop = "FileDrop";
        public const string Html = "HTML Format";
        public const string Rtf = "Rich Text Format";
    }

    public interface IDataObject
    {
        object GetData(string format);

        object GetData(Type format);

        bool GetDataPresent(string format);

        bool GetDataPresent(Type format);

        void SetData(string format, object data);
    }

    /// <summary>Data that is dragged or copied, keyed by format name.</summary>
    public sealed class DataObject : IDataObject
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public DataObject() { }

        public DataObject(object data) => SetData(data?.GetType().FullName ?? DataFormats.StringFormat, data);

        public DataObject(string format, object data) => SetData(format, data);

        public object GetData(string format) => _data.TryGetValue(format, out object value) ? value : null;

        public object GetData(Type format) => GetData(format.FullName);

        public bool GetDataPresent(string format) => _data.ContainsKey(format);

        public bool GetDataPresent(Type format) => GetDataPresent(format.FullName);

        public void SetData(string format, object data) => _data[format] = data;

        public void SetData(object data) => SetData(data?.GetType().FullName ?? DataFormats.StringFormat, data);

        public bool ContainsText() => _data.ContainsKey(DataFormats.Text) || _data.ContainsKey(DataFormats.UnicodeText);

        public string GetText() => GetData(DataFormats.UnicodeText) as string ?? GetData(DataFormats.Text) as string ?? "";

        public void SetText(string text) => SetData(DataFormats.UnicodeText, text);

        public static readonly RoutedEvent PastingEvent = EventManager.RegisterRoutedEvent("Pasting", RoutingStrategy.Bubble, typeof(DataObjectPastingEventHandler), typeof(DataObject));

        public static void AddPastingHandler(DependencyObject element, DataObjectPastingEventHandler handler) { }

        public static void RemovePastingHandler(DependencyObject element, DataObjectPastingEventHandler handler) { }
    }

    public class DataObjectPastingEventArgs : RoutedEventArgs
    {
        public DataObjectPastingEventArgs(IDataObject dataObject, bool isDragDrop, string formatToApply)
        {
            DataObject = dataObject;
            IsDragDrop = isDragDrop;
            FormatToApply = formatToApply;
        }

        public IDataObject DataObject { get; set; }
        public bool IsDragDrop { get; }
        public string FormatToApply { get; set; }
        public bool CommandCancelled { get; private set; }

        public void CancelCommand() => CommandCancelled = true;
    }

    public delegate void DataObjectPastingEventHandler(object sender, DataObjectPastingEventArgs e);

    public class DragEventArgs : RoutedEventArgs
    {
        public IDataObject Data { get; internal set; } = new DataObject();
        public DragDropEffects Effects { get; set; }
        public DragDropEffects AllowedEffects { get; internal set; } = DragDropEffects.All;
        public DragDropKeyStates KeyStates { get; internal set; }

        internal Point Position { get; set; }

        public Point GetPosition(IInputElement relativeTo) => Position;
    }

    public delegate void DragEventHandler(object sender, DragEventArgs e);

    /// <summary>The steps of a drop that <see cref="DragDrop.DoDragDrop"/> delivers to the target.</summary>
    internal enum DragEventKind { Enter, Over, Drop }

    public class GiveFeedbackEventArgs : RoutedEventArgs
    {
        public DragDropEffects Effects { get; internal set; }
        public bool UseDefaultCursors { get; set; } = true;
    }

    public delegate void GiveFeedbackEventHandler(object sender, GiveFeedbackEventArgs e);

    public class QueryContinueDragEventArgs : RoutedEventArgs
    {
        public bool EscapePressed { get; internal set; }
        public DragDropKeyStates KeyStates { get; internal set; }
        public DragAction Action { get; set; }
    }

    public enum DragAction { Continue, Drop, Cancel }

    public delegate void QueryContinueDragEventHandler(object sender, QueryContinueDragEventArgs e);

    /// <summary>
    /// Drag and drop in the browser host.
    ///
    /// In WPF, <see cref="DoDragDrop"/> blocks until the user releases the mouse, and plugins rely on that: the
    /// code after the call checks what the Drop handler did. The browser cannot block, so it does the whole
    /// gesture itself (the dragged element follows the pointer) and reports it once, when the mouse is released:
    /// source, drop target and positions (<see cref="Hosting.PresentationHost"/>, kind "drag"). The host then
    /// replays the gesture — mouse down and mouse move on the source — with the drop target prepared in
    /// <see cref="PendingDrop"/>. When a mouse-move handler starts <see cref="DoDragDrop"/>, the drop is
    /// delivered right there, so DoDragDrop returns after the Drop handlers ran, as in WPF.
    /// </summary>
    public static class DragDrop
    {
        /// <summary>Where the replayed gesture ends; set by the host only while it replays a drag.</summary>
        internal static (UIElement Target, Point Position)? PendingDrop { get; set; }

        /// <summary>True once DoDragDrop consumed <see cref="PendingDrop"/> during the replay.</summary>
        internal static bool Dropped { get; set; }

        public static DragDropEffects DoDragDrop(DependencyObject dragSource, object data, DragDropEffects allowedEffects)
        {
            if (PendingDrop is not { } drop || Dropped)
            {
                return DragDropEffects.None;
            }
            Dropped = true;

            IDataObject dataObject = data as IDataObject ?? new DataObject(data);
            UIElement target = FindDropTarget(drop.Target);
            if (target == null)
            {
                return DragDropEffects.None;
            }

            var args = new DragEventArgs
            {
                Source = drop.Target, OriginalSource = drop.Target, Data = dataObject, Position = drop.Position,
                AllowedEffects = allowedEffects, Effects = allowedEffects, KeyStates = DragDropKeyStates.LeftMouseButton,
            };
            drop.Target.RaiseDragEvent(DragEventKind.Enter, args);
            args.Handled = false;
            drop.Target.RaiseDragEvent(DragEventKind.Over, args);
            if (args.Effects == DragDropEffects.None)
            {
                return DragDropEffects.None;    // the target refused the data in DragOver
            }
            args.Handled = false;
            drop.Target.RaiseDragEvent(DragEventKind.Drop, args);
            return args.Effects;
        }

        /// <summary>The element under the pointer, or its nearest ancestor, that accepts drops (AllowDrop).</summary>
        private static UIElement FindDropTarget(UIElement hit)
        {
            for (UIElement element = hit; element != null; element = element.ParentElement)
            {
                if (element.AllowDrop)
                {
                    return element;
                }
            }
            return null;
        }
    }

    /// <summary>System metrics as on a default Windows desktop.</summary>
    public static class SystemParameters
    {
        public static double MinimumHorizontalDragDistance => 4;
        public static double MinimumVerticalDragDistance => 4;
        public static double PrimaryScreenWidth => 1920;
        public static double PrimaryScreenHeight => 1080;
        public static double WorkArea_Width => 1920;
        public static Rect WorkArea => new Rect(0, 0, 1920, 1040);
        public static double VerticalScrollBarWidth => 17;
        public static double HorizontalScrollBarHeight => 17;
        public static double CaretWidth => 1;
        public static bool HighContrast => false;
    }

    public enum MessageBoxButton { OK = 0, OKCancel = 1, YesNoCancel = 3, YesNo = 4 }

    public enum MessageBoxResult { None = 0, OK = 1, Cancel = 2, Yes = 6, No = 7 }

    public enum MessageBoxImage { None = 0, Error = 16, Hand = 16, Stop = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }

    /// <summary>
    /// Message boxes cannot block in the browser worker. The text is reported through <see cref="Shown"/>
    /// and the answer is the affirmative one (OK/Yes).
    /// </summary>
    public static class MessageBox
    {
        public static event Action<string, string> Shown;

        public static MessageBoxResult Show(string messageBoxText) => Show(messageBoxText, "", MessageBoxButton.OK);

        public static MessageBoxResult Show(string messageBoxText, string caption) => Show(messageBoxText, caption, MessageBoxButton.OK);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => Show(messageBoxText, caption, button);

        public static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) => Show(messageBoxText, caption, button);

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        {
            Shown?.Invoke(caption, messageBoxText);
            return button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel ? MessageBoxResult.Yes : MessageBoxResult.OK;
        }
    }
}

namespace System.Windows.Documents
{
    /// <summary>Adorners float above an element; they are not drawn in the browser.</summary>
    public class AdornerLayer : Controls.Decorator
    {
        public static AdornerLayer GetAdornerLayer(Media.Visual visual) => new AdornerLayer();

        public void Add(Adorner adorner) { }

        public void Remove(Adorner adorner) { }

        public void Update() { }

        public void Update(UIElement element) { }

        public Adorner[] GetAdorners(UIElement element) => Array.Empty<Adorner>();
    }

    /// <summary>A decoration drawn above an element (selection handles, drag images). Not drawn in the browser.</summary>
    public abstract class Adorner : FrameworkElement
    {
        protected Adorner(UIElement adornedElement) => AdornedElement = adornedElement;

        public UIElement AdornedElement { get; }

        protected virtual Media.Visual GetVisualChild(int index) => null;

        protected virtual int VisualChildrenCount => 0;

        public virtual Media.GeneralTransform GetDesiredTransform(Media.GeneralTransform transform) => transform;
    }
}

namespace System.Windows.Media
{
    /// <summary>Children of a custom element that draws its own visuals.</summary>
    public class VisualCollection : System.Collections.ObjectModel.Collection<Visual>
    {
        public VisualCollection(Visual parent) { }
    }

    /// <summary>A visual that is drawn by code through <see cref="RenderOpen"/>.</summary>
    public class DrawingVisual : Visual
    {
        internal DrawingContext Drawing { get; private set; }

        public DrawingContext RenderOpen()
        {
            Drawing = new DrawingContext();
            RenderVersion.Bump();
            return Drawing;
        }
    }

    public static class Fonts
    {
        /// <summary>The fonts the browser can be expected to have.</summary>
        public static System.Collections.Generic.ICollection<FontFamily> SystemFontFamilies { get; } = new[]
        {
            new FontFamily("Arial"), new FontFamily("Consolas"), new FontFamily("Courier New"), new FontFamily("Georgia"),
            new FontFamily("Segoe UI"), new FontFamily("Tahoma"), new FontFamily("Times New Roman"), new FontFamily("Verdana"),
        };
    }
}
