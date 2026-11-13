using System.Globalization;
using System.Windows.Data;

namespace System.Windows
{
    /// <summary>Comparing the current value of a trigger condition with the value written in XAML.</summary>
    internal static class TriggerCondition
    {
        // a stand-in property to evaluate a data trigger's binding as "object"
        private static readonly DependencyProperty ProbeProperty = DependencyProperty.RegisterAttached("TriggerProbe", typeof(object), typeof(TriggerCondition));

        // triggers can depend on properties that other triggers set: do not evaluate them recursively
        [ThreadStatic] private static int _depth;

        /// <summary>
        /// True when the element's property has the expected value. The CLR property is read when there is one,
        /// because some states are computed rather than stored (TabItem.IsSelected, IsMouseOver, IsPressed).
        /// </summary>
        internal static bool PropertyMatches(FrameworkElement element, DependencyProperty property, string propertyName, object expected)
        {
            if (_depth > 0) return false;
            string name = ShortName(property?.Name ?? propertyName);
            if (string.IsNullOrEmpty(name)) return false;
            _depth++;
            try
            {
                System.Reflection.PropertyInfo clrProperty = element.GetType().GetProperty(name);
                if (clrProperty != null && clrProperty.GetIndexParameters().Length == 0)
                {
                    return ValuesMatch(clrProperty.GetValue(element), expected);
                }
                property ??= Markup.DependencyPropertyLookup.Find(element.GetType(), propertyName);
                return property != null && ValuesMatch(element.GetValue(property), expected);
            }
            finally
            {
                _depth--;
            }
        }

        /// <summary>True when a trigger condition (a property or its XAML name) is about <paramref name="name"/>.</summary>
        internal static bool NamesProperty(DependencyProperty property, string propertyName, string name) =>
            ShortName(property?.Name ?? propertyName) == name;

        // "Button.IsPressed" -> "IsPressed"
        private static string ShortName(string name) =>
            name == null ? null : name.Substring(name.LastIndexOf('.') + 1);

        internal static bool BindingMatches(FrameworkElement element, BindingBase binding, object expected)
        {
            if (binding == null || _depth > 0) return false;
            _depth++;
            try
            {
                return ValuesMatch(binding.CreateExpression().Evaluate(element, ProbeProperty), expected);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _depth--;
            }
        }

        /// <summary>Equal values; XAML gives the expected value as text, so it is converted to the actual value's type.</summary>
        internal static bool ValuesMatch(object actual, object expected)
        {
            if (Equals(actual, expected)) return true;
            if (expected is string text)
            {
                if (actual == null) return text.Length == 0 || text == "{x:Null}";
                if (actual is string) return false;
                if (Markup.XamlValueConverter.TryConvert(text, actual.GetType(), out object converted)
                    && Equals(actual, converted)) return true;
                return string.Equals(Convert.ToString(actual, CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
