# Overlay Clear and Focus Visibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add “clear this / clear all” context-menu actions to original-position translation overlays and automatically hide overlays outside their captured source window while restoring valid overlays when the source returns to the foreground.

**Architecture:** `TextOverlayWindow` owns the final composition of user, source-focus, browser-tracking, and context-menu-interaction visibility. A WinEvent-backed `ForegroundWindowMonitor` publishes normalized foreground root-window handles; a small `OverlayFocusCoordinator` applies those changes to the current overlay group. `ApplicationController` captures the source handle before selection, wires clear actions, and removes closed overlays from both focus and browser-follow coordinators.

**Tech Stack:** .NET 8, C# 12, WPF, WPF-UI, CommunityToolkit.Mvvm, Win32 `SetWinEventHook`, xUnit

---

## File Structure

- Create `src/ScreenTranslator.App/Services/Overlays/OverlayVisibilityState.cs`: pure visibility-state composition.
- Create `src/ScreenTranslator.App/Services/Overlays/OverlayFocusCoordinator.cs`: source-window identity and overlay-group focus updates.
- Create `src/ScreenTranslator.App/Services/Overlays/ForegroundWindowMonitor.cs`: WinEvent hook and root-window normalization.
- Modify `src/ScreenTranslator.App/ViewModels/TranslationResultViewModel.cs`: expose clear-all command/event.
- Modify `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml`: add context menu.
- Modify `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs`: apply composed visibility and expose overlay clear/focus methods.
- Modify `src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs`: allow a closed overlay to be removed.
- Modify `src/ScreenTranslator.App/Services/ApplicationController.cs`: capture source window, wire clear events, manage overlay/focus lifecycle.
- Modify `src/ScreenTranslator.App/Services/ApplicationController.Browser.cs`: remove individual overlays from browser follow without invalidating the others.
- Create `tests/ScreenTranslator.IntegrationTests/Overlays/OverlayVisibilityStateTests.cs`: three-state and context-menu visibility tests.
- Create `tests/ScreenTranslator.IntegrationTests/Overlays/OverlayFocusCoordinatorTests.cs`: focus hide/restore and removal tests.
- Modify `tests/ScreenTranslator.IntegrationTests/Browser/BrowserFollowCoordinatorTests.cs`: removed overlays no longer move.
- Modify `tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs`: reuse its single WPF `Application` instance to verify overlay commands and visibility without creating a second `Application` in the test process.

### Task 1: Context menu commands and composed overlay visibility

**Files:**
- Create: `src/ScreenTranslator.App/Services/Overlays/OverlayVisibilityState.cs`
- Modify: `src/ScreenTranslator.App/ViewModels/TranslationResultViewModel.cs`
- Modify: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/Overlays/OverlayVisibilityStateTests.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs`

- [ ] **Step 1: Write failing pure-state tests**

Create `OverlayVisibilityStateTests.cs`:

```csharp
using ScreenTranslator.App.Services.Overlays;

namespace ScreenTranslator.IntegrationTests.Overlays;

public sealed class OverlayVisibilityStateTests
{
    [Theory]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, true, true)]
    [InlineData(false, false, true, true, false)]
    [InlineData(true, false, false, true, false)]
    public void ShouldShow_Composes_All_Visibility_Inputs(
        bool userVisible,
        bool sourceWindowActive,
        bool trackingVisible,
        bool contextMenuOpen,
        bool expected)
    {
        var state = new OverlayVisibilityState
        {
            UserVisible = userVisible,
            SourceWindowActive = sourceWindowActive,
            TrackingVisible = trackingVisible,
            ContextMenuOpen = contextMenuOpen,
        };

        Assert.Equal(expected, state.ShouldShow);
    }
}
```

- [ ] **Step 2: Run the pure-state test and verify it fails**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~OverlayVisibilityStateTests
```

Expected: compilation fails because `OverlayVisibilityState` does not exist.

- [ ] **Step 3: Implement the visibility state**

Create `OverlayVisibilityState.cs`:

```csharp
namespace ScreenTranslator.App.Services.Overlays;

public sealed class OverlayVisibilityState
{
    public bool UserVisible { get; set; } = true;

    public bool SourceWindowActive { get; set; } = true;

    public bool TrackingVisible { get; set; } = true;

    public bool ContextMenuOpen { get; set; }

    public bool ShouldShow =>
        UserVisible
        && TrackingVisible
        && (SourceWindowActive || ContextMenuOpen);
}
```

- [ ] **Step 4: Add the clear-all command and event**

In `TranslationResultViewModel`, initialize and expose:

```csharp
ClearAllCommand = new RelayCommand(
    () => ClearAllRequested?.Invoke(this, EventArgs.Empty));
```

```csharp
public RelayCommand ClearAllCommand { get; }

public event EventHandler? ClearAllRequested;
```

Keep `CloseCommand` as the single-overlay clear command so the existing action-bar Close button and context menu share one path.

- [ ] **Step 5: Add the context menu**

Set `RootCard.ContextMenu` in `TextOverlayWindow.xaml`:

```xml
<Border.ContextMenu>
    <ContextMenu DataContext="{Binding PlacementTarget.DataContext,
                                       RelativeSource={RelativeSource Self}}"
                 Opened="OverlayContextMenu_OnOpened"
                 Closed="OverlayContextMenu_OnClosed">
        <MenuItem Header="清除此条译文"
                  Command="{Binding CloseCommand}" />
        <MenuItem Header="清除全部译文"
                  Command="{Binding ClearAllCommand}" />
    </ContextMenu>
</Border.ContextMenu>
```

- [ ] **Step 6: Route all overlay visibility through one method**

In `TextOverlayWindow.xaml.cs`, add:

```csharp
private readonly OverlayVisibilityState _visibility = new();
private bool _closed;

public void SetUserVisibility(bool visible)
{
    _visibility.UserVisible = visible;
    ApplyVisibility();
}

public void SetSourceWindowActive(bool active)
{
    _visibility.SourceWindowActive = active;
    ApplyVisibility();
}

public void SetTrackingVisibility(bool visible)
{
    _visibility.TrackingVisible = visible;
    ApplyVisibility();
}

private void OverlayContextMenu_OnOpened(object sender, RoutedEventArgs e)
{
    _visibility.ContextMenuOpen = true;
    ApplyVisibility();
}

private void OverlayContextMenu_OnClosed(object sender, RoutedEventArgs e)
{
    _visibility.ContextMenuOpen = false;
    ApplyVisibility();
}

private void ApplyVisibility()
{
    if (_closed)
    {
        return;
    }

    if (_visibility.ShouldShow)
    {
        RootCard.Visibility = Visibility.Visible;
        RootCard.IsHitTestVisible = ActionBar.Visibility == Visibility.Visible;
        if (IsLoaded && !IsVisible)
        {
            Show();
        }
    }
    else
    {
        RootCard.IsHitTestVisible = false;
        Hide();
    }
}
```

Set `_closed = true` at the beginning of `OnClosed`. Remove the old `RootCard.Visibility` implementation from `SetTrackingVisibility`.

- [ ] **Step 7: Extend the existing single-Application window integration test**

Extend the existing `MainWindowLifecycleTests.Close_Hides_To_Tray_And_Shutdown_Closes_Window` body before `application.Shutdown()`. Reuse the `App` already created by that test because WPF only permits one `Application` instance per test process. Show an overlay, call `SetSourceWindowActive(false)`, assert `IsVisible` is false, call `SetSourceWindowActive(true)`, assert true, then execute `ViewModel.ClearAllCommand` and assert one `ClearAllRequested` event.

```csharp
var overlayViewModel = new TranslationResultViewModel();
var overlay = new TextOverlayWindow(overlayViewModel);
var clearAllCount = 0;
overlayViewModel.ClearAllRequested += (_, _) => clearAllCount++;

overlay.Show();
overlay.SetSourceWindowActive(false);
Assert.False(overlay.IsVisible);

overlay.SetSourceWindowActive(true);
Assert.True(overlay.IsVisible);

overlayViewModel.ClearAllCommand.Execute(null);
Assert.Equal(1, clearAllCount);
overlay.Close();
```

