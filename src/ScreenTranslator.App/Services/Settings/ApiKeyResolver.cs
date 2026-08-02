using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.App.Services.Settings;

public static class ApiKeyResolver
{
    public static Task<string?> ResolveAsync(
        string? candidate,
        ISecretStore secretStore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secretStore);

        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return Task.FromResult<string?>(candidate.Trim());
        }

        return secretStore.GetAsync(
            DeepSeekTranslationProvider.ApiKeyName,
            cancellationToken);
    }
}
