# Continuous Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an explicit continuous capture mode that immediately accepts the next screen region, processes at most five queued items sequentially, preserves completed results, and stops acquisition with Escape without discarding queued work.

**Architecture:** Extract a reusable bounded sequential work queue into Core, then split the application controller's current capture-and-translate method into acquisition and processing stages. A continuous session owns cancellation for acquisition, a capacity-five queue, and result groups; one-shot capture continues to use the same processing method without changing its replace-old-result behavior.

**Tech Stack:** C# 12, .NET 8, WPF, `System.Threading.Channels`, xUnit

---

### Task 1: Bounded sequential queue

**Files:**
- Create: `src/ScreenTranslator.Core/Sessions/SequentialWorkQueue.cs`
- Create: `tests/ScreenTranslator.Core.Tests/Sessions/SequentialWorkQueueTests.cs`

- [ ] **Step 1: Write queue tests**

Cover FIFO processing, a capacity of five, completion after the writer closes, and isolation of one item failure:

```csharp
[Fact]
public async Task Processes_Items_In_FIFO_Order()
{
    var processed = new List<int>();
    await using var queue = new SequentialWorkQueue<int>(
        capacity: 5,
        async (item, _) =>
        {
            processed.Add(item);
            await Task.Yield();
        });

    await queue.EnqueueAsync(1);
    await queue.EnqueueAsync(2);
    await queue.EnqueueAsync(3);
    await queue.CompleteAsync();

    Assert.Equal([1, 2, 3], processed);
}
```

- [ ] **Step 2: Run the tests and verify failure**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 test tests\ScreenTranslator.Core.Tests --no-restore --filter SequentialWorkQueueTests
```

Expected: compilation fails because `SequentialWorkQueue<T>` does not exist.

- [ ] **Step 3: Implement the queue**

Implement a bounded `Channel<T>` with `BoundedChannelFullMode.Wait`, one consumer task, an `ItemFailed` event, `PendingCount`, `EnqueueAsync`, `CompleteAsync`, and `DisposeAsync`. The consumer must catch per-item exceptions and continue unless cancellation was requested.

- [ ] **Step 4: Run the focused tests**

Expected: all `SequentialWorkQueueTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.Core/Sessions/SequentialWorkQueue.cs tests/ScreenTranslator.Core.Tests/Sessions/SequentialWorkQueueTests.cs
git commit -m "feat: add bounded sequential work queue"
```

### Task 2: Continuous mode commands and selection cancellation reason

**Files:**
- Modify: `src/ScreenTranslator.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/ScreenTranslator.App/Services/Tray/TrayIconService.cs`
- Modify: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml.cs`
- Modify: `src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Windows/MainWindowLifecycleTests.cs`

- [ ] **Step 1: Add failing command tests**

Verify that `StartContinuousCaptureCommand` raises `StartContinuousCaptureRequested`, becomes unavailable while a continuous session is running, and that the existing one-shot command remains independent.

- [ ] **Step 2: Add view-model state**

Add:

```csharp
public RelayCommand StartContinuousCaptureCommand { get; }
public event EventHandler? StartContinuousCaptureRequested;
public bool IsContinuousCaptureActive { get; set; }
public int ContinuousPendingCount { get; set; }
public string ContinuousHintText =>
    $"连续框选 · Esc 结束 · 待处理 {ContinuousPendingCount}";
```

Notify both commands and the hint when state changes.

- [ ] **Step 3: Add tray entry**

Insert “连续框选翻译” after “开始框选翻译” and expose:

```csharp
public event EventHandler? ContinuousCaptureRequested;
```

- [ ] **Step 4: Distinguish cancel from finish**

Add `ScreenSelectionCancelReason` with `Escape`, `RightClick`, and `WindowClosed`. Include the reason in `SelectionCancelled` event arguments so continuous mode can treat Escape/right-click as “finish the session” while one-shot mode keeps its existing cancel behavior.

- [ ] **Step 5: Show continuous hint**