- [ ] **Step 8: Run the Task 1 tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~OverlayVisibilityStateTests|FullyQualifiedName~MainWindowLifecycleTests"
```

Expected: all Task 1 tests pass.

- [ ] **Step 9: Commit Task 1**

```powershell
git add src/ScreenTranslator.App/Services/Overlays/OverlayVisibilityState.cs src/ScreenTranslator.App/ViewModels/TranslationResultViewModel.cs src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs tests/ScreenTranslator.IntegrationTests/Overlays/OverlayVisibilityStateTests.cs tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs
git commit -m "feat: add overlay clear menu and visibility state"
```

### Task 2: Remove closed overlays from browser following

**Files:**
- Modify: `src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserFollowCoordinatorTests.cs`

- [ ] **Step 1: Write the failing removal test**

Add:

```csharp
[Fact]
public void Removed_Overlay_No_Longer_Receives_Scroll_Updates()
{
    var removed = new FakeOverlay(new DipRect(100, 200, 220, 40));
    var remaining = new FakeOverlay(new DipRect(100, 260, 220, 40));
    var coordinator = CreateCoordinator(removed, remaining);

    Assert.True(coordinator.RemoveOverlay(removed));
    coordinator.Handle(CreateScroll(deltaYCss: 50));

    Assert.Equal(200, removed.TrackingBounds.Y);
    Assert.Equal(210, remaining.TrackingBounds.Y);
    Assert.Equal(1, coordinator.OverlayCount);
}
```

Change the test helper signature to:

```csharp
private static BrowserFollowCoordinator CreateCoordinator(
    params ITrackedOverlay[] overlays)
```

- [ ] **Step 2: Run the removal test and verify it fails**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~Removed_Overlay_No_Longer
```

Expected: compilation fails because `RemoveOverlay` and `OverlayCount` do not exist.

- [ ] **Step 3: Implement removable tracking**

In `BrowserFollowCoordinator`:

```csharp
private readonly List<ITrackedOverlay> _overlays;
```

Copy constructor input:

```csharp
_overlays = overlays.ToList();
```

Expose:

```csharp
public int OverlayCount => _overlays.Count;

public bool RemoveOverlay(ITrackedOverlay overlay)
{
    ArgumentNullException.ThrowIfNull(overlay);
    return _overlays.Remove(overlay);
}
```

Iterate snapshots in move/invalidate paths to prevent collection modification during callbacks:

```csharp
foreach (var overlay in _overlays.ToArray())
```

- [ ] **Step 4: Run all browser coordinator tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~BrowserFollowCoordinatorTests
```

Expected: all browser coordinator tests pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs tests/ScreenTranslator.IntegrationTests/Browser/BrowserFollowCoordinatorTests.cs
git commit -m "feat: remove closed overlays from browser following"
```

### Task 3: Foreground WinEvent monitoring and focus coordination

**Files:**
- Create: `src/ScreenTranslator.App/Services/Overlays/ForegroundWindowMonitor.cs`
- Create: `src/ScreenTranslator.App/Services/Overlays/OverlayFocusCoordinator.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/Overlays/OverlayFocusCoordinatorTests.cs`

- [ ] **Step 1: Write failing focus-coordinator tests**

Use a fake target:

```csharp
private sealed class FakeTarget : IOverlayFocusTarget
{
    public List<bool> States { get; } = [];

    public void SetSourceWindowActive(bool active) => States.Add(active);
}
```

Test:

```csharp
[Fact]
public void Foreground_Changes_Hide_And_Restore_All_Remaining_Overlays()
{
    var first = new FakeTarget();
    var second = new FakeTarget();
    var coordinator = new OverlayFocusCoordinator(
        new IntPtr(100),
        [first, second]);

    coordinator.HandleForegroundChanged(new IntPtr(200));
    coordinator.Remove(first);
    coordinator.HandleForegroundChanged(new IntPtr(100));

    Assert.Equal([false], first.States);
    Assert.Equal([false, true], second.States);
    Assert.Equal(1, coordinator.Count);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~OverlayFocusCoordinatorTests
```

