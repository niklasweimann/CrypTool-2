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
        catch (Exception) { /* the library is not part of this build: use the fallback list */ }
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
        catch (Exception) { /* the plugin has no such list: no options */ }
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
            list.Add(BuildField(settings, attr));
        return list;
    }

    static SettingField BuildField(ISettings settings, TaskPaneAttribute attr)
    {
        var field = new SettingField
        {
            Caption = attr.Caption ?? "",
            Tooltip = attr.ToolTip ?? "",
            Group = attr.HasGroupName ? attr.GroupName ?? "" : "",
            Order = attr.Order,
            Control = attr.ControlType.ToString(),
            Live = attr.ChangeableWhileExecuting,
        };

        var property = attr.PropertyInfo;
        if (property == null)
        {
            // a method attribute: shown as a button
            field.Name = attr.MethodInfo?.Name ?? "";
            field.Control = "Button";
            return field;
        }

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        field.Name = property.Name;
        field.TypeName = type.FullName ?? "";
        field.ValueType = ValueTypeOf(type);
        try
        {
            field.Value = DataConvert.ToSettingString(property.GetValue(settings));
        }
        catch (Exception) { /* a property that throws shows no value */ }

        var controlValues = attr.ControlValues;
        if (attr.ControlType == ControlType.DynamicComboBox) field.Options = DynamicOptions(settings, controlValues);
        else if (controlValues != null) field.Options = controlValues.Select(x => x ?? "").ToList();
        if (field.Options.Count == 0 && attr.ControlType == ControlType.LanguageSelector) field.Options = SupportedLanguages();

        bool ranged = attr.ControlType is ControlType.Slider or ControlType.NumericUpDown
            || attr.ValidationType is ValidationType.RangeInteger or ValidationType.RangeDouble;
        if (ranged) ApplyRange(field, attr);

        if (attr.ValidationType == ValidationType.RegEx) field.Regex = attr.RegularExpression;
        return field;
    }

    /// <summary>The simplified type the settings dialog uses to pick an editor.</summary>
    static string ValueTypeOf(Type type)
    {
        if (type == typeof(bool)) return "bool";
        if (type.IsEnum) return "enum";
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return "int";
        if (type == typeof(double) || type == typeof(float)) return "double";
        if (type == typeof(string)) return "string";
        return "other";
    }

    /// <summary>Min/max/step of a slider or number field; a range of 0..0 means "not specified".</summary>
    static void ApplyRange(SettingField field, TaskPaneAttribute attr)
    {
        bool isInteger = field.ValueType == "int" || attr.ValidationType == ValidationType.RangeInteger;
        if (isInteger && (attr.IntegerMinValue != 0 || attr.IntegerMaxValue != 0))
        {
            field.Min = attr.IntegerMinValue;
            field.Max = attr.IntegerMaxValue;
            field.Step = 1;
        }
        else if (attr.DoubleMinValue != 0 || attr.DoubleMaxValue != 0)
        {
            field.Min = attr.DoubleMinValue;
            field.Max = attr.DoubleMaxValue;
            field.Step = attr.DoubleIncrement > 0 ? attr.DoubleIncrement : null;
        }
    }
}
