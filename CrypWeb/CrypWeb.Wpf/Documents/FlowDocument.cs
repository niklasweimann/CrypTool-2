using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media;

namespace System.Windows.Documents
{
    /// <summary>A block of a flow document.</summary>
    public abstract class Block : TextElement
    {
        public Thickness Margin { get; set; }
        public Thickness Padding { get; set; }
        public TextAlignment TextAlignment { get; set; }
        public double LineHeight { get; set; } = double.NaN;
    }

    public class BlockCollection : Collection<Block>
    {
        public Block FirstBlock => Count > 0 ? this[0] : null;

        public Block LastBlock => Count > 0 ? this[Count - 1] : null;

        public void AddRange(IEnumerable<Block> blocks)
        {
            foreach (Block block in blocks)
            {
                Add(block);
            }
        }

        protected override void InsertItem(int index, Block item)
        {
            base.InsertItem(index, item);
            RenderVersion.Bump();
        }

        protected override void RemoveItem(int index)
        {
            base.RemoveItem(index);
            RenderVersion.Bump();
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            RenderVersion.Bump();
        }
    }

    [Markup.ContentProperty(nameof(Inlines))]
    public class Paragraph : Block
    {
        public Paragraph() { }

        public Paragraph(Inline inline) => Inlines.Add(inline);

        public InlineCollection Inlines { get; } = new InlineCollection();
    }

    /// <summary>A group of blocks.</summary>
    [Markup.ContentProperty(nameof(Blocks))]
    public class Section : Block
    {
        public BlockCollection Blocks { get; } = new BlockCollection();
    }

    /// <summary>Formatted text made of paragraphs; shown by a rich text box or a document viewer.</summary>
    [Markup.ContentProperty(nameof(Blocks))]
    public class FlowDocument : TextElement
    {
        public FlowDocument() { }

        public FlowDocument(Block block) => Blocks.Add(block);

        public bool AllowDrop { get; set; }

        public LineStackingStrategy LineStackingStrategy { get; set; }

        public BlockCollection Blocks { get; } = new BlockCollection();

        public Thickness PagePadding { get; set; }
        public double PageWidth { get; set; } = double.NaN;
        public double PageHeight { get; set; } = double.NaN;
        public double ColumnWidth { get; set; } = double.NaN;
        public double LineHeight { get; set; } = double.NaN;
        public TextAlignment TextAlignment { get; set; }
        public bool IsOptimalParagraphEnabled { get; set; }
        public bool IsHyphenationEnabled { get; set; }

        public TextPointer ContentStart => new TextPointer(this, atEnd: false);

        public TextPointer ContentEnd => new TextPointer(this, atEnd: true);

        /// <summary>All paragraphs, including those nested in sections.</summary>
        internal IEnumerable<Paragraph> Paragraphs => Flatten(Blocks);

        private static IEnumerable<Paragraph> Flatten(BlockCollection blocks)
        {
            foreach (Block block in blocks)
            {
                if (block is Paragraph paragraph)
                {
                    yield return paragraph;
                }
                else if (block is Section section)
                {
                    foreach (Paragraph nested in Flatten(section.Blocks))
                    {
                        yield return nested;
                    }
                }
            }
        }

        internal string PlainText
        {
            get
            {
                var text = new StringBuilder();
                foreach (Paragraph paragraph in Paragraphs)
                {
                    if (text.Length > 0)
                    {
                        text.Append("\r\n");
                    }
                    text.Append(paragraph.Inlines.PlainText);
                }
                return text.ToString();
            }
        }

