using System.Windows.Controls;

namespace CrypTool.PluginBase.Miscellaneous
{
    /// <summary>
    /// A TextBlock whose text the user can select and copy. The original reaches into WPF internals for that; in the
    /// browser, text is selectable anyway, so the plain TextBlock is enough.
    /// </summary>
    public class SelectableTextBlock : TextBlock { }
}
