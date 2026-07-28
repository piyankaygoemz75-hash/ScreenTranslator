# Interaction Fixes and Browser Following Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix configurable hotkeys, mouse-wheel scrolling, and side-panel layout/dragging, then make overlay translations follow ordinary Chrome and Edge webpage scrolling without repeating OCR or DeepSeek requests.

**Architecture:** Keep deterministic geometry, settings, and browser-session rules in `ScreenTranslator.Core`; keep WPF, Win32 registration, named-pipe transport, and overlay-window movement in `ScreenTranslator.App`. A shared Manifest V3 extension sends scroll geometry through Chrome Native Messaging to a short-lived native-host process, which forwards validated messages to the running desktop process.

**Tech Stack:** .NET 8, WPF, WPF-UI, Win32 `RegisterHotKey`, named pipes, Chrome/Edge Manifest V3, JavaScript `node:test`, xUnit.

---

## File structure

New focused units:

- `src/ScreenTranslator.Core/Hotkeys/HotkeyGesture.cs`: parse, validate, and format persisted keyboard gestures.
- `src/ScreenTranslator.Core/Browser/BrowserProtocol.cs`: immutable browser messages and JSON protocol names.
- `src/ScreenTranslator.Core/Browser/BrowserTrackingSession.cs`: validate document identity and compute overlay movement.
- `src/ScreenTranslator.Core/Browser/OverlayFollowCalculator.cs`: convert CSS scroll deltas to WPF DIP and clip translated blocks.
- `src/ScreenTranslator.Core/Layout/SidePanelBoundsService.cs`: calculate safe panel bounds.
- `src/ScreenTranslator.App/Behaviors/MouseWheelRouter.cs`: route unhandled WPF wheel input to the nearest scroll viewer.
- `src/ScreenTranslator.App/Services/Hotkeys/HotkeyRegistrationCoordinator.cs`: transactional hotkey replacement and rollback.
- `src/ScreenTranslator.App/Services/Browser/NativeMessagingHost.cs`: Chrome length-prefixed stdin/stdout protocol.
- `src/ScreenTranslator.App/Services/Browser/BrowserBridgeServer.cs`: current-user named-pipe server in the main process.
- `src/ScreenTranslator.App/Services/Browser/BrowserBridgeClient.cs`: native-host named-pipe client.
- `src/ScreenTranslator.App/Services/Browser/NativeMessagingRegistrationService.cs`: per-user Chrome/Edge host registration.
- `src/ScreenTranslator.App/Services/Browser/BrowserWindowEventMonitor.cs`: observe browser move, minimize, DPI transition, and destruction.
- `src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs`: correlate capture windows, browser sessions, and overlay windows.
- `src/ScreenTranslator.App/ViewModels/BrowserIntegrationViewModel.cs`: connection and installation status.
- `browser-extension/manifest.json`: common deterministic Chrome/Edge extension identity.
- `browser-extension/background.js`: native connection and active-tab metadata.
- `browser-extension/scroll-accumulator.js`: browser-neutral scroll coalescing logic.
- `browser-extension/content.js`: root/nested scroll observation and animation-frame coalescing.
- `browser-extension/tests/content.test.js`: dependency-free Node tests for scroll aggregation.

Existing `ApplicationController` remains the composition root. Make it `partial` and place browser-only orchestration in `ApplicationController.Browser.cs` instead of expanding the already large file further.

### Task 1: Settings migration and hotkey value object

**Files:**
- Create: `src/ScreenTranslator.Core/Hotkeys/HotkeyGesture.cs`
- Modify: `src/ScreenTranslator.Core/Settings/AppSettings.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Hotkeys/HotkeyGestureTests.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Settings/AppSettingsTests.cs`

- [ ] **Step 1: Write failing hotkey parsing tests**

```csharp
[Theory]
[InlineData("Ctrl+Alt+G", "Ctrl + Alt + G")]
[InlineData("Shift+F8", "Shift + F8")]
[InlineData("Win+Shift+S", "Win + Shift + S")]
public void Parse_Normalizes_Valid_Gestures(string persisted, string display)
{
    var gesture = HotkeyGesture.Parse(persisted);
    Assert.Equal(persisted, gesture.ToPersistedString());
    Assert.Equal(display, gesture.ToDisplayString());
}

[Theory]
[InlineData("T")]
[InlineData("Ctrl")]
[InlineData("Alt+Shift")]
[InlineData("Ctrl+Alt+Delete")]
public void Parse_Rejects_Unsafe_Or_Incomplete_Gestures(string persisted) =>
    Assert.Throws<FormatException>(() => HotkeyGesture.Parse(persisted));
```