Expected: compilation fails because the focus types do not exist.

- [ ] **Step 3: Implement the focus coordinator**

Create:

```csharp
namespace ScreenTranslator.App.Services.Overlays;

public interface IOverlayFocusTarget
{
    void SetSourceWindowActive(bool active);
}

public sealed class OverlayFocusCoordinator
{
    private readonly IntPtr _sourceWindow;
    private readonly List<IOverlayFocusTarget> _targets;

    public OverlayFocusCoordinator(
        IntPtr sourceWindow,
        IEnumerable<IOverlayFocusTarget> targets)
    {
        if (sourceWindow == IntPtr.Zero)
        {
            throw new ArgumentException("Source window is required.", nameof(sourceWindow));
        }

        _sourceWindow = sourceWindow;
        _targets = targets.ToList();
    }

    public int Count => _targets.Count;

    public bool Remove(IOverlayFocusTarget target) => _targets.Remove(target);

    public void HandleForegroundChanged(IntPtr foregroundWindow)
    {
        var active = foregroundWindow == _sourceWindow;
        foreach (var target in _targets.ToArray())
        {
            target.SetSourceWindowActive(active);
        }
    }
}
```

Make `TextOverlayWindow` implement `IOverlayFocusTarget`.

- [ ] **Step 4: Implement the WinEvent monitor**

Create `ForegroundWindowMonitor` with:

```csharp
public sealed class ForegroundWindowChangedEventArgs(IntPtr windowHandle) : EventArgs
{
    public IntPtr WindowHandle { get; } = windowHandle;
}
```

Use:

```csharp
private const uint EventSystemForeground = 0x0003;
private const uint WineventOutofcontext = 0x0000;
private const uint GaRoot = 2;
```

Expose:

```csharp
public static IntPtr CaptureForegroundRootWindow() =>
    NormalizeRootWindow(GetForegroundWindow());

public event EventHandler<ForegroundWindowChangedEventArgs>? Changed;
```

Register one hook:

```csharp
_hook = SetWinEventHook(
    EventSystemForeground,
    EventSystemForeground,
    IntPtr.Zero,
    _callback,
    0,
    0,
    WineventOutofcontext);
```

Normalize callback handles with:

```csharp
private static IntPtr NormalizeRootWindow(IntPtr handle) =>
    handle == IntPtr.Zero ? IntPtr.Zero : GetAncestor(handle, GaRoot);
```

Throw when hook registration returns zero; make `Dispose` idempotently call `UnhookWinEvent`.

- [ ] **Step 5: Run focus tests and build the App**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~OverlayFocusCoordinatorTests
.\.tools\dotnet\dotnet.exe build src\ScreenTranslator.App\ScreenTranslator.App.csproj -c Release --no-restore
```

Expected: focus tests pass; App builds with zero warnings and errors.

- [ ] **Step 6: Commit Task 3**

```powershell
git add src/ScreenTranslator.App/Services/Overlays tests/ScreenTranslator.IntegrationTests/Overlays src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs
git commit -m "feat: track overlay source window focus"
```

### Task 4: Wire overlay lifecycle into ApplicationController

**Files:**
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.Browser.cs`
- Modify: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs`

- [ ] **Step 1: Capture the source root window before hiding ScreenTranslator**

At the beginning of `CaptureAndTranslateAsync`, before `CloseResultWindows()`:

```csharp
var sourceWindowHandle =
    ForegroundWindowMonitor.CaptureForegroundRootWindow();
var capturedBrowser =
    BrowserWindowEventMonitor.CaptureForegroundBrowser();
```

Extend `LastTranslationWork`:

```csharp
private sealed record LastTranslationWork(
    ScreenMonitor Monitor,
    PixelRect AbsoluteSelection,
    IReadOnlyList<OcrBlock> Blocks,
    CapturedBrowserWindow? CapturedBrowser,
    IntPtr SourceWindowHandle);
