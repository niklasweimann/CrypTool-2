using System.Globalization;

namespace System.Windows.Data
{
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
                    value = _binding.Converter.Convert(value,
                        property.PropertyType,
                        _binding.ConverterParameter,
                        _binding.ConverterCulture ?? CultureInfo.CurrentUICulture);
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
                    value = _binding.Converter.ConvertBack(value,
                        typeof(object),
                        _binding.ConverterParameter,
                        CultureInfo.CurrentUICulture);
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
            return target?.InheritedDataContext();
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
}