- [ ] **Step 2: Run the tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter HotkeyGestureTests
```

Expected: FAIL because `HotkeyGesture` does not exist.

- [ ] **Step 3: Implement the immutable gesture**

```csharp
public sealed record HotkeyGesture(
    HotkeyModifiers Modifiers,
    string KeyName)
{
    public static HotkeyGesture Default { get; } =
        new(HotkeyModifiers.Alt | HotkeyModifiers.Shift, "T");

    public static HotkeyGesture Parse(string value)
    {
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new FormatException("快捷键必须包含修饰键和一个可用按键。");
        var modifiers = parts[..^1].Aggregate(HotkeyModifiers.None, ParseModifier);
        var key = parts[^1].ToUpperInvariant();
        if (modifiers == HotkeyModifiers.None || !AllowedKeys.Contains(key) || key == "DELETE")
            throw new FormatException("快捷键必须包含修饰键和一个可用按键。");
        return new HotkeyGesture(modifiers, key);
    }

    public string ToPersistedString() => string.Join("+", GetParts(spaced: false));
    public string ToDisplayString() => string.Join(" + ", GetParts(spaced: true));
}
```

Implement `HotkeyModifiers` as a `[Flags]` enum and use a fixed allowed-key set covering letters, digits, `F1`–`F12`, arrows, `Space`, `Home`, `End`, `PageUp`, and `PageDown`. Reject `Tab`, `Escape`, `Enter`, `Backspace`, `Delete`, `PrintScreen`, and pure modifier input.

- [ ] **Step 4: Upgrade settings with backward-compatible defaults**

```csharp
public const int CurrentVersion = 2;
public string Hotkey { get; init; } = HotkeyGesture.Default.ToPersistedString();
public bool HotkeyEnabled { get; init; } = true;
public bool BrowserFollowingEnabled { get; init; } = true;
public WindowPlacement? SidePanelPlacement { get; init; }

public sealed record WindowPlacement(double Left, double Top, double Width, double Height);
```

Update the settings test to deserialize a version-1 JSON document and assert the three new properties use defaults.

- [ ] **Step 5: Run core tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter "HotkeyGestureTests|AppSettingsTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenTranslator.Core/Hotkeys src/ScreenTranslator.Core/Settings tests/ScreenTranslator.Core.Tests/Hotkeys tests/ScreenTranslator.Core.Tests/Settings
git -c user.name=Codex -c user.email=codex@local commit -m "feat: persist validated hotkey gestures"
```

### Task 2: Transactional hotkey recording

**Files:**
- Create: `src/ScreenTranslator.App/Services/Hotkeys/HotkeyRegistrationCoordinator.cs`
- Modify: `src/ScreenTranslator.App/Services/Hotkeys/GlobalHotkeyService.cs`
- Modify: `src/ScreenTranslator.App/ViewModels/SettingsViewModels.cs`
- Modify: `src/ScreenTranslator.App/Pages/HotkeyPage.xaml`
- Modify: `src/ScreenTranslator.App/Pages/HotkeyPage.xaml.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Hotkeys/HotkeyRegistrationCoordinatorTests.cs`

- [ ] **Step 1: Write registration rollback tests with a fake service**

```csharp
[Fact]
public void Replace_Restores_Old_Gesture_When_New_Gesture_Conflicts()
{
    var service = new FakeHotkeyService(failOn: "Ctrl+Alt+G");
    var coordinator = new HotkeyRegistrationCoordinator(service);
    coordinator.Register(HotkeyGesture.Parse("Ctrl+Shift+G"));

    var result = coordinator.TryReplace(HotkeyGesture.Parse("Ctrl+Alt+G"));

    Assert.False(result.Succeeded);
    Assert.Equal("Ctrl+Shift+G", service.RegisteredGesture);
}
```

Add a second test asserting that failure to restore returns `IsEnabled=false`.

- [ ] **Step 2: Run the test and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter HotkeyRegistrationCoordinatorTests
```

Expected: FAIL because the coordinator does not exist.

- [ ] **Step 3: Add gesture-based registration**

Change `IGlobalHotkeyService.Register` to accept `HotkeyGesture`, map `HotkeyModifiers` to Win32 modifier flags, and map `KeyName` through `KeyConverter` plus `KeyInterop.VirtualKeyFromKey`.

```csharp
public HotkeyReplacementResult TryReplace(HotkeyGesture next)
{
    var previous = CurrentGesture;
    _service.Unregister();
    try
    {
        _service.Register(next);
        CurrentGesture = next;
        return HotkeyReplacementResult.Success(next);
    }
    catch (HotkeyConflictException conflict)
    {
        return Restore(previous, conflict.Message);
    }
}
```

- [ ] **Step 4: Implement the recording UI**

In `HotkeyPage.xaml`, replace the reset button with a keyboard-focusable recording button and add `PreviewKeyDown="Page_OnPreviewKeyDown"`.

```csharp
private void Page_OnPreviewKeyDown(object sender, KeyEventArgs e)
{
    if (DataContext is not HotkeySettingsViewModel vm || !vm.IsRecording)
        return;

    e.Handled = true;
    if (e.Key == Key.Escape) vm.CancelRecordingCommand.Execute(null);
    else if (e.Key == Key.Back) vm.UseDefaultCommand.Execute(null);
    else vm.AcceptKeyboardInput(Keyboard.Modifiers, e.Key == Key.System ? e.SystemKey : e.Key);
}
```

The view model exposes `IsRecording`, `BeginRecordingCommand`, `CancelRecordingCommand`, `UseDefaultCommand`, and `GestureSubmitted`. It only emits a submitted gesture after `HotkeyGesture` validation succeeds.

- [ ] **Step 5: Wire controller persistence**

Replace `RegisterDefaultHotkey()` with `RegisterSavedHotkey()` using `_persistedSettings.Hotkey`. On `GestureSubmitted`, call `TryReplace`; only on success update the view model, update `_persistedSettings`, and await `SaveSettingsAsync()`. The enable switch registers/unregisters the saved gesture without rewriting it.

- [ ] **Step 6: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter HotkeyRegistrationCoordinatorTests
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: tests PASS and build succeeds with 0 errors.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Hotkeys src/ScreenTranslator.App/ViewModels/SettingsViewModels.cs src/ScreenTranslator.App/Pages/HotkeyPage.xaml* src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.IntegrationTests/Hotkeys
git -c user.name=Codex -c user.email=codex@local commit -m "fix: record and safely replace global hotkeys"
```

