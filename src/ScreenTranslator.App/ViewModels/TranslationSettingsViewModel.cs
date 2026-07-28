using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScreenTranslator.App.ViewModels;

public sealed class TranslationSettingsViewModel : ObservableObject
{
    private string _apiKey = string.Empty;
    private string _selectedModel = "deepseek-v4-flash";
    private string _baseUrl = "https://api.deepseek.com";
    private string _sourceLanguage = "自动检测";
    private string _targetLanguage = "简体中文";
    private string _translationStyle = "自然";
    private string _customContext = string.Empty;
    private string _connectionStatus = "尚未测试连接";
    private bool _isTesting;
    private bool _connectionSucceeded;

    public TranslationSettingsViewModel()
    {
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanTestConnection);
        SaveCommand = new RelayCommand(() => SaveRequested?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<string> Models { get; } =
        ["deepseek-v4-flash", "deepseek-v4-pro"];

    public ObservableCollection<string> SourceLanguages { get; } =
        ["自动检测", "英语", "简体中文", "日语", "韩语"];

    public ObservableCollection<string> TargetLanguages { get; } =
        ["简体中文", "繁体中文", "英语", "日语", "韩语"];

    public ObservableCollection<string> TranslationStyles { get; } =
        ["自然", "直译", "学习模式"];

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value))
            {
                TestConnectionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string SourceLanguage
    {
        get => _sourceLanguage;
        set => SetProperty(ref _sourceLanguage, value);
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    public string TranslationStyle
    {
        get => _translationStyle;
        set => SetProperty(ref _translationStyle, value);
    }

    public string CustomContext
    {
        get => _customContext;
        set => SetProperty(ref _customContext, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public bool IsTesting
    {
        get => _isTesting;
        private set
        {
            if (SetProperty(ref _isTesting, value))
            {
                TestConnectionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool ConnectionSucceeded
    {
        get => _connectionSucceeded;
        private set => SetProperty(ref _connectionSucceeded, value);
    }

    public IAsyncRelayCommand TestConnectionCommand { get; }

    public IRelayCommand SaveCommand { get; }

    public Func<DeepSeekConnectionTestRequest, CancellationToken, Task<ConnectionTestResult>>?
        ConnectionTester { get; set; }

    public event EventHandler? SaveRequested;

    public void SetConnectionStatus(string message)
    {
        ConnectionSucceeded = true;
        ConnectionStatus = message;
    }

    private bool CanTestConnection() =>
        !IsTesting && !string.IsNullOrWhiteSpace(ApiKey);

    private async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        IsTesting = true;
        ConnectionSucceeded = false;
        ConnectionStatus = "正在连接 DeepSeek…";

        try
        {
            if (ConnectionTester is null)
            {
                ConnectionStatus = "等待应用服务接入连接测试";
                return;
            }

            ConnectionTestResult result = await ConnectionTester(
                new DeepSeekConnectionTestRequest(ApiKey, SelectedModel, BaseUrl),
                cancellationToken);

            ConnectionSucceeded = result.Succeeded;
            ConnectionStatus = result.Elapsed is { } elapsed && result.Succeeded
                ? $"{result.Message} · {elapsed.TotalMilliseconds:F0} ms"
                : result.Message;
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus = "连接测试已取消";
        }
        catch (Exception exception)
        {
            ConnectionStatus = $"连接失败：{exception.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}

public sealed record DeepSeekConnectionTestRequest(
    string ApiKey,
    string Model,
    string BaseUrl);

public sealed record ConnectionTestResult(
    bool Succeeded,
    string Message,
    TimeSpan? Elapsed = null);
