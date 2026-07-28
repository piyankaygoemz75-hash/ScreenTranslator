using ScreenTranslator.Core.Models;

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
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public string SourceLanguage { get; init; } = "auto";

    public string TargetLanguage { get; init; } = "zh-CN";

    public TranslationStyle TranslationStyle { get; init; } = TranslationStyle.Natural;

    public string TranslationContext { get; init; } = string.Empty;

    public string DeepSeekModel { get; init; } = "deepseek-v4-flash";

    public string DeepSeekBaseUrl { get; init; } = "https://api.deepseek.com";

    public DisplayMode DisplayMode { get; init; } = DisplayMode.SidePanel;

    public ThemePreference Theme { get; init; } = ThemePreference.System;

    public string Hotkey { get; init; } = "Alt+Shift+T";

    public double MinimumOverlayFontSize { get; init; } = 12;

    public double MaximumOverlayFontSize { get; init; } = 32;

    public double OverlayOpacity { get; init; } = 0.88;

    public bool SaveHistory { get; init; }

    public bool StartWithWindows { get; init; }
}
