namespace ScreenTranslator.Core.Translation;

public sealed record DeepSeekOptions
{
    public Uri BaseUri { get; init; } = new("https://api.deepseek.com/");

    public string Model { get; init; } = "deepseek-v4-flash";

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
