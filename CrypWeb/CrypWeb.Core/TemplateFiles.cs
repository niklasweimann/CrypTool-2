namespace CrypTool.Web.Core;

/// <summary>
/// Files that templates refer to. CT2 templates name their data files relative to the CT2 program folder with
/// Windows separators ("Templates\Codes\QR_Code.png", the picture a FileInput opens). In the browser those files are
/// in the virtual file system under <see cref="Root"/> (see tools-gen-vfs.py).
/// </summary>
public static class TemplateFiles
{
    /// <summary>Folder of the templates' data files ("/Templates" in the browser; the template folder in tests).</summary>
    public static string Root { get; set; } = "/Templates";

    /// <summary>A setting value that names a template file, as a path in this file system; other values unchanged.</summary>
    public static string Resolve(string value)
    {
        if (value == null) return null!;
        foreach (string prefix in new[] { "Templates\\", "Templates/" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Root.TrimEnd('/') + "/" + value.Substring(prefix.Length).Replace('\\', '/');
            }
        }
        return value;
    }
}
