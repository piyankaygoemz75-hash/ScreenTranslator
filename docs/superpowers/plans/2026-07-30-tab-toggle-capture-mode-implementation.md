# Tab-Switchable Capture Sessions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make normal capture start in single mode, allow `Tab` to switch single/multiple mode inside the selection overlay, stop acquisition with a visible notice when the pending queue reaches five, and keep the first translation bound to the real source window.

**Architecture:** Replace the two divergent capture entry paths with one session orchestrator parameterized by `CaptureMode`. Multi-monitor selection windows share a small observable mode state, while each window retains independent geometry state. Source context is captured only after application-owned surfaces are hidden and the dispatcher has yielded; multiple selections use the existing sequential queue and stop acquisition when an enqueue reports that capacity was reached.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.Mvvm, `System.Threading.Channels`, xUnit, Inno Setup, GitHub Actions.

---

### Task 1: Add shared capture-mode state

**Files:**
- Create: `src/ScreenTranslator.App/ViewModels/CaptureModeState.cs`
- Modify: `src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/ViewModels/CaptureModeStateTests.cs`

- [ ] **Step 1: Write failing mode-state tests**

```csharp
using ScreenTranslator.App.ViewModels;

namespace ScreenTranslator.IntegrationTests.ViewModels;

public sealed class CaptureModeStateTests
{
    [Fact]
    public void Defaults_To_Single_And_Toggles_Both_Ways()
    {
        var state = new CaptureModeState();

        Assert.Equal(CaptureMode.Single, state.Mode);
        Assert.Contains("单条框选", state.InstructionText);

        state.Toggle();
        Assert.Equal(CaptureMode.Multiple, state.Mode);
        Assert.Contains("多条框选", state.InstructionText);

        state.Toggle();
        Assert.Equal(CaptureMode.Single, state.Mode);
    }

    [Fact]
    public void Two_Selection_ViewModels_Observe_The_Same_Mode()
    {
        var state = new CaptureModeState(CaptureMode.Multiple);
        var first = new SelectionOverlayViewModel(state);
        var second = new SelectionOverlayViewModel(state);

        first.ModeState.Toggle();

        Assert.Equal(CaptureMode.Single, second.ModeState.Mode);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter FullyQualifiedName~CaptureModeStateTests
```

Expected: compilation fails because `CaptureModeState` and `CaptureMode` do not exist.

- [ ] **Step 3: Implement the shared state**

Create `CaptureModeState.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenTranslator.App.ViewModels;

public enum CaptureMode
{
    Single,
    Multiple,
}

public sealed class CaptureModeState : ObservableObject
{
    private CaptureMode _mode;

    public CaptureModeState(CaptureMode mode = CaptureMode.Single)
    {
        _mode = mode;
    }

    public CaptureMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(InstructionText));
            }
        }
    }

    public string InstructionText => Mode == CaptureMode.Multiple
        ? "多条框选 · Tab 切换为单条 · Esc 或右键结束"
        : "单条框选 · Tab 切换为多条 · Esc 取消";

    public void Toggle() =>
        Mode = Mode == CaptureMode.Single
            ? CaptureMode.Multiple
            : CaptureMode.Single;
}
```

Change `SelectionOverlayViewModel` to accept and expose a shared mode state:

```csharp
public SelectionOverlayViewModel(CaptureModeState? modeState = null)
{
    ModeState = modeState ?? new CaptureModeState();
}

public CaptureModeState ModeState { get; }
```

Remove the old `IsContinuous`, `InstructionText`, and `CancelText` members.

- [ ] **Step 4: Run the tests and verify they pass**

Run the command from Step 2.

Expected: 2 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.App/ViewModels/CaptureModeState.cs src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs tests/ScreenTranslator.IntegrationTests/ViewModels/CaptureModeStateTests.cs
git commit -m "feat: add shared capture mode state"
```

### Task 2: Handle Tab in every selection overlay

**Files:**
- Modify: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/ViewModels/SelectionOverlayViewModelTests.cs`

- [ ] **Step 1: Write the failing drag-state test**