        /// <summary>Adds text at the end; line breaks start new paragraphs. Returns the runs that were created.</summary>
        internal List<Run> Append(string text)
        {
            var created = new List<Run>();
            string[] lines = (text ?? "").Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0 || Blocks.Count == 0 || Blocks.LastBlock is not Paragraph)
                {
                    Blocks.Add(new Paragraph());
                }
                if (lines[i].Length > 0)
                {
                    var run = new Run(lines[i]);
                    ((Paragraph)Blocks.LastBlock).Inlines.Add(run);
                    created.Add(run);
                }
            }
            return created;
        }
    }

    /// <summary>
    /// A position in a flow document: the start or the end of the document, or a character position inside a
    /// <see cref="Run"/> (Run.ContentStart plus an offset), which is how plugins colour parts of a text.
    /// </summary>
    public class TextPointer
    {
        internal TextPointer(FlowDocument document, bool atEnd)
        {
            Document = document;
            AtEnd = atEnd;
        }

        internal TextPointer(Run run, int offset)
        {
            Run = run;
            Offset = Math.Clamp(offset, 0, (run.Text ?? "").Length);
            AtEnd = Offset == (run.Text ?? "").Length;
        }

        internal FlowDocument Document { get; }

        internal bool AtEnd { get; }

        /// <summary>The run this position is in (null for document positions), and the character offset in it.</summary>
        internal Run Run { get; }

        internal int Offset { get; }

        public Paragraph Paragraph => Document.Blocks.LastBlock as Paragraph;

        public TextPointer DocumentStart => Document.ContentStart;

        public TextPointer DocumentEnd => Document.ContentEnd;

        public TextPointer GetPositionAtOffset(int offset) => Run != null ? new TextPointer(Run, Offset + offset) : this;

        public TextPointer GetPositionAtOffset(int offset, LogicalDirection direction) => GetPositionAtOffset(offset);

        public TextPointer GetNextInsertionPosition(LogicalDirection direction) => this;

        public TextPointer GetInsertionPosition(LogicalDirection direction) => this;

        public int GetOffsetToPosition(TextPointer position) =>
            Run != null && ReferenceEquals(position.Run, Run) ? position.Offset - Offset : 0;

        public int CompareTo(TextPointer position) => AtEnd == position.AtEnd ? 0 : AtEnd ? 1 : -1;

        /// <summary>The element the position is in: its run, or the document.</summary>
        public DependencyObject Parent => (DependencyObject)Run ?? Document;

        /// <summary>Characters from the position to the start (Backward) or end (Forward) of its run.</summary>
        public int GetTextRunLength(LogicalDirection direction)
        {
            if (Run == null) return 0;
            return direction == LogicalDirection.Backward ? Offset : (Run.Text ?? "").Length - Offset;
        }

        /// <summary>
        /// The box of the character at the position. Positions are not measured in the browser (the text is laid
        /// out there), so this is an empty box at the origin.
        /// </summary>
        public Rect GetCharacterRect(LogicalDirection direction) => new Rect(0, 0, 0, 0);
    }

    public enum LogicalDirection { Backward, Forward }

    /// <summary>
    /// A stretch of a document. Supported are the two patterns plugin code uses: the whole document
    /// (start..end: read or replace all text) and the end of the document (end..end: append formatted text).
    /// </summary>
    public class TextRange
    {
        private readonly FlowDocument _document;
        private readonly bool _wholeDocument;
        private List<Run> _runs = new List<Run>();

        // a range inside one run (from Run.ContentStart offsets): formatting is stored on the run for its characters
        private readonly Run _run;

        public TextRange(TextPointer position1, TextPointer position2)
        {
            if (position1.Run != null && ReferenceEquals(position1.Run, position2.Run))
            {
                _run = position1.Run;
                Start = position1;
                End = position2;
                return;
            }
            _document = position1.Document;
            _wholeDocument = !position1.AtEnd && position2.AtEnd;
            Start = position1;
            End = position2;
        }

        public TextPointer Start { get; }

        public TextPointer End { get; }

        public bool IsEmpty => Text.Length == 0;

        public string Text
        {
            get
            {
                if (_run != null)
                {
                    int from = Math.Min(Start.Offset, End.Offset);
                    return (_run.Text ?? "").Substring(from, Math.Abs(End.Offset - Start.Offset));
                }
                if (_wholeDocument)
                {
                    return _document.PlainText;
                }
                var text = new StringBuilder();
                foreach (Run run in _runs)
                {
                    text.Append(run.Text);
                }
                return text.ToString();
            }
            set
            {
                if (_wholeDocument)
                {
                    _document.Blocks.Clear();
                }
                _runs = _document.Append(value);
            }
        }

        /// <summary>Formats the text of this range (font and colour properties of <see cref="TextElement"/>).</summary>
        public void ApplyPropertyValue(DependencyProperty formattingProperty, object value)
        {
            if (_run != null)
            {
                _run.Format(Math.Min(Start.Offset, End.Offset), Math.Max(Start.Offset, End.Offset), formattingProperty, value);
                return;
            }
            IEnumerable<Inline> targets = _runs;
            if (_wholeDocument)
            {
                var all = new List<Inline>();
                foreach (Paragraph paragraph in _document.Paragraphs)
                {
                    all.AddRange(paragraph.Inlines);
                }
                targets = all;
            }

            foreach (Inline inline in targets)
            {
                if (formattingProperty == Inline.TextDecorationsProperty)
                {
                    inline.TextDecorations = value as TextDecorationCollection;
                }
                else
                {
                    inline.SetValue(formattingProperty, value);
                }
            }
        }

        public object GetPropertyValue(DependencyProperty formattingProperty)
        {
            return _runs.Count > 0 ? _runs[0].GetValue(formattingProperty) : DependencyProperty.UnsetValue;
        }

        public void Select(TextPointer position1, TextPointer position2) { }

        public void ClearAllProperties() { }

        public void Load(IO.Stream stream, string dataFormat)
        {
            using var reader = new IO.StreamReader(stream);
            Text = reader.ReadToEnd();
        }

        public void Save(IO.Stream stream, string dataFormat)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Text);
            stream.Write(bytes, 0, bytes.Length);
        }
    }

    public sealed class TextSelection : TextRange
    {
        internal TextSelection(FlowDocument document) : base(document.ContentEnd, document.ContentEnd) { }
    }
}

