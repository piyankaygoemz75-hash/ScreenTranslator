using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Models;

namespace ScreenTranslator.Core.Translation;

public sealed class DeepSeekTranslationProvider : ITranslationProvider
{
    public const string ApiKeyName = "deepseek-api-key";

    public const string SystemPrompt =
        """
        You are a screen translation engine. Return JSON only.
        Translate every input block into the requested target language and preserve every block ID exactly.
        Do not add, remove, merge, split, or duplicate blocks.
        Follow the requested style and use context only to disambiguate terminology.
        The complete JSON output shape is:
        {"blocks":[{"id":"block-1","translation":"translated text"}]}
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;
    private readonly DeepSeekOptions _options;

    public DeepSeekTranslationProvider(
        HttpClient httpClient,
        ISecretStore secretStore,
        DeepSeekOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        ValidateOptions(options);
    }

    public async Task<string> TranslateRawAsync(
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var apiKey = await _secretStore.GetAsync(ApiKeyName, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TranslationAuthenticationException("尚未配置 DeepSeek API Key。");
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_options.Timeout);

        using var message = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint(_options.BaseUri));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = JsonContent.Create(CreatePayload(request), options: SerializerOptions);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationUnavailableException("DeepSeek 请求超时，请检查网络后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new TranslationUnavailableException(
                "无法连接 DeepSeek 服务，请检查网络和 Base URL。",
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                ThrowMappedError(response.StatusCode);
            }

            string responseBody;
            try
            {
                responseBody = await response.Content
                    .ReadAsStringAsync(timeoutSource.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TranslationUnavailableException("读取 DeepSeek 响应超时，请重试。");
            }
            catch (HttpRequestException exception)
            {
                throw new TranslationUnavailableException(
                    "读取 DeepSeek 响应失败，请检查网络后重试。",
                    innerException: exception);
            }

            return ExtractMessageContent(responseBody);
        }
    }

    private object CreatePayload(TranslationRequest request)
    {
        var userRequest = new
        {
            sourceLanguage = request.SourceLanguage,
            targetLanguage = request.TargetLanguage,
            style = request.Style.ToString().ToLowerInvariant(),
            context = request.Context,
            blocks = request.Blocks.Select(block => new
            {
                id = block.Id,
                text = block.Text,
            }),
        };

        return new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(userRequest, SerializerOptions),
                },
            },
            thinking = new { type = "disabled" },
            response_format = new { type = "json_object" },
            stream = false,
        };
    }

    private static Uri BuildEndpoint(Uri baseUri) =>
        new($"{baseUri.AbsoluteUri.TrimEnd('/')}/chat/completions", UriKind.Absolute);

    private static string ExtractMessageContent(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var choice = document.RootElement
                .GetProperty("choices")
                .EnumerateArray()
                .FirstOrDefault();

            if (choice.ValueKind == JsonValueKind.Undefined)
            {
                throw new TranslationFormatException("DeepSeek 响应缺少 choices。");
            }

            if (choice.TryGetProperty("finish_reason", out var finishReason)
                && finishReason.ValueKind == JsonValueKind.String
                && string.Equals(finishReason.GetString(), "length", StringComparison.OrdinalIgnoreCase))
            {
                throw new TranslationFormatException("DeepSeek 响应因长度限制被截断。");
            }

            var content = choice.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new TranslationFormatException("DeepSeek 返回了空内容。");
            }

            return content;
        }
        catch (TranslationFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new TranslationFormatException("DeepSeek 返回了无法解析的响应。", exception);
        }
    }

    private static void ThrowMappedError(HttpStatusCode statusCode)
    {
        switch (statusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new TranslationAuthenticationException(
                    "DeepSeek 拒绝了请求，请检查 API Key 或服务权限。",
                    statusCode);
            case HttpStatusCode.NotFound:
                throw new TranslationConfigurationException(
                    "DeepSeek 接口或模型不存在，请检查 Base URL 和模型名。",
                    statusCode);
            case HttpStatusCode.TooManyRequests:
                throw new TranslationRateLimitException(
                    "DeepSeek 请求受限，请检查余额或稍后重试。",
                    statusCode);
            case HttpStatusCode.RequestTimeout:
                throw new TranslationUnavailableException(
                    "DeepSeek 请求超时，请检查网络后重试。",
                    statusCode);
            default:
                if ((int)statusCode >= 500)
                {
                    throw new TranslationUnavailableException(
                        "DeepSeek 服务暂时不可用，请稍后重试。",
                        statusCode);
                }

                throw new TranslationConfigurationException(
                    $"DeepSeek 请求失败（HTTP {(int)statusCode}）。",
                    statusCode);
        }
    }

    private static void ValidateOptions(DeepSeekOptions options)
    {
        if (!options.BaseUri.IsAbsoluteUri
            || (options.BaseUri.Scheme != Uri.UriSchemeHttps
                && options.BaseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("DeepSeek Base URI must be an absolute HTTP(S) URI.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new ArgumentException("DeepSeek model is required.", nameof(options));
        }

        if (options.Timeout <= TimeSpan.Zero
            || options.Timeout == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "DeepSeek timeout must be positive.");
        }
    }
}
