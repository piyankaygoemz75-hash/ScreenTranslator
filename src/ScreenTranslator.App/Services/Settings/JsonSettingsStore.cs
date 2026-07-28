using System.IO;
using System.Text.Json;
using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Settings;

namespace ScreenTranslator.App.Services.Settings;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScreenTranslator",
            "settings.json"))
    {
    }

    public JsonSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            return await JsonSerializer.DeserializeAsync<AppSettings>(
                       stream,
                       SerializerOptions,
                       cancellationToken)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            return new AppSettings();
        }
        catch (NotSupportedException)
        {
            BackupCorruptSettings();
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("设置文件路径没有父目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = _settingsPath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _settingsPath, overwrite: true);
    }

    private void BackupCorruptSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_settingsPath)!;
        var backupName = $"settings.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}.json";
        File.Move(_settingsPath, Path.Combine(directory, backupName), overwrite: false);
    }
}
