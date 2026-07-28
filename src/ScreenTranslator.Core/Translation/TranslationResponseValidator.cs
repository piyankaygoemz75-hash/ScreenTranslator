using System.Text.Json;
using ScreenTranslator.Core.Models;

namespace ScreenTranslator.Core.Translation;

public sealed class TranslationResponseValidator
{
    public IReadOnlyDictionary<string, string> Parse(
        string json,
        IReadOnlyCollection<string> expectedIds)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new TranslationFormatException("DeepSeek 返回了空 JSON。");
        }

        ArgumentNullException.ThrowIfNull(expectedIds);

        HashSet<string> expected;
        try
        {
            expected = expectedIds.ToHashSet(StringComparer.Ordinal);
        }
        catch (ArgumentNullException exception)
        {
            throw new ArgumentException("Expected block IDs cannot contain null.", nameof(expectedIds), exception);
        }

        if (expected.Count != expectedIds.Count || expected.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Expected block IDs must be non-empty and unique.", nameof(expectedIds));
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("blocks", out var blocks)
                || blocks.ValueKind != JsonValueKind.Array)
            {
                throw new TranslationFormatException("DeepSeek 返回的 JSON 缺少 blocks 数组。");
            }

            var translations = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var block in blocks.EnumerateArray())
            {
                if (block.ValueKind != JsonValueKind.Object
                    || !block.TryGetProperty("id", out var idElement)
                    || idElement.ValueKind != JsonValueKind.String
                    || !block.TryGetProperty("translation", out var translationElement)
                    || translationElement.ValueKind != JsonValueKind.String)
                {
                    throw new TranslationFormatException("DeepSeek 返回了无效的文本块结构。");
                }

                var id = idElement.GetString();
                var translation = translationElement.GetString();
                if (string.IsNullOrWhiteSpace(id) || translation is null)
                {
                    throw new TranslationFormatException("DeepSeek 返回的文本块字段为空。");
                }

                if (!translations.TryAdd(id, translation))
                {
                    throw new TranslationFormatException($"DeepSeek 返回了重复 ID：{id}");
                }
            }

            if (!translations.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            {
                throw new TranslationFormatException("DeepSeek 返回的文本块与 OCR 文本块不一致。");
            }

            return translations;
        }
        catch (TranslationFormatException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new TranslationFormatException("DeepSeek 返回的内容不是有效 JSON。", exception);
        }
    }
}
