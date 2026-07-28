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
        Assert.DoesNotContain(
            settings.GetType().GetProperties(),
            property => property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
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
