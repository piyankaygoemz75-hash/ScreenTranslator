using System.Text.Json.Serialization;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Hotkeys;

namespace ScreenTranslator.Core.Settings;

public enum DisplayMode
{
    SidePanel,
    Overlay,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public sealed record AppSettings
{
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;

    public string SourceLanguage { get; init; } = "auto";

    public string TargetLanguage { get; init; } = "zh-CN";

    public TranslationStyle TranslationStyle { get; init; } = TranslationStyle.Natural;

    public string TranslationContext { get; init; } = string.Empty;

    public string DeepSeekModel { get; init; } = "deepseek-v4-flash";

    public string DeepSeekBaseUrl { get; init; } = "https://api.deepseek.com";

    public DisplayMode DisplayMode { get; init; } = DisplayMode.SidePanel;

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public string Hotkey { get; init; } = HotkeyGesture.Default.ToPersistedString();

    public bool HotkeyEnabled { get; init; } = true;

    public bool BrowserFollowingEnabled { get; init; } = true;

    public WindowPlacement? SidePanelPlacement { get; init; }

    public double MinimumOverlayFontSize { get; init; } = 12;

    public double MaximumOverlayFontSize { get; init; } = 32;

    public double OverlayOpacity { get; init; } = 0.88;

    public bool SaveHistory { get; init; }

    public bool StartWithWindows { get; init; }

    public AppSettings Migrate(out bool hotkeyWasReset)
    {
        hotkeyWasReset = !HotkeyGesture.TryParse(Hotkey, out var gesture);
        var placement = SidePanelPlacement is { } value && value.IsValid
            ? value
            : null;

        return this with
        {
            Version = CurrentVersion,
            Hotkey = gesture.ToPersistedString(),
            SidePanelPlacement = placement,
        };
    }
}

public sealed record WindowPlacement(double Left, double Top, double Width, double Height)
{
    [JsonIgnore]
    public bool IsValid =>
        double.IsFinite(Left) &&
        double.IsFinite(Top) &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;
}
