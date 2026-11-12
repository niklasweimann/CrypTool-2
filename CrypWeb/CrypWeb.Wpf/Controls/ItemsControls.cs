using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace System.Windows.Controls
{
    /// <summary>The items added directly to an items control (as opposed to an <c>ItemsSource</c>).</summary>
    public class ItemCollection : Collection<object>
    {
        private readonly UIElement _owner;

        internal ItemCollection(UIElement owner) => _owner = owner;

        protected override void InsertItem(int index, object item)
        {
            base.InsertItem(index, item);
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
            foreach (object item in this)
            {
                UIElement.SetParent(item, null);
            }
            base.ClearItems();
            RenderVersion.Bump();
        }

        public void Refresh() => RenderVersion.Bump();

        public object CurrentItem => Count > 0 ? this[0] : null;

        /// <summary>The order in which the items are shown (by property values).</summary>
        public System.ComponentModel.SortDescriptionCollection SortDescriptions { get; } = new System.ComponentModel.SortDescriptionCollection();

        // As in WPF, Items is the view of what the control shows: with an ItemsSource it lists the source's items,
        // in the order of SortDescriptions.

        /// <summary>The shown item at a position.</summary>
        public new object this[int index]
        {
            get => View[index];
            set => base[index] = value;
        }

        /// <summary>The number of shown items.</summary>
        public new int Count => View.Count;

        public new int IndexOf(object item) => View.IndexOf(item);

        public new bool Contains(object item) => View.Contains(item);

        private IList<object> View => (_owner as ItemsControl)?.EffectiveItems ?? this;

        /// <summary>The items in the order of <see cref="SortDescriptions"/> (unchanged when there are none).</summary>
        internal IList<object> Sort(IList<object> items)
        {
            if (SortDescriptions.Count == 0)
            {
                return items;
            }
            IEnumerable<object> sorted = items;
            IOrderedEnumerable<object> ordered = null;
            foreach (System.ComponentModel.SortDescription description in SortDescriptions)
            {
                Func<object, object> key = item => SortKey(item, description.PropertyName);
                bool descending = description.Direction == System.ComponentModel.ListSortDirection.Descending;
                ordered = ordered == null
                    ? (descending ? sorted.OrderByDescending(key, Comparer<object>.Default) : sorted.OrderBy(key, Comparer<object>.Default))
                    : (descending ? ordered.ThenByDescending(key, Comparer<object>.Default) : ordered.ThenBy(key, Comparer<object>.Default));
            }
            return ordered.ToList();
        }

        private static object SortKey(object item, string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || item == null)
            {
                return item;
            }
            return item.GetType().GetProperty(propertyName)?.GetValue(item);
        }
    }

    /// <summary>Shows a list of items, either its own <see cref="Items"/> or those of <see cref="ItemsSource"/>.</summary>
    [Markup.ContentProperty(nameof(Items))]
    public class ItemsControl : Control
    {
        public static readonly DependencyProperty ItemsSourceProperty = Dp.Register<IEnumerable, ItemsControl>(nameof(ItemsSource));
        public static readonly DependencyProperty ItemTemplateProperty = Dp.Register<DataTemplate, ItemsControl>(nameof(ItemTemplate));
        public static readonly DependencyProperty ItemsPanelProperty = Dp.Register<ItemsPanelTemplate, ItemsControl>(nameof(ItemsPanel));
        public static readonly DependencyProperty DisplayMemberPathProperty = Dp.Register<string, ItemsControl>(nameof(DisplayMemberPath));
        public static readonly DependencyProperty ItemContainerStyleProperty = Dp.Register<Style, ItemsControl>(nameof(ItemContainerStyle));
        public static readonly DependencyProperty AlternationCountProperty = Dp.Register<int, ItemsControl>(nameof(AlternationCount));

        private readonly Dictionary<object, UIElement> _templatedItems = new Dictionary<object, UIElement>();

        public ItemsControl() => Items = new ItemCollection(this);

        public ItemCollection Items { get; }

        public IEnumerable ItemsSource { get => (IEnumerable)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
        public DataTemplate ItemTemplate { get => (DataTemplate)GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }
        public ItemsPanelTemplate ItemsPanel { get => (ItemsPanelTemplate)GetValue(ItemsPanelProperty); set => SetValue(ItemsPanelProperty, value); }

        private ItemsPanelTemplate _prototypeFor;
        private Panel _prototype;

        /// <summary>One instance of the items panel, read by the renderer for its kind and settings (never drawn itself).</summary>
        internal Panel ItemsPanelPrototype
        {
            get
            {
                ItemsPanelTemplate template = ItemsPanel;
                if (!ReferenceEquals(template, _prototypeFor))
                {
                    _prototypeFor = template;
                    _prototype = template?.LoadContent() as Panel;
                }
                return _prototype;
            }
        }
        public string DisplayMemberPath { get => (string)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
        public Style ItemContainerStyle { get => (Style)GetValue(ItemContainerStyleProperty); set => SetValue(ItemContainerStyleProperty, value); }
        public int AlternationCount { get => (int)GetValue(AlternationCountProperty); set => SetValue(AlternationCountProperty, value); }
        public bool HasItems => EffectiveItems.Count > 0;

        /// <summary>The items that are shown: the bound source if there is one, else <see cref="Items"/>.</summary>
        internal IList<object> EffectiveItems
        {
            get
            {
                IEnumerable source = ItemsSource;
                if (source == null)
                {
                    return Items.SortDescriptions.Count == 0 ? Items : Items.Sort(new List<object>(Items.AsEnumerable()));
                }
                PropertyPathWalker.Watch(source);
                var list = new List<object>();
                foreach (object item in source)
                {
                    list.Add(item);
                }
                return Items.Sort(list);
            }
        }

        /// <summary>
        /// The element that shows <paramref name="item"/>: the item itself if it is an element, otherwise the
        /// item template instantiated with the item as data context (cached per item); null for plain data.
        /// </summary>
        internal UIElement ElementFor(object item)
        {
            if (item is UIElement element)
            {
                return element;
            }
            if (item == null || ItemTemplate == null)
            {
                return null;
            }
            if (!_templatedItems.TryGetValue(item, out UIElement created))
            {
                created = ItemTemplate.LoadContent() as UIElement;
                if (created is FrameworkElement frameworkElement)
                {
                    frameworkElement.DataContext = item;
                }
                SetParent(created, this);
                _templatedItems[item] = created;
            }
            return created;
        }

        private readonly Dictionary<(object Item, object Column), UIElement> _cells = new Dictionary<(object, object), UIElement>();

        /// <summary>The element a column's cell template creates for <paramref name="item"/> (cached per cell).</summary>
        // cells of a grid with a cell style: (item, column) -> the DataGridCell and its text
        private readonly Dictionary<(object Item, object Column), (DataGridCell Cell, TextBlock Text)> _styledCells = new();

        /// <summary>A DataGridCell showing <paramref name="text"/> with the grid's or column's cell style.</summary>
        internal UIElement StyledCell(object item, DataGridColumn column, Style style, string text)
        {
            if (!_styledCells.TryGetValue((item, column), out var cell))
            {
                var textBlock = new TextBlock();
                var created = new DataGridCell { Column = column, Content = textBlock, DataContext = item };
                created.Style = style;
                SetParent(created, this);
                cell = (created, textBlock);
                _styledCells[(item, column)] = cell;
            }
            if (cell.Text.Text != text)
            {
                cell.Text.Text = text ?? "";
            }
            return cell.Cell;
        }

        internal UIElement CellElement(object item, object column, DataTemplate template)
        {
            if (item == null || template == null)
            {
                return null;
            }
            if (!_cells.TryGetValue((item, column), out UIElement created))
            {
                created = template.LoadContent() as UIElement;
                if (created is FrameworkElement frameworkElement)
                {
                    frameworkElement.DataContext = item;
                }
                SetParent(created, this);
                _cells[(item, column)] = created;
            }
            return created;
        }

        /// <summary>Text shown for a plain data item (honours <see cref="DisplayMemberPath"/>).</summary>
        internal string TextFor(object item)
        {
            if (!string.IsNullOrEmpty(DisplayMemberPath))
            {
                item = PropertyPathWalker.GetValue(item, DisplayMemberPath, out _);
            }
            return Convert.ToString(item, Globalization.CultureInfo.CurrentCulture) ?? "";
        }

        internal override IEnumerable<UIElement> ChildElements
        {
            get
            {
                foreach (object item in EffectiveItems)
                {
                    UIElement element = ElementFor(item);
                    if (element != null)
                    {
                        yield return element;
                    }
                }
            }
        }
    }

    public class SelectionChangedEventArgs : RoutedEventArgs
    {
        public IList AddedItems { get; internal set; } = new List<object>();
        public IList RemovedItems { get; internal set; } = new List<object>();
    }

    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    public enum SelectionMode { Single, Multiple, Extended }
}

namespace System.Windows.Controls.Primitives
{
    /// <summary>An items control in which one item can be selected.</summary>
    public class Selector : ItemsControl
    {
        public static readonly DependencyProperty SelectedIndexProperty = Dp.Register<int, Selector>(nameof(SelectedIndex), -1, OnSelectedIndexChanged);
        public static readonly DependencyProperty SelectedValuePathProperty = Dp.Register<string, Selector>(nameof(SelectedValuePath));
        public static readonly DependencyProperty IsSynchronizedWithCurrentItemProperty = Dp.Register<bool?, Selector>(nameof(IsSynchronizedWithCurrentItem));

        public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
        public string SelectedValuePath { get => (string)GetValue(SelectedValuePathProperty); set => SetValue(SelectedValuePathProperty, value); }
        public bool? IsSynchronizedWithCurrentItem { get => (bool?)GetValue(IsSynchronizedWithCurrentItemProperty); set => SetValue(IsSynchronizedWithCurrentItemProperty, value); }

        public object SelectedItem
        {
            get
            {
                IList<object> items = EffectiveItems;
                int index = SelectedIndex;
                return index >= 0 && index < items.Count ? items[index] : null;
            }
            set => SelectedIndex = EffectiveItems.IndexOf(value);
        }

        public object SelectedValue
        {
            get
            {
                object item = SelectedItem;
                return string.IsNullOrEmpty(SelectedValuePath) ? item : PropertyPathWalker.GetValue(item, SelectedValuePath, out _);
            }
            set => SelectedItem = value;
        }

        public IList SelectedItems
        {
            get
            {
                var selected = new List<object>();
                if (SelectedItem != null)
                {
                    selected.Add(SelectedItem);
                }
                return selected;
            }
        }

        public event SelectionChangedEventHandler SelectionChanged;

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (Selector)d;
            var args = new SelectionChangedEventArgs { Source = selector };
            if (selector.SelectedItem != null)
            {
                args.AddedItems.Add(selector.SelectedItem);
            }
            selector.SelectionChanged?.Invoke(selector, args);
        }

        /// <summary>A selection made in the browser.</summary>
        internal void SelectFromUser(int index) => SelectedIndex = index;

        /// <summary>A double click on an item in the browser.</summary>
        internal void ActivateFromUser(int index)
        {
            SelectedIndex = index;
            object item = SelectedItem;
            if (item != null)
            {
                OnItemActivated(item);
            }
        }

        /// <summary>Called when the user double-clicks an item (WPF: MouseDoubleClick on the item container).</summary>
        protected virtual void OnItemActivated(object item) { }
    }
}

