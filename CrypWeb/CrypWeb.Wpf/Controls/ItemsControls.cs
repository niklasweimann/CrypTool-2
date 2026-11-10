using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
                    return Items;
                }
                PropertyPathWalker.Watch(source);
                var list = new List<object>();
                foreach (object item in source)
                {
                    list.Add(item);
                }
                return list;
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
        public object Header { get; set; }
        public double Width { get; set; } = double.NaN;
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

    public struct DataGridLength
    {
        public DataGridLength(double pixels) => Value = pixels;

        public double Value { get; }

        public static DataGridLength Auto => default;
        public static DataGridLength SizeToCells => default;
        public static DataGridLength SizeToHeader => default;

        public static implicit operator DataGridLength(double pixels) => new DataGridLength(pixels);
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

    /// <summary>A table of data. Columns are declared or generated from the items (data tables, object properties).</summary>
    public class DataGrid : Selector
    {
        public ObservableCollection<DataGridColumn> Columns { get; } = new ObservableCollection<DataGridColumn>();
        public bool AutoGenerateColumns { get; set; } = true;
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

        internal bool RaiseAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
        {
            AutoGeneratingColumn?.Invoke(this, e);
            return !e.Cancel;
        }

        public void ScrollIntoView(object item) { }

        public void UnselectAll() => SelectedIndex = -1;
    }
}
