// Menus. Plugins declare them in XAML (a context menu on a log list, a menu bar in a tool); they load as element
// trees so that code can reach their items. A Menu is drawn like its items in a row; context menus open on a
// right click in WPF, which the browser page does not offer, so they are kept but not shown.
using System.Windows.Input;

namespace System.Windows.Controls
{
    /// <summary>A menu bar: its items side by side.</summary>
    public class Menu : ItemsControl
    {
        public bool IsMainMenu { get; set; }
    }

    /// <summary>One entry of a menu: a header, optional sub-items, and a command or Click handler.</summary>
    public class MenuItem : ItemsControl
    {
        public static readonly DependencyProperty HeaderProperty = Dp.Register<object, MenuItem>(nameof(Header));
        public static readonly DependencyProperty IsCheckedProperty = Dp.Register<bool, MenuItem>(nameof(IsChecked));

        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

        public bool IsChecked { get => (bool)GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

        public bool IsCheckable { get; set; }

        public object Icon { get; set; }

        public string InputGestureText { get; set; }

        public ICommand Command { get; set; }

        public object CommandParameter { get; set; }

        public IInputElement CommandTarget { get; set; }

        public DataTemplate HeaderTemplate { get; set; }

        public bool StaysOpenOnClick { get; set; }

        public event RoutedEventHandler Click;

        public event RoutedEventHandler Checked;

        public event RoutedEventHandler Unchecked;

        /// <summary>What a click does: toggles a checkable item, runs Click and the command.</summary>
        protected virtual void OnClick()
        {
            if (IsCheckable)
            {
                IsChecked = !IsChecked;
                (IsChecked ? Checked : Unchecked)?.Invoke(this, new RoutedEventArgs { Source = this });
            }
            Click?.Invoke(this, new RoutedEventArgs { Source = this });
            if (Command?.CanExecute(CommandParameter) == true)
            {
                Command.Execute(CommandParameter);
            }
        }

        public override string ToString() => Convert.ToString(Header);
    }

    /// <summary>A menu that WPF opens on a right click; not shown in the browser.</summary>
    public class ContextMenu : ItemsControl
    {
        public bool IsOpen { get; set; }

        public bool StaysOpen { get; set; }

        public Primitives.PlacementMode Placement { get; set; }

        public UIElement PlacementTarget { get; set; }

        public event RoutedEventHandler Opened;

        public event RoutedEventHandler Closed;
    }
}

namespace System.Windows.Controls.Primitives
{
    /// <summary>Where a popup (context menu, tool tip) opens relative to its target.</summary>
    public enum PlacementMode
    {
        Absolute, Relative, Bottom, Center, Right, AbsolutePoint, RelativePoint, Mouse, MousePoint, Left, Top, Custom,
    }
}