Allow `SelectionOverlayWindow.Configure` to receive optional hint text and show it in the existing selection badge area without changing the one-shot appearance.

- [ ] **Step 6: Run focused and full integration tests**

Expected: command and selection behavior tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src/ScreenTranslator.App/ViewModels/MainWindowViewModel.cs src/ScreenTranslator.App/Services/Tray/TrayIconService.cs src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml.cs src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs tests/ScreenTranslator.IntegrationTests
git commit -m "feat: expose continuous capture workflow"
```

### Task 3: Split acquisition from OCR and translation

**Files:**
- Create: `src/ScreenTranslator.App/Services/Capture/TranslationCaptureWorkItem.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Capture/ContinuousCaptureCoordinatorTests.cs`

- [ ] **Step 1: Add immutable work item**

Define a record containing the cropped `SoftwareBitmap`, monitor, absolute selection, captured browser, source window handle, and settings snapshot. The work item owns the cropped bitmap and implements `IDisposable`.

- [ ] **Step 2: Refactor one-shot capture**

Extract:

```csharp
private async Task<TranslationCaptureWorkItem?> AcquireWorkItemAsync(
    CaptureMode mode,
    CancellationToken cancellationToken);

private async Task ProcessWorkItemAsync(
    TranslationCaptureWorkItem item,
    bool preserveExistingResults,
    CancellationToken cancellationToken);
```

`AcquireWorkItemAsync` performs foreground capture, screen snapshot, region selection, crop, and context capture. `ProcessWorkItemAsync` performs OCR, translation, and display.

- [ ] **Step 3: Prove one-shot behavior remains**

Run all existing Core and Integration tests before adding continuous orchestration. Expected: 139 existing .NET tests pass.

- [ ] **Step 4: Commit the refactor**

```powershell
git add src/ScreenTranslator.App/Services/Capture/TranslationCaptureWorkItem.cs src/ScreenTranslator.App/Services/ApplicationController.cs tests
git commit -m "refactor: separate capture acquisition from translation"
```

### Task 4: Continuous session orchestration

**Files:**
- Create: `src/ScreenTranslator.App/Services/Capture/ContinuousCaptureCoordinator.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Capture/ContinuousCaptureCoordinatorTests.cs`

- [ ] **Step 1: Write coordinator tests**

Test that acquisition repeats, Escape closes the writer, queued items finish, capacity waits, individual processor failures are reported, and disposal cancels both loops.

- [ ] **Step 2: Implement coordinator**

The coordinator accepts acquisition and processing delegates, creates a capacity-five `SequentialWorkQueue<TranslationCaptureWorkItem>`, exposes `PendingCountChanged`, `ItemFailed`, and `Completed`, and guarantees a single active session.

- [ ] **Step 3: Wire controller events**

Subscribe main-window and tray continuous events during initialization. Before starting, check the secret store for `DeepSeekApiKey`; if absent, show translation settings and status text instead of creating a session.

- [ ] **Step 4: Preserve queued work after Escape**

Escape/right-click must stop only the acquisition token and complete the queue writer. Application exit cancels the processor token and disposes remaining work items.

- [ ] **Step 5: Run tests and commit**

```powershell
git add src/ScreenTranslator.App/Services/Capture/ContinuousCaptureCoordinator.cs src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.IntegrationTests/Capture/ContinuousCaptureCoordinatorTests.cs
git commit -m "feat: process continuous captures in the background"
```

### Task 5: Preserve and group results

**Files:**
- Create: `src/ScreenTranslator.App/Services/Overlays/TranslationResultGroup.cs`
- Create: `src/ScreenTranslator.App/Services/Overlays/TranslationResultGroupRegistry.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Services/Overlays/OverlayFocusCoordinator.cs`
- Modify: `src/ScreenTranslator.App/Services/Overlays/ForegroundWindowMonitor.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Overlays/TranslationResultGroupRegistryTests.cs`

- [ ] **Step 1: Write registry tests**

Cover adding two groups for one source, adding another source, independent hide/restore, remove-one, remove-source, and clear-all.

- [ ] **Step 2: Implement group registry**

Each group owns its result windows, source window handle, optional browser tracking key, and disposal callbacks. The registry routes foreground changes to all groups instead of keeping a single `OverlayFocusCoordinator`.

- [ ] **Step 3: Update display semantics**

Add `preserveExistingResults` to `ShowTranslation`. One-shot calls `CloseResultWindows`; continuous mode does not. “清除此条” removes its group when its last overlay closes, while “清除全部译文” still clears the registry.

- [ ] **Step 4: Run overlay and lifecycle tests**

Expected: existing clear/focus behavior and new multi-source grouping tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Overlays src/ScreenTranslator.App/Services/ApplicationController.cs tests/ScreenTranslator.IntegrationTests/Overlays
git commit -m "feat: preserve grouped continuous translations"
```

