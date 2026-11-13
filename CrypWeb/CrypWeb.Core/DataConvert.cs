using System.Globalization;
using System.Numerics;
using System.Text;
using CrypTool.PluginBase.IO;

namespace CrypTool.Web.Core;

/// <summary>Conversions between the text form of settings and typed values, and between types flowing along wires.</summary>
public static class DataConvert
{
    /// <summary>A setting value in the text form stored in workspaces (the inverse of <see cref="FromString"/>).</summary>
    public static string ToSettingString(object? value) => value switch
    {
        null => "",
        bool b => b ? "True" : "False",
        Enum e => Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    public static object? FromString(string text, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return text;
        if (type.IsEnum) return int.TryParse(text, out var number) ? Enum.ToObject(type, number) : Enum.Parse(type, text, true);
        if (type == typeof(bool)) return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (type == typeof(BigInteger)) return BigInteger.Parse(text);
        if (type == typeof(double)) return double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
        if (type == typeof(float)) return float.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
        return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }

    /// <summary>Converts a value flowing along a wire to the type expected by the target property (the same conversions CT2 does).</summary>
    public static object? To(object? value, Type? target)
    {
        if (value == null || target == null || target.IsInstanceOfType(value)) return value;

        if (value is ICrypToolStream stream)
        {
            using var reader = stream.CreateReader();
            var buffer = new byte[stream.Length];
            int filled = 0;
            while (filled < buffer.Length)
            {
                int count = reader.Read(buffer, filled, buffer.Length - filled);
                if (count <= 0) break;
                filled += count;
            }
            return To(buffer, target);
        }
        if (target == typeof(string))
            return value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : value.ToString();
        if (target == typeof(byte[]))
            return value is string text ? Encoding.UTF8.GetBytes(text) : value;
        if (target.IsAssignableFrom(typeof(CStreamWriter)))
        {
            var writer = new CStreamWriter();
            writer.Write(value is string s ? Encoding.UTF8.GetBytes(s) : (byte[])value);
            writer.Close();
            return writer;
        }
        if (target == typeof(BigInteger))
            return value is double d ? new BigInteger(d) : BigInteger.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
        if (value is BigInteger big)
        {
            if (target == typeof(int)) return (int)big;
            if (target == typeof(long)) return (long)big;
            if (target == typeof(double)) return (double)big;
            if (target == typeof(string)) return big.ToString();
        }

        try
        {
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return value;
        } // not convertible: pass it on and let the plugin complain
    }
}