namespace System.Windows.Controls
{
    using System.Windows.Controls.Primitives;

    public class ListBox : Selector
    {
        public SelectionMode SelectionMode { get; set; }

        public void ScrollIntoView(object item) { }

        public void UnselectAll() => SelectedIndex = -1;

        public void SelectAll() { }
    }

    public class ListBoxItem : ContentControl
    {
        public bool IsSelected { get; set; }
    }

    public class ComboBox : Selector
    {
        public static readonly DependencyProperty TextProperty = Dp.Register<string, ComboBox>(nameof(Text), "");

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public bool IsEditable { get; set; }
        public bool IsReadOnly { get; set; }
        public bool IsDropDownOpen { get; set; }
        public double MaxDropDownHeight { get; set; }

        public event EventHandler DropDownOpened;
        public event EventHandler DropDownClosed;
    }

    public class ComboBoxItem : ListBoxItem { }

    /// <summary>A tab page.</summary>
    public class TabItem : HeaderedContentControl
    {
        public static readonly DependencyProperty IsSelectedProperty = Dp.Register<bool, TabItem>(nameof(IsSelected));

        public bool IsSelected
        {
            get => ParentElement is TabControl tabs ? ReferenceEquals(tabs.SelectedItem, this) : (bool)GetValue(IsSelectedProperty);
            set
            {
                SetValue(IsSelectedProperty, value);
                if (value && ParentElement is TabControl tabs)
                {
                    tabs.SelectedItem = this;
                }
            }
        }
    }

