using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScreenTranslator.App.ViewModels;

public sealed class TranslationResultViewModel : ObservableObject
{
    private string _sourceText = string.Empty;
    private string _translatedText = string.Empty;
    private string _sourceLanguageLabel = "自动检测";
    private string _targetLanguageLabel = "简体中文";
    private bool _isPinned;
    private bool _isSourceVisible;
    private bool _isBusy;
    private string? _errorMessage;

    public TranslationResultViewModel()
    {
        CopyCommand = new RelayCommand(
            () => CopyRequested?.Invoke(this, TranslatedText),
            () => !string.IsNullOrWhiteSpace(TranslatedText));
        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        ToggleSourceCommand = new RelayCommand(() => IsSourceVisible = !IsSourceVisible);
        RetryCommand = new RelayCommand(
            () => RetryRequested?.Invoke(this, EventArgs.Empty),
            () => !IsBusy);
        SwitchModeCommand = new RelayCommand(
            () => SwitchModeRequested?.Invoke(this, EventArgs.Empty));
        CloseCommand = new RelayCommand(
            () => CloseRequested?.Invoke(this, EventArgs.Empty));
        ClearAllCommand = new RelayCommand(
            () => ClearAllRequested?.Invoke(this, EventArgs.Empty));
    }

    public string SourceText
    {
        get => _sourceText;
        set => SetProperty(ref _sourceText, value);
    }

    public string TranslatedText
    {
        get => _translatedText;
        set
        {
            if (SetProperty(ref _translatedText, value))
            {
                CopyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SourceLanguageLabel
    {
        get => _sourceLanguageLabel;
        set => SetProperty(ref _sourceLanguageLabel, value);
    }

    public string TargetLanguageLabel
    {
        get => _targetLanguageLabel;
        set => SetProperty(ref _targetLanguageLabel, value);
    }

    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public bool IsSourceVisible
    {
        get => _isSourceVisible;
        set => SetProperty(ref _isSourceVisible, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RetryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public RelayCommand CopyCommand { get; }

    public RelayCommand TogglePinCommand { get; }

    public RelayCommand ToggleSourceCommand { get; }

    public RelayCommand RetryCommand { get; }

    public RelayCommand SwitchModeCommand { get; }

    public RelayCommand CloseCommand { get; }

    public RelayCommand ClearAllCommand { get; }

    public event EventHandler<string>? CopyRequested;

    public event EventHandler? RetryRequested;

    public event EventHandler? SwitchModeRequested;

    public event EventHandler? CloseRequested;

    public event EventHandler? ClearAllRequested;
}
