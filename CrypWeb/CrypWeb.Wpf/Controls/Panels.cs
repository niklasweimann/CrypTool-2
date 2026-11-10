using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace System.Windows.Controls
{
    public enum Orientation { Horizontal, Vertical }

    public enum Dock { Left, Top, Right, Bottom }

    /// <summary>The children of a panel. Adding or removing a child attaches or detaches it from the tree.</summary>
    public class UIElementCollection : Collection<UIElement>
    {
        private readonly UIElement _owner;

        internal UIElementCollection(UIElement owner) => _owner = owner;

        public void AddRange(IEnumerable<UIElement> elements)
        {
            foreach (UIElement element in elements)
            {
                Add(element);
            }
        }

        public void RemoveRange(int index, int count)
        {
            for (int i = 0; i < count && index < Count; i++)
            {
                RemoveAt(index);
            }
        }

        protected override void InsertItem(int index, UIElement item)
        {
            base.InsertItem(index, item);
            UIElement.SetParent(item, _owner);
            RenderVersion.Bump();
        }

        protected override void SetItem(int index, UIElement item)
        {
            UIElement.SetParent(this[index], null);
            base.SetItem(index, item);
            UIElement.SetParent(item, _owner);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            UIElement.SetParent(this[index], null);
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            foreach (UIElement item in this)
            {
                UIElement.SetParent(item, null);
            }
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    /// <summary>An element that arranges several children.</summary>
    [Markup.ContentProperty(nameof(Children))]
    public abstract class Panel : FrameworkElement
    {
        public static readonly DependencyProperty BackgroundProperty = Dp.Register<Brush, Panel>(nameof(Background));
        public static readonly DependencyProperty ZIndexProperty = Dp.Attached<int, Panel>("ZIndex");
        public static readonly DependencyProperty IsItemsHostProperty = Dp.Register<bool, Panel>(nameof(IsItemsHost));

        protected Panel() => Children = new UIElementCollection(this);

        public UIElementCollection Children { get; }

        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public bool IsItemsHost { get => (bool)GetValue(IsItemsHostProperty); set => SetValue(IsItemsHostProperty, value); }

        protected internal UIElementCollection InternalChildren => Children;

        internal override IEnumerable<UIElement> ChildElements => Children;

        public static void SetZIndex(UIElement element, int value) => element.SetValue(ZIndexProperty, value);

        public static int GetZIndex(UIElement element) => (int)element.GetValue(ZIndexProperty);
    }

    public class RowDefinition : DependencyObject
    {
        public static readonly DependencyProperty HeightProperty = Dp.Register<GridLength, RowDefinition>(nameof(Height), new GridLength(1, GridUnitType.Star));

        public GridLength Height { get => (GridLength)GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
        public double MinHeight { get; set; }
        public double MaxHeight { get; set; } = double.PositiveInfinity;
        public string Name { get; set; }
        public string SharedSizeGroup { get; set; }
        public double ActualHeight => Height.IsAbsolute ? Height.Value : 0;
    }

    public class ColumnDefinition : DependencyObject
    {
        public static readonly DependencyProperty WidthProperty = Dp.Register<GridLength, ColumnDefinition>(nameof(Width), new GridLength(1, GridUnitType.Star));

        public GridLength Width { get => (GridLength)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
        public double MinWidth { get; set; }
        public double MaxWidth { get; set; } = double.PositiveInfinity;
        public string Name { get; set; }
        public string SharedSizeGroup { get; set; }
        public double ActualWidth => Width.IsAbsolute ? Width.Value : 0;
    }

    public class RowDefinitionCollection : Collection<RowDefinition>
    {
        protected override void InsertItem(int index, RowDefinition item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    public class ColumnDefinitionCollection : Collection<ColumnDefinition>
    {
        protected override void InsertItem(int index, ColumnDefinition item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    /// <summary>Rows and columns; children are placed with the attached Row/Column(/Span) properties.</summary>
    public class Grid : Panel
    {
        public static readonly DependencyProperty RowProperty = Dp.Attached<int, Grid>("Row");
        public static readonly DependencyProperty ColumnProperty = Dp.Attached<int, Grid>("Column");
        public static readonly DependencyProperty RowSpanProperty = Dp.Attached<int, Grid>("RowSpan", 1);
        public static readonly DependencyProperty ColumnSpanProperty = Dp.Attached<int, Grid>("ColumnSpan", 1);
        public static readonly DependencyProperty ShowGridLinesProperty = Dp.Register<bool, Grid>(nameof(ShowGridLines));
        public static readonly DependencyProperty IsSharedSizeScopeProperty = Dp.Attached<bool, Grid>("IsSharedSizeScope");

        public RowDefinitionCollection RowDefinitions { get; } = new RowDefinitionCollection();
        public ColumnDefinitionCollection ColumnDefinitions { get; } = new ColumnDefinitionCollection();
        public bool ShowGridLines { get => (bool)GetValue(ShowGridLinesProperty); set => SetValue(ShowGridLinesProperty, value); }

        public static void SetRow(UIElement element, int value) => element.SetValue(RowProperty, value);
        public static int GetRow(UIElement element) => (int)element.GetValue(RowProperty);
        public static void SetColumn(UIElement element, int value) => element.SetValue(ColumnProperty, value);
        public static int GetColumn(UIElement element) => (int)element.GetValue(ColumnProperty);
        public static void SetRowSpan(UIElement element, int value) => element.SetValue(RowSpanProperty, value);
        public static int GetRowSpan(UIElement element) => (int)element.GetValue(RowSpanProperty);
        public static void SetColumnSpan(UIElement element, int value) => element.SetValue(ColumnSpanProperty, value);
        public static int GetColumnSpan(UIElement element) => (int)element.GetValue(ColumnSpanProperty);
        public static void SetIsSharedSizeScope(UIElement element, bool value) => element.SetValue(IsSharedSizeScopeProperty, value);
    }

    /// <summary>Children at explicit coordinates (attached Left/Top/Right/Bottom).</summary>
    public class Canvas : Panel
    {
        public static readonly DependencyProperty LeftProperty = Dp.Attached<double, Canvas>("Left", double.NaN);
        public static readonly DependencyProperty TopProperty = Dp.Attached<double, Canvas>("Top", double.NaN);
        public static readonly DependencyProperty RightProperty = Dp.Attached<double, Canvas>("Right", double.NaN);
        public static readonly DependencyProperty BottomProperty = Dp.Attached<double, Canvas>("Bottom", double.NaN);

        public static void SetLeft(UIElement element, double value) => element.SetValue(LeftProperty, value);
        public static double GetLeft(UIElement element) => (double)element.GetValue(LeftProperty);
        public static void SetTop(UIElement element, double value) => element.SetValue(TopProperty, value);
        public static double GetTop(UIElement element) => (double)element.GetValue(TopProperty);
        public static void SetRight(UIElement element, double value) => element.SetValue(RightProperty, value);
        public static double GetRight(UIElement element) => (double)element.GetValue(RightProperty);
        public static void SetBottom(UIElement element, double value) => element.SetValue(BottomProperty, value);
        public static double GetBottom(UIElement element) => (double)element.GetValue(BottomProperty);
    }

    public class StackPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty = Dp.Register<Orientation, StackPanel>(nameof(Orientation), Orientation.Vertical);

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
    }

    public class VirtualizingStackPanel : StackPanel { }

    public class WrapPanel : Panel
    {
        public static readonly DependencyProperty OrientationProperty = Dp.Register<Orientation, WrapPanel>(nameof(Orientation), Orientation.Horizontal);
        public static readonly DependencyProperty ItemWidthProperty = Dp.Register<double, WrapPanel>(nameof(ItemWidth), double.NaN);
        public static readonly DependencyProperty ItemHeightProperty = Dp.Register<double, WrapPanel>(nameof(ItemHeight), double.NaN);

        public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
        public double ItemWidth { get => (double)GetValue(ItemWidthProperty); set => SetValue(ItemWidthProperty, value); }
        public double ItemHeight { get => (double)GetValue(ItemHeightProperty); set => SetValue(ItemHeightProperty, value); }
    }

    public class DockPanel : Panel
    {
        public static readonly DependencyProperty DockProperty = Dp.Attached<Dock, DockPanel>("Dock", Dock.Left);
        public static readonly DependencyProperty LastChildFillProperty = Dp.Register<bool, DockPanel>(nameof(LastChildFill), true);

        public bool LastChildFill { get => (bool)GetValue(LastChildFillProperty); set => SetValue(LastChildFillProperty, value); }

        public static void SetDock(UIElement element, Dock value) => element.SetValue(DockProperty, value);
        public static Dock GetDock(UIElement element) => (Dock)element.GetValue(DockProperty);
    }
}

namespace System.Windows.Controls.Primitives
{
    /// <summary>A grid in which all cells have the same size.</summary>
    public class UniformGrid : Panel
    {
        public static readonly DependencyProperty RowsProperty = Dp.Register<int, UniformGrid>(nameof(Rows));
        public static readonly DependencyProperty ColumnsProperty = Dp.Register<int, UniformGrid>(nameof(Columns));

        public int Rows { get => (int)GetValue(RowsProperty); set => SetValue(RowsProperty, value); }
        public int Columns { get => (int)GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
        public int FirstColumn { get; set; }
    }
}