    public class TabControl : Selector
    {
        public Dock TabStripPlacement { get; set; } = Dock.Top;

        /// <summary>The page that is shown: the selected one, or the first when nothing was selected yet.</summary>
        internal TabItem CurrentTab
        {
            get
            {
                if (SelectedItem is TabItem selected)
                {
                    return selected;
                }
                foreach (object item in EffectiveItems)
                {
                    if (item is TabItem first)
                    {
                        return first;
                    }
                }
                return null;
            }
        }
    }

    // ---- lists with columns ---------------------------------------------------------------------------------

    public abstract class ViewBase : DependencyObject { }

    public class GridViewColumn : DependencyObject
    {
        public static readonly DependencyProperty WidthProperty = Dp.Register<double, GridViewColumn>(nameof(Width), double.NaN);

        public object Header { get; set; }
        public double Width { get => (double)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
        public Style HeaderContainerStyle { get; set; }
        public BindingBase DisplayMemberBinding { get; set; }
        public DataTemplate CellTemplate { get; set; }
        public DataTemplate HeaderTemplate { get; set; }
        public double ActualWidth => double.IsNaN(Width) ? 0 : Width;
    }

    public class GridViewColumnCollection : ObservableCollection<GridViewColumn> { }

    [Markup.ContentProperty(nameof(Columns))]
    public class GridView : ViewBase
    {
        public GridViewColumnCollection Columns { get; } = new GridViewColumnCollection();
        public bool AllowsColumnReorder { get; set; }
        public Style ColumnHeaderContainerStyle { get; set; }
    }