```csharp
using System.Windows;
using ScreenTranslator.App.ViewModels;

namespace ScreenTranslator.IntegrationTests.ViewModels;

public sealed class SelectionOverlayViewModelTests
{
    [Fact]
    public void Mode_Can_Only_Toggle_While_Not_Dragging()
    {
        var state = new CaptureModeState();
        var viewModel = new SelectionOverlayViewModel(state);

        Assert.True(viewModel.TryToggleMode());
        Assert.Equal(CaptureMode.Multiple, state.Mode);

        viewModel.BeginSelection(new Point(10, 10));
        Assert.False(viewModel.TryToggleMode());
        Assert.Equal(CaptureMode.Multiple, state.Mode);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter FullyQualifiedName~SelectionOverlayViewModelTests
```

Expected: compilation fails because `TryToggleMode` does not exist.

- [ ] **Step 3: Implement safe Tab switching and shared multi-monitor state**

Add to `SelectionOverlayViewModel`:

```csharp
public bool TryToggleMode()
{
    if (IsSelecting)
    {
        return false;
    }

    ModeState.Toggle();
    return true;
}
```

Update the XAML top banner to one binding:

```xml
<TextBlock Text="{Binding ModeState.InstructionText}"
           Foreground="White"
           FontFamily="Segoe UI Variable, Microsoft YaHei UI"
           FontWeight="SemiBold" />
```

Handle `Tab` before `Escape` in `SelectionOverlayWindow.Window_OnKeyDown`:

```csharp
if (e.Key == Key.Tab)
{
    e.Handled = true;
    _viewModel.TryToggleMode();
    return;
}
```

Change `SelectionOverlayWindow.Configure` to stop accepting a `continuous` boolean. Change `SelectRegionAsync` to accept `CaptureMode initialMode`, create one `CaptureModeState`, and pass it to a separate `SelectionOverlayViewModel` for each monitor:

```csharp
var modeState = new CaptureModeState(initialMode);
var viewModel = new SelectionOverlayViewModel(modeState);
var window = new SelectionOverlayWindow(viewModel);
```

Extend `SelectedRegion` to include `CaptureMode Mode`, populated from `modeState.Mode` when selection completes.

- [ ] **Step 4: Run targeted tests and compile XAML**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~CaptureModeStateTests|FullyQualifiedName~SelectionOverlayViewModelTests"
.\eng\dotnet.ps1 build ScreenTranslator.sln -c Release
```

Expected: 3 targeted tests pass; build succeeds with 0 warnings and 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml.cs src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.IntegrationTests/ViewModels/SelectionOverlayViewModelTests.cs
git commit -m "feat: switch capture mode with tab"
```

### Task 3: Report the pending count reached by each enqueue

**Files:**
- Modify: `src/ScreenTranslator.Core/Sessions/SequentialWorkQueue.cs`
- Modify: `tests/ScreenTranslator.Core.Tests/Sessions/SequentialWorkQueueTests.cs`

- [ ] **Step 1: Write the failing capacity-result test**

```csharp
[Fact]
public async Task Enqueue_Returns_The_Pending_Count_At_Acceptance()
{
    var release = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously);
    await using var queue = new SequentialWorkQueue<int>(
        2,
        async (_, cancellationToken) =>
            await release.Task.WaitAsync(cancellationToken));

    var firstPending = await queue.EnqueueAsync(1);
    var secondPending = await queue.EnqueueAsync(2);

    Assert.Equal(1, firstPending);
    Assert.Equal(2, secondPending);

    release.TrySetResult();
    await queue.CompleteAsync();
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.Core.Tests\ScreenTranslator.Core.Tests.csproj -c Release --filter FullyQualifiedName~Enqueue_Returns_The_Pending_Count
```

Expected: compilation fails because `EnqueueAsync` does not return an integer.

- [ ] **Step 3: Return the precise enqueue count**

Change the signature and successful return path:

```csharp
public async ValueTask<int> EnqueueAsync(
    T item,
    CancellationToken cancellationToken = default)
{
    // Existing validation, slot wait, increment, notification and write.
    await _channel.Writer.WriteAsync(item, cancellationToken);
    return pending;
}
```

Keep the existing rollback behavior when writing fails. Existing callers may ignore the returned integer.