### Task 3: Mouse-wheel routing for every GUI scroll area

**Files:**
- Create: `src/ScreenTranslator.App/Behaviors/MouseWheelRouter.cs`
- Modify: `src/ScreenTranslator.App/Windows/MainWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml`
- Create: `tests/ScreenTranslator.IntegrationTests/TestInfrastructure/StaTest.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Behaviors/MouseWheelRouterTests.cs`

- [ ] **Step 1: Write tests for scroll-target selection**

```csharp
[Fact]
public Task FindScrollableAncestor_Returns_Inner_Viewer_Until_Boundary() =>
    StaTest.RunAsync(() =>
{
    var outer = new ScrollViewer { Height = 100, Content = new StackPanel() };
    var inner = new ScrollViewer { Height = 50, Content = new Border { Height = 400 } };
    ((StackPanel)outer.Content).Children.Add(inner);
    TestWindowHost.MeasureAndArrange(outer, new Size(200, 100));

    Assert.Same(inner, MouseWheelRouter.FindTarget(inner, delta: -120));
    inner.ScrollToEnd();
    Assert.Same(outer, MouseWheelRouter.FindTarget(inner, delta: -120));
});
```

Add cases for no overflow and a `ComboBox` whose drop-down is open.

Use this STA helper:

```csharp
public static Task RunAsync(Action action)
{
    var completion = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var thread = new Thread(() =>
    {
        try { action(); completion.SetResult(); }
        catch (Exception exception) { completion.SetException(exception); }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    return completion.Task;
}
```

- [ ] **Step 2: Run the test and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter MouseWheelRouterTests
```

Expected: FAIL because `MouseWheelRouter` does not exist.

- [ ] **Step 3: Implement the attached behavior**

```csharp
public static readonly DependencyProperty IsEnabledProperty =
    DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(MouseWheelRouter),
        new PropertyMetadata(false, OnIsEnabledChanged));

private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
{
    if (IsDirectWheelConsumer(e.OriginalSource as DependencyObject))
        return;

    var target = FindTarget(e.OriginalSource as DependencyObject, e.Delta);
    if (target is null)
        return;

    target.ScrollToVerticalOffset(
        Math.Clamp(target.VerticalOffset - e.Delta / 3.0, 0, target.ScrollableHeight));
    e.Handled = true;
}
```

Walk both visual and logical parents. Treat open combo-box popups, sliders, and controls that already marked the event handled as direct consumers.

- [ ] **Step 4: Enable routing at the two window roots**

Add `behaviors:MouseWheelRouter.IsEnabled="True"` to the root content grid of `MainWindow` and `SidePanelWindow`. Keep the existing page `ScrollViewer` controls; the behavior only repairs routing.

- [ ] **Step 5: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter MouseWheelRouterTests
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: PASS and build succeeds.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenTranslator.App/Behaviors src/ScreenTranslator.App/Windows/MainWindow.xaml src/ScreenTranslator.App/Windows/SidePanelWindow.xaml tests/ScreenTranslator.IntegrationTests/Behaviors
git -c user.name=Codex -c user.email=codex@local commit -m "fix: route mouse wheel input to GUI scroll areas"
```

### Task 4: Draggable and bounded side panel

**Files:**
- Create: `src/ScreenTranslator.Core/Layout/SidePanelBoundsService.cs`
- Modify: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Layout/SidePanelBoundsServiceTests.cs`

- [ ] **Step 1: Write safe-bounds tests**

```csharp
[Fact]
public void Place_Clamps_Long_Panel_To_Eighty_Percent_Of_Work_Area()
{
    var bounds = SidePanelBoundsService.Place(
        source: new DipRect(900, 300, 300, 200),
        workArea: new DipRect(0, 0, 1920, 1040),
        desired: new DipSize(392, 2000),
        previous: null);

    Assert.Equal(832, bounds.Height);
    Assert.True(bounds.Bottom <= 1040);
}
```

Add tests for previous user placement, off-screen placement after monitor removal, and minimum title-bar visibility.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter SidePanelBoundsServiceTests
```

