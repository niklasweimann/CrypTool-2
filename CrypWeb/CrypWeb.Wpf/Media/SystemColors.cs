// Windows system colours (SystemColors) with the default values of Windows 10, so that XAML referring to them
// ({x:Static SystemColors.HighlightBrushKey}, {DynamicResource {x:Static SystemColors.ControlBrushKey}}) gets the
// look it has on a standard desktop. A resource dictionary may redefine a key, as in WPF.
// The members follow one pattern per colour: XColor, XBrush, XColorKey, XBrushKey.
namespace System.Windows
{
    using System.Windows.Media;

    /// <summary>The key of a system resource; it knows its default value.</summary>
    public sealed class SystemResourceKey
    {
        private readonly string _name;

        internal SystemResourceKey(string name, object value)
        {
            _name = name;
            Value = value;
        }

        internal object Value { get; }

        public override string ToString() => _name;
    }

    public static class SystemColors
    {
        private static SolidColorBrush Frozen(Color color) => new SolidColorBrush(color);


        public static Color ActiveBorderColor => Color.FromRgb(0xB4, 0xB4, 0xB4);
        public static SolidColorBrush ActiveBorderBrush { get; } = Frozen(ActiveBorderColor);
        public static SystemResourceKey ActiveBorderColorKey { get; } = new SystemResourceKey("ActiveBorderColor", ActiveBorderColor);
        public static SystemResourceKey ActiveBorderBrushKey { get; } = new SystemResourceKey("ActiveBorderBrush", ActiveBorderBrush);

        public static Color ActiveCaptionColor => Color.FromRgb(0x99, 0xB4, 0xD1);
        public static SolidColorBrush ActiveCaptionBrush { get; } = Frozen(ActiveCaptionColor);
        public static SystemResourceKey ActiveCaptionColorKey { get; } = new SystemResourceKey("ActiveCaptionColor", ActiveCaptionColor);
        public static SystemResourceKey ActiveCaptionBrushKey { get; } = new SystemResourceKey("ActiveCaptionBrush", ActiveCaptionBrush);

        public static Color ActiveCaptionTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush ActiveCaptionTextBrush { get; } = Frozen(ActiveCaptionTextColor);
        public static SystemResourceKey ActiveCaptionTextColorKey { get; } = new SystemResourceKey("ActiveCaptionTextColor", ActiveCaptionTextColor);
        public static SystemResourceKey ActiveCaptionTextBrushKey { get; } = new SystemResourceKey("ActiveCaptionTextBrush", ActiveCaptionTextBrush);

        public static Color AppWorkspaceColor => Color.FromRgb(0xAB, 0xAB, 0xAB);
        public static SolidColorBrush AppWorkspaceBrush { get; } = Frozen(AppWorkspaceColor);
        public static SystemResourceKey AppWorkspaceColorKey { get; } = new SystemResourceKey("AppWorkspaceColor", AppWorkspaceColor);
        public static SystemResourceKey AppWorkspaceBrushKey { get; } = new SystemResourceKey("AppWorkspaceBrush", AppWorkspaceBrush);

        public static Color ControlColor => Color.FromRgb(0xF0, 0xF0, 0xF0);
        public static SolidColorBrush ControlBrush { get; } = Frozen(ControlColor);
        public static SystemResourceKey ControlColorKey { get; } = new SystemResourceKey("ControlColor", ControlColor);
        public static SystemResourceKey ControlBrushKey { get; } = new SystemResourceKey("ControlBrush", ControlBrush);

        public static Color ControlDarkColor => Color.FromRgb(0xA0, 0xA0, 0xA0);
        public static SolidColorBrush ControlDarkBrush { get; } = Frozen(ControlDarkColor);
        public static SystemResourceKey ControlDarkColorKey { get; } = new SystemResourceKey("ControlDarkColor", ControlDarkColor);
        public static SystemResourceKey ControlDarkBrushKey { get; } = new SystemResourceKey("ControlDarkBrush", ControlDarkBrush);