    public class GridViewColumnHeader : Primitives.ButtonBase
    {
        public GridViewColumn Column { get; set; }
    }

    public class ListViewItem : ListBoxItem { }

    public class ListView : ListBox
    {
        public static readonly DependencyProperty ViewProperty = Dp.Register<ViewBase, ListView>(nameof(View));

        public ViewBase View { get => (ViewBase)GetValue(ViewProperty); set => SetValue(ViewProperty, value); }
    }

    public enum DataGridHeadersVisibility { None, Column, Row, All }

    public enum DataGridGridLinesVisibility { All, Horizontal, None, Vertical }

    public enum DataGridSelectionUnit { Cell, FullRow, CellOrRowHeader }

    public abstract class DataGridColumn : DependencyObject
    {
        public object Header { get; set; }
        public DataGridLength Width { get; set; }
        public double MinWidth { get; set; }
        public double MaxWidth { get; set; } = double.PositiveInfinity;
        public bool IsReadOnly { get; set; }
        public Visibility Visibility { get; set; }
        public int DisplayIndex { get; set; }
        public Style CellStyle { get; set; }
        public Style HeaderStyle { get; set; }
        public string SortMemberPath { get; set; }
        public bool CanUserSort { get; set; }

        /// <summary>The binding that provides the cell value (null for template columns).</summary>
        internal virtual BindingBase CellBinding => null;

        internal virtual DataTemplate CellContentTemplate => null;
    }

    public enum DataGridLengthUnitType { Auto, Pixel, SizeToCells, SizeToHeader, Star }

    /// <summary>Width of a data grid column: pixels, a share of the free width ("2*") or sized to its content.</summary>
    public struct DataGridLength
    {
        public DataGridLength(double pixels) : this(pixels, DataGridLengthUnitType.Pixel) { }

        public DataGridLength(double value, DataGridLengthUnitType type)
        {
            Value = value;
            UnitType = type;
        }

        public double Value { get; }

        public DataGridLengthUnitType UnitType { get; }

        public bool IsAbsolute => UnitType == DataGridLengthUnitType.Pixel;

        public bool IsStar => UnitType == DataGridLengthUnitType.Star;

        public bool IsAuto => UnitType == DataGridLengthUnitType.Auto;

        public static DataGridLength Auto => new DataGridLength(1, DataGridLengthUnitType.Auto);
        public static DataGridLength SizeToCells => new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
        public static DataGridLength SizeToHeader => new DataGridLength(1, DataGridLengthUnitType.SizeToHeader);

        public static implicit operator DataGridLength(double pixels) => new DataGridLength(pixels);