Expected: FAIL because the service does not exist.

- [ ] **Step 3: Implement placement and clamping**

```csharp
public static DipRect Place(
    DipRect source,
    DipRect workArea,
    DipSize desired,
    WindowPlacement? previous)
{
    var width = Math.Clamp(desired.Width, 320, Math.Min(520, workArea.Width));
    var height = Math.Clamp(desired.Height, 280, workArea.Height * 0.8);
    var candidate = previous is null
        ? PlaceBeside(source, width, height, workArea)
        : new DipRect(previous.Left, previous.Top, width, height);
    return ClampTitleBarVisible(candidate, workArea, titleBarHeight: 48);
}
```

- [ ] **Step 4: Replace auto-height XAML**

Set `Height="520"`, `MinHeight="280"`, `MaxWidth="520"`, `ResizeMode="CanResizeWithGrip"`, and remove `SizeToContent`. Keep rows `Auto, Auto, *, Auto`; remove the body `MaxHeight`, name it `ContentScrollViewer`, and leave the footer in the final `Auto` row.

- [ ] **Step 5: Use native caption dragging**

```csharp
private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (e.LeftButton != MouseButtonState.Pressed)
        return;
    ReleaseCapture();
    SendMessage(new WindowInteropHelper(this).Handle, WmNcLButtonDown, HtCaption, IntPtr.Zero);
}
```

On `LocationChanged` and `SizeChanged`, debounce persistence. On drag/resize completion, apply `SidePanelBoundsService` clamping and raise a `PlacementChanged` event for `ApplicationController` to save.

- [ ] **Step 6: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter SidePanelBoundsServiceTests
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: PASS and build succeeds.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenTranslator.Core/Layout/SidePanelBoundsService.cs src/ScreenTranslator.App/Windows/SidePanelWindow.xaml* src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.Core.Tests/Layout/SidePanelBoundsServiceTests.cs
git -c user.name=Codex -c user.email=codex@local commit -m "fix: keep the side panel draggable and bounded"
```

### Task 5: Browser protocol and deterministic overlay movement

**Files:**
- Create: `src/ScreenTranslator.Core/Browser/BrowserProtocol.cs`
- Create: `src/ScreenTranslator.Core/Browser/BrowserTrackingSession.cs`
- Create: `src/ScreenTranslator.Core/Browser/OverlayFollowCalculator.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Browser/BrowserTrackingSessionTests.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Browser/OverlayFollowCalculatorTests.cs`

- [ ] **Step 1: Write CSS-to-DIP and clipping tests**

```csharp
[Theory]
[InlineData(100, 1.25, 1.25, 100)]
[InlineData(100, 1.50, 1.25, 120)]
public void Css_Delta_Uses_Device_And_Monitor_Scale(
    double css, double dpr, double monitorScale, double expectedDip) =>
    Assert.Equal(expectedDip, OverlayFollowCalculator.ToDip(css, dpr, monitorScale), 3);

[Fact]
public void Root_Scroll_Moves_Block_And_Hides_It_Outside_Selection()
{
    var update = OverlayFollowCalculator.ApplyRootScroll(
        new DipRect(100, 100, 200, 40),
        selection: new DipRect(80, 80, 260, 180),
        deltaXDip: 0,
        deltaYDip: 240);
    Assert.False(update.IsVisible);
}
```

- [ ] **Step 2: Write session invalidation tests**

Test that matching `TabId`, `WindowId`, `DocumentToken`, and increasing `NavigationGeneration` accept scroll events, while changed document tokens, NaN values, or stale generations return `BrowserSessionDecision.Invalidate`.

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter "BrowserTrackingSessionTests|OverlayFollowCalculatorTests"
```

Expected: FAIL because browser types do not exist.

- [ ] **Step 4: Implement the protocol**

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(BrowserHello), "hello")]
[JsonDerivedType(typeof(BrowserScroll), "scroll")]
[JsonDerivedType(typeof(BrowserInvalidated), "invalidated")]
public abstract record BrowserMessage;

public sealed record BrowserScroll(
    int BrowserWindowId,
    int TabId,
    string DocumentToken,
    long NavigationGeneration,
    double DeltaXCss,
    double DeltaYCss,
    double DevicePixelRatio,
    CssRect? ScrollContainer) : BrowserMessage;
```

Reject messages with empty tokens, non-finite geometry, device-pixel ratios outside `0.5..8`, or absolute deltas greater than `100_000` CSS pixels.

- [ ] **Step 5: Implement immutable session decisions**

`BrowserTrackingSession.Apply` returns one of `Ignore`, `Move`, or `Invalidate`. `Move` contains DIP deltas and an optional mapped container rectangle. It never mutates WPF windows and never performs I/O.

- [ ] **Step 6: Run tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter "BrowserTrackingSessionTests|OverlayFollowCalculatorTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenTranslator.Core/Browser tests/ScreenTranslator.Core.Tests/Browser
git -c user.name=Codex -c user.email=codex@local commit -m "feat: model browser scroll tracking sessions"
```