        public static Color ControlDarkDarkColor => Color.FromRgb(0x69, 0x69, 0x69);
        public static SolidColorBrush ControlDarkDarkBrush { get; } = Frozen(ControlDarkDarkColor);
        public static SystemResourceKey ControlDarkDarkColorKey { get; } = new SystemResourceKey("ControlDarkDarkColor", ControlDarkDarkColor);
        public static SystemResourceKey ControlDarkDarkBrushKey { get; } = new SystemResourceKey("ControlDarkDarkBrush", ControlDarkDarkBrush);

        public static Color ControlLightColor => Color.FromRgb(0xE3, 0xE3, 0xE3);
        public static SolidColorBrush ControlLightBrush { get; } = Frozen(ControlLightColor);
        public static SystemResourceKey ControlLightColorKey { get; } = new SystemResourceKey("ControlLightColor", ControlLightColor);
        public static SystemResourceKey ControlLightBrushKey { get; } = new SystemResourceKey("ControlLightBrush", ControlLightBrush);

        public static Color ControlLightLightColor => Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static SolidColorBrush ControlLightLightBrush { get; } = Frozen(ControlLightLightColor);
        public static SystemResourceKey ControlLightLightColorKey { get; } = new SystemResourceKey("ControlLightLightColor", ControlLightLightColor);
        public static SystemResourceKey ControlLightLightBrushKey { get; } = new SystemResourceKey("ControlLightLightBrush", ControlLightLightBrush);

        public static Color ControlTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush ControlTextBrush { get; } = Frozen(ControlTextColor);
        public static SystemResourceKey ControlTextColorKey { get; } = new SystemResourceKey("ControlTextColor", ControlTextColor);
        public static SystemResourceKey ControlTextBrushKey { get; } = new SystemResourceKey("ControlTextBrush", ControlTextBrush);

        public static Color DesktopColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush DesktopBrush { get; } = Frozen(DesktopColor);
        public static SystemResourceKey DesktopColorKey { get; } = new SystemResourceKey("DesktopColor", DesktopColor);
        public static SystemResourceKey DesktopBrushKey { get; } = new SystemResourceKey("DesktopBrush", DesktopBrush);

        public static Color GradientActiveCaptionColor => Color.FromRgb(0xB9, 0xD1, 0xEA);
        public static SolidColorBrush GradientActiveCaptionBrush { get; } = Frozen(GradientActiveCaptionColor);
        public static SystemResourceKey GradientActiveCaptionColorKey { get; } = new SystemResourceKey("GradientActiveCaptionColor", GradientActiveCaptionColor);
        public static SystemResourceKey GradientActiveCaptionBrushKey { get; } = new SystemResourceKey("GradientActiveCaptionBrush", GradientActiveCaptionBrush);

        public static Color GradientInactiveCaptionColor => Color.FromRgb(0xD7, 0xE4, 0xF2);
        public static SolidColorBrush GradientInactiveCaptionBrush { get; } = Frozen(GradientInactiveCaptionColor);
        public static SystemResourceKey GradientInactiveCaptionColorKey { get; } = new SystemResourceKey("GradientInactiveCaptionColor", GradientInactiveCaptionColor);
        public static SystemResourceKey GradientInactiveCaptionBrushKey { get; } = new SystemResourceKey("GradientInactiveCaptionBrush", GradientInactiveCaptionBrush);

        public static Color GrayTextColor => Color.FromRgb(0x6D, 0x6D, 0x6D);
        public static SolidColorBrush GrayTextBrush { get; } = Frozen(GrayTextColor);
        public static SystemResourceKey GrayTextColorKey { get; } = new SystemResourceKey("GrayTextColor", GrayTextColor);
        public static SystemResourceKey GrayTextBrushKey { get; } = new SystemResourceKey("GrayTextBrush", GrayTextBrush);

        public static Color HighlightColor => Color.FromRgb(0x00, 0x78, 0xD7);
        public static SolidColorBrush HighlightBrush { get; } = Frozen(HighlightColor);
        public static SystemResourceKey HighlightColorKey { get; } = new SystemResourceKey("HighlightColor", HighlightColor);
        public static SystemResourceKey HighlightBrushKey { get; } = new SystemResourceKey("HighlightBrush", HighlightBrush);

