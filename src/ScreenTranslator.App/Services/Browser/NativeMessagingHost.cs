using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ScreenTranslator.App.Services.Browser;

public static class NativeMessagingHost
{
    public const int MaximumMessageBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static bool IsBrowserInvocation(IReadOnlyList<string> arguments) =>
        arguments.Count > 0 &&
        Uri.TryCreate(arguments[0], UriKind.Absolute, out var origin) &&
        origin.Scheme.Equals("chrome-extension", StringComparison.OrdinalIgnoreCase);

    public static async Task RunAsync(
        Stream browserInput,
        Stream browserOutput,
        CancellationToken cancellationToken)
    {
        await using var bridge = new BrowserBridgeClient();
        await bridge.ConnectAsync(cancellationToken);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var browserToDesktop = ForwardBrowserToDesktopAsync(
            browserInput,
            bridge,
            linkedCancellation.Token);
        var desktopToBrowser = ForwardDesktopToBrowserAsync(
            bridge,
            browserOutput,
            linkedCancellation.Token);

        await Task.WhenAny(browserToDesktop, desktopToBrowser);
        linkedCancellation.Cancel();

        try
        {
            await Task.WhenAll(browserToDesktop, desktopToBrowser);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            // The other side closing is the normal native-messaging shutdown path.
        }
    }

    public static async Task<string?> ReadAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[sizeof(int)];
        var prefixRead = await ReadAtMostAsync(input, prefix, cancellationToken);
        if (prefixRead == 0)
        {
            return null;
        }

        if (prefixRead != prefix.Length)
        {
            throw new EndOfStreamException("原生消息长度前缀不完整。");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is <= 0 or > MaximumMessageBytes)
        {
            throw new InvalidDataException("原生消息长度无效。");
        }

        var payload = new byte[length];
        await input.ReadExactlyAsync(payload, cancellationToken);
        return StrictUtf8.GetString(payload);
    }

    public static async Task WriteAsync(
        Stream output,
        string json,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var payload = StrictUtf8.GetBytes(json);
        if (payload.Length > MaximumMessageBytes)
        {
            throw new InvalidDataException("原生消息超过 1 MiB 上限。");
        }

        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await output.WriteAsync(prefix, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static async Task ForwardBrowserToDesktopAsync(
        Stream browserInput,
        BrowserBridgeClient bridge,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await ReadAsync(browserInput, cancellationToken);
            if (message is null)
            {
                return;
            }

            await bridge.WriteAsync(message, cancellationToken);
        }
    }

    private static async Task ForwardDesktopToBrowserAsync(
        BrowserBridgeClient bridge,
        Stream browserOutput,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await bridge.ReadAsync(cancellationToken);
            if (message is null)
            {
                return;
            }

            await WriteAsync(browserOutput, message, cancellationToken);
        }
    }

    private static async Task<int> ReadAtMostAsync(
        Stream input,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await input.ReadAsync(buffer[totalRead..], cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }
}
