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

        // ---- control templates ---------------------------------------------------------------------------------

        // the element tree built from the current Template, and the template it was built from
        private ControlTemplate _templateFor;
        private UIElement _templateRoot;

        /// <summary>
        /// The element tree of the control's <see cref="Template"/>, built once per template; null without one (the
        /// control is then drawn in its standard look). As in WPF, the tree's elements know the control as their
        /// TemplatedParent ({TemplateBinding}), a ContentPresenter in it shows the control's Content, and its
        /// bindings see the control's DataContext.
        /// </summary>
        internal UIElement TemplateRoot
        {
            get
            {
                ControlTemplate template = SupportsTemplate ? Template : null;
                if (ReferenceEquals(template, _templateFor))
                {
                    return _templateRoot;
                }
                SetParent(_templateRoot, null);
                _templateFor = template;
                _templateRoot = template?.LoadContent() as UIElement;
                if (_templateRoot != null)
                {
                    AdoptTemplateTree(_templateRoot);
                    SetParent(_templateRoot, this);
                }
                return _templateRoot;
            }
        }

        /// <summary>The triggers of the template the control is drawn with (empty without a template).</summary>
        internal IEnumerable<TriggerBase> TemplateTriggers
        {
            get
            {
                if (!SupportsTemplate || GetValueIgnoringTriggers(TemplateProperty) is not ControlTemplate template)
                {
                    yield break;
                }
                foreach (object trigger in template.Triggers)
                {
                    if (trigger is TriggerBase triggerBase) yield return triggerBase;
                }
            }
        }

        /// <summary>True when the control is drawn with a template that has triggers.</summary>
        internal bool HasTemplateTriggers =>
            SupportsTemplate && GetValueIgnoringTriggers(TemplateProperty) is ControlTemplate { Triggers.Count: > 0 };

        /// <summary>Controls the renderer draws itself (text boxes, sliders, lists) keep their standard look.</summary>
        private protected virtual bool SupportsTemplate => false;

        /// <summary>Marks the template's elements as created for this control and connects content presenters.</summary>
        private void AdoptTemplateTree(UIElement element)
        {
            if (element is FrameworkElement fe)
            {
                fe.TemplatedParent = this;
            }
            if (element is ContentPresenter presenter && this is ContentControl
                && !presenter.HasLocalValue(ContentControl.ContentProperty))
            {
                // the presenter shows what the control shows (its children are the control's content, not template parts)
                presenter.SetValue(ContentControl.ContentProperty, new TemplateBindingExpression(nameof(ContentControl.Content)));
                if (!presenter.HasLocalValue(ContentControl.ContentTemplateProperty))
                {
                    presenter.SetValue(ContentControl.ContentTemplateProperty, new TemplateBindingExpression(nameof(ContentControl.ContentTemplate)));
                }
                return;
            }
            foreach (UIElement child in new List<UIElement>(element.ChildElements))
            {
                AdoptTemplateTree(child);
            }
        }

        /// <summary>A named element in the template tree (GetTemplateChild).</summary>
        internal DependencyObject FindTemplatePart(string name)
        {
            return TemplateRoot == null ? null : Find(TemplateRoot);

            DependencyObject Find(UIElement element)
            {
                if (element is FrameworkElement fe && fe.Name == name && ReferenceEquals(fe.TemplatedParent, this))
                {
                    return fe;
                }
                foreach (UIElement child in element.ChildElements)
                {
                    if (child is FrameworkElement { TemplatedParent: var owner } && !ReferenceEquals(owner, this))
                    {
                        continue;   // content shown by a presenter is not part of the template
                    }
                    DependencyObject found = Find(child);
                    if (found != null)
                    {
                        return found;
                    }
                }
                return null;
            }
        }

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

        private protected override bool SupportsTemplate => true;

        internal override IEnumerable<UIElement> ChildElements
        {
            get
            {
                if (TemplateRoot is { } templateRoot)
                {
                    yield return templateRoot;
                    yield break;
                }
                if (Content is UIElement element)
                {
                    yield return element;
                }
                else if (TemplatedContent is { } templated)
                {
                    yield return templated;
                }
            }
        }

        // the element built for the current (non-element) content, and the content it was built for
        private object _templatedFor;
        private UIElement _templated;

        /// <summary>
        /// The element that shows content which is not an element itself (e.g. a view model): built from
        /// <see cref="ContentTemplate"/> or from a DataTemplate in the resources whose DataType matches the content
        /// (an "implicit" data template), with the content as DataContext. Null when there is no such template;
        /// the content is then shown as text.
        /// </summary>
        internal UIElement TemplatedContent
        {
            get
            {
                object content = Content;
                if (content == null || content is UIElement)
                {
                    return null;
                }
                if (ReferenceEquals(content, _templatedFor))
                {
                    return _templated;
                }

                SetParent(_templated, null);
                _templatedFor = content;
                _templated = (ContentTemplate ?? FindDataTemplate(content.GetType()))?.LoadContent() as UIElement;
                // the template's root gets the content as data context, unless the template sets one itself
                // (DataContext="{Binding}" then finds the content as its source: Binding.InheritedDataContext)
                if (_templated is FrameworkElement element && !element.HasLocalValue(FrameworkElement.DataContextProperty))
                {
                    element.DataContext = content;
                }
                SetParent(_templated, this);
                return _templated;
            }
        }

        /// <summary>The implicit data template for a type or one of its base types (resources are keyed by the type).</summary>
        private DataTemplate FindDataTemplate(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (TryFindResource(current) is DataTemplate template)
                {
                    return template;
                }
            }
            return null;
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

    /// <summary>The place in a control template where the control's content is shown (see Control.TemplateRoot).</summary>
    public class ContentPresenter : ContentControl
    {
        private protected override bool SupportsTemplate => false;

        public string ContentSource { get; set; }
    }

    /// <summary>A scrollable area around its content.</summary>
    public class ScrollViewer : ContentControl
    {
        // the browser draws scrolling itself (native scroll bars); a template with its own scroll bars is not used
        private protected override bool SupportsTemplate => false;

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
        public static readonly DependencyProperty HeaderTemplateProperty = Dp.Register<DataTemplate, HeaderedContentControl>(nameof(HeaderTemplate));

        public object Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

        public DataTemplate HeaderTemplate { get => (DataTemplate)GetValue(HeaderTemplateProperty); set => SetValue(HeaderTemplateProperty, value); }

        // the element built from HeaderTemplate, and the header it was built for
        private object _templatedHeaderFor;
        private UIElement _templatedHeader;

        /// <summary>The header drawn through <see cref="HeaderTemplate"/> (with the header as DataContext), or null.</summary>
        internal UIElement TemplatedHeader
        {
            get
            {
                if (HeaderTemplate == null || Header is UIElement)
                {
                    return null;
                }
                if (_templatedHeader == null || !ReferenceEquals(_templatedHeaderFor, Header))
                {
                    SetParent(_templatedHeader, null);
                    _templatedHeaderFor = Header;
                    _templatedHeader = HeaderTemplate.LoadContent() as UIElement;
                    if (_templatedHeader is FrameworkElement element)
                    {
                        element.DataContext = Header;
                    }
                    SetParent(_templatedHeader, this);
                }
                return _templatedHeader;
            }
        }
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

        protected virtual void OnClosing(ComponentModel.CancelEventArgs e) => Closing?.Invoke(this, e);

        protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);

        public void Close()
        {
            OnClosing(new ComponentModel.CancelEventArgs());
            OnClosed(EventArgs.Empty);
        }

        public static Window GetWindow(DependencyObject element) => null;
    }
}
