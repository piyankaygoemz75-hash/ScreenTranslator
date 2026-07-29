using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.App.Windows;
using ScreenTranslator.Core.Browser;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Settings;

namespace ScreenTranslator.App.Services;

public sealed partial class ApplicationController
{
    private static readonly JsonSerializerOptions BrowserJsonOptions =
        CreateBrowserJsonOptions();

    private readonly ConcurrentDictionary<Guid, BrowserKind> _browserConnections = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ActiveTabReply>>
        _pendingActiveTabQueries = new();

    private BrowserBridgeServer? _browserBridgeServer;
    private NativeMessagingRegistrationService? _nativeMessagingRegistration;
    private BrowserFollowCoordinator? _browserFollowCoordinator;
    private BrowserWindowEventMonitor? _browserWindowMonitor;
    private BrowserWindowSnapshot? _browserWindowSnapshot;
    private Guid? _activeBrowserConnectionId;
    private int _browserFollowGeneration;

    private async Task InitializeBrowserIntegrationAsync()
    {
        _browserBridgeServer = new BrowserBridgeServer();
        _browserBridgeServer.MessageReceived += OnBrowserBridgeMessageReceived;
        _browserBridgeServer.ConnectionClosed += OnBrowserBridgeConnectionClosed;
        _browserBridgeServer.Start();

        BrowserIntegration.OpenBrowserExtensionsRequested +=
            OnOpenBrowserExtensionsRequested;
        BrowserIntegration.OpenExtensionFolderRequested +=
            OnOpenExtensionFolderRequested;

        try
        {
            _nativeMessagingRegistration = new NativeMessagingRegistrationService();
            await _nativeMessagingRegistration.RegisterAsync();
            BrowserIntegration.DetailText =
                "本机桥接已就绪。加载配套扩展后，Chrome/Edge 普通网页中的原位译文可随滚动移动。";
        }
        catch (Exception exception)
        {
            BrowserIntegration.DetailText =
                $"网页跟随桥接暂不可用：{exception.Message}";
        }
    }

    private async Task TryStartBrowserFollowingAsync(
        LastTranslationWork work,
        IReadOnlyList<TextOverlayWindow> overlays,
        AppSettings settings)
    {
        if (!settings.BrowserFollowingEnabled
            || !BrowserIntegration.IsEnabled
            || work.CapturedBrowser is null
            || overlays.Count == 0
            || _browserBridgeServer is null)
        {
            return;
        }

        BrowserWindowEventMonitor? monitor = null;
        var generation = Volatile.Read(ref _browserFollowGeneration);
        try
        {
            monitor = new BrowserWindowEventMonitor(
                work.CapturedBrowser.Snapshot.Handle);
            var currentSnapshot = monitor.GetSnapshot();
            if (currentSnapshot is null)
            {
                monitor.Dispose();
                return;
            }

            var match = await QueryMatchingActiveTabAsync(
                work.CapturedBrowser.Browser,
                currentSnapshot,
                CancellationToken.None);
            if (match is null
                || _disposed
                || generation != Volatile.Read(ref _browserFollowGeneration)
                || overlays.Any(overlay => !overlay.IsLoaded))
            {
                monitor.Dispose();
                return;
            }

            var monitorScale = work.Monitor.ScaleX;
            var hello = match.Value.Reply.ToHello();
            var viewportBounds = CalculateViewportBounds(
                currentSnapshot,
                hello,
                monitorScale);
            var session = new BrowserTrackingSession(
                hello,
                monitorScale,
                viewportBounds);
            var coordinator = new BrowserFollowCoordinator(
                session,
                overlays.Cast<ITrackedOverlay>().ToArray(),
                ToCoreDipRect(work.AbsoluteSelection, work.Monitor));
            coordinator.Invalidated += OnBrowserFollowInvalidated;

            if (_browserFollowCoordinator is not null)
            {
                monitor.Dispose();
                return;
            }

            _browserFollowCoordinator = coordinator;
            _browserWindowMonitor = monitor;
            _browserWindowSnapshot = currentSnapshot;
            _activeBrowserConnectionId = match.Value.ConnectionId;
            monitor.Changed += OnBrowserWindowChanged;
            BrowserIntegration.DetailText =
                $"{BrowserLabel(hello.Browser)} 网页跟随已启用；滚动不会重复 OCR 或调用 DeepSeek。";
        }
        catch (OperationCanceledException)
        {
            monitor?.Dispose();
        }
        catch (Exception exception)
        {
            monitor?.Dispose();
            BrowserIntegration.DetailText =
                $"本次保持静态译文，网页跟随未启动：{exception.Message}";
        }
    }