### Task 6: Continuous side panel and UI entry

**Files:**
- Create: `src/ScreenTranslator.App/ViewModels/ContinuousResultsViewModel.cs`
- Create: `src/ScreenTranslator.App/Windows/ContinuousSidePanelWindow.xaml`
- Create: `src/ScreenTranslator.App/Windows/ContinuousSidePanelWindow.xaml.cs`
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`

- [ ] **Step 1: Add collection view model**

Expose an `ObservableCollection<TranslationResultViewModel>`, append results in completion order, and provide per-item retry/close plus clear-all.

- [ ] **Step 2: Build the side panel**

Reuse the existing translation surface brushes, fixed header/action bar, mouse-wheel routing, drag behavior, work-area clamping, and a virtualized `ItemsControl` inside a `ScrollViewer`.

- [ ] **Step 3: Add main-page controls**

Place “开始框选” and “连续框选” as adjacent rounded-rectangle buttons. Bind status to pending count and active state.

- [ ] **Step 4: Verify visual rules**

Extend `VisualStyleTests` to assert non-pill corner radii, opaque translation surfaces at 100%, fixed action area, and the continuous button binding.

- [ ] **Step 5: Run full tests and commit**

```powershell
git add src/ScreenTranslator.App/ViewModels/ContinuousResultsViewModel.cs src/ScreenTranslator.App/Windows/ContinuousSidePanelWindow.xaml src/ScreenTranslator.App/Windows/ContinuousSidePanelWindow.xaml.cs src/ScreenTranslator.App/Pages/GeneralPage.xaml src/ScreenTranslator.App/Services/ApplicationController.cs tests
git commit -m "feat: add continuous translation results UI"
```

### Task 7: Browser-follow multiple continuous groups

**Files:**
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserFollowSessionRegistry.cs`
- Modify: `src/ScreenTranslator.App/Services/Browser/BrowserFollowCoordinator.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.Browser.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserFollowSessionRegistryTests.cs`

- [ ] **Step 1: Write multi-group tests**

Cover two selection groups on one document receiving the same root scroll, removing one group without stopping the other, and invalidating only the matching document on navigation.

- [ ] **Step 2: Add group-aware coordinator**

Replace the single selection boundary with entries containing their own selection boundary and overlays:

```csharp
public Guid AddGroup(
    DipRect selectionBounds,
    IReadOnlyList<ITrackedOverlay> overlays);
public bool RemoveGroup(Guid groupId);
```

- [ ] **Step 3: Add session registry**

Key sessions by browser kind, browser window ID, tab ID, and document ID. Reuse an existing session for subsequent continuous results on the same page.

- [ ] **Step 4: Wire controller**

Replace `_browserFollowCoordinator` singleton lifecycle with the registry and keep existing startup waiter behavior for the first group.

- [ ] **Step 5: Run .NET and extension tests**

Expected: all existing browser-follow tests plus multi-group tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/ScreenTranslator.App/Services/Browser src/ScreenTranslator.App/Services/ApplicationController.Browser.cs tests/ScreenTranslator.IntegrationTests/Browser
git commit -m "feat: follow multiple continuous browser overlays"
```

