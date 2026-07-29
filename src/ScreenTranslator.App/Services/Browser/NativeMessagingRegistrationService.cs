using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using ScreenTranslator.Core.Browser;

namespace ScreenTranslator.App.Services.Browser;

public interface INativeMessagingRegistry
{
    void SetHostManifest(BrowserKind browser, string hostName, string manifestPath);
}

public sealed class WindowsNativeMessagingRegistry : INativeMessagingRegistry
{
    public void SetHostManifest(
        BrowserKind browser,
        string hostName,
        string manifestPath)
    {
        var vendor = browser == BrowserKind.Chrome
            ? @"Software\Google\Chrome"
            : @"Software\Microsoft\Edge";
        var keyPath = $@"{vendor}\NativeMessagingHosts\{hostName}";
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true)
                        ?? throw new InvalidOperationException(
                            $"无法创建浏览器注册表项：{keyPath}");
        key.SetValue(null, manifestPath, RegistryValueKind.String);
    }
}

public sealed class NativeMessagingRegistrationService
{
    public const string HostName = "com.screentranslator.browser_bridge";
    public const string ExtensionId = "plpgmkbadcfnkmolbeecggbbopilajed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly INativeMessagingRegistry _registry;
    private readonly string _manifestDirectory;
    private readonly string _executablePath;

    public NativeMessagingRegistrationService()
        : this(
            new WindowsNativeMessagingRegistry(),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScreenTranslator",
                "BrowserHost"),
            Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定屏译程序路径。"))
    {
    }

    public NativeMessagingRegistrationService(
        INativeMessagingRegistry registry,
        string manifestDirectory,
        string executablePath)
    {
        _registry = registry;
        _manifestDirectory = manifestDirectory;
        _executablePath = Path.GetFullPath(executablePath);
    }

    public async Task<string> RegisterAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_manifestDirectory);
        var manifestPath = Path.Combine(
            _manifestDirectory,
            "native-host.json");
        var temporaryPath = manifestPath + ".tmp";
        var manifest = new NativeHostManifest(
            HostName,
            "ScreenTranslator browser scroll bridge",
            _executablePath,
            "stdio",
            [$"chrome-extension://{ExtensionId}/"]);

        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(manifest, JsonOptions),
            cancellationToken);
        File.Move(temporaryPath, manifestPath, overwrite: true);

        _registry.SetHostManifest(BrowserKind.Chrome, HostName, manifestPath);
        _registry.SetHostManifest(BrowserKind.Edge, HostName, manifestPath);
        return manifestPath;
    }

    private sealed record NativeHostManifest(
        string Name,
        string Description,
        string Path,
        string Type,
        IReadOnlyList<string> AllowedOrigins);
}
