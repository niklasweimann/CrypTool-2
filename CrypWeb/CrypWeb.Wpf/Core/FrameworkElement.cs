using System.Linq;
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

        /// <summary>Key of a control's default (theme) style; controls override its metadata, the value is not used here.</summary>
        protected internal static readonly DependencyProperty DefaultStyleKeyProperty = Dp.Register<object, FrameworkElement>("DefaultStyleKey");
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

        /// <summary>The control whose template created this element (null outside control templates).</summary>
        public DependencyObject TemplatedParent { get; internal set; }

        public bool IsLoaded => _loadedRaised;

        public bool IsInitialized => true;

        /// <summary>The explicit size if there is one, otherwise what the browser measured (0 before the first drawing).</summary>
        public double ActualWidth
        {
            get
            {
                NoteSizeRead();
                return double.IsNaN(Width) ? RenderSize.Width : Width;
            }
        }

        public double ActualHeight
        {
            get
            {
                NoteSizeRead();
                return double.IsNaN(Height) ? RenderSize.Height : Height;
            }
        }

        // code that reads ActualWidth/ActualHeight needs the size the browser lays the element out with: from then on
        // the element reports it (see WantsSize)
        private bool _sizeRead;

        private void NoteSizeRead()
        {
            if (!_sizeRead && RenderSize.Width == 0 && RenderSize.Height == 0)
            {
                _sizeRead = true;
                RenderVersion.Bump();
            }
        }

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

        /// <summary>Triggers declared in XAML; event triggers start working when the element is loaded.</summary>
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
            foreach (EventTrigger trigger in element._styleTriggers?.OfType<EventTrigger>() ?? Enumerable.Empty<EventTrigger>())
            {
                trigger.Attach(element);
            }
            foreach (EventSetter setter in style?.AllEventSetters() ?? Enumerable.Empty<EventSetter>())
            {
                setter.Attach(element);
            }
        }

        // the triggers of the applied style (null or empty: none) and the properties the style has set
        private List<TriggerBase> _styleTriggers;
        private HashSet<DependencyProperty> _styledProperties;

        /// <summary>Properties whose current value came from the style (a trigger may override those).</summary>
        internal HashSet<DependencyProperty> StyledProperties => _styledProperties ??= new HashSet<DependencyProperty>();

        /// <summary>A named element of the control template's tree (see Control.TemplateRoot).</summary>
        protected DependencyObject GetTemplateChild(string childName) =>
            (this as Controls.Control)?.FindTemplatePart(childName);

        /// <summary>True when a style trigger or a control-template trigger may set properties of this element.</summary>
        internal override bool HasStyleTriggers =>
            _styleTriggers is { Count: > 0 }
            || (this is Controls.Control { HasTemplateTriggers: true })
            || (TemplatedParent is Controls.Control { HasTemplateTriggers: true } && !string.IsNullOrEmpty(TemplatePartName));

        // the name without asking triggers (Name is a dependency property itself)
        private string TemplatePartName => GetValueIgnoringTriggers(NameProperty) as string;

        /// <summary>
        /// The value that active triggers set for <paramref name="dp"/>, in WPF's order of precedence:
        /// <list type="number">
        /// <item>triggers of the control template this element is part of that name it (TargetName); they win over
        ///   the values written on the element in the template,</item>
        /// <item>otherwise, unless the element set a value itself (not through its style): the triggers of its
        ///   style, then the triggers of its own control template.</item>
        /// </list>
        /// Within one list the last active trigger wins.
        /// </summary>
        internal override bool TryGetTriggerValue(DependencyProperty dp, out object value)
        {
            // 1. the template this element belongs to changes it by name
            if (TemplatedParent is Controls.Control owner && owner.HasTemplateTriggers && TemplatePartName is { Length: > 0 } partName
                && TryGetActiveSetterValue(owner.TemplateTriggers, owner, dp, partName, out value))
            {
                return true;
            }

            value = null;
            if (HasLocalValue(dp) && !StyledProperties.Contains(dp))
            {
                return false;
            }

            // 2. the element's style, then its own template (setters without TargetName)
            if (_styleTriggers != null && TryGetActiveSetterValue(_styleTriggers, this, dp, null, out value))
            {
                return true;
            }
            if (this is Controls.Control { HasTemplateTriggers: true } control
                && TryGetActiveSetterValue(control.TemplateTriggers, control, dp, null, out value))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// The value of the last active trigger in <paramref name="triggers"/> that sets <paramref name="dp"/> on the
        /// element named <paramref name="targetName"/> (null: the element the triggers belong to). Conditions are
        /// evaluated on <paramref name="conditionElement"/> (the styled element or the templated control), or on
        /// the template part a trigger names in SourceName.
        /// </summary>
        private bool TryGetActiveSetterValue(IEnumerable<TriggerBase> triggers, FrameworkElement conditionElement,
            DependencyProperty dp, string targetName, out object value)
        {
            value = null;
            bool found = false;
            foreach (TriggerBase trigger in triggers)
            {
                if (!SetsProperty(trigger, dp, targetName, out object triggerValue))
                {
                    continue;
                }
                FrameworkElement source = conditionElement;
                if (trigger is Trigger { SourceName: { Length: > 0 } sourceName } && conditionElement is Controls.Control templated)
                {
                    source = templated.FindTemplatePart(sourceName) as FrameworkElement ?? conditionElement;
                }
                if (trigger.IsActive(source))
                {
                    value = triggerValue;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Whether a trigger has a setter for the property on the element named <paramref name="targetName"/> (null:
        /// setters without TargetName), and its value converted to the property type.
        /// </summary>
        private bool SetsProperty(TriggerBase trigger, DependencyProperty dp, string targetName, out object value)
        {
            value = null;
            foreach (SetterBase setterBase in trigger.ActiveSetters)
            {
                if (setterBase is not Setter setter)
                {
                    continue;
                }
                bool targetsThis = string.IsNullOrEmpty(targetName)
                    ? string.IsNullOrEmpty(setter.TargetName)
                    : setter.TargetName == targetName;
                if (!targetsThis)
                {
                    continue;
                }
                // the loader resolved the property against the style's or template's target type; a setter for a
                // named template part means the property of that part's type (Border.Background, not Control.Background)
                DependencyProperty property = string.IsNullOrEmpty(setter.TargetName)
                    ? setter.Property ?? Markup.DependencyPropertyLookup.Find(GetType(), setter.PropertyName)
                    : Markup.DependencyPropertyLookup.Find(GetType(), setter.PropertyName) ?? setter.Property;
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

        /// <summary>
        /// True when a trigger of the element's style or control template has a condition on the property named
        /// <paramref name="propertyName"/> (IsMouseOver, IsPressed): the browser then reports that state.
        /// </summary>
        internal bool HasTriggerOn(string propertyName)
        {
            IEnumerable<TriggerBase> triggers = _styleTriggers ?? Enumerable.Empty<TriggerBase>();
            if (this is Controls.Control { HasTemplateTriggers: true } control)
            {
                triggers = triggers.Concat(control.TemplateTriggers);
            }
            foreach (TriggerBase trigger in triggers)
            {
                if (trigger is Trigger single && TriggerCondition.NamesProperty(single.Property, single.PropertyName, propertyName))
                {
                    return true;
                }
                if (trigger is MultiTrigger multi
                    && multi.Conditions.Any(c => c.Binding == null && TriggerCondition.NamesProperty(c.Property, c.PropertyName, propertyName)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Raises <see cref="Loaded"/> once on this element and its descendants (called by the host before the first drawing).</summary>
        internal void RaiseLoaded()
        {
            if (!_loadedRaised)
            {
                _loadedRaised = true;
                foreach (EventTrigger trigger in Triggers.OfType<EventTrigger>())
                {
                    trigger.Attach(this);
                }
                Initialized?.Invoke(this, EventArgs.Empty);
                Loaded?.Invoke(this, new RoutedEventArgs { Source = this });
            }
            foreach (UIElement child in new List<UIElement>(ChildElements))
            {
                (child as FrameworkElement)?.RaiseLoaded();
            }
        }

        /// <summary>
        /// True when the browser should report this element's size: code handles SizeChanged, has read its actual
        /// size, or may read it (the element has a name, so the code-behind can reach it).
        /// </summary>
        internal bool WantsSize => SizeChanged != null || _sizeRead || !string.IsNullOrEmpty(Name);

        internal void ReportSize(Size size)
        {
            Size previous = RenderSize;
            if (previous.Width == size.Width && previous.Height == size.Height)
            {
                return;
            }
            RenderSize = size;
            if (_sizeRead)
            {
                RenderVersion.Bump();   // something depends on this size (code, or a binding to ActualWidth/ActualHeight)
            }
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
