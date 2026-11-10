using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media;

namespace System.Windows.Controls
{
    /// <summary>Shows text: either plain (<see cref="Text"/>) or formatted (<see cref="Inlines"/>).</summary>
    [Markup.ContentProperty(nameof(Inlines))]
    public class TextBlock : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty = Dp.Register<string, TextBlock>(nameof(Text), "", OnTextChanged);
        public static readonly DependencyProperty TextWrappingProperty = Dp.Register<TextWrapping, TextBlock>(nameof(TextWrapping), TextWrapping.NoWrap);
        public static readonly DependencyProperty TextAlignmentProperty = Dp.Register<TextAlignment, TextBlock>(nameof(TextAlignment));
        public static readonly DependencyProperty TextTrimmingProperty = Dp.Register<TextTrimming, TextBlock>(nameof(TextTrimming));
        public static readonly DependencyProperty TextDecorationsProperty = Dp.Register<TextDecorationCollection, TextBlock>(nameof(TextDecorations));
        public static readonly DependencyProperty LineHeightProperty = Dp.Register<double, TextBlock>(nameof(LineHeight), double.NaN);
        public static readonly DependencyProperty BackgroundProperty = Panel.BackgroundProperty;
        public static readonly DependencyProperty PaddingProperty = Border.PaddingProperty;
        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty;
        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty;
        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty;
        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty;
        public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty;
        public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty;

        private bool _syncingText;

        public TextBlock() => Inlines = new InlineCollection();

        public TextBlock(Inline inline) : this() => Inlines.Add(inline);

        public InlineCollection Inlines { get; }

        /// <summary>The plain text. Setting it replaces all inlines; reading it joins them.</summary>
        public string Text
        {
            get => GetStoredValue(TextProperty) is IDeferredValue ? (string)GetValue(TextProperty) : Inlines.PlainText;
            set => SetValue(TextProperty, value);
        }

        public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
        public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
        public TextTrimming TextTrimming { get => (TextTrimming)GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }
        public TextDecorationCollection TextDecorations { get => (TextDecorationCollection)GetValue(TextDecorationsProperty); set => SetValue(TextDecorationsProperty, value); }
        public double LineHeight { get => (double)GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }

        /// <summary>True when the text comes from a binding (then <see cref="Text"/> is drawn, not the inlines).</summary>
        internal bool HasBoundText => GetStoredValue(TextProperty) is IDeferredValue;

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var block = (TextBlock)d;
            if (block._syncingText)
            {
                return;
            }
            block._syncingText = true;
            block.Inlines.Clear();
            if (!string.IsNullOrEmpty((string)e.NewValue))
            {
                block.Inlines.Add(new Run((string)e.NewValue));
            }
            block._syncingText = false;
        }

        public static void SetFontSize(DependencyObject element, double value) => element.SetValue(FontSizeProperty, value);
        public static void SetFontWeight(DependencyObject element, FontWeight value) => element.SetValue(FontWeightProperty, value);
        public static void SetFontFamily(DependencyObject element, FontFamily value) => element.SetValue(FontFamilyProperty, value);
        public static void SetForeground(DependencyObject element, Brush value) => element.SetValue(ForegroundProperty, value);
        public static void SetTextAlignment(DependencyObject element, TextAlignment value) => element.SetValue(TextAlignmentProperty, value);
        public static void SetTextWrapping(DependencyObject element, TextWrapping value) => element.SetValue(TextWrappingProperty, value);
    }

    public class TextChangedEventArgs : RoutedEventArgs { }

    public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);
}

namespace System.Windows.Controls.Primitives
{
    public abstract class TextBoxBase : Control
    {
        public static readonly DependencyProperty IsReadOnlyProperty = Dp.Register<bool, TextBoxBase>(nameof(IsReadOnly));
        public static readonly DependencyProperty AcceptsReturnProperty = Dp.Register<bool, TextBoxBase>(nameof(AcceptsReturn));

        public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
        public bool AcceptsReturn { get => (bool)GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
        public bool AcceptsTab { get; set; }
        public bool IsUndoEnabled { get; set; }
        public bool IsReadOnlyCaretVisible { get; set; }
        public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
        public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
        public Media.Brush SelectionBrush { get; set; }
        public Media.Brush CaretBrush { get; set; }

        public event TextChangedEventHandler TextChanged;
        public event RoutedEventHandler SelectionChanged;

        protected void RaiseTextChanged() => TextChanged?.Invoke(this, new TextChangedEventArgs { Source = this });

        public void ScrollToEnd() { }
        public void ScrollToHome() { }
        public void SelectAll() { }
        public void Copy() { }
        public void Cut() { }
        public void Paste() { }
        public bool Undo() => false;
        public void AppendText(string text) => OnAppendText(text);

        protected virtual void OnAppendText(string text) { }
    }
}

namespace System.Windows.Controls
{
    using System.Windows.Controls.Primitives;

    public enum CharacterCasing { Normal, Lower, Upper }

    /// <summary>An editable text field; the browser reports what the user types.</summary>
    [Markup.ContentProperty(nameof(Text))]
    public class TextBox : TextBoxBase
    {
        public static readonly DependencyProperty TextProperty = Dp.Register<string, TextBox>(nameof(Text), "", (d, e) => ((TextBox)d).RaiseTextChanged());
        public static readonly DependencyProperty TextWrappingProperty = Dp.Register<TextWrapping, TextBox>(nameof(TextWrapping), TextWrapping.NoWrap);
        public static readonly DependencyProperty TextAlignmentProperty = Dp.Register<TextAlignment, TextBox>(nameof(TextAlignment));
        public static readonly DependencyProperty MaxLengthProperty = Dp.Register<int, TextBox>(nameof(MaxLength));

        public string Text { get => (string)GetValue(TextProperty) ?? ""; set => SetValue(TextProperty, value); }
        public TextWrapping TextWrapping { get => (TextWrapping)GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
        public TextAlignment TextAlignment { get => (TextAlignment)GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
        public int MaxLength { get => (int)GetValue(MaxLengthProperty); set => SetValue(MaxLengthProperty, value); }
        public CharacterCasing CharacterCasing { get; set; }
        public int MaxLines { get; set; } = int.MaxValue;
        public int MinLines { get; set; } = 1;
        public int CaretIndex { get; set; }
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }
        public string SelectedText { get; set; } = "";
        public int LineCount => Text.Split('\n').Length;

        public void Clear() => Text = "";

        public void Select(int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
        }

        protected override void OnAppendText(string text) => Text += text;

        /// <summary>Text typed in the browser: goes to the binding source if the text is bound, otherwise to the property.</summary>
        internal void SetTextFromUser(string text)
        {
            if (GetStoredValue(TextProperty) is Data.BindingExpression binding)
            {
                binding.WriteBack(this, text);
                RenderVersion.Bump();
                RaiseTextChanged();
            }
            else
            {
                Text = text;
            }
        }
    }

    public class PasswordBox : Control
    {
        public string Password { get; set; } = "";
        public char PasswordChar { get; set; } = '●';
        public int MaxLength { get; set; }

        public event RoutedEventHandler PasswordChanged;

        internal void SetPasswordFromUser(string password)
        {
            Password = password;
            PasswordChanged?.Invoke(this, new RoutedEventArgs { Source = this });
        }
    }
}
