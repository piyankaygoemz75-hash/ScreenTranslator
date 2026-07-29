using System.Buffers.Binary;
using System.Text;
using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.IntegrationTests.TestInfrastructure;

namespace ScreenTranslator.IntegrationTests.Browser;

public sealed class NativeMessagingHostTests
{
    [Fact]
    public async Task ReadAsync_Reads_Little_Endian_Length_Prefixed_Json()
    {
        const string json = """{"type":"hello","browser":"chrome"}""";
        await using var stream = CreateMessageStream(json);

        var result = await NativeMessagingHost.ReadAsync(
            stream,
            CancellationToken.None);

        Assert.Equal(json, result);
    }

    [Fact]
    public async Task ReadAsync_Returns_Null_For_Clean_End_Of_Stream()
    {
        await using var stream = new MemoryStream();

        var result = await NativeMessagingHost.ReadAsync(
            stream,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReadAsync_Rejects_Incomplete_Length_Prefix()
    {
        await using var stream = new MemoryStream([0x01, 0x02]);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => NativeMessagingHost.ReadAsync(
                stream,
                CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_Rejects_Messages_Over_One_Mebibyte()
    {
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(
            prefix,
            NativeMessagingHost.MaximumMessageBytes + 1);
        await using var stream = new MemoryStream(prefix);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => NativeMessagingHost.ReadAsync(
                stream,
                CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_Uses_Expected_Native_Message_Frame()
    {
        const string json = """{"type":"pong"}""";
        await using var stream = new MemoryStream();

        await NativeMessagingHost.WriteAsync(
            stream,
            json,
            CancellationToken.None);

        var bytes = stream.ToArray();
        Assert.Equal(
            Encoding.UTF8.GetByteCount(json),
            BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0, 4)));
        Assert.Equal(json, Encoding.UTF8.GetString(bytes.AsSpan(4)));
    }

    [Theory]
    [InlineData("chrome-extension://plpgmkbadcfnkmolbeecggbbopilajed/", true)]
    [InlineData("--parent-window=123", false)]
    [InlineData("", false)]
    public void IsBrowserInvocation_Recognizes_Extension_Origin(
        string argument,
        bool expected)
    {
        Assert.Equal(
            expected,
            NativeMessagingHost.IsBrowserInvocation([argument]));
    }

    [Fact]
    public async Task Bridge_RoundTrips_Messages_Between_Host_And_Desktop()
    {
        await using var server = new BrowserBridgeServer();
        server.Start();
        var received = new TaskCompletionSource<BrowserBridgeMessageEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        server.MessageReceived += (_, args) => received.TrySetResult(args);

        await using var client = new BrowserBridgeClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(timeout.Token);
        await client.WriteAsync("""{"type":"hello"}""", timeout.Token);

        var message = await received.Task.WaitAsync(timeout.Token);
        Assert.Equal("""{"type":"hello"}""", message.Json);

        var readTask = client.ReadAsync(timeout.Token);
        await server.SendAsync(
            message.ConnectionId,
            """{"type":"ready"}""",
            timeout.Token);
        Assert.Equal(
            """{"type":"ready"}""",
            await readTask);
    }

    [Fact]
    public void Bridge_Dispose_Does_Not_Deadlock_A_Shutting_Down_Ui_Thread()
    {
        NonPumpingContextTest.Run(
            () =>
            {
                var server = new BrowserBridgeServer();
                server.Start();
                server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            },
            TimeSpan.FromSeconds(3));
    }

    private static MemoryStream CreateMessageStream(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        var frame = new byte[payload.Length + sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length);
        payload.CopyTo(frame.AsSpan(sizeof(int)));
        return new MemoryStream(frame);
    }
}
