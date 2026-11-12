namespace System.Windows.Controls
{
    /// <summary>A row of command buttons; drawn as its items side by side.</summary>
    public class ToolBar : ItemsControl
    {
        public static readonly DependencyProperty BandProperty = Dp.Register<int, ToolBar>(nameof(Band));
        public static readonly DependencyProperty BandIndexProperty = Dp.Register<int, ToolBar>(nameof(BandIndex));

        public ToolBar()
        {
            // the toolbar's own panel is horizontal (ToolBarPanel)
            ItemsPanel = new ItemsPanelTemplate { ContentFactory = () => new StackPanel { Orientation = Orientation.Horizontal } };
        }

        public int Band { get => (int)GetValue(BandProperty); set => SetValue(BandProperty, value); }

        public int BandIndex { get => (int)GetValue(BandIndexProperty); set => SetValue(BandIndexProperty, value); }

        public string Header { get; set; }
    }

    /// <summary>The panel that arranges the items of a tool bar: a stack panel.</summary>
    public class ToolBarPanel : StackPanel { }

    /// <summary>Holds toolbars next to each other (drawn as a horizontal stack).</summary>
    [Markup.ContentProperty(nameof(ToolBars))]
    public class ToolBarTray : StackPanel
    {
        public ToolBarTray()
        {
            Orientation = Orientation.Horizontal;
            ToolBars = new ToolBarCollection(this);
        }

        public ToolBarCollection ToolBars { get; }

        public bool IsLocked { get; set; }
    }

    /// <summary>The toolbars of a tray; they are the tray's children.</summary>
    public sealed class ToolBarCollection : System.Collections.ObjectModel.Collection<ToolBar>
    {
        private readonly ToolBarTray _tray;

        internal ToolBarCollection(ToolBarTray tray) => _tray = tray;

        protected override void InsertItem(int index, ToolBar item)
        {
            base.InsertItem(index, item);
            _tray.Children.Add(item);
        }
    }
}

namespace System.Windows.Media
{
    /// <summary>How digits are substituted when text is shaped; text here is drawn by the browser, which decides.</summary>
    public class NumberSubstitution
    {
        // how digits are shown in a culture; the browser shows Western digits, so these are only stored
        public static readonly DependencyProperty CultureSourceProperty =
            DependencyProperty.RegisterAttached("CultureSource", typeof(NumberCultureSource), typeof(NumberSubstitution));
        public static readonly DependencyProperty SubstitutionProperty =
            DependencyProperty.RegisterAttached("Substitution", typeof(NumberSubstitutionMethod), typeof(NumberSubstitution));

        public static void SetCultureSource(DependencyObject target, NumberCultureSource value) => target.SetValue(CultureSourceProperty, value);
        public static NumberCultureSource GetCultureSource(DependencyObject target) => (NumberCultureSource)target.GetValue(CultureSourceProperty);
        public static void SetSubstitution(DependencyObject target, NumberSubstitutionMethod value) => target.SetValue(SubstitutionProperty, value);
        public static NumberSubstitutionMethod GetSubstitution(DependencyObject target) => (NumberSubstitutionMethod)target.GetValue(SubstitutionProperty);

        public NumberSubstitution() { }

        public NumberSubstitution(NumberCultureSource source, Globalization.CultureInfo cultureOverride, NumberSubstitutionMethod substitution) { }
    }

    public enum NumberCultureSource { Text, User, Override }

    public enum NumberSubstitutionMethod { AsCulture, Context, European, NativeNational, Traditional }

    public enum TextFormattingMode { Ideal, Display }
}

namespace System.Windows
{
    /// <summary>How the height of text lines is determined (WPF: MaxHeight or BlockLineHeight).</summary>
    public enum LineStackingStrategy { BlockLineHeight, MaxHeight }
}