    private async Task<ActiveTabMatch?> QueryMatchingActiveTabAsync(
        BrowserKind expectedBrowser,
        BrowserWindowSnapshot expectedWindow,
        CancellationToken cancellationToken)
    {
        if (_browserBridgeServer is null)
        {
            return null;
        }

        var preferredConnections = _browserConnections
            .Where(pair => pair.Value == expectedBrowser)
            .Select(pair => pair.Key)
            .ToArray();
        var connectionIds = preferredConnections.Length > 0
            ? preferredConnections
            : _browserBridgeServer.Connections.ToArray();
        if (connectionIds.Length == 0)
        {
            return null;
        }

        var replies = await Task.WhenAll(connectionIds.Select(
            connectionId => QueryActiveTabAsync(connectionId, cancellationToken)));
        return replies
            .Where(reply => reply is not null)
            .Select(reply => reply!.Value)
            .Where(reply =>
                reply.Reply.Found
                && reply.Reply.BrowserKind == expectedBrowser
                && reply.Reply.FrameId == 0
                && reply.Reply.BrowserWindowBounds is not null
                && WindowBoundsMatch(
                    expectedWindow,
                    reply.Reply.BrowserWindowBounds.Value))
            .OrderBy(reply => WindowMatchError(
                expectedWindow,
                reply.Reply.BrowserWindowBounds!.Value))
            .Select(reply => (ActiveTabMatch?)reply)
            .FirstOrDefault();
    }

