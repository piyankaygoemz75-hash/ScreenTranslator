using System.Text.Json;
using ScreenTranslator.Core.Settings;

namespace ScreenTranslator.Core.Tests.Settings;

public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_Match_Product_Decisions()
    {
        var settings = new AppSettings();

        Assert.Equal("zh-CN", settings.TargetLanguage);
        Assert.Equal("deepseek-v4-flash", settings.DeepSeekModel);
        Assert.Equal(DisplayMode.SidePanel, settings.DisplayMode);
        Assert.False(settings.SaveHistory);
        Assert.False(settings.StartWithWindows);
        Assert.Equal("Alt+Shift+T", settings.Hotkey);
        Assert.True(settings.HotkeyEnabled);
        Assert.True(settings.BrowserFollowingEnabled);
        Assert.True(settings.MinimizeToTray);
        Assert.Null(settings.SidePanelPlacement);
        Assert.DoesNotContain(
            settings.GetType().GetProperties(),
            property => property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Version_One_Settings_Use_Backward_Compatible_Defaults()
    {
        const string json = """
            {
              "Version": 1,
              "TargetLanguage": "ja",
              "Hotkey": "invalid"
            }
            """;

        var restored = JsonSerializer.Deserialize<AppSettings>(json)!;
        var migrated = restored.Migrate(out var hotkeyWasReset);

        Assert.Equal(AppSettings.CurrentVersion, migrated.Version);
        Assert.Equal("ja", migrated.TargetLanguage);
        Assert.Equal("Alt+Shift+T", migrated.Hotkey);
        Assert.True(migrated.HotkeyEnabled);
        Assert.True(migrated.BrowserFollowingEnabled);
        Assert.True(migrated.MinimizeToTray);
        Assert.Null(migrated.SidePanelPlacement);
        Assert.True(hotkeyWasReset);
    }

    [Fact]
    public void Settings_RoundTrip_With_SystemTextJson()
    {
        var settings = new AppSettings
        {
            TargetLanguage = "ja-JP",
            DeepSeekModel = "deepseek-v4-pro",
            DisplayMode = DisplayMode.Overlay,
            SaveHistory = true,
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.Equal(settings, restored);
    }
}
