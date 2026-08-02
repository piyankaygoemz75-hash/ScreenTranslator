# Side Panel Drag Handle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the full top header of both side-panel windows draggable and restyle the `AI 翻译` badge as a rounded rectangle.

**Architecture:** Preserve the existing drag implementations and expand only the WPF hit-test surface by giving each header a transparent background. Keep all content, scrolling, focus behavior, placement persistence, and commands unchanged.

**Tech Stack:** .NET 8, WPF, WPF UI 4.3, xUnit, Inno Setup, GitHub Actions.

---

### Task 1: Lock the drag surface and badge shape with XAML tests

**Files:**
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`

- [ ] **Step 1: Add a failing test**

Load `SidePanelWindow.xaml` and `ContinuousSidePanelWindow.xaml`. Find named header elements and assert:

```csharp
Assert.Equal("Transparent", (string?)header.Attribute("Background"));
Assert.Equal("SizeAll", (string?)header.Attribute("Cursor"));
```

Find `AiTranslationBadge` and assert `CornerRadius="5"`.

- [ ] **Step 2: Run the focused test**

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter FullyQualifiedName~Side_Panel_Headers
```

Expected: fail because the names and attributes are missing.

### Task 2: Expand the headers' hit-test areas

**Files:**
- Modify: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/ContinuousSidePanelWindow.xaml`

- [ ] **Step 1: Update the single-result header and badge**

```xml
<Grid x:Name="HeaderArea"
      Background="Transparent"
      Cursor="SizeAll"
      MouseLeftButtonDown="Header_OnMouseLeftButtonDown">
```

Name the badge `AiTranslationBadge` and set `CornerRadius="5"`. Leave the existing native drag handler and close button unchanged.

- [ ] **Step 2: Update the continuous-result header**

Name it `HeaderArea`, add `Background="Transparent"` and `Cursor="SizeAll"`, and leave its existing `DragMove()` handler unchanged.

- [ ] **Step 3: Run the focused and full tests**

Expected: the new visual test, all .NET tests, and all browser-extension tests pass with a Release build containing zero warnings.

### Task 3: Package and publish v0.2.5

**Files:**
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Modify: `eng/build-release.ps1`
- Modify: `README.md`

- [ ] **Step 1: Set all release-version references to `0.2.5`**

Update the project `<Version>`, release-script default, and README build example.

- [ ] **Step 2: Build release artifacts**

Run the secret scan, `git diff --check`, `eng/build-release.ps1 -Version 0.2.5`, Inno Setup compilation, and checksum generation.

- [ ] **Step 3: Publish after clean CI**

Commit as `fix: expand side panel drag handle`, push `main`, wait for CI including silent installer verification, tag `v0.2.5`, and verify all four GitHub Release assets.
