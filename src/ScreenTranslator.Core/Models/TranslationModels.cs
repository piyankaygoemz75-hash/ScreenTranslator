namespace ScreenTranslator.Core.Models;

public enum TranslationStyle
{
    Natural,
    Literal,
    Learning,
}

public sealed record TranslationRequest(
    string SourceLanguage,
    string TargetLanguage,
    TranslationStyle Style,
    string Context,
    IReadOnlyList<OcrBlock> Blocks);

public sealed record TranslatedBlock(
    string Id,
    string SourceText,
    string Translation,
    PixelRect Bounds);

public sealed record TranslationResult(IReadOnlyList<TranslatedBlock> Blocks);
