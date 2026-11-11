using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace System.Windows
{
    public class PropertyPath
    {
        public PropertyPath(string path, params object[] pathParameters) => Path = path;

        public PropertyPath(object parameter) => Path = (parameter as DependencyProperty)?.Name ?? parameter?.ToString();

        public string Path { get; set; }
    }
}

namespace System.Windows.Data
{
    public enum UpdateSourceTrigger { Default, PropertyChanged, LostFocus, Explicit }

    public enum RelativeSourceMode { PreviousData, TemplatedParent, Self, FindAncestor }

    public class RelativeSource
    {
        public RelativeSource() { }

        public RelativeSource(RelativeSourceMode mode) => Mode = mode;

        public RelativeSource(RelativeSourceMode mode, Type ancestorType, int ancestorLevel)
        {
            Mode = mode;
            AncestorType = ancestorType;
            AncestorLevel = ancestorLevel;
        }

        public RelativeSourceMode Mode { get; set; }
        public Type AncestorType { get; set; }
        public int AncestorLevel { get; set; } = 1;

        public static RelativeSource Self => new RelativeSource(RelativeSourceMode.Self);
        public static RelativeSource TemplatedParent => new RelativeSource(RelativeSourceMode.TemplatedParent);
    }

    public abstract class BindingBase
    {
        public string StringFormat { get; set; }
        public object FallbackValue { get; set; }
        public object TargetNullValue { get; set; }

        internal abstract BindingExpressionBase CreateExpression();
    }

    /// <summary>Connects a property of an element to a property of a source object (data context, named element, ...).</summary>
    public class Binding : BindingBase
    {
        public Binding() { }

        public Binding(string path) => Path = new PropertyPath(path);

        public PropertyPath Path { get; set; }
        public object Source { get; set; }
        public string ElementName { get; set; }
        public RelativeSource RelativeSource { get; set; }
        public IValueConverter Converter { get; set; }
        public object ConverterParameter { get; set; }
        public CultureInfo ConverterCulture { get; set; }
        public BindingMode Mode { get; set; } = BindingMode.Default;
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
        public bool NotifyOnTargetUpdated { get; set; }
        public bool NotifyOnSourceUpdated { get; set; }
        public bool ValidatesOnDataErrors { get; set; }
        public bool ValidatesOnExceptions { get; set; }
        public bool IsAsync { get; set; }
        public string XPath { get; set; }
        public Collection<Controls.ValidationRule> ValidationRules { get; } = new Collection<Controls.ValidationRule>();

        internal override BindingExpressionBase CreateExpression() => new BindingExpression(this);
    }

    public class MultiBinding : BindingBase
    {
        public Collection<BindingBase> Bindings { get; } = new Collection<BindingBase>();
        public IMultiValueConverter Converter { get; set; }
        public object ConverterParameter { get; set; }
        public BindingMode Mode { get; set; } = BindingMode.Default;

        internal override BindingExpressionBase CreateExpression() => new MultiBindingExpression(this);
    }

    /// <summary>A binding attached to one property of one element. It is evaluated every time the property is read.</summary>
    public abstract class BindingExpressionBase : IDeferredValue
    {
        public abstract object Evaluate(DependencyObject target, DependencyProperty property);

        public virtual void UpdateTarget() => RenderVersion.Bump();

        public virtual void UpdateSource() { }
    }

    public sealed class BindingExpression : BindingExpressionBase
    {
        private readonly Binding _binding;
        private DependencyObject _target;

        internal BindingExpression(Binding binding) => _binding = binding;

        public Binding ParentBinding => _binding;

        public object DataItem => _target == null ? null : ResolveSource(_target);

        public override object Evaluate(DependencyObject target, DependencyProperty property)
        {
            _target = target;
            object value;
            try
            {
                object source = ResolveSource(target, property);
                value = PropertyPathWalker.GetValue(source, _binding.Path?.Path, out bool found);
                if (!found)
                {
                    return _binding.FallbackValue != null ? Convert(_binding.FallbackValue, property) : property.Metadata.DefaultValue;
                }
                if (_binding.Converter != null)
                {
                    value = _binding.Converter.Convert(value, property.PropertyType, _binding.ConverterParameter, _binding.ConverterCulture ?? CultureInfo.CurrentUICulture);
                }
            }
            catch (Exception)
            {
                return property.Metadata.DefaultValue;
            }

            if (value == null && _binding.TargetNullValue != null)
            {
                value = _binding.TargetNullValue;
            }
            if (_binding.StringFormat != null)
            {
                value = BindingFormat.Apply(_binding.StringFormat, value);
            }
            return Convert(value, property);
        }

        /// <summary>Writes a value entered by the user back to the source property (two-way bindings).</summary>
        internal void WriteBack(DependencyObject target, object value)
        {
            if (_binding.Mode == BindingMode.OneWay || _binding.Mode == BindingMode.OneTime)
            {
                return;
            }
            try
            {
                if (_binding.Converter != null)
                {
                    value = _binding.Converter.ConvertBack(value, typeof(object), _binding.ConverterParameter, CultureInfo.CurrentUICulture);
                }
                PropertyPathWalker.SetValue(ResolveSource(target), _binding.Path?.Path, value);
            }
            catch (Exception)
            {
                // an invalid input leaves the source unchanged, as in WPF
            }
        }

