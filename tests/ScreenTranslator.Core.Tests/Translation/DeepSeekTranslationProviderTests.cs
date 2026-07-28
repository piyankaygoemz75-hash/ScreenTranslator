using System.Net;
using System.Text;
using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.Core.Tests.Translation;

public sealed class DeepSeekTranslationProviderTests
{
    [Fact]
    public async Task Provider_Sends_V4_Flash_NonThinking_Json_Request()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.OK,
            """{"choices":[{"message":{"content":"{\"blocks\":[]}"}}]}""");
        var provider = CreateProvider(handler);

        await provider.TranslateRawAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal("/chat/completions", handler.Path);
        Assert.Equal("Bearer test-key", handler.Authorization);
        Assert.Contains("\"model\":\"deepseek-v4-flash\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"thinking\":{\"type\":\"disabled\"}", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"stream\":false", handler.Body, StringComparison.Ordinal);
        Assert.Contains("JSON", handler.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, typeof(TranslationAuthenticationException))]
    [InlineData(HttpStatusCode.Forbidden, typeof(TranslationAuthenticationException))]
    [InlineData(HttpStatusCode.NotFound, typeof(TranslationConfigurationException))]
    [InlineData(HttpStatusCode.TooManyRequests, typeof(TranslationRateLimitException))]
    [InlineData(HttpStatusCode.InternalServerError, typeof(TranslationUnavailableException))]
    public async Task Provider_Maps_Http_Status(
        HttpStatusCode statusCode,
        Type expectedException)
    {
        var provider = CreateProvider(new RecordingHandler(statusCode, "{}"));

        var exception = await Record.ExceptionAsync(
            () => provider.TranslateRawAsync(CreateRequest(), CancellationToken.None));

        Assert.IsType(expectedException, exception);
    }

    [Fact]
    public async Task Provider_Maps_Timeout_But_Preserves_User_Cancellation()
    {
        var timeoutProvider = CreateProvider(
            new RecordingHandler(HttpStatusCode.OK, "{}", delay: TimeSpan.FromSeconds(1)),
            TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAsync<TranslationUnavailableException>(
            () => timeoutProvider.TranslateRawAsync(CreateRequest(), CancellationToken.None));

        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();
        var cancelledProvider = CreateProvider(
            new RecordingHandler(HttpStatusCode.OK, "{}", delay: TimeSpan.FromSeconds(1)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelledProvider.TranslateRawAsync(CreateRequest(), cancellationSource.Token));
    }

    [Theory]
    [InlineData("""{"choices":[]}""")]
    [InlineData("""{"choices":[{"message":{"content":""}}]}""")]
    [InlineData("""{"choices":[{"finish_reason":"length","message":{"content":"{}"}}]}""")]
    public async Task Provider_Rejects_Missing_Empty_Or_Truncated_Content(string response)
    {
        var provider = CreateProvider(new RecordingHandler(HttpStatusCode.OK, response));

        await Assert.ThrowsAsync<TranslationFormatException>(
            () => provider.TranslateRawAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Provider_Does_Not_Send_Request_Without_Key()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var provider = new DeepSeekTranslationProvider(
            new HttpClient(handler),
            new FixedSecretStore(null),
            new DeepSeekOptions());

        await Assert.ThrowsAsync<TranslationAuthenticationException>(
            () => provider.TranslateRawAsync(CreateRequest(), CancellationToken.None));

        Assert.Null(handler.Path);
    }

    private static DeepSeekTranslationProvider CreateProvider(
        RecordingHandler handler,
        TimeSpan? timeout = null) =>
        new(
            new HttpClient(handler),
            new FixedSecretStore("test-key"),
            new DeepSeekOptions
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(15),
            });

    private static TranslationRequest CreateRequest() =>
        new(
            "auto",
            "zh-CN",
            TranslationStyle.Natural,
            string.Empty,
            [new OcrBlock("b1", "Hello", 1, new PixelRect(0, 0, 100, 20), 0)]);

    private sealed class FixedSecretStore(string? secret) : ISecretStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(secret);

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingHandler(
        HttpStatusCode statusCode,
        string response,
        TimeSpan? delay = null) : HttpMessageHandler
    {
        public string? Path { get; private set; }

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (delay is not null)
            {
                await Task.Delay(delay.Value, cancellationToken);
            }

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            };
        }
    }
}
