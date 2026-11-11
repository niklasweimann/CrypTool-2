using System.Globalization;
using CrypTool.PluginBase;

namespace CrypTool.Web.Core;

/// <summary>One editable plugin setting, derived from the plugin's [TaskPane] attribute.</summary>
public sealed class SettingField
{
    public string Name { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Tooltip { get; set; } = "";
    public string Group { get; set; } = "";
    public int Order { get; set; }
    /// <summary>TextBox, ComboBox, RadioButton, CheckBox, NumericUpDown, Slider, TextBoxReadOnly, TextBoxHidden, KeyTextBox, DynamicComboBox, OpenFileDialog, SaveFileDialog, Button, LanguageSelector.</summary>
    public string Control { get; set; } = "TextBox";
    /// <summary>bool, int, double, string, enum, other.</summary>
    public string ValueType { get; set; } = "string";
    /// <summary>Full .NET type name of the setting (stored in .cwm files).</summary>
    public string TypeName { get; set; } = "";
    public string Value { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Step { get; set; }
    public string? Regex { get; set; }
    public bool Live { get; set; }
}

/// <summary>Builds the settings form description of a plugin by reflecting over its ISettings object.</summary>
public static class SettingsSchema
{
    /// <summary>Language names (index = language id) from LanguageStatisticsLib, resolved by reflection so Core needs no reference to it.</summary>
    static List<string> SupportedLanguages()
    {
        try
        {
            var t = Type.GetType("LanguageStatisticsLib.LanguageStatistics, LanguageStatisticsLib");
            if (t?.GetProperty("SupportedLanguages")?.GetValue(null) is string[] names) return names.ToList();
        }
        catch { }
        return new();
    }

    /// <summary>
    /// A DynamicComboBox does not list its entries in the attribute: ControlValues[0] names a property of the
    /// settings object (usually an ObservableCollection&lt;string&gt;) that holds the current entries.
    /// </summary>
    static List<string> DynamicOptions(ISettings settings, string[]? controlValues)
    {
        var listProperty = controlValues?.FirstOrDefault();
        if (string.IsNullOrEmpty(listProperty)) return new();
        try
        {
            var value = settings.GetType().GetProperty(listProperty)?.GetValue(settings);
            if (value is System.Collections.IEnumerable entries and not string)
                return entries.Cast<object?>().Select(e => e?.ToString() ?? "").ToList();
        }
        catch { }
        return new();
    }

    public static List<SettingField> Build(string typeName, IReadOnlyDictionary<string, string> values)
    {
        var type = PluginRegistry.FindType(typeName);
        if (type == null) return new();
        var plugin = (ICrypComponent)Activator.CreateInstance(type)!;
        var comp = new PluginComponent(plugin); // runs Initialize()
        comp.ApplySettings(values);
        var settings = plugin.Settings;
        if (settings == null) return new();
        var list = new List<SettingField>();
        foreach (var attr in settings.GetSettingsProperties(plugin).OrderBy(a => a.Order))
        {
            var pi = attr.PropertyInfo;
            var f = new SettingField
            {
                Caption = attr.Caption ?? "", Tooltip = attr.ToolTip ?? "", Group = attr.HasGroupName ? attr.GroupName ?? "" : "",
                Order = attr.Order, Control = attr.ControlType.ToString(), Live = attr.ChangeableWhileExecuting,
            };
            if (pi == null) { f.Name = attr.MethodInfo?.Name ?? ""; f.Control = "Button"; list.Add(f); continue; }
            f.Name = pi.Name;
            f.TypeName = (Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType).FullName ?? "";
            var t = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
            object? val = null; try { val = pi.GetValue(settings); } catch { }
            f.ValueType = t == typeof(bool) ? "bool" : t.IsEnum ? "enum" : t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ? "int" : t == typeof(double) || t == typeof(float) ? "double" : t == typeof(string) ? "string" : "other";
            f.Value = val == null ? "" : val switch
            {
                bool b => b ? "True" : "False",
                Enum e => Convert.ToInt64(e).ToString(CultureInfo.InvariantCulture),
                IFormattable fm => fm.ToString(null, CultureInfo.InvariantCulture),
                _ => val.ToString() ?? ""
            };
            var cv = attr.ControlValues;
            if (attr.ControlType == ControlType.DynamicComboBox)
                f.Options = DynamicOptions(settings, cv);
            else if (cv != null)
                f.Options = cv.Select(x => x ?? "").ToList();
            if (f.Options.Count == 0 && attr.ControlType == ControlType.LanguageSelector) f.Options = SupportedLanguages();
            if (attr.ControlType == ControlType.Slider || attr.ControlType == ControlType.NumericUpDown || attr.ValidationType == ValidationType.RangeInteger || attr.ValidationType == ValidationType.RangeDouble)
            {
                bool isInt = f.ValueType == "int" || attr.ValidationType == ValidationType.RangeInteger;
                if (isInt && (attr.IntegerMinValue != 0 || attr.IntegerMaxValue != 0)) { f.Min = attr.IntegerMinValue; f.Max = attr.IntegerMaxValue; f.Step = 1; }
                else if (attr.DoubleMinValue != 0 || attr.DoubleMaxValue != 0) { f.Min = attr.DoubleMinValue; f.Max = attr.DoubleMaxValue; f.Step = attr.DoubleIncrement > 0 ? attr.DoubleIncrement : null; }
            }
            if (attr.ValidationType == ValidationType.RegEx) f.Regex = attr.RegularExpression;
            list.Add(f);
        }
        return list;
    }
}
