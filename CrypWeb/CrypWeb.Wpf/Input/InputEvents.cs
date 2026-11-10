namespace System.Windows
{
    /// <summary>Marker of elements that can receive input (parameter type of <c>GetPosition</c>).</summary>
    public interface IInputElement { }
}

namespace System.Windows.Input
{
    public enum MouseButton { Left, Middle, Right, XButton1, XButton2 }

    public enum MouseButtonState { Released, Pressed }

    [Flags]
    public enum ModifierKeys { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }

    public class InputEventArgs : RoutedEventArgs
    {
        public int Timestamp { get; set; }
    }

    public class MouseEventArgs : InputEventArgs
    {
        /// <summary>Pointer position relative to the element that raised the event (as reported by the browser).</summary>
        internal Point Position { get; set; }

        public MouseButtonState LeftButton { get; internal set; }
        public MouseButtonState RightButton { get; internal set; }
        public MouseButtonState MiddleButton { get; internal set; }

        public Point GetPosition(IInputElement relativeTo) => Position;
    }

    public class MouseButtonEventArgs : MouseEventArgs
    {
        public MouseButton ChangedButton { get; internal set; }
        public MouseButtonState ButtonState { get; internal set; }
        public int ClickCount { get; internal set; } = 1;
    }

    public class MouseWheelEventArgs : MouseEventArgs
    {
        public int Delta { get; internal set; }
    }

    public class KeyEventArgs : InputEventArgs
    {
        public Key Key { get; internal set; }
        public Key SystemKey => Key.None;
        public bool IsRepeat { get; internal set; }
        public bool IsDown => true;
        public bool IsUp => false;
    }

    public class TextCompositionEventArgs : InputEventArgs
    {
        public string Text { get; internal set; } = "";
    }

    public delegate void MouseEventHandler(object sender, MouseEventArgs e);

    public delegate void MouseButtonEventHandler(object sender, MouseButtonEventArgs e);

    public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);

    public delegate void KeyEventHandler(object sender, KeyEventArgs e);

    public delegate void TextCompositionEventHandler(object sender, TextCompositionEventArgs e);

    /// <summary>The keyboard state is not tracked in the browser host: no key is ever reported as held down.</summary>
    public static class Keyboard
    {
        public static ModifierKeys Modifiers => ModifierKeys.None;

        public static bool IsKeyDown(Key key) => false;

        public static bool IsKeyUp(Key key) => true;

        public static IInputElement Focus(IInputElement element) => element;

        public static void ClearFocus() { }
    }

    public static class Mouse
    {
        public static MouseButtonState LeftButton => MouseButtonState.Released;

        public static Cursor OverrideCursor { get; set; }

        public static Point GetPosition(IInputElement relativeTo) => default;

        public static bool Capture(IInputElement element) => true;
    }

    /// <summary>A mouse pointer shape; rendered as the CSS <c>cursor</c> property.</summary>
    public sealed class Cursor
    {
        internal Cursor(string cssName) => CssName = cssName;

        internal string CssName { get; }

        public override string ToString() => CssName;
    }

    public static class Cursors
    {
        public static Cursor Arrow { get; } = new Cursor("default");
        public static Cursor Hand { get; } = new Cursor("pointer");
        public static Cursor Wait { get; } = new Cursor("wait");
        public static Cursor AppStarting { get; } = new Cursor("progress");
        public static Cursor Cross { get; } = new Cursor("crosshair");
        public static Cursor IBeam { get; } = new Cursor("text");
        public static Cursor Help { get; } = new Cursor("help");
        public static Cursor No { get; } = new Cursor("not-allowed");
        public static Cursor None { get; } = new Cursor("none");
        public static Cursor SizeAll { get; } = new Cursor("move");
        public static Cursor SizeWE { get; } = new Cursor("ew-resize");
        public static Cursor SizeNS { get; } = new Cursor("ns-resize");
        public static Cursor SizeNWSE { get; } = new Cursor("nwse-resize");
        public static Cursor SizeNESW { get; } = new Cursor("nesw-resize");
        public static Cursor ScrollAll { get; } = new Cursor("all-scroll");
        public static Cursor Pen { get; } = new Cursor("crosshair");
        public static Cursor UpArrow { get; } = new Cursor("n-resize");

        internal static Cursor Parse(string name)
        {
            var property = typeof(Cursors).GetProperty(name.Trim(), Reflection.BindingFlags.Public | Reflection.BindingFlags.Static | Reflection.BindingFlags.IgnoreCase);
            return property?.GetValue(null) as Cursor ?? Arrow;
        }
    }

    public enum Key
    {
        None, Cancel, Back, Tab, LineFeed, Clear, Return, Enter = Return, Pause, Capital, CapsLock = Capital, Escape, Space,
        PageUp, Prior = PageUp, PageDown, Next = PageDown, End, Home, Left, Up, Right, Down, Select, Print, Execute,
        PrintScreen, Snapshot = PrintScreen, Insert, Delete, Help,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        LWin, RWin, Apps, Sleep,
        NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
        Multiply, Add, Separator, Subtract, Decimal, Divide,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        NumLock, Scroll, LeftShift, RightShift, LeftCtrl, RightCtrl, LeftAlt, RightAlt,
        OemSemicolon, Oem1 = OemSemicolon, OemPlus, OemComma, OemMinus, OemPeriod, OemQuestion, Oem2 = OemQuestion,
        OemTilde, Oem3 = OemTilde, OemOpenBrackets, Oem4 = OemOpenBrackets, OemPipe, Oem5 = OemPipe,
        OemCloseBrackets, Oem6 = OemCloseBrackets, OemQuotes, Oem7 = OemQuotes, Oem8, OemBackslash, Oem102 = OemBackslash,
        System, DeadCharProcessed,
    }
}