### Task 6: Native Messaging transport and per-user registration

**Files:**
- Create: `src/ScreenTranslator.App/Services/Browser/NativeMessagingHost.cs`
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserBridgeServer.cs`
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserBridgeClient.cs`
- Create: `src/ScreenTranslator.App/Services/Browser/NativeMessagingRegistrationService.cs`
- Modify: `src/ScreenTranslator.App/App.xaml.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/NativeMessagingHostTests.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/NativeMessagingRegistrationServiceTests.cs`

- [ ] **Step 1: Write framing tests**

```csharp
[Fact]
public async Task ReadAsync_Reads_Little_Endian_Length_Prefixed_Json()
{
    var json = """{"type":"hello","browser":"chrome"}""";
    using var input = NativeMessageTestStream.FromJson(json);
    var message = await NativeMessagingHost.ReadAsync(input, CancellationToken.None);
    Assert.Equal(json, message);
}

[Fact]
public async Task ReadAsync_Rejects_Messages_Over_One_Megabyte()
{
    using var input = NativeMessageTestStream.WithDeclaredLength(1_048_577);
    await Assert.ThrowsAsync<InvalidDataException>(
        () => NativeMessagingHost.ReadAsync(input, CancellationToken.None));
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter "NativeMessagingHostTests|NativeMessagingRegistrationServiceTests"
```

Expected: FAIL because the browser transport does not exist.

- [ ] **Step 3: Implement bounded native framing**

```csharp
public static async Task<string?> ReadAsync(Stream input, CancellationToken token)
{
    var prefix = new byte[4];
    if (!await input.TryReadExactlyAsync(prefix, token))
        return null;
    var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
    if (length is <= 0 or > 1_048_576)
        throw new InvalidDataException("Invalid native message length.");
    var payload = new byte[length];
    await input.ReadExactlyAsync(payload, token);
    return StrictUtf8.GetString(payload);
}
```

Write responses with the same four-byte little-endian framing and flush after each JSON payload.

- [ ] **Step 4: Implement current-user named-pipe transport**

Use `NamedPipeServerStream` with a name containing the current user SID and `PipeOptions.Asynchronous`. Accept one host connection at a time, validate every JSON payload through `BrowserProtocol`, and publish valid messages as an event. `BrowserBridgeClient` connects with a two-second timeout and forwards browser responses bidirectionally.

- [ ] **Step 5: Implement deterministic extension identity and registry writes**

The extension manifest uses this exact public key:

```text
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEApy4FPRsCWmLezOby6qFw+3sMzE0telN7PAE9KrO8Gyy90O1v3EdX2io1qLiwB+aK4r1LH9IEkgvThsOhPZt/l4kMGPEs8LyqhkgS3Nx3nzxFT6kRQYX4JOKohjtKUomK6KOx5GcfLJFX7sF38A2d8UDeRD1KXEO92kn68w66Pl8XNFERtpTtzfjHdLJugHcN/CrXlR/dk0XLp/EVLmzVJA803HmQE2WwghEk3bMXTjrtzIglygJbkrC8NkYxQN1VMNeWhUN5HtOqWo2Vqly8Nm1lsMl/YgI7XSZ6IR+Y+N0yV92aRbDjCbG7T64ElNU19XMRQz4PPxjXyuxMVsO7fQIDAQAB
```

Its expected extension ID is `plpgmkbadcfnkmolbeecggbbopilajed`. At startup, write `%LOCALAPPDATA%\ScreenTranslator\BrowserHost\native-host.json` from this object so the executable path is always concrete:

```csharp
var manifest = new
{
    name = "com.screentranslator.browser_bridge",
    description = "ScreenTranslator browser scroll bridge",
    path = Environment.ProcessPath
        ?? throw new InvalidOperationException("无法确定程序路径。"),
    type = "stdio",
    allowed_origins = new[]
    {
        "chrome-extension://plpgmkbadcfnkmolbeecggbbopilajed/"
    }
};
await File.WriteAllTextAsync(
    manifestPath,
    JsonSerializer.Serialize(manifest, JsonOptions),
    cancellationToken);
```

Write its absolute path as the default value under both:

```text
HKCU\Software\Google\Chrome\NativeMessagingHosts\com.screentranslator.browser_bridge
HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.screentranslator.browser_bridge
```

Tests use a temporary manifest directory and injectable registry abstraction; they must not write the real registry.

- [ ] **Step 6: Add the native-host startup branch**

Before creating `SingleInstanceGuard`, detect the first argument beginning with `chrome-extension://`. In that branch, run `NativeMessagingHost` against `Console.OpenStandardInput()` and `Console.OpenStandardOutput()`, forward through `BrowserBridgeClient`, then shut down without creating WPF windows.

- [ ] **Step 7: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter "NativeMessagingHostTests|NativeMessagingRegistrationServiceTests"
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: PASS and build succeeds.