        public static Color HighlightTextColor => Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static SolidColorBrush HighlightTextBrush { get; } = Frozen(HighlightTextColor);
        public static SystemResourceKey HighlightTextColorKey { get; } = new SystemResourceKey("HighlightTextColor", HighlightTextColor);
        public static SystemResourceKey HighlightTextBrushKey { get; } = new SystemResourceKey("HighlightTextBrush", HighlightTextBrush);

        public static Color HotTrackColor => Color.FromRgb(0x00, 0x66, 0xCC);
        public static SolidColorBrush HotTrackBrush { get; } = Frozen(HotTrackColor);
        public static SystemResourceKey HotTrackColorKey { get; } = new SystemResourceKey("HotTrackColor", HotTrackColor);
        public static SystemResourceKey HotTrackBrushKey { get; } = new SystemResourceKey("HotTrackBrush", HotTrackBrush);

        public static Color InactiveBorderColor => Color.FromRgb(0xF4, 0xF7, 0xFC);
        public static SolidColorBrush InactiveBorderBrush { get; } = Frozen(InactiveBorderColor);
        public static SystemResourceKey InactiveBorderColorKey { get; } = new SystemResourceKey("InactiveBorderColor", InactiveBorderColor);
        public static SystemResourceKey InactiveBorderBrushKey { get; } = new SystemResourceKey("InactiveBorderBrush", InactiveBorderBrush);

        public static Color InactiveCaptionColor => Color.FromRgb(0xBF, 0xCD, 0xDB);
        public static SolidColorBrush InactiveCaptionBrush { get; } = Frozen(InactiveCaptionColor);
        public static SystemResourceKey InactiveCaptionColorKey { get; } = new SystemResourceKey("InactiveCaptionColor", InactiveCaptionColor);
        public static SystemResourceKey InactiveCaptionBrushKey { get; } = new SystemResourceKey("InactiveCaptionBrush", InactiveCaptionBrush);

        public static Color InactiveCaptionTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush InactiveCaptionTextBrush { get; } = Frozen(InactiveCaptionTextColor);
        public static SystemResourceKey InactiveCaptionTextColorKey { get; } = new SystemResourceKey("InactiveCaptionTextColor", InactiveCaptionTextColor);
        public static SystemResourceKey InactiveCaptionTextBrushKey { get; } = new SystemResourceKey("InactiveCaptionTextBrush", InactiveCaptionTextBrush);

        public static Color InactiveSelectionHighlightColor => Color.FromRgb(0xF0, 0xF0, 0xF0);
        public static SolidColorBrush InactiveSelectionHighlightBrush { get; } = Frozen(InactiveSelectionHighlightColor);
        public static SystemResourceKey InactiveSelectionHighlightColorKey { get; } = new SystemResourceKey("InactiveSelectionHighlightColor", InactiveSelectionHighlightColor);
        public static SystemResourceKey InactiveSelectionHighlightBrushKey { get; } = new SystemResourceKey("InactiveSelectionHighlightBrush", InactiveSelectionHighlightBrush);

        public static Color InactiveSelectionHighlightTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush InactiveSelectionHighlightTextBrush { get; } = Frozen(InactiveSelectionHighlightTextColor);
        public static SystemResourceKey InactiveSelectionHighlightTextColorKey { get; } = new SystemResourceKey("InactiveSelectionHighlightTextColor", InactiveSelectionHighlightTextColor);
        public static SystemResourceKey InactiveSelectionHighlightTextBrushKey { get; } = new SystemResourceKey("InactiveSelectionHighlightTextBrush", InactiveSelectionHighlightTextBrush);

        public static Color InfoColor => Color.FromRgb(0xFF, 0xFF, 0xE1);
        public static SolidColorBrush InfoBrush { get; } = Frozen(InfoColor);
        public static SystemResourceKey InfoColorKey { get; } = new SystemResourceKey("InfoColor", InfoColor);
        public static SystemResourceKey InfoBrushKey { get; } = new SystemResourceKey("InfoBrush", InfoBrush);

