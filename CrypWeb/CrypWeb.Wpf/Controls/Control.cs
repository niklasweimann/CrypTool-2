using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Media;

namespace System.Windows.Controls
{
    /// <summary>Base of controls: font, colours, border and padding.</summary>
    public class Control : FrameworkElement
    {
        public static readonly DependencyProperty BackgroundProperty = Panel.BackgroundProperty;
        public static readonly DependencyProperty BorderBrushProperty = Border.BorderBrushProperty;
        public static readonly DependencyProperty BorderThicknessProperty = Border.BorderThicknessProperty;
        public static readonly DependencyProperty PaddingProperty = Border.PaddingProperty;
        public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty;
        public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty;
        public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty;
        public static readonly DependencyProperty FontWeightProperty = TextElement.FontWeightProperty;
        public static readonly DependencyProperty FontStyleProperty = TextElement.FontStyleProperty;
        public static readonly DependencyProperty FontStretchProperty = TextElement.FontStretchProperty;
        public static readonly DependencyProperty HorizontalContentAlignmentProperty = Dp.Register<HorizontalAlignment, Control>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);
        public static readonly DependencyProperty VerticalContentAlignmentProperty = Dp.Register<VerticalAlignment, Control>(nameof(VerticalContentAlignment), VerticalAlignment.Top);
        public static readonly DependencyProperty TemplateProperty = Dp.Register<ControlTemplate, Control>(nameof(Template));
        public static readonly DependencyProperty IsTabStopProperty = Dp.Register<bool, Control>(nameof(IsTabStop), true);
        public static readonly DependencyProperty TabIndexProperty = Dp.Register<int, Control>(nameof(TabIndex));