- [ ] **Step 8: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Browser src/ScreenTranslator.App/App.xaml.cs tests/ScreenTranslator.IntegrationTests/Browser
git -c user.name=Codex -c user.email=codex@local commit -m "feat: bridge browser events through native messaging"
```

### Task 7: Chrome/Edge Manifest V3 extension

**Files:**
- Create: `browser-extension/manifest.json`
- Create: `browser-extension/background.js`
- Create: `browser-extension/scroll-accumulator.js`
- Create: `browser-extension/content.js`
- Create: `browser-extension/tests/content.test.js`
- Create: `browser-extension/README.md`

- [ ] **Step 1: Write failing dependency-free JavaScript tests**

```javascript
const test = require("node:test");
const assert = require("node:assert/strict");
const { ScrollAccumulator } = require("../scroll-accumulator.js");

test("coalesces multiple root scroll positions into one delta", () => {
  const acc = new ScrollAccumulator();
  acc.observe("root", 0, 100);
  acc.observe("root", 0, 125);
  assert.deepEqual(acc.flush(), [{
    target: "root", deltaXCss: 0, deltaYCss: 25
  }]);
});
```

Add tests for nested target separation, unchanged positions, non-finite values, and invalidation generation.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
node --test browser-extension/tests/content.test.js
```

Expected: FAIL because extension files do not exist.

- [ ] **Step 3: Create the fixed-identity manifest**

```json
{
  "manifest_version": 3,
  "name": "屏译网页跟随",
  "version": "0.1.0",
  "description": "让屏译覆盖层跟随 Chrome/Edge 网页滚动。",
  "key": "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEApy4FPRsCWmLezOby6qFw+3sMzE0telN7PAE9KrO8Gyy90O1v3EdX2io1qLiwB+aK4r1LH9IEkgvThsOhPZt/l4kMGPEs8LyqhkgS3Nx3nzxFT6kRQYX4JOKohjtKUomK6KOx5GcfLJFX7sF38A2d8UDeRD1KXEO92kn68w66Pl8XNFERtpTtzfjHdLJugHcN/CrXlR/dk0XLp/EVLmzVJA803HmQE2WwghEk3bMXTjrtzIglygJbkrC8NkYxQN1VMNeWhUN5HtOqWo2Vqly8Nm1lsMl/YgI7XSZ6IR+Y+N0yV92aRbDjCbG7T64ElNU19XMRQz4PPxjXyuxMVsO7fQIDAQAB",
  "permissions": ["nativeMessaging"],
  "background": { "service_worker": "background.js" },
  "content_scripts": [{
    "matches": ["http://*/*", "https://*/*"],
    "js": ["scroll-accumulator.js", "content.js"],
    "all_frames": true,
    "run_at": "document_start"
  }]
}
```

- [ ] **Step 4: Implement frame-coalesced scroll observation**

`scroll-accumulator.js` assigns `ScrollAccumulator` to `globalThis` and also exports it through `module.exports` when running under Node. `content.js` registers a capture-phase `scroll` listener, identifies root versus element scrolling, assigns ephemeral target IDs with `WeakMap`, and posts at most one batch per `requestAnimationFrame`. Generate a random `documentToken` once per document with `crypto.randomUUID()`. Send only geometry and identity; never send text or URL.

- [ ] **Step 5: Implement service-worker native forwarding**

```javascript
const HOST = "com.screentranslator.browser_bridge";
let port;

function connect() {
  port = chrome.runtime.connectNative(HOST);
  port.onDisconnect.addListener(() => {
    port = undefined;
    setTimeout(connect, 1000);
  });
}

chrome.runtime.onMessage.addListener((message, sender) => {
  if (!sender.tab || !port) return;
  chrome.windows.get(sender.tab.windowId).then(browserWindow => {
    port.postMessage({
      ...message,
      browser: navigator.userAgent.includes("Edg/") ? "edge" : "chrome",
      tabId: sender.tab.id,
      frameId: sender.frameId,
      browserWindowId: sender.tab.windowId,
      browserWindowBounds: {
        left: browserWindow.left,
        top: browserWindow.top,
        width: browserWindow.width,
        height: browserWindow.height
      }
    });
  });
});

connect();
```

Use capped exponential reconnect delays from one to thirty seconds and reset after a successful hello. Handle desktop `queryActiveTab` messages through `port.onMessage`, query the focused browser window plus its active tab, and return their IDs, bounds, browser kind, and current top-frame document token. Mark non-zero `frameId` events so the desktop can invalidate instead of applying frame-relative geometry as screen geometry.

- [ ] **Step 6: Run extension tests and syntax checks**

Run:

```powershell
node --test browser-extension/tests/content.test.js
node --check browser-extension/background.js
node --check browser-extension/scroll-accumulator.js
node --check browser-extension/content.js
```

Expected: all tests PASS and all syntax checks exit 0.

- [ ] **Step 7: Commit**

```powershell
git add browser-extension
git -c user.name=Codex -c user.email=codex@local commit -m "feat: add Chrome and Edge scroll companion"
```

### Task 8: Overlay integration and browser-session lifecycle