```

Pass `sourceWindowHandle` when creating `_lastWork`.

- [ ] **Step 2: Add controller overlay-focus fields**

```csharp
private readonly List<TextOverlayWindow> _activeTextOverlays = [];
private ForegroundWindowMonitor? _foregroundWindowMonitor;
private OverlayFocusCoordinator? _overlayFocusCoordinator;
```

- [ ] **Step 3: Start focus tracking after showing overlays**

At the end of `ShowTextOverlays`, call:

```csharp
StartOverlayFocusTracking(work.SourceWindowHandle, overlays);
```

Implement:

```csharp
private void StartOverlayFocusTracking(
    IntPtr sourceWindow,
    IReadOnlyList<TextOverlayWindow> overlays)
{
    StopOverlayFocusTracking();
    _activeTextOverlays.AddRange(overlays);
    if (sourceWindow == IntPtr.Zero || overlays.Count == 0)
    {
        return;
    }

    _overlayFocusCoordinator = new OverlayFocusCoordinator(
        sourceWindow,
        overlays);
    _foregroundWindowMonitor = new ForegroundWindowMonitor();
    _foregroundWindowMonitor.Changed += OnForegroundWindowChanged;
    _overlayFocusCoordinator.HandleForegroundChanged(
        ForegroundWindowMonitor.CaptureForegroundRootWindow());
}

private void OnForegroundWindowChanged(
    object? sender,
    ForegroundWindowChangedEventArgs e)
{
    _application.Dispatcher.BeginInvoke(() =>
        _overlayFocusCoordinator?.HandleForegroundChanged(e.WindowHandle));
}
```

- [ ] **Step 4: Wire clear-current and clear-all**

For each created overlay:

```csharp
viewModel.ClearAllRequested += OnClearAllOverlaysRequested;
overlay.Closed += OnTextOverlayClosed;
```

Implement:

```csharp
private void OnClearAllOverlaysRequested(object? sender, EventArgs e) =>
    CloseResultWindows();

private void OnTextOverlayClosed(object? sender, EventArgs e)
{
    if (sender is not TextOverlayWindow overlay)
    {
        return;
    }

    overlay.Closed -= OnTextOverlayClosed;
    overlay.ViewModel.ClearAllRequested -= OnClearAllOverlaysRequested;
    _activeTextOverlays.Remove(overlay);
    _overlayFocusCoordinator?.Remove(overlay);
    RemoveBrowserTrackedOverlay(overlay);

    if (_activeTextOverlays.Count == 0)
    {
        StopOverlayFocusTracking();
        StopBrowserFollowing(hideOverlays: false);
    }
}
```

- [ ] **Step 5: Remove one overlay from browser following**

In `ApplicationController.Browser.cs`:

```csharp
private void RemoveBrowserTrackedOverlay(TextOverlayWindow overlay)
{
    if (_browserFollowCoordinator is null)
    {
        return;
    }

    _browserFollowCoordinator.RemoveOverlay(overlay);
    if (_browserFollowCoordinator.OverlayCount == 0)
    {
        StopBrowserFollowing(hideOverlays: false);
    }
}
```

- [ ] **Step 6: Stop focus tracking during group close and shutdown**

Add:

```csharp
private void StopOverlayFocusTracking()
{
    if (_foregroundWindowMonitor is not null)
    {
        _foregroundWindowMonitor.Changed -= OnForegroundWindowChanged;
        _foregroundWindowMonitor.Dispose();
        _foregroundWindowMonitor = null;
    }

    _overlayFocusCoordinator = null;
    _activeTextOverlays.Clear();
}
```

Call it at the beginning of `CloseResultWindows` and from `Dispose`.

- [ ] **Step 7: Make tray visibility update overlay state**

Replace direct Show/Hide for text overlays in `ToggleOverlays`:

```csharp
if (window is TextOverlayWindow overlay)
{
    overlay.SetUserVisibility(_overlaysVisible);
}
else if (_overlaysVisible)
{
    window.Show();
}
else
{
    window.Hide();
}
```

- [ ] **Step 8: Add group-interaction assertions**

Extend the existing `MainWindowLifecycleTests` single-`Application` body to create two overlay windows, remove one via `CloseCommand`, invoke foreground changes through `OverlayFocusCoordinator`, and assert the remaining window restores while the closed window stays closed. Invoke `ClearAllCommand` and assert the shared event fires once.

- [ ] **Step 9: Run all focused tests**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter "FullyQualifiedName~Overlay|FullyQualifiedName~BrowserFollowCoordinatorTests|FullyQualifiedName~MainWindowLifecycleTests"
```