        public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
        public Brush BorderBrush { get => (Brush)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
        public Thickness BorderThickness { get => (Thickness)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
        public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
        public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
        public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontWeight FontWeight { get => (FontWeight)GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
        public FontStyle FontStyle { get => (FontStyle)GetValue(FontStyleProperty); set => SetValue(FontStyleProperty, value); }
        public FontStretch FontStretch { get => (FontStretch)GetValue(FontStretchProperty); set => SetValue(FontStretchProperty, value); }
        public HorizontalAlignment HorizontalContentAlignment { get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty); set => SetValue(HorizontalContentAlignmentProperty, value); }
        public VerticalAlignment VerticalContentAlignment { get => (VerticalAlignment)GetValue(VerticalContentAlignmentProperty); set => SetValue(VerticalContentAlignmentProperty, value); }
        public ControlTemplate Template { get => (ControlTemplate)GetValue(TemplateProperty); set => SetValue(TemplateProperty, value); }
        public bool IsTabStop { get => (bool)GetValue(IsTabStopProperty); set => SetValue(IsTabStopProperty, value); }
        public int TabIndex { get => (int)GetValue(TabIndexProperty); set => SetValue(TabIndexProperty, value); }

        public event Input.MouseButtonEventHandler MouseDoubleClick;
        public event Input.MouseButtonEventHandler PreviewMouseDoubleClick;

        internal override void OnClicked(Input.MouseButtonEventArgs e)
        {
            base.OnClicked(e);
            if (e.ClickCount == 2)
            {
                PreviewMouseDoubleClick?.Invoke(this, e);
                MouseDoubleClick?.Invoke(this, e);
            }
        }
    }

    /// <summary>A control that shows one piece of content: text or another element.</summary>
    [Markup.ContentProperty(nameof(Content))]
    public class ContentControl : Control
    {
        public static readonly DependencyProperty ContentProperty = Dp.Register<object, ContentControl>(nameof(Content), null, OnContentChanged);
        public static readonly DependencyProperty ContentTemplateProperty = Dp.Register<DataTemplate, ContentControl>(nameof(ContentTemplate));
        public static readonly DependencyProperty ContentStringFormatProperty = Dp.Register<string, ContentControl>(nameof(ContentStringFormat));

        public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
        public DataTemplate ContentTemplate { get => (DataTemplate)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
        public string ContentStringFormat { get => (string)GetValue(ContentStringFormatProperty); set => SetValue(ContentStringFormatProperty, value); }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetParent(e.OldValue, null);
            SetParent(e.NewValue, (UIElement)d);
        }

        internal override IEnumerable<UIElement> ChildElements
        {
            get
            {
                if (Content is UIElement element)
                {
                    yield return element;
                }
            }
        }
    }

    /// <summary>Base class of the plugin presentations.</summary>
    public class UserControl : ContentControl { }

    public class Page : UserControl
    {
        public string Title { get; set; }
    }

    public class Label : ContentControl
    {
        public static readonly DependencyProperty TargetProperty = Dp.Register<UIElement, Label>(nameof(Target));

        public UIElement Target { get => (UIElement)GetValue(TargetProperty); set => SetValue(TargetProperty, value); }
    }

    public class ToolTip : ContentControl { }

    public class ContentPresenter : ContentControl { }

    /// <summary>A scrollable area around its content.</summary>
    public class ScrollViewer : ContentControl
    {
        public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty = Dp.Attached<ScrollBarVisibility, ScrollViewer>("HorizontalScrollBarVisibility", ScrollBarVisibility.Disabled);
        public static readonly DependencyProperty VerticalScrollBarVisibilityProperty = Dp.Attached<ScrollBarVisibility, ScrollViewer>("VerticalScrollBarVisibility", ScrollBarVisibility.Visible);
        public static readonly DependencyProperty CanContentScrollProperty = Dp.Attached<bool, ScrollViewer>("CanContentScroll");

        public ScrollBarVisibility HorizontalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty); set => SetValue(HorizontalScrollBarVisibilityProperty, value); }
        public ScrollBarVisibility VerticalScrollBarVisibility { get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty); set => SetValue(VerticalScrollBarVisibilityProperty, value); }
        public bool CanContentScroll { get => (bool)GetValue(CanContentScrollProperty); set => SetValue(CanContentScrollProperty, value); }

        public double VerticalOffset => 0;
        public double HorizontalOffset => 0;
        public double ScrollableHeight => 0;
        public double ScrollableWidth => 0;
        public double ViewportHeight => ActualHeight;
        public double ViewportWidth => ActualWidth;
        public double ExtentHeight => ActualHeight;
        public double ExtentWidth => ActualWidth;

        public event ScrollChangedEventHandler ScrollChanged;

        public void ScrollToEnd() { }
        public void ScrollToBottom() { }
        public void ScrollToTop() { }
        public void ScrollToHome() { }
        public void ScrollToVerticalOffset(double offset) { }
        public void ScrollToHorizontalOffset(double offset) { }
        public void LineDown() { }
        public void LineUp() { }

        public static void SetHorizontalScrollBarVisibility(DependencyObject element, ScrollBarVisibility value) => element.SetValue(HorizontalScrollBarVisibilityProperty, value);
        public static void SetVerticalScrollBarVisibility(DependencyObject element, ScrollBarVisibility value) => element.SetValue(VerticalScrollBarVisibilityProperty, value);
        public static void SetCanContentScroll(DependencyObject element, bool value) => element.SetValue(CanContentScrollProperty, value);
    }

    public enum ScrollBarVisibility { Disabled, Auto, Hidden, Visible }

    /// <summary>A content control with a caption (group box, expander, tab page).</summary>
    public class HeaderedContentControl : ContentControl
    {
        public static readonly DependencyProperty HeaderProperty = Dp.Register<object, HeaderedContentControl>(nameof(Header));

        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }
    }

    public class GroupBox : HeaderedContentControl { }

    public class Expander : HeaderedContentControl
    {
        public static readonly DependencyProperty IsExpandedProperty = Dp.Register<bool, Expander>(nameof(IsExpanded));

        public bool IsExpanded { get => (bool)GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

        public event RoutedEventHandler Expanded;
        public event RoutedEventHandler Collapsed;

        internal void Toggle()
        {
            IsExpanded = !IsExpanded;
            (IsExpanded ? Expanded : Collapsed)?.Invoke(this, new RoutedEventArgs { Source = this });
        }
    }

    public class Separator : Control { }
}

namespace System.Windows
{
    /// <summary>Presentations are never shown as windows in the browser; a window is drawn like a user control.</summary>
    public class Window : Controls.ContentControl
    {
        public string Title { get; set; }
        public Window Owner { get; set; }
        public bool? DialogResult { get; set; }
        public bool Topmost { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }

        public event EventHandler Closed;
        public event ComponentModel.CancelEventHandler Closing;

        public void Show() { }
        public bool? ShowDialog() => false;
        public void Hide() { }
        public bool Activate() => true;

        public void Close()
        {
            Closing?.Invoke(this, new ComponentModel.CancelEventArgs());
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public static Window GetWindow(DependencyObject element) => null;
    }
}