**Files:**
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs`
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserWindowEventMonitor.cs`
- Create: `src/ScreenTranslator.App/Services/ApplicationController.Browser.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs`
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserFollowCoordinatorTests.cs`

- [ ] **Step 1: Write coordinator tests with fake overlay handles**

```csharp
[Fact]
public void Scroll_Moves_Existing_Overlays_Without_Starting_Translation()
{
    var overlay = new FakeOverlayHandle(new DipRect(100, 200, 220, 40));
    var coordinator = BrowserFollowCoordinator.CreateTracked(
        session: BrowserSessionFixtures.Chrome(),
        overlays: [overlay],
        selection: new DipRect(80, 120, 300, 300));

    coordinator.Handle(BrowserMessageFixtures.RootScroll(deltaYCss: 50));

    Assert.Equal(150, overlay.Bounds.Top);
    Assert.Equal(0, coordinator.TranslationRequestCount);
}
```

Add tests that tab invalidation hides overlays and unrelated browser-window events are ignored.

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter BrowserFollowCoordinatorTests
```

Expected: FAIL because the coordinator does not exist.

- [ ] **Step 3: Introduce an overlay handle boundary**

```csharp
public interface ITrackedOverlay
{
    DipRect Bounds { get; }
    void MoveTo(DipRect bounds);
    void SetTrackingVisibility(bool visible);
}
```

Implement it in `TextOverlayWindow`. Preserve the user-visible visibility toggle separately, so tracking cannot accidentally reveal globally hidden overlays.

- [ ] **Step 4: Capture the foreground browser identity**

Before hiding the settings window and opening selection overlays, capture `GetForegroundWindow`, process name, physical window bounds, and monitor scale. Store it in `LastTranslationWork`. Only `chrome.exe` and `msedge.exe` are eligible.

- [ ] **Step 5: Start tracking only after successful overlay display**

Change `ShowOverlay` to return the created `TextOverlayWindow` instances. After translation succeeds, ask the bridge for active metadata matching the captured browser window, create `BrowserTrackingSession`, and attach the returned overlays. Side-panel mode does not create an overlay tracking session.

- [ ] **Step 6: Process events on the Dispatcher**

`BrowserBridgeServer.MessageReceived` calls `BrowserFollowCoordinator.Handle` through `Application.Dispatcher.BeginInvoke`. Coalesce queued scroll messages per `(windowId, tabId, documentToken, targetId)` by summing their deltas before the next Dispatcher render pass. Invalidation hides and disposes the session.

- [ ] **Step 7: Monitor browser-window lifecycle**

`BrowserWindowEventMonitor` installs `SetWinEventHook` for `EVENT_OBJECT_LOCATIONCHANGE`, `EVENT_SYSTEM_MINIMIZESTART`, and `EVENT_OBJECT_DESTROY` on the captured browser process. Same-monitor location changes offset overlays by the window delta; monitor/DPI changes, minimize, and destroy invalidate and hide the tracking session. Keep the native callback delegate rooted until disposal and always call `UnhookWinEvent`.

- [ ] **Step 8: Copy extension into build and publish output**

```xml
<ItemGroup>
  <Content Include="..\..\browser-extension\**\*"
           Link="browser-extension\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest"
           Exclude="..\..\browser-extension\tests\**\*" />
</ItemGroup>
```

- [ ] **Step 9: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter BrowserFollowCoordinatorTests
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: PASS and build output contains `browser-extension\manifest.json`.

- [ ] **Step 10: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Browser src/ScreenTranslator.App/Services/ApplicationController*.cs src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs src/ScreenTranslator.App/ScreenTranslator.App.csproj tests/ScreenTranslator.IntegrationTests/Browser
git -c user.name=Codex -c user.email=codex@local commit -m "feat: move overlays with browser scroll sessions"
```

### Task 9: Browser connection UI and installation handoff

**Files:**
- Create: `src/ScreenTranslator.App/ViewModels/BrowserIntegrationViewModel.cs`
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `README.md`
- Create: `docs/testing/browser-follow-manual-test.md`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserIntegrationViewModelTests.cs`

- [ ] **Step 1: Write view-model state tests**

```csharp
[Fact]
public void Connected_Chrome_Shows_Ready_State()
{
    var vm = new BrowserIntegrationViewModel();
    vm.UpdateConnection(BrowserKind.Chrome, connected: true);
    Assert.Equal("已连接", vm.ChromeStatus);
    Assert.True(vm.IsBrowserFollowingAvailable);
}
```

