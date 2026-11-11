using System.Collections.Generic;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace System.Windows
{
    /// <summary>An element with size, margin, alignment, a name, resources and a data context.</summary>
    public class FrameworkElement : UIElement
    {
        public static readonly DependencyProperty WidthProperty = Dp.Register<double, FrameworkElement>(nameof(Width), double.NaN);
        public static readonly DependencyProperty HeightProperty = Dp.Register<double, FrameworkElement>(nameof(Height), double.NaN);
        public static readonly DependencyProperty MinWidthProperty = Dp.Register<double, FrameworkElement>(nameof(MinWidth), 0.0);
        public static readonly DependencyProperty MinHeightProperty = Dp.Register<double, FrameworkElement>(nameof(MinHeight), 0.0);
        public static readonly DependencyProperty MaxWidthProperty = Dp.Register<double, FrameworkElement>(nameof(MaxWidth), double.PositiveInfinity);
        public static readonly DependencyProperty MaxHeightProperty = Dp.Register<double, FrameworkElement>(nameof(MaxHeight), double.PositiveInfinity);
        public static readonly DependencyProperty MarginProperty = Dp.Register<Thickness, FrameworkElement>(nameof(Margin));
        public static readonly DependencyProperty HorizontalAlignmentProperty = Dp.Register<HorizontalAlignment, FrameworkElement>(nameof(HorizontalAlignment), HorizontalAlignment.Stretch);
        public static readonly DependencyProperty VerticalAlignmentProperty = Dp.Register<VerticalAlignment, FrameworkElement>(nameof(VerticalAlignment), VerticalAlignment.Stretch);
        public static readonly DependencyProperty NameProperty = Dp.Register<string, FrameworkElement>(nameof(Name), "");
        public static readonly DependencyProperty TagProperty = Dp.Register<object, FrameworkElement>(nameof(Tag));
        public static readonly DependencyProperty DataContextProperty = Dp.Register<object, FrameworkElement>(nameof(DataContext), null, OnDataContextChanged);
        public static readonly DependencyProperty StyleProperty = Dp.Register<Style, FrameworkElement>(nameof(Style), null, OnStyleChanged);
        public static readonly DependencyProperty CursorProperty = Dp.Register<Cursor, FrameworkElement>(nameof(Cursor));
        public static readonly DependencyProperty ToolTipProperty = Dp.Register<object, FrameworkElement>(nameof(ToolTip));
        public static readonly DependencyProperty LayoutTransformProperty = Dp.Register<Transform, FrameworkElement>(nameof(LayoutTransform));
        public static readonly DependencyProperty FlowDirectionProperty = Dp.Register<FlowDirection, FrameworkElement>(nameof(FlowDirection));
        public static readonly DependencyProperty UseLayoutRoundingProperty = Dp.Register<bool, FrameworkElement>(nameof(UseLayoutRounding));
        public static readonly DependencyProperty FocusVisualStyleProperty = Dp.Register<Style, FrameworkElement>(nameof(FocusVisualStyle));
        public static readonly DependencyProperty ContextMenuProperty = Dp.Register<object, FrameworkElement>(nameof(ContextMenu));

        private ResourceDictionary _resources;
        private bool _loadedRaised;

        public double Width { get => (double)GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
        public double Height { get => (double)GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
        public double MinWidth { get => (double)GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }
        public double MinHeight { get => (double)GetValue(MinHeightProperty); set => SetValue(MinHeightProperty, value); }
        public double MaxWidth { get => (double)GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }
        public double MaxHeight { get => (double)GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
        public Thickness Margin { get => (Thickness)GetValue(MarginProperty); set => SetValue(MarginProperty, value); }
        public HorizontalAlignment HorizontalAlignment { get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty, value); }
        public VerticalAlignment VerticalAlignment { get => (VerticalAlignment)GetValue(VerticalAlignmentProperty); set => SetValue(VerticalAlignmentProperty, value); }
        public string Name { get => (string)GetValue(NameProperty); set => SetValue(NameProperty, value); }
        public object Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }
        public object DataContext { get => GetValue(DataContextProperty); set => SetValue(DataContextProperty, value); }
        public Style Style { get => (Style)GetValue(StyleProperty); set => SetValue(StyleProperty, value); }
        public Cursor Cursor { get => (Cursor)GetValue(CursorProperty); set => SetValue(CursorProperty, value); }
        public object ToolTip { get => GetValue(ToolTipProperty); set => SetValue(ToolTipProperty, value); }
        public Transform LayoutTransform { get => (Transform)GetValue(LayoutTransformProperty); set => SetValue(LayoutTransformProperty, value); }
        public FlowDirection FlowDirection { get => (FlowDirection)GetValue(FlowDirectionProperty); set => SetValue(FlowDirectionProperty, value); }
        public bool UseLayoutRounding { get => (bool)GetValue(UseLayoutRoundingProperty); set => SetValue(UseLayoutRoundingProperty, value); }
        public Style FocusVisualStyle { get => (Style)GetValue(FocusVisualStyleProperty); set => SetValue(FocusVisualStyleProperty, value); }
        public object ContextMenu { get => GetValue(ContextMenuProperty); set => SetValue(ContextMenuProperty, value); }

        public DependencyObject Parent => ParentElement;

        public DependencyObject TemplatedParent => null;

        public bool IsLoaded => _loadedRaised;

        public bool IsInitialized => true;

        /// <summary>The explicit size if there is one, otherwise what the browser measured (0 before the first drawing).</summary>
        public double ActualWidth => double.IsNaN(Width) ? RenderSize.Width : Width;

        public double ActualHeight => double.IsNaN(Height) ? RenderSize.Height : Height;

        public ResourceDictionary Resources
        {
            get => _resources ??= new ResourceDictionary();
            set => _resources = value;
        }

        internal bool HasResources => _resources != null && _resources.Count > 0;

        /// <summary>Names declared in the XAML file this element is the root of (null for all other elements).</summary>
        internal Dictionary<string, object> NameScope { get; set; }

        /// <summary>Web folder of the plugin whose XAML this element is the root of ("T9Code"); picture paths are relative to it.</summary>
        internal string ResourceRoot { get; set; }

        /// <summary>Folder of the XAML file inside <see cref="ResourceRoot"/> ("View/"), for paths relative to the file.</summary>
        internal string ResourceDirectory { get; set; }

        /// <summary>Storyboards and triggers declared in XAML; triggers are not evaluated.</summary>
        public List<object> Triggers { get; } = new List<object>();

        public event RoutedEventHandler Loaded;
        public event RoutedEventHandler Unloaded;
        public event SizeChangedEventHandler SizeChanged;
        public event DependencyPropertyChangedEventHandler DataContextChanged;
        public event EventHandler Initialized;
        public event EventHandler LayoutUpdated;

        protected override object GetDefaultValue(DependencyProperty dp)
        {
            // the data context is inherited from the parent
            if (dp == DataContextProperty && ParentElement != null)
            {
                return ParentElement.GetValue(dp);
            }
            return base.GetDefaultValue(dp);
        }

        private static void OnDataContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = (FrameworkElement)d;
            element.DataContextChanged?.Invoke(element, e);
        }

        private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = (FrameworkElement)d;
            var style = e.NewValue as Style;
            style?.ApplyTo(element);
            element._styleTriggers = style == null ? null : new List<TriggerBase>(style.AllTriggers());
        }

        // the triggers of the applied style (null or empty: none) and the properties the style has set
        private List<TriggerBase> _styleTriggers;
        private HashSet<DependencyProperty> _styledProperties;

        /// <summary>Properties whose current value came from the style (a trigger may override those).</summary>
        internal HashSet<DependencyProperty> StyledProperties => _styledProperties ??= new HashSet<DependencyProperty>();

        internal override bool HasStyleTriggers => _styleTriggers is { Count: > 0 };

        /// <summary>
        /// The value that the last active trigger of the style sets for <paramref name="dp"/>; a value the element
        /// set itself (not through the style) wins, as in WPF.
        /// </summary>
        internal override bool TryGetTriggerValue(DependencyProperty dp, out object value)
        {
            value = null;
            if (HasLocalValue(dp) && !StyledProperties.Contains(dp))
            {
                return false;
            }
            bool found = false;
            foreach (TriggerBase trigger in _styleTriggers)
            {
                if (!SetsProperty(trigger, dp, out object triggerValue) || !trigger.IsActive(this))
                {
                    continue;
                }
                value = triggerValue;
                found = true;
            }
            return found;
        }

        /// <summary>Whether a trigger has a setter for the property, and its value converted to the property type.</summary>
        private bool SetsProperty(TriggerBase trigger, DependencyProperty dp, out object value)
        {
            value = null;
            foreach (SetterBase setterBase in trigger.ActiveSetters)
            {
                if (setterBase is not Setter setter || !string.IsNullOrEmpty(setter.TargetName))
                {
                    continue;
                }
                DependencyProperty property = setter.Property ?? Markup.DependencyPropertyLookup.Find(GetType(), setter.PropertyName);
                if (property != dp)
                {
                    continue;
                }
                value = setter.Value;
                if (value is string text && dp.PropertyType != typeof(string) && dp.PropertyType != typeof(object)
                    && !Markup.XamlValueConverter.TryConvert(text, dp.PropertyType, out value))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>Raises <see cref="Loaded"/> once on this element and its descendants (called by the host before the first drawing).</summary>
        internal void RaiseLoaded()
        {
            if (!_loadedRaised)
            {
                _loadedRaised = true;
                Initialized?.Invoke(this, EventArgs.Empty);
                Loaded?.Invoke(this, new RoutedEventArgs { Source = this });
            }
            foreach (UIElement child in new List<UIElement>(ChildElements))
            {
                (child as FrameworkElement)?.RaiseLoaded();
            }
        }

        /// <summary>True when code listens for size changes, so the browser has to report the measured size.</summary>
        internal bool WantsSize => SizeChanged != null;

        internal void ReportSize(Size size)
        {
            Size previous = RenderSize;
            if (previous.Width == size.Width && previous.Height == size.Height)
            {
                return;
            }
            RenderSize = size;
            SizeChanged?.Invoke(this, new SizeChangedEventArgs { Source = this, PreviousSize = previous, NewSize = size });
        }

        /// <summary>Makes an object a child for data context and resource lookup without drawing it.</summary>
        protected internal void AddLogicalChild(object child) => SetParent(child, this);

        protected internal void RemoveLogicalChild(object child) => SetParent(child, null);

        // ---- names and resources ------------------------------------------------------------------------------

        public object FindName(string name)
        {
            for (UIElement e = this; e != null; e = e.ParentElement)
            {
                if (e is FrameworkElement fe && fe.NameScope != null && fe.NameScope.TryGetValue(name, out object found))
                {
                    return found;
                }
            }
            return null;
        }

        public void RegisterName(string name, object scopedElement)
        {
            FrameworkElement owner = this;
            for (UIElement e = this; e != null; e = e.ParentElement)
            {
                if (e is FrameworkElement fe && fe.NameScope != null)
                {
                    owner = fe;
                    break;
                }
            }
            owner.NameScope ??= new Dictionary<string, object>();
            owner.NameScope[name] = scopedElement;
        }

        public void UnregisterName(string name)
        {
            for (UIElement e = this; e != null; e = e.ParentElement)
            {
                (e as FrameworkElement)?.NameScope?.Remove(name);
            }
        }

        public object TryFindResource(object resourceKey)
        {
            for (UIElement e = this; e != null; e = e.ParentElement)
            {
                if (e is FrameworkElement fe && fe._resources != null && fe._resources.TryGetResource(resourceKey, out object found))
                {
                    return found;
                }
            }
            return null;
        }

        public object FindResource(object resourceKey) => TryFindResource(resourceKey);

        // ---- bindings and animations --------------------------------------------------------------------------

        public BindingExpressionBase SetBinding(DependencyProperty dp, BindingBase binding)
        {
            return BindingOperations.SetBinding(this, dp, binding);
        }

        public BindingExpressionBase SetBinding(DependencyProperty dp, string path) => SetBinding(dp, new Binding(path));

        public BindingExpression GetBindingExpression(DependencyProperty dp) => GetStoredValue(dp) as BindingExpression;

        public void BeginStoryboard(Storyboard storyboard) => storyboard?.Begin(this);

        public void BringIntoView() { }

        public bool ApplyTemplate() => false;

        public virtual void OnApplyTemplate() { }

        /// <summary>Custom layout is not run (the browser lays out); present so that custom elements compile.</summary>
        protected virtual Size MeasureOverride(Size availableSize) => availableSize;

        protected virtual Size ArrangeOverride(Size finalSize) => finalSize;

        /// <summary>Custom drawing: what is drawn here is recorded and shown as SVG behind the children.</summary>
        protected virtual void OnRender(DrawingContext drawingContext) { }

        internal void InvokeOnRender(DrawingContext drawingContext) => OnRender(drawingContext);
    }
}