Expected: all overlay and browser-follow tests pass.

- [ ] **Step 10: Commit Task 4**

```powershell
git add src/ScreenTranslator.App/Services/ApplicationController.cs src/ScreenTranslator.App/Services/ApplicationController.Browser.cs src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs
git commit -m "feat: wire overlay clearing and focus visibility"
```

### Task 5: Regression, secret scan, and release package

**Files:**
- Modify: `README.md`
- Output: `artifacts/ScreenTranslator-win-x64-overlay-focus-fix/`
- Output: `artifacts/ScreenTranslator-win-x64-overlay-focus-fix.zip`

- [ ] **Step 1: Update user documentation**

Add to README feature and usage sections:

```markdown
- 原位覆盖译文支持右键清除此条或清除全部。
- 覆盖译文只在原始来源窗口位于前台时显示，切回后恢复有效译文。
```

- [ ] **Step 2: Run formatting verification**

Run `dotnet format --verify-no-changes --include` for every changed C# file.

Expected: exit code 0.

- [ ] **Step 3: Run the complete regression suite**

Run:

```powershell
.\.tools\dotnet\dotnet.exe build src\ScreenTranslator.App\ScreenTranslator.App.csproj -c Release --no-restore
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.Core.Tests\ScreenTranslator.Core.Tests.csproj -c Release --no-restore
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore
node --test browser-extension\tests\content.test.js
node --check browser-extension\background.js
node --check browser-extension\content.js
node --check browser-extension\scroll-accumulator.js
git diff --check
```

Expected: App has zero warnings/errors; all Core, Integration, and extension tests pass; `git diff --check` exits 0.

- [ ] **Step 4: Scan tracked and untracked source for API keys**

Run a filename-only PowerShell scan using pattern `sk-[A-Za-z0-9]{16,}` while excluding `bin`, `obj`, `.tools`, and `artifacts`.

Expected: no matching paths.

- [ ] **Step 5: Publish a new self-contained package**

Run:

```powershell
.\.tools\dotnet\dotnet.exe publish src\ScreenTranslator.App\ScreenTranslator.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --no-restore -o artifacts\ScreenTranslator-win-x64-overlay-focus-fix
Compress-Archive -Path artifacts\ScreenTranslator-win-x64-overlay-focus-fix\* -DestinationPath artifacts\ScreenTranslator-win-x64-overlay-focus-fix.zip -CompressionLevel Optimal
Get-FileHash -Algorithm SHA256 artifacts\ScreenTranslator-win-x64-overlay-focus-fix.zip
```

Expected: executable and `browser-extension` folder are present; ZIP hash is printed.

- [ ] **Step 6: Commit Task 5**

```powershell
git add README.md src tests
git commit -m "docs: document overlay clear and focus behavior"
```

## Plan Self-Review

- Spec coverage: right-click single/all clear, focus hide/restore, browser invalidation precedence, tray visibility composition, individual browser-follow removal, non-UI WinEvent callbacks, and final packaging are all assigned to explicit tasks.
- Placeholder scan: no placeholder markers or unspecified error-handling steps remain.
- Type consistency: `OverlayVisibilityState`, `IOverlayFocusTarget`, `OverlayFocusCoordinator`, `ForegroundWindowMonitor`, `RemoveOverlay`, `OverlayCount`, `SetUserVisibility`, and `SetSourceWindowActive` use the same names and signatures throughout the plan.
