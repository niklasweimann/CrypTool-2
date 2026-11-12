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
        public NumberSubstitution() { }

        public NumberSubstitution(NumberCultureSource source, Globalization.CultureInfo cultureOverride, NumberSubstitutionMethod substitution) { }
    }

    public enum NumberCultureSource { Text, User, Override }

    public enum NumberSubstitutionMethod { AsCulture, Context, European, NativeNational, Traditional }

    public enum TextFormattingMode { Ideal, Display }
}