    private async Task<ActiveTabMatch?> QueryActiveTabAsync(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        if (_browserBridgeServer is null)
        {
            return null;
        }

        var requestId = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource<ActiveTabReply>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingActiveTabQueries.TryAdd(requestId, completion))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(900));
        try
        {
            var request = JsonSerializer.Serialize(
                new ActiveTabQuery("queryActiveTab", requestId),
                BrowserJsonOptions);
            await _browserBridgeServer.SendAsync(
                connectionId,
                request,
                timeout.Token);
            var reply = await completion.Task.WaitAsync(timeout.Token);
            return new ActiveTabMatch(connectionId, reply);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException
                or InvalidOperationException
                or IOException)
        {
            return null;
        }
        finally
        {
            _pendingActiveTabQueries.TryRemove(requestId, out _);
        }
    }

    private void OnBrowserBridgeMessageReceived(
        object? sender,
        BrowserBridgeMessageEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.Json);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            if (string.Equals(type, "bridgeReady", StringComparison.Ordinal))
            {
                var ready = JsonSerializer.Deserialize<BridgeReady>(
                    e.Json,
                    BrowserJsonOptions);
                if (ready?.BrowserKind is { } readyBrowser)
                {
                    _browserConnections[e.ConnectionId] = readyBrowser;
                    UpdateBrowserConnectionStatus(readyBrowser);
                }

                return;
            }

            if (string.Equals(type, "activeTab", StringComparison.Ordinal))
            {
                var reply = JsonSerializer.Deserialize<ActiveTabReply>(
                    e.Json,
                    BrowserJsonOptions);
                if (reply?.RequestId is { Length: > 0 } requestId
                    && _pendingActiveTabQueries.TryGetValue(
                        requestId,
                        out var completion))
                {
                    if (reply.BrowserKind is { } browser)
                    {
                        _browserConnections[e.ConnectionId] = browser;
                        UpdateBrowserConnectionStatus(browser);
                    }

                    completion.TrySetResult(reply);
                }

                return;
            }

            var message = BrowserProtocol.Deserialize(e.Json);
            if (message is BrowserHello hello)
            {
                _browserConnections[e.ConnectionId] = hello.Browser;
                UpdateBrowserConnectionStatus(hello.Browser);
            }

            _application.Dispatcher.BeginInvoke(() =>
            {
                if (_activeBrowserConnectionId == e.ConnectionId)
                {
                    _browserFollowCoordinator?.Handle(message);
                }
            });
        }
        catch (Exception exception) when (
            exception is JsonException
                or BrowserProtocolException
                or InvalidOperationException)
        {
            // Malformed or stale extension messages are ignored.
        }
    }

    private void OnBrowserBridgeConnectionClosed(
        object? sender,
        BrowserBridgeConnectionEventArgs e)
    {
        if (_browserConnections.TryRemove(e.ConnectionId, out var browser))
        {
            UpdateBrowserConnectionStatus(browser);
        }

        _application.Dispatcher.BeginInvoke(() =>
        {
            if (_activeBrowserConnectionId == e.ConnectionId)
            {
                _browserFollowCoordinator?.Invalidate("浏览器扩展连接已断开。");
            }
        });
    }

    private void UpdateBrowserConnectionStatus(BrowserKind browser)
    {
        _application.Dispatcher.BeginInvoke(() =>
            BrowserIntegration.UpdateConnection(
                browser,
                _browserConnections.Values.Any(value => value == browser)));
    }

    private void OnBrowserWindowChanged(
        object? sender,
        BrowserWindowChangedEventArgs e)
    {
        _application.Dispatcher.BeginInvoke(() =>
        {
            var coordinator = _browserFollowCoordinator;
            if (coordinator is null)
            {
                return;
            }

            if (e.Kind is BrowserWindowChangeKind.Minimized
                or BrowserWindowChangeKind.Destroyed
                || e.Snapshot is null
                || _browserWindowSnapshot is null)
            {
                coordinator.Invalidate("浏览器窗口已最小化或关闭。");
                return;
            }

            var previous = _browserWindowSnapshot;
            var current = e.Snapshot;
            if (previous.Dpi != current.Dpi
                || previous.Bounds.Width != current.Bounds.Width
                || previous.Bounds.Height != current.Bounds.Height)
            {
                coordinator.Invalidate("浏览器缩放、DPI 或窗口大小已经变化。");
                return;
            }

            _browserWindowSnapshot = current;
            var scale = previous.Dpi / 96d;
            coordinator.OffsetWithBrowserWindow(
                (current.Bounds.X - previous.Bounds.X) / scale,
                (current.Bounds.Y - previous.Bounds.Y) / scale);
        });
    }

    private void OnBrowserFollowInvalidated(
        object? sender,
        BrowserFollowInvalidatedEventArgs e)
    {
        if (!ReferenceEquals(sender, _browserFollowCoordinator))
        {
            return;
        }

        _browserWindowMonitor?.Dispose();
        _browserWindowMonitor = null;
        _browserWindowSnapshot = null;
        _browserFollowCoordinator = null;
        _activeBrowserConnectionId = null;
        BrowserIntegration.DetailText =
            $"网页状态已变化，旧译文已隐藏：{e.Reason}";
    }

    private void StopBrowserFollowing(bool hideOverlays)
    {
        Interlocked.Increment(ref _browserFollowGeneration);
        foreach (var pending in _pendingActiveTabQueries.Values)
        {
            pending.TrySetCanceled();
        }

        var coordinator = _browserFollowCoordinator;
        _browserFollowCoordinator = null;
        _activeBrowserConnectionId = null;
        if (coordinator is not null)
        {
            coordinator.Invalidated -= OnBrowserFollowInvalidated;
            if (hideOverlays && !coordinator.IsInvalidated)
            {
                coordinator.Invalidate("网页跟随已停止。");
            }
        }

        if (_browserWindowMonitor is not null)
        {
            _browserWindowMonitor.Changed -= OnBrowserWindowChanged;
            _browserWindowMonitor.Dispose();
            _browserWindowMonitor = null;
        }

        _browserWindowSnapshot = null;
    }

    private void OnOpenBrowserExtensionsRequested(
        object? sender,
        BrowserKind browser)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = browser == BrowserKind.Chrome
                    ? "chrome.exe"
                    : "msedge.exe",
                Arguments = browser == BrowserKind.Chrome
                    ? "chrome://extensions"
                    : "edge://extensions",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            BrowserIntegration.DetailText =
                $"无法打开浏览器扩展页：{exception.Message}";
        }
    }

    private void OnOpenExtensionFolderRequested(object? sender, EventArgs e)
    {
        var folder = FindExtensionFolder();
        if (folder is null)
        {
            BrowserIntegration.DetailText =
                "未找到 browser-extension 文件夹，请重新解压完整发布包。";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            BrowserIntegration.DetailText =
                $"无法打开扩展文件夹：{exception.Message}";
        }
    }

    private void DisposeBrowserIntegration()
    {
        StopBrowserFollowing(hideOverlays: false);
        BrowserIntegration.OpenBrowserExtensionsRequested -=
            OnOpenBrowserExtensionsRequested;
        BrowserIntegration.OpenExtensionFolderRequested -=
            OnOpenExtensionFolderRequested;

        if (_browserBridgeServer is null)
        {
            return;
        }

        _browserBridgeServer.MessageReceived -= OnBrowserBridgeMessageReceived;
        _browserBridgeServer.ConnectionClosed -= OnBrowserBridgeConnectionClosed;
        try
        {
            _browserBridgeServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Application shutdown must continue if a browser closes concurrently.
        }

        _browserBridgeServer = null;
    }

    private static DipRect CalculateViewportBounds(
        BrowserWindowSnapshot snapshot,
        BrowserHello hello,
        double monitorScale)
    {
        var windowBounds = ToDip(snapshot.Bounds, snapshot.Dpi / 96d);
        var viewportWidth =
            hello.ViewportSize.Width * hello.DevicePixelRatio / monitorScale;
        var viewportHeight =
            hello.ViewportSize.Height * hello.DevicePixelRatio / monitorScale;
        return new DipRect(
            windowBounds.X + Math.Max(0, (windowBounds.Width - viewportWidth) / 2),
            windowBounds.Y + Math.Max(0, windowBounds.Height - viewportHeight),
            Math.Min(viewportWidth, windowBounds.Width),
            Math.Min(viewportHeight, windowBounds.Height));
    }

    private static bool WindowBoundsMatch(
        BrowserWindowSnapshot expected,
        CssRect actual) =>
        WindowMatchError(expected, actual) <= 260;

    private static double WindowMatchError(
        BrowserWindowSnapshot expected,
        CssRect actual)
    {
        var expectedDip = ToDip(expected.Bounds, expected.Dpi / 96d);
        return Math.Abs(expectedDip.X - actual.Left)
               + Math.Abs(expectedDip.Y - actual.Top)
               + Math.Abs(expectedDip.Width - actual.Width)
               + Math.Abs(expectedDip.Height - actual.Height);
    }

    private static DipRect ToDip(PixelRect bounds, double scale) =>
        new(
            bounds.X / scale,
            bounds.Y / scale,
            bounds.Width / scale,
            bounds.Height / scale);

    private static string? FindExtensionFolder()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "browser-extension"),
            Path.Combine(Environment.CurrentDirectory, "browser-extension"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "browser-extension")),
        };
        return candidates.FirstOrDefault(
            candidate => File.Exists(Path.Combine(candidate, "manifest.json")));
    }

    private static string BrowserLabel(BrowserKind browser) =>
        browser == BrowserKind.Chrome ? "Chrome" : "Edge";

    private static JsonSerializerOptions CreateBrowserJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record ActiveTabQuery(string Type, string RequestId);

    private sealed record BridgeReady(string Type, string? Browser)
    {
        public BrowserKind? BrowserKind =>
            ParseBrowserKind(Browser);
    }

    private sealed record ActiveTabReply(
        string Type,
        string? RequestId,
        bool Found,
        string? Browser,
        int BrowserWindowId,
        CssRect? BrowserWindowBounds,
        int TabId,
        int FrameId,
        string? DocumentToken,
        long NavigationGeneration,
        double DevicePixelRatio,
        CssSize? ViewportSize)
    {
        public BrowserKind? BrowserKind =>
            ParseBrowserKind(Browser);

        public BrowserHello ToHello()
        {
            if (!Found
                || BrowserKind is not { } browser
                || BrowserWindowBounds is not { } bounds
                || ViewportSize is not { } viewport
                || string.IsNullOrWhiteSpace(DocumentToken))
            {
                throw new BrowserProtocolException(
                    "浏览器活动标签页响应不完整。");
            }

            var hello = new BrowserHello(
                browser,
                BrowserWindowId,
                TabId,
                DocumentToken,
                NavigationGeneration,
                DevicePixelRatio,
                viewport,
                bounds,
                FrameId);
            BrowserProtocol.Validate(hello);
            return hello;
        }
    }

    private readonly record struct ActiveTabMatch(
        Guid ConnectionId,
        ActiveTabReply Reply);

    private static BrowserKind? ParseBrowserKind(string? browser) =>
        browser?.ToLowerInvariant() switch
        {
            "chrome" => BrowserKind.Chrome,
            "edge" => BrowserKind.Edge,
            _ => null,
        };
}