        /// <summary>XAML text: "Auto", "SizeToCells", "SizeToHeader", "120" or "2*".</summary>
        public static DataGridLength Parse(string text)
        {
            text = text.Trim();
            if (Enum.TryParse(text, true, out DataGridLengthUnitType named) && named != DataGridLengthUnitType.Pixel && named != DataGridLengthUnitType.Star)
            {
                return new DataGridLength(1, named);
            }
            if (text.EndsWith("*", StringComparison.Ordinal))
            {
                string factor = text.Substring(0, text.Length - 1);
                return new DataGridLength(factor.Length == 0 ? 1 : double.Parse(factor, System.Globalization.CultureInfo.InvariantCulture), DataGridLengthUnitType.Star);
            }
            return new DataGridLength(double.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public class DataGridBoundColumn : DataGridColumn
    {
        public BindingBase Binding { get; set; }
        public Style ElementStyle { get; set; }
        public Style EditingElementStyle { get; set; }

        internal override BindingBase CellBinding => Binding;
    }

    public class DataGridTextColumn : DataGridBoundColumn
    {
        public double FontSize { get; set; }
        public Media.Brush Foreground { get; set; }
    }

    public class DataGridCheckBoxColumn : DataGridBoundColumn { }

    public class DataGridTemplateColumn : DataGridColumn
    {
        public DataTemplate CellTemplate { get; set; }
        public DataTemplate CellEditingTemplate { get; set; }

        internal override DataTemplate CellContentTemplate => CellTemplate;
    }

    public class DataGridAutoGeneratingColumnEventArgs : EventArgs
    {
        public DataGridColumn Column { get; set; }
        public string PropertyName { get; set; }
        public Type PropertyType { get; set; }
        public bool Cancel { get; set; }
    }

    public class DataGridRow : Control
    {
        public object Item { get; set; }
        public object Header { get; set; }
        public int GetIndex() => 0;
    }

    public class DataGridCell : ContentControl
    {
        public DataGridColumn Column { get; set; }
        public bool IsSelected { get; set; }
        public bool IsEditing { get; set; }
    }

    public class DataGridRowEventArgs : EventArgs
    {
        public DataGridRowEventArgs(DataGridRow row) => Row = row;

        public DataGridRow Row { get; }
    }

    public class SelectedCellsChangedEventArgs : EventArgs { }

    public delegate void SelectedCellsChangedEventHandler(object sender, SelectedCellsChangedEventArgs e);

    /// <summary>A table of data. Columns are declared or generated from the items (data tables, object properties).</summary>
    public class DataGrid : Selector
    {
        public ObservableCollection<DataGridColumn> Columns { get; } = new ObservableCollection<DataGridColumn>();
        public bool AutoGenerateColumns { get; set; } = true;
        public double ColumnHeaderHeight { get; set; } = double.NaN;
        public bool AreRowDetailsFrozen { get; set; }
        public bool IsReadOnly { get; set; }
        public bool CanUserAddRows { get; set; }
        public bool CanUserDeleteRows { get; set; }
        public bool CanUserReorderColumns { get; set; }
        public bool CanUserResizeColumns { get; set; }
        public bool CanUserResizeRows { get; set; }
        public bool CanUserSortColumns { get; set; }
        public DataGridHeadersVisibility HeadersVisibility { get; set; } = DataGridHeadersVisibility.All;
        public DataGridGridLinesVisibility GridLinesVisibility { get; set; }
        public DataGridSelectionUnit SelectionUnit { get; set; }
        public SelectionMode SelectionMode { get; set; }
        public double RowHeight { get; set; } = double.NaN;
        public double ColumnWidth { get; set; } = double.NaN;
        public double MinColumnWidth { get; set; }
        public double RowHeaderWidth { get; set; }
        public Media.Brush RowBackground { get; set; }
        public Media.Brush AlternatingRowBackground { get; set; }
        public Media.Brush HorizontalGridLinesBrush { get; set; }
        public Media.Brush VerticalGridLinesBrush { get; set; }
        public Style CellStyle { get; set; }
        public Style RowStyle { get; set; }
        public Style ColumnHeaderStyle { get; set; }
        public Style RowHeaderStyle { get; set; }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }

        public event EventHandler<DataGridAutoGeneratingColumnEventArgs> AutoGeneratingColumn;
        public event EventHandler<DataGridRowEventArgs> LoadingRow;
        public event SelectedCellsChangedEventHandler SelectedCellsChanged;

        internal bool RaiseAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            AutoGeneratingColumn?.Invoke(this, e);
            return !e.Cancel;
        }

        public void ScrollIntoView(object item) { }

        public void UnselectAll() => SelectedIndex = -1;
    }
}