        public static Color InfoTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush InfoTextBrush { get; } = Frozen(InfoTextColor);
        public static SystemResourceKey InfoTextColorKey { get; } = new SystemResourceKey("InfoTextColor", InfoTextColor);
        public static SystemResourceKey InfoTextBrushKey { get; } = new SystemResourceKey("InfoTextBrush", InfoTextBrush);

        public static Color MenuColor => Color.FromRgb(0xF0, 0xF0, 0xF0);
        public static SolidColorBrush MenuBrush { get; } = Frozen(MenuColor);
        public static SystemResourceKey MenuColorKey { get; } = new SystemResourceKey("MenuColor", MenuColor);
        public static SystemResourceKey MenuBrushKey { get; } = new SystemResourceKey("MenuBrush", MenuBrush);

        public static Color MenuBarColor => Color.FromRgb(0xF0, 0xF0, 0xF0);
        public static SolidColorBrush MenuBarBrush { get; } = Frozen(MenuBarColor);
        public static SystemResourceKey MenuBarColorKey { get; } = new SystemResourceKey("MenuBarColor", MenuBarColor);
        public static SystemResourceKey MenuBarBrushKey { get; } = new SystemResourceKey("MenuBarBrush", MenuBarBrush);

        public static Color MenuHighlightColor => Color.FromRgb(0x00, 0x78, 0xD7);
        public static SolidColorBrush MenuHighlightBrush { get; } = Frozen(MenuHighlightColor);
        public static SystemResourceKey MenuHighlightColorKey { get; } = new SystemResourceKey("MenuHighlightColor", MenuHighlightColor);
        public static SystemResourceKey MenuHighlightBrushKey { get; } = new SystemResourceKey("MenuHighlightBrush", MenuHighlightBrush);

        public static Color MenuTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush MenuTextBrush { get; } = Frozen(MenuTextColor);
        public static SystemResourceKey MenuTextColorKey { get; } = new SystemResourceKey("MenuTextColor", MenuTextColor);
        public static SystemResourceKey MenuTextBrushKey { get; } = new SystemResourceKey("MenuTextBrush", MenuTextBrush);

        public static Color ScrollBarColor => Color.FromRgb(0xC8, 0xC8, 0xC8);
        public static SolidColorBrush ScrollBarBrush { get; } = Frozen(ScrollBarColor);
        public static SystemResourceKey ScrollBarColorKey { get; } = new SystemResourceKey("ScrollBarColor", ScrollBarColor);
        public static SystemResourceKey ScrollBarBrushKey { get; } = new SystemResourceKey("ScrollBarBrush", ScrollBarBrush);

        public static Color WindowColor => Color.FromRgb(0xFF, 0xFF, 0xFF);
        public static SolidColorBrush WindowBrush { get; } = Frozen(WindowColor);
        public static SystemResourceKey WindowColorKey { get; } = new SystemResourceKey("WindowColor", WindowColor);
        public static SystemResourceKey WindowBrushKey { get; } = new SystemResourceKey("WindowBrush", WindowBrush);

        public static Color WindowFrameColor => Color.FromRgb(0x64, 0x64, 0x64);
        public static SolidColorBrush WindowFrameBrush { get; } = Frozen(WindowFrameColor);
        public static SystemResourceKey WindowFrameColorKey { get; } = new SystemResourceKey("WindowFrameColor", WindowFrameColor);
        public static SystemResourceKey WindowFrameBrushKey { get; } = new SystemResourceKey("WindowFrameBrush", WindowFrameBrush);

        public static Color WindowTextColor => Color.FromRgb(0x00, 0x00, 0x00);
        public static SolidColorBrush WindowTextBrush { get; } = Frozen(WindowTextColor);
        public static SystemResourceKey WindowTextColorKey { get; } = new SystemResourceKey("WindowTextColor", WindowTextColor);
        public static SystemResourceKey WindowTextBrushKey { get; } = new SystemResourceKey("WindowTextBrush", WindowTextBrush);

    }
}