namespace System.Windows.Controls
{
    using System.Windows.Documents;

    /// <summary>Shows a flow document. Editing in the browser is not supported: the content is read-only.</summary>
    [Markup.ContentProperty(nameof(Document))]
    public class RichTextBox : Primitives.TextBoxBase
    {
        private FlowDocument _document = new FlowDocument();

        public RichTextBox() { }

        public RichTextBox(FlowDocument document) => Document = document;

        public FlowDocument Document
        {
            get => _document;
            set
            {
                _document = value ?? new FlowDocument();
                RenderVersion.Bump();
            }
        }

        public TextSelection Selection => new TextSelection(_document);

        public TextPointer CaretPosition { get; set; }

        protected override void OnAppendText(string text) => _document.Append(text);
    }

    /// <summary>Read-only viewers for flow documents.</summary>
    [Markup.ContentProperty(nameof(Document))]
    public class FlowDocumentScrollViewer : Control
    {
        private FlowDocument _document;

        public FlowDocument Document
        {
            get => _document;
            set
            {
                _document = value;
                RenderVersion.Bump();
            }
        }

        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
        public bool IsToolBarVisible { get; set; }
        public double Zoom { get; set; } = 100;
    }

    public enum FlowDocumentReaderViewingMode { Page, TwoPage, Scroll }

    public class FlowDocumentReader : FlowDocumentScrollViewer
    {
        public FlowDocumentReaderViewingMode ViewingMode { get; set; }
        public bool IsFindEnabled { get; set; }
        public bool IsPrintEnabled { get; set; }
        public bool IsPageViewEnabled { get; set; }
        public bool IsTwoPageViewEnabled { get; set; }
        public bool IsScrollViewEnabled { get; set; }
    }
}
