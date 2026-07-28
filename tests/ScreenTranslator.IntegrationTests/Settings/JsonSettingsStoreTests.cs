using ScreenTranslator.App.Services.Settings;
using ScreenTranslator.Core.Settings;

namespace ScreenTranslator.IntegrationTests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "ScreenTranslator.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Save_Then_Load_RoundTrips_Settings()
    {
        var path = Path.Combine(_directory, "settings.json");
        var store = new JsonSettingsStore(path);
        var expected = new AppSettings
        {
            TargetLanguage = "ja-JP",
            DeepSeekModel = "deepseek-v4-pro",
            SaveHistory = true
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_Corrupt_File_Backs_It_Up_And_Returns_Defaults()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(path, "{not-json");
        var store = new JsonSettingsStore(path);

        var actual = await store.LoadAsync();

        Assert.Equal(new AppSettings(), actual);
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
