using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace System.Windows.Controls
{

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
            if (Enum.TryParse(text, true, out DataGridLengthUnitType named)
                && named != DataGridLengthUnitType.Pixel
                && named != DataGridLengthUnitType.Star)
            {
                return new DataGridLength(1, named);
            }
            if (text.EndsWith("*", StringComparison.Ordinal))
            {
                string factor = text.Substring(0, text.Length - 1);
                return new DataGridLength(factor.Length == 0 ? 1 : double.Parse(factor, System.Globalization.CultureInfo.InvariantCulture),
                    DataGridLengthUnitType.Star);
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