- [ ] **Step 4: Run all queue tests**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.Core.Tests\ScreenTranslator.Core.Tests.csproj -c Release --filter FullyQualifiedName~SequentialWorkQueueTests
```

Expected: all queue tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.Core/Sessions/SequentialWorkQueue.cs tests/ScreenTranslator.Core.Tests/Sessions/SequentialWorkQueueTests.cs
git commit -m "feat: report queue depth on enqueue"
```

### Task 4: Make source-window capture ordering testable

**Files:**
- Create: `src/ScreenTranslator.App/Services/Capture/CaptureContextSequencer.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/Capture/CaptureContextSequencerTests.cs`

- [ ] **Step 1: Write the failing ordering test**

```csharp
using ScreenTranslator.App.Services.Capture;

namespace ScreenTranslator.IntegrationTests.Capture;

public sealed class CaptureContextSequencerTests
{
    [Fact]
    public async Task Hides_And_Yields_Before_Capturing_Context()
    {
        var calls = new List<string>();

        var result = await CaptureContextSequencer.CaptureAsync(
            () => calls.Add("hide"),
            () =>
            {
                calls.Add("yield");
                return Task.CompletedTask;
            },
            () =>
            {
                calls.Add("capture");
                return 42;
            });

        Assert.Equal(42, result);
        Assert.Equal(["hide", "yield", "capture"], calls);
    }
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
.\eng\dotnet.ps1 test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter FullyQualifiedName~CaptureContextSequencerTests
```

Expected: compilation fails because `CaptureContextSequencer` does not exist.

- [ ] **Step 3: Implement the sequencer**

```csharp
namespace ScreenTranslator.App.Services.Capture;

public static class CaptureContextSequencer
{
    public static async Task<T> CaptureAsync<T>(
        Action hideSurfaces,
        Func<Task> yieldUi,
        Func<T> captureContext)
    {
        ArgumentNullException.ThrowIfNull(hideSurfaces);
        ArgumentNullException.ThrowIfNull(yieldUi);
        ArgumentNullException.ThrowIfNull(captureContext);

        hideSurfaces();
        await yieldUi();
        return captureContext();
    }
}
```

- [ ] **Step 4: Run the test and verify it passes**

Run the command from Step 2.

Expected: 1 test passes.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Capture/CaptureContextSequencer.cs tests/ScreenTranslator.IntegrationTests/Capture/CaptureContextSequencerTests.cs
git commit -m "fix: capture source after hiding app surfaces"
```

### Task 5: Unify single and multiple capture orchestration

**Files:**
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Services/Tray/TrayIconService.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/ViewModels/GeneralSettingsViewModelTests.cs`

- [ ] **Step 1: Preserve entry-mode expectations in tests**

Add controller-independent assertions around the commands/events already used by the controller:

```csharp
[Fact]
public void Normal_And_Continuous_Commands_Remain_Distinct_Entries()
{
    var viewModel = new GeneralSettingsViewModel();
    var normal = 0;
    var multiple = 0;
    viewModel.StartCaptureRequested += (_, _) => normal++;
    viewModel.StartContinuousCaptureRequested += (_, _) => multiple++;

    viewModel.StartCaptureCommand.Execute(null);
    viewModel.StartContinuousCaptureCommand.Execute(null);

    Assert.Equal(1, normal);
    Assert.Equal(1, multiple);
}
```

- [ ] **Step 2: Replace divergent entry methods with one session method**

Route entries as follows:

```csharp
private void OnCaptureRequested(object? sender, EventArgs e) =>
    _ = RunCaptureSessionAsync(CaptureMode.Single);

private void OnContinuousCaptureRequested(object? sender, EventArgs e) =>
    _ = RunCaptureSessionAsync(CaptureMode.Multiple);
```

Implement `RunCaptureSessionAsync(CaptureMode initialMode)` with these exact state transitions:

1. Acquire `_captureGate`; reject a concurrent session.
2. Check the DeepSeek key.
3. Close old result windows only for an initially single session.
4. Acquire a selection using the current mode.
5. If it is single and no multiple item has been queued, process it as the ordinary one-shot result and finish.
6. Otherwise lazily create `SequentialWorkQueue<TranslationCaptureWorkItem>(5, ProcessContinuousWorkItemAsync)`, enqueue the item, and preserve all results.
7. If the returned enqueue count is 5, set the queue-limit end reason and stop acquisition without opening another overlay.
8. If a multiple session returns a single-mode selection, enqueue it as the last item and stop acquisition.
9. Drain the queue in `finally`; never cancel already accepted work when the user ends acquisition.

