// Tables and lists in flow documents (the RichTextBox pages of ChaCha, for example). They are written as HTML
// tables and lists by the renderer (HtmlRenderer.Layout.cs, WriteBlocks).
using System.Collections.ObjectModel;

namespace System.Windows.Documents
{
    /// <summary>Blocks that hold other blocks (list items, table cells).</summary>
    public interface IBlockContainer
    {
        BlockCollection Blocks { get; }
    }

    // ---- lists -----------------------------------------------------------------------------------------------------

    public enum TextMarkerStyle { None, Disc, Circle, Square, Box, LowerRoman, UpperRoman, LowerLatin, UpperLatin, Decimal }

    [Markup.ContentProperty(nameof(Blocks))]
    public class ListItem : TextElement, IBlockContainer
    {
        public ListItem() { }

        public ListItem(Paragraph paragraph) => Blocks.Add(paragraph);

        public BlockCollection Blocks { get; } = new BlockCollection();
        public Thickness Margin { get; set; }
        public Thickness Padding { get; set; }
    }

    public class ListItemCollection : Collection<ListItem>
    {
        protected override void InsertItem(int index, ListItem item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    /// <summary>A numbered or bulleted list.</summary>
    [Markup.ContentProperty(nameof(ListItems))]
    public class List : Block
    {
        public List() { }

        public List(ListItem listItem) => ListItems.Add(listItem);

        public ListItemCollection ListItems { get; } = new ListItemCollection();
        public TextMarkerStyle MarkerStyle { get; set; } = TextMarkerStyle.Disc;
        public double MarkerOffset { get; set; } = double.NaN;
        public int StartIndex { get; set; } = 1;
    }

    // ---- tables ----------------------------------------------------------------------------------------------------

    public class TableColumn : DependencyObject
    {
        public GridLength Width { get; set; } = GridLength.Auto;
        public Media.Brush Background { get; set; }
    }

    public class TableColumnCollection : Collection<TableColumn> { }

    [Markup.ContentProperty(nameof(Blocks))]
    public class TableCell : TextElement, IBlockContainer
    {
        public TableCell() { }

        public TableCell(Block block) => Blocks.Add(block);

        public BlockCollection Blocks { get; } = new BlockCollection();
        public int ColumnSpan { get; set; } = 1;
        public int RowSpan { get; set; } = 1;
        public Thickness Padding { get; set; }
        public Thickness BorderThickness { get; set; }
        public Media.Brush BorderBrush { get; set; }
        public TextAlignment TextAlignment { get; set; }
    }

    public class TableCellCollection : Collection<TableCell>
    {
        protected override void InsertItem(int index, TableCell item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    [Markup.ContentProperty(nameof(Cells))]
    public class TableRow : TextElement
    {
        public TableCellCollection Cells { get; } = new TableCellCollection();
    }

    public class TableRowCollection : Collection<TableRow>
    {
        protected override void InsertItem(int index, TableRow item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    [Markup.ContentProperty(nameof(Rows))]
    public class TableRowGroup : TextElement
    {
        public TableRowCollection Rows { get; } = new TableRowCollection();
    }

    public class TableRowGroupCollection : Collection<TableRowGroup>
    {
        protected override void InsertItem(int index, TableRowGroup item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }
    }

    /// <summary>A table of cells in rows, grouped; <see cref="CellSpacing"/> is the gap between cells.</summary>
    [Markup.ContentProperty(nameof(RowGroups))]
    public class Table : Block
    {
        public TableColumnCollection Columns { get; } = new TableColumnCollection();
        public TableRowGroupCollection RowGroups { get; } = new TableRowGroupCollection();
        public double CellSpacing { get; set; } = 2;
        public Thickness BorderThickness { get; set; }
        public Media.Brush BorderBrush { get; set; }
    }

    /// <summary>An element (a control) inside a flow document.</summary>
    [Markup.ContentProperty(nameof(Child))]
    public class BlockUIContainer : Block
    {
        public BlockUIContainer() { }

        public BlockUIContainer(UIElement uiElement) => Child = uiElement;

        public UIElement Child { get; set; }
    }
}
