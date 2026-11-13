using System.Collections.Generic;
using System.Text;

namespace System.Windows.Documents
{
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
}
