using System.Collections.ObjectModel;
using System.Text;

namespace System.Windows
{
    public sealed class TextDecoration { }

    public class TextDecorationCollection : Collection<TextDecoration>
    {
        /// <summary>CSS <c>text-decoration</c> value ("underline", "line-through", ...).</summary>
        internal string Css { get; set; } = "";
    }

    public static class TextDecorations
    {
        public static TextDecorationCollection Underline => new TextDecorationCollection { Css = "underline" };
        public static TextDecorationCollection Strikethrough => new TextDecorationCollection { Css = "line-through" };
        public static TextDecorationCollection OverLine => new TextDecorationCollection { Css = "overline" };
        public static TextDecorationCollection Baseline => new TextDecorationCollection();

        internal static TextDecorationCollection Parse(string text)
        {
            var css = new StringBuilder();
            foreach (string part in text.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.ToLowerInvariant();
                css.Append(name == "strikethrough" ? "line-through" : name == "none" ? "" : name).Append(' ');
            }
            return new TextDecorationCollection { Css = css.ToString().Trim() };
        }
    }
}

namespace System.Windows.Documents
{
    /// <summary>A piece of formatted text inside a text block or paragraph.</summary>
    public abstract class Inline : TextElement
    {
        public static readonly DependencyProperty TextDecorationsProperty = Dp.Register<TextDecorationCollection, Inline>(nameof(TextDecorations));
        public static readonly DependencyProperty BaselineAlignmentProperty = Dp.Register<BaselineAlignment, Inline>(nameof(BaselineAlignment), BaselineAlignment.Baseline);

        /// <summary>Vertical position of the text, e.g. Superscript for exponents (rendered as CSS vertical-align).</summary>
        public BaselineAlignment BaselineAlignment { get => (BaselineAlignment)GetValue(BaselineAlignmentProperty); set => SetValue(BaselineAlignmentProperty, value); }

        public TextDecorationCollection TextDecorations { get => (TextDecorationCollection)GetValue(TextDecorationsProperty); set => SetValue(TextDecorationsProperty, value); }

        public string Name { get; set; }

        public object Tag { get; set; }

        public object ToolTip { get; set; }

        /// <summary>The plain text of this inline and its children.</summary>
        internal abstract void AppendText(StringBuilder text);
    }

    public class InlineCollection : Collection<Inline>
    {
        public void Add(string text) => Add(new Run(text));

        public void Add(UIElement element) => Add(new InlineUIContainer(element));

        public void AddRange(Collections.IEnumerable range)
        {
            foreach (object item in range)
            {
                if (item is Inline inline)
                {
                    Add(inline);
                }
                else if (item is string text)
                {
                    Add(text);
                }
            }
        }

        public Inline FirstInline => Count > 0 ? this[0] : null;

        public Inline LastInline => Count > 0 ? this[Count - 1] : null;

        protected override void InsertItem(int index, Inline item)
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

        internal string PlainText
        {
            get
            {
                var text = new StringBuilder();
                foreach (Inline inline in this)
                {
                    inline.AppendText(text);
                }
                return text.ToString();
            }
        }
    }

    [Markup.ContentProperty(nameof(Text))]
    public class Run : Inline
    {
        public static readonly DependencyProperty TextProperty = Dp.Register<string, Run>(nameof(Text), "");

        public Run() { }

        public Run(string text) => Text = text;

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

        internal override void AppendText(StringBuilder text) => text.Append(Text);
    }

    public class LineBreak : Inline
    {
        internal override void AppendText(StringBuilder text) => text.Append('\n');
    }

    /// <summary>An inline that contains other inlines (and gives them a common format).</summary>
    [Markup.ContentProperty(nameof(Inlines))]
    public class Span : Inline
    {
        public Span() { }

        public Span(Inline childInline) => Inlines.Add(childInline);

        public InlineCollection Inlines { get; } = new InlineCollection();

        internal override void AppendText(StringBuilder text) => text.Append(Inlines.PlainText);
    }

    public class Bold : Span
    {
        public Bold() => FontWeight = FontWeights.Bold;

        public Bold(Inline childInline) : this() => Inlines.Add(childInline);
    }

    public class Italic : Span
    {
        public Italic() => FontStyle = FontStyles.Italic;

        public Italic(Inline childInline) : this() => Inlines.Add(childInline);
    }

    public class Underline : Span
    {
        public Underline() => TextDecorations = System.Windows.TextDecorations.Underline;

        public Underline(Inline childInline) : this() => Inlines.Add(childInline);
    }

    public class Hyperlink : Span
    {
        public Hyperlink() { }

        public Hyperlink(Inline childInline) => Inlines.Add(childInline);

        public Uri NavigateUri { get; set; }

        public event RoutedEventHandler Click;

        internal void RaiseClick() => Click?.Invoke(this, new RoutedEventArgs { Source = this });
    }

    /// <summary>An element placed inside running text.</summary>
    [Markup.ContentProperty(nameof(Child))]
    public class InlineUIContainer : Inline
    {
        public InlineUIContainer() { }

        public InlineUIContainer(UIElement child) => Child = child;

        public UIElement Child { get; set; }

        internal override void AppendText(StringBuilder text) { }
    }
}

namespace System.Windows
{
    public enum BaselineAlignment { Top, Center, Bottom, Baseline, TextTop, TextBottom, Subscript, Superscript }
}