        private static object Convert(object value, DependencyProperty property)
        {
            Type type = property.PropertyType;
            if (value == null || type == typeof(object) || type.IsInstanceOfType(value))
            {
                return value;
            }
            if (type == typeof(string))
            {
                return System.Convert.ToString(value, CultureInfo.CurrentCulture);
            }
            if (value is string text && Markup.XamlValueConverter.TryConvert(text, type, out object converted))
            {
                return converted;
            }
            object coerced = ValueCoercion.ToPropertyType(value, type);
            return type.IsInstanceOfType(coerced) ? coerced : property.Metadata.DefaultValue;
        }

        private object ResolveSource(DependencyObject target, DependencyProperty property = null)
        {
            if (_binding.Source != null)
            {
                return _binding.Source;
            }
            if (!string.IsNullOrEmpty(_binding.ElementName))
            {
                return (target as FrameworkElement)?.FindName(_binding.ElementName);
            }
            if (_binding.RelativeSource != null)
            {
                return ResolveRelativeSource(target, _binding.RelativeSource);
            }
            if (property == FrameworkElement.DataContextProperty)
            {
                // DataContext="{Binding ...}" is evaluated against the parent's data context (as in WPF), not its own
                return InheritedDataContext(target as UIElement);
            }
            return (target as FrameworkElement)?.DataContext;
        }

        /// <summary>The data context an element gets from above: from its parent, or the content it shows as a template.</summary>
        private static object InheritedDataContext(UIElement element)
        {
            UIElement parent = element?.ParentElement;
            if (parent is Controls.ContentControl owner && ReferenceEquals(owner.TemplatedContent, element))
            {
                return owner.Content;
            }
            return (parent as FrameworkElement)?.DataContext;
        }

        private static object ResolveRelativeSource(DependencyObject target, RelativeSource relative)
        {
            if (relative.Mode == RelativeSourceMode.Self)
            {
                return target;
            }
            if (relative.Mode != RelativeSourceMode.FindAncestor)
            {
                return null;
            }
            int level = Math.Max(1, relative.AncestorLevel);
            for (UIElement e = (target as UIElement)?.ParentElement; e != null; e = e.ParentElement)
            {
                if ((relative.AncestorType == null || relative.AncestorType.IsInstanceOfType(e)) && --level == 0)
                {
                    return e;
                }
            }
            return null;
        }
    }

    public sealed class MultiBindingExpression : BindingExpressionBase
    {
        private readonly MultiBinding _binding;

        internal MultiBindingExpression(MultiBinding binding) => _binding = binding;

        public override object Evaluate(DependencyObject target, DependencyProperty property)
        {
            try
            {
                var values = new List<object>();
                foreach (BindingBase child in _binding.Bindings)
                {
                    values.Add(child.CreateExpression().Evaluate(target, ObjectProperty));
                }
                object result = _binding.Converter != null
                    ? _binding.Converter.Convert(values.ToArray(), property.PropertyType, _binding.ConverterParameter, CultureInfo.CurrentUICulture)
                    : _binding.StringFormat != null ? string.Format(CultureInfo.CurrentCulture, _binding.StringFormat, values.ToArray()) : null;
                return result ?? property.Metadata.DefaultValue;
            }
            catch (Exception)
            {
                return property.Metadata.DefaultValue;
            }
        }

        private static readonly DependencyProperty ObjectProperty =
            DependencyProperty.RegisterAttached("MultiBindingValue", typeof(object), typeof(MultiBindingExpression));
    }

    public static class BindingOperations
    {
        public static BindingExpressionBase SetBinding(DependencyObject target, DependencyProperty dp, BindingBase binding)
        {
            BindingExpressionBase expression = binding.CreateExpression();
            target.SetValue(dp, expression);
            return expression;
        }

        public static BindingExpression GetBindingExpression(DependencyObject target, DependencyProperty dp)
        {
            return target.GetStoredValue(dp) as BindingExpression;
        }

        public static void ClearBinding(DependencyObject target, DependencyProperty dp)
        {
            if (target.GetStoredValue(dp) is BindingExpressionBase)
            {
                target.ClearValue(dp);
            }
        }

        public static void ClearAllBindings(DependencyObject target) { }

        public static void EnableCollectionSynchronization(Collections.IEnumerable collection, object lockObject) { }
    }

    /// <summary>Evaluates a binding against a data item that is not the data context of an element (table cells).</summary>
    internal static class BindingEvaluator
    {
        private static readonly DependencyProperty ValueProperty =
            DependencyProperty.RegisterAttached("Value", typeof(object), typeof(BindingEvaluator));

        public static object Evaluate(BindingBase binding, object dataItem)
        {
            var probe = new FrameworkElement { DataContext = dataItem };
            return binding.CreateExpression().Evaluate(probe, ValueProperty);
        }
    }

    internal static class BindingFormat
    {
        /// <summary>Applies a binding <c>StringFormat</c>: either a composite format ("{0:N2} %") or a plain format ("N2").</summary>
        public static object Apply(string format, object value)
        {
            if (format.StartsWith("{}", StringComparison.Ordinal))
            {
                format = format.Substring(2);
            }
            try
            {
                return format.Contains("{0")
                    ? string.Format(CultureInfo.CurrentCulture, format, value)
                    : string.Format(CultureInfo.CurrentCulture, "{0:" + format + "}", value);
            }
            catch (FormatException)
            {
                return value;
            }
        }
    }
}

namespace System.Windows.Controls
{
    /// <summary>Binding converter: true -> Visible, false -> Collapsed.</summary>
    public sealed class BooleanToVisibilityConverter : Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, Globalization.CultureInfo culture)
        {
            return value is true ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, Globalization.CultureInfo culture)
        {
            return value is Visibility.Visible;
        }
    }
}
