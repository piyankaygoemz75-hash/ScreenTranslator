using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Models;

namespace ScreenTranslator.Core.Translation;

public sealed class TranslationOrchestrator
{
    private const string RepairInstruction =
        """

        FORMAT_REPAIR: The previous response did not match the required JSON schema.
        Return one valid JSON object only, with exactly one translation for every supplied block ID.
        Previous invalid response:
        """;

    private readonly ITranslationProvider _provider;
    private readonly TranslationResponseValidator _validator;

    public TranslationOrchestrator(
        ITranslationProvider provider,
        TranslationResponseValidator validator)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Blocks.Count == 0)
        {
            return new TranslationResult(Array.Empty<TranslatedBlock>());
        }

        var expectedIds = ValidateRequest(request);
        var rawResponse = await _provider
            .TranslateRawAsync(request, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<string, string> translations;
        try
        {
            translations = _validator.Parse(rawResponse, expectedIds);
        }
        catch (TranslationFormatException firstException)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var repairRequest = request with
            {
                Context = string.Concat(
                    request.Context,
                    RepairInstruction,
                    rawResponse),
            };

            var repairedResponse = await _provider
                .TranslateRawAsync(repairRequest, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                translations = _validator.Parse(repairedResponse, expectedIds);
            }
            catch (TranslationFormatException secondException)
            {
                throw new TranslationFormatException(
                    "DeepSeek 两次返回的文本块格式都无法验证。",
                    new AggregateException(firstException, secondException));
            }
        }

        var blocks = request.Blocks
            .Select(block => new TranslatedBlock(
                block.Id,
                block.Text,
                translations[block.Id],
                block.BoundsInCapturePixels))
            .ToArray();

        return new TranslationResult(blocks);
    }

    private static string[] ValidateRequest(TranslationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TargetLanguage))
        {
            throw new ArgumentException("Target language is required.", nameof(request));
        }

        var ids = request.Blocks.Select(block => block.Id).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new ArgumentException("Translation block IDs must be non-empty and unique.", nameof(request));
        }

        return ids;
    }
}
