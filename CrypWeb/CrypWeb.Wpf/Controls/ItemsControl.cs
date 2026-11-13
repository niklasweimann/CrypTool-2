using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace System.Windows.Controls
{
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

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public ItemsPanelTemplate ItemsPanel
        {
            get => (ItemsPanelTemplate)GetValue(ItemsPanelProperty);
            set => SetValue(ItemsPanelProperty, value);
        }

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

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public Style ItemContainerStyle
        {
            get => (Style)GetValue(ItemContainerStyleProperty);
            set => SetValue(ItemContainerStyleProperty, value);
        }

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
}