- [ ] **Step 2: Run test and verify failure**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter BrowserIntegrationViewModelTests
```

Expected: FAIL because the view model does not exist.

- [ ] **Step 3: Implement the status card**

Expose `ChromeStatus`, `EdgeStatus`, `IsEnabled`, `OpenChromeExtensionsCommand`, `OpenEdgeExtensionsCommand`, and `OpenExtensionFolderCommand`. Launch browser pages with explicit executables and open the extension folder through Explorer:

```text
chrome.exe chrome://extensions
msedge.exe edge://extensions
explorer.exe "<publish directory>\browser-extension"
```

If a browser protocol URL cannot be opened, show a concise status message and leave the extension-folder action available.

- [ ] **Step 4: Add the Fluent UI card**

Add a “浏览器译文跟随” setting card to `GeneralPage` with the enable toggle, separate Chrome/Edge status rows, the three actions, and a note that ordinary `http/https` pages are supported while internal pages and built-in PDF are not.

- [ ] **Step 5: Add installation and manual test documentation**

Document the exact load-unpacked steps for both browsers. The manual matrix must cover mouse wheel, touchpad, keyboard scrolling, scrollbar dragging, nested overflow containers, 100%/125% zoom, tab changes, reload, browser close, extension disconnect, and static fallback.

- [ ] **Step 6: Run tests and build**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter BrowserIntegrationViewModelTests
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: PASS and build succeeds.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenTranslator.App/ViewModels/BrowserIntegrationViewModel.cs src/ScreenTranslator.App/Pages/GeneralPage.xaml src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.IntegrationTests/Browser/BrowserIntegrationViewModelTests.cs README.md docs/testing/browser-follow-manual-test.md
git -c user.name=Codex -c user.email=codex@local commit -m "feat: add browser companion setup and status"
```

### Task 10: Full verification and release

**Files:**
- Modify: `eng/publish.ps1`
- Modify: `README.md`
- Update generated output: `dist/ScreenTranslator-win-x64/`

- [ ] **Step 1: Run formatting and placeholder checks**

Run:

```powershell
.\.tools\dotnet\dotnet.exe format ScreenTranslator.sln --verify-no-changes
$forbidden = @('T' + 'BD', 'T' + 'ODO', 'implement' + ' later', 'fill' + ' in')
Get-ChildItem browser-extension -Recurse -File | Select-String -Pattern $forbidden
```

Expected: formatter exits 0 and the placeholder search returns no matches.

- [ ] **Step 2: Run every automated test**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test ScreenTranslator.sln -c Release
node --test browser-extension/tests/content.test.js
node --check browser-extension/background.js
node --check browser-extension/scroll-accumulator.js
node --check browser-extension/content.js
```

Expected: all .NET and Node tests PASS.

- [ ] **Step 3: Publish self-contained output**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File eng/publish.ps1
```

Expected:

```text
dist\ScreenTranslator-win-x64\ScreenTranslator.exe
dist\ScreenTranslator-win-x64\browser-extension\manifest.json
dist\ScreenTranslator-win-x64\README.md
```

- [ ] **Step 4: Perform desktop interaction smoke tests**

Launch the published executable and verify:

1. Change the shortcut to `Ctrl + Shift + G`, restart, and confirm it remains active.
2. Attempt a known occupied shortcut and confirm the old shortcut still works.
3. Use the wheel on every settings page and on a long side-panel translation.
4. Drag and resize the side panel at each screen edge; confirm the footer remains visible.

- [ ] **Step 5: Perform Chrome and Edge follow tests**

Load `dist\ScreenTranslator-win-x64\browser-extension` unpacked in both browsers. On a normal long webpage, create an overlay translation and scroll using wheel, keyboard, scrollbar, and a nested scroll container. Confirm:

- overlays move in the same frame direction as the source;
- overlays outside the original selection are hidden;
- no OCR or DeepSeek status appears during scrolling;
- tab switch, reload, zoom change, and browser close remove old overlays;
- `chrome://extensions`, `edge://extensions`, and built-in PDF fall back to static behavior.

- [ ] **Step 6: Inspect runtime registration and secret safety**

Run:

```powershell
Get-ItemProperty 'HKCU:\Software\Google\Chrome\NativeMessagingHosts\com.screentranslator.browser_bridge'
Get-ItemProperty 'HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\com.screentranslator.browser_bridge'
Get-ChildItem src,tests,browser-extension -Recurse -File | Select-String -Pattern 'sk-[A-Za-z0-9]{16,}'
```

Expected: both registry keys point to the current-user native-host manifest and the secret scan returns no real API key.

- [ ] **Step 7: Commit release artifacts and documentation**

```powershell
git add eng/publish.ps1 README.md docs/testing dist
git -c user.name=Codex -c user.email=codex@local commit -m "release: ship interaction fixes and browser following"
```

## Completion checklist

- [ ] Saved shortcuts survive restart and registration conflicts roll back safely.
- [ ] All setting pages and side-panel text respond to mouse-wheel input.
- [ ] Side-panel header dragging works without stealing browser focus.
- [ ] Long translations scroll while the action footer remains visible.
- [ ] Chrome/Edge ordinary webpages move overlays from local scroll messages only.
- [ ] Scrolling never invokes OCR or DeepSeek.
- [ ] Unsupported pages and disconnected extensions retain static translation.
- [ ] Old overlays disappear on tab, navigation, zoom, and browser-lifecycle invalidation.
- [ ] Release output includes the fixed-ID unpacked extension and setup instructions.
- [ ] .NET and JavaScript test suites pass in Release configuration.