Extract one `AcquireCaptureWorkItemAsync(CaptureMode mode, CancellationToken token)` method. Its preparation must use:

```csharp
var context = await CaptureContextSequencer.CaptureAsync(
    () =>
    {
        _mainWindow?.Hide();
        SetResultWindowsCaptureVisibility(visible: false);
    },
    async () =>
    {
        await _application.Dispatcher.InvokeAsync(
            () => { },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    },
    () => new
    {
        SourceWindowHandle =
            ForegroundWindowMonitor.CaptureForegroundRootWindow(),
        CapturedBrowser =
            BrowserWindowEventMonitor.CaptureForegroundBrowser(),
    });
```

Capture the screenshot only after this block. Restore result visibility in a `finally` block.

- [ ] **Step 3: Add a visible queue-limit notification**

Add to `TrayIconService`:

```csharp
public void ShowInformation(string title, string message)
{
    if (_disposed)
    {
        return;
    }

    _notifyIcon.ShowBalloonTip(
        4000,
        title,
        message,
        Forms.ToolTipIcon.Info);
}
```

When the enqueue return value reaches capacity:

```csharp
limitReached = true;
MainWindow.StatusText =
    "处理队列已满，本轮框选已停止；剩余译文将继续完成";
_tray.ShowInformation(
    "多条框选已停止",
    "处理队列已满，剩余译文将继续完成。");
break;
```

- [ ] **Step 4: Build and run capture-related tests**

Run:

```powershell
.\eng\dotnet.ps1 build ScreenTranslator.sln -c Release
.\eng\dotnet.ps1 test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~Capture|FullyQualifiedName~Selection|FullyQualifiedName~GeneralSettingsViewModelTests"
```

Expected: build succeeds with 0 warnings and 0 errors; all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.App/Services/ApplicationController.cs src/ScreenTranslator.App/Services/Tray/TrayIconService.cs tests/ScreenTranslator.IntegrationTests/ViewModels/GeneralSettingsViewModelTests.cs
git commit -m "feat: unify single and multiple capture sessions"
```

### Task 6: Document, version, verify, and release

**Files:**
- Modify: `README.md`
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Modify: `eng/build-release.ps1`

- [ ] **Step 1: Update user-facing documentation and version**

Document that the ordinary shortcut defaults to single mode, `Tab` toggles multiple mode, total selections are unlimited, and only five unfinished items may accumulate. Increment the application and release-script version from `0.2.1` to `0.2.2`.

- [ ] **Step 2: Run the complete verification suite**

Run:

```powershell
.\eng\dotnet.ps1 build ScreenTranslator.sln -c Release
.\eng\dotnet.ps1 test ScreenTranslator.sln -c Release --no-build
node --test browser-extension/tests/content.test.js browser-extension/tests/document-state.test.js
git diff --check
```

Expected: 0 build warnings/errors, all .NET tests pass, 11 extension tests pass, and `git diff --check` produces no output.

- [ ] **Step 3: Build local release packages**

Run:

```powershell
.\eng\build-release.ps1 -Version 0.2.2
```

Expected: self-contained publish directory, portable ZIP, and extension ZIP are created under `artifacts\release\v0.2.2`.

- [ ] **Step 4: Commit and push**

```powershell
git add README.md src/ScreenTranslator.App/ScreenTranslator.App.csproj eng/build-release.ps1
git commit -m "release: prepare version 0.2.2"
git -c http.proxy= -c https.proxy= -c http.sslBackend=openssl push origin main
```

Wait for the main Windows CI run and require success, including installer compile and silent install/uninstall verification.

- [ ] **Step 5: Tag and verify the GitHub Release**

```powershell
git tag -a v0.2.2 -m "ScreenTranslator v0.2.2"
git -c http.proxy= -c https.proxy= -c http.sslBackend=openssl push origin v0.2.2
gh release view v0.2.2 --repo piyankaygoemz75-hash/ScreenTranslator --json url,assets,isDraft,isPrerelease
```

Expected: a public, non-draft, non-prerelease v0.2.2 release with installer, portable ZIP, browser extension ZIP, and `SHA256SUMS.txt`.
