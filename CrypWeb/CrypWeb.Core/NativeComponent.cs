using System.Numerics;
using CrypTool.PluginBase.IO;

namespace CrypTool.Web.Core;

/// <summary>
/// Browser-native replacements for components whose original implementation is WPF-only
/// (text/number/boolean in- and output, picture output). The app draws them as its own fields.
/// </summary>
public sealed class NativeComponent : RunComponent
{
    public enum Kind { TextInput, TextOutput, NumberInput, BooleanInput, BooleanOutput, PictureOutput }

    public Kind Which { get; }

    /// <summary>Values of the output properties (inputs: what the user typed, as text).</summary>
    readonly Dictionary<string, object?> values = new();

    /// <summary>The text of an input component, or the text an output component shows.</summary>
    public string Text { get; set; } = "";

    public NativeComponent(Kind kind) => Which = kind;

    /// <summary>Creates the replacement for a plugin type name (e.g. "TextInput.TextInput"), or null if the plugin has none.</summary>
    public static NativeComponent? TryCreate(string typeName) => typeName.Split('.').Last().ToLowerInvariant() switch
    {
        "textinput" => new NativeComponent(Kind.TextInput),
        "textoutput" => new NativeComponent(Kind.TextOutput),
        "numberinput" => new NativeComponent(Kind.NumberInput),
        "booleaninput" => new NativeComponent(Kind.BooleanInput),
        "booleanoutput" => new NativeComponent(Kind.BooleanOutput),
        "pictureoutput" => new NativeComponent(Kind.PictureOutput),
        _ => null,
    };

    /// <summary>PictureOutput: the received image as a data: URI.</summary>
    public string? ImageUri { get; private set; }

    /// <summary>PictureOutput: a counter that changes with every new picture.</summary>
    public int ImageVersion { get; private set; }

    public override Type? TypeOf(string property) => Which switch
    {
        Kind.PictureOutput => property == "PictureStream" ? typeof(ICrypToolStream) : typeof(byte[]),
        Kind.TextInput => typeof(string),
        // the text output takes anything (CT2's TextOutput input is "object"): bytes are shown as hex, see OutputText
        Kind.TextOutput => typeof(object),
        Kind.NumberInput => typeof(BigInteger),
        _ => typeof(bool),
    };

    /// <summary>The image MIME type from the file signature, or null if the browser cannot show the data.</summary>
    static string? ImageMimeType(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0x89 && b[1] == 0x50) return "image/png";
        if (b.Length > 2 && b[0] == 0xFF && b[1] == 0xD8) return "image/jpeg";
        if (b.Length > 3 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F') return "image/gif";
        if (b.Length > 2 && b[0] == 'B' && b[1] == 'M') return "image/bmp";
        if (b.Length > 11 && b[0] == 'R' && b[1] == 'I' && b[8] == 'W') return "image/webp";
        return null;
    }

    public override void Set(string property, object? value)
    {
        if (Which == Kind.PictureOutput)
        {
            ShowPicture(value);
            return;
        }
        values[property] = value;
        if (Which is Kind.TextOutput or Kind.BooleanOutput && value != null) Text = OutputText(value);
    }

    void ShowPicture(object? value)
    {
        var bytes = DataConvert.To(value, typeof(byte[])) as byte[];
        if (bytes == null) return;

        var mime = ImageMimeType(bytes);
        if (mime == null)
        {
            RaiseLog(LogLevel.Warning, "The data is not a picture the browser can show (PNG, JPEG, GIF, BMP, WebP).");
            return;
        }
        ImageUri = "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
        ImageVersion++;
    }

    /// <summary>How CT2's TextOutput shows a value: bytes (arrays and streams) as hex pairs separated by spaces.</summary>
    static string OutputText(object value)
    {
        byte[]? bytes = value switch
        {
            byte[] array => array,
            ICrypToolStream => DataConvert.To(value, typeof(byte[])) as byte[],
            _ => null,
        };
        if (bytes != null) return BitConverter.ToString(bytes).Replace("-", " ");
        if (value is bool flag) return flag ? "True" : "False";
        return value.ToString() ?? "";
    }

    public override object? Get(string property) => values.GetValueOrDefault(property);

    public override string? Display => Text;

    /// <summary>Inputs store their value as the setting "Text", "Number" or "Value" (names as in the original plugins).</summary>
    public override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (Which == Kind.TextInput && settings.TryGetValue("Text", out var text)) Text = text;
        if (Which == Kind.NumberInput && settings.TryGetValue("Number", out var number)) Text = number;
        if (Which == Kind.BooleanInput && settings.TryGetValue("Value", out var flag))
            Text = flag == "1" || flag.Equals("true", StringComparison.OrdinalIgnoreCase) ? "True" : "False";
    }

    string OutputName => Which switch
    {
        Kind.TextInput => "TextOutput",
        Kind.NumberInput => "NumberOutput",
        _ => "Output",
    };

    /// <summary>Inputs publish their value on their output connector.</summary>
    public override void Execute()
    {
        switch (Which)
        {
            case Kind.TextInput:
                values[OutputName] = Text;
                RaiseOutput(OutputName);
                break;
            case Kind.NumberInput:
                values[OutputName] = BigInteger.TryParse(Text.Trim(), out var number) ? number : BigInteger.Zero;
                RaiseOutput(OutputName);
                break;
            case Kind.BooleanInput:
                values[OutputName] = Text == "True";
                RaiseOutput(OutputName);
                break;
        }
        RaiseProgress(1);
    }
}
