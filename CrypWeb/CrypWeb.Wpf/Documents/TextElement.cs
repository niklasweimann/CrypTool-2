using System.Windows.Media;

namespace System.Windows.Documents
{
    /// <summary>
    /// Owner of the font properties. Controls, text blocks and inlines all use these same properties, so a value
    /// set on any element (also as attached property, e.g. <c>TextElement.FontSize</c> on a panel) is drawn alike.
    /// In the browser they are inherited by the children through CSS.
    /// </summary>
    public class TextElement : DependencyObject
    {
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached("FontSize", typeof(double), typeof(TextElement), new PropertyMetadata(12.0));
        public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.RegisterAttached("FontFamily", typeof(FontFamily), typeof(TextElement), new PropertyMetadata(new FontFamily("Segoe UI")));
        public static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterAttached("FontWeight", typeof(FontWeight), typeof(TextElement), new PropertyMetadata(FontWeights.Normal));
        public static readonly DependencyProperty FontStyleProperty = DependencyProperty.RegisterAttached("FontStyle", typeof(FontStyle), typeof(TextElement), new PropertyMetadata(FontStyles.Normal));
        public static readonly DependencyProperty FontStretchProperty = DependencyProperty.RegisterAttached("FontStretch", typeof(FontStretch), typeof(TextElement));
        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached("Foreground", typeof(Brush), typeof(TextElement), new PropertyMetadata(new SolidColorBrush(Colors.Black)));
        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.RegisterAttached("Background", typeof(Brush), typeof(TextElement));

        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

        public static void SetFontSize(DependencyObject element, double value) => element.SetValue(FontSizeProperty, value);
        public static double GetFontSize(DependencyObject element) => (double)element.GetValue(FontSizeProperty);
        public static void SetFontFamily(DependencyObject element, FontFamily value) => element.SetValue(FontFamilyProperty, value);
        public static FontFamily GetFontFamily(DependencyObject element) => (FontFamily)element.GetValue(FontFamilyProperty);
        public static void SetFontWeight(DependencyObject element, FontWeight value) => element.SetValue(FontWeightProperty, value);
        public static FontWeight GetFontWeight(DependencyObject element) => (FontWeight)element.GetValue(FontWeightProperty);
        public static void SetFontStyle(DependencyObject element, FontStyle value) => element.SetValue(FontStyleProperty, value);
        public static FontStyle GetFontStyle(DependencyObject element) => (FontStyle)element.GetValue(FontStyleProperty);
        public static void SetForeground(DependencyObject element, Brush value) => element.SetValue(ForegroundProperty, value);
        public static Brush GetForeground(DependencyObject element) => (Brush)element.GetValue(ForegroundProperty);
    }
}
