using System.Collections.Generic;

namespace System.Windows.Controls
{
    /// <summary>A control that shows one piece of content: text or another element.</summary>
    [Markup.ContentProperty(nameof(Content))]
    public class ContentControl : Control
    {
        public static readonly DependencyProperty ContentProperty = Dp.Register<object, ContentControl>(nameof(Content), null, OnContentChanged);
        public static readonly DependencyProperty ContentTemplateProperty = Dp.Register<DataTemplate, ContentControl>(nameof(ContentTemplate));
        public static readonly DependencyProperty ContentStringFormatProperty = Dp.Register<string, ContentControl>(nameof(ContentStringFormat));

        public object Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

        public DataTemplate ContentTemplate
        {
            get => (DataTemplate)GetValue(ContentTemplateProperty);
            set => SetValue(ContentTemplateProperty, value);
        }

        public string ContentStringFormat
        {
            get => (string)GetValue(ContentStringFormatProperty);
            set => SetValue(ContentStringFormatProperty, value);
        }

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
}
