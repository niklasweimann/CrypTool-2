using System.Globalization;

namespace System.Windows
{
    /// <summary>Makes a value assignable to a dependency property (WPF itself is strict; plugin code is not always).</summary>
    internal static class ValueCoercion
    {
        public static object ToPropertyType(object value, Type propertyType)
        {
            if (value == null || propertyType == null || propertyType.IsInstanceOfType(value))
            {
                return value;
            }

            Type target = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (target.IsEnum && value is string name)
            {
                return Enum.Parse(target, name, ignoreCase: true);
            }

            if (value is IConvertible && target.IsPrimitive)
            {
                try
                {
                    return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    return value;
                }
            }

            return value;
        }
    }
}
