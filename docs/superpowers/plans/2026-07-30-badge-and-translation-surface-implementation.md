# Badge and Translation Surface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace two pill-shaped badges with compact rounded rectangles and make translation surfaces fully opaque when the opacity setting is 100%.

**Architecture:** Shared XAML resources own the badge geometry and translation surface brush. A small theme-aware palette updates the opaque translation brush whenever the application theme changes; both translation windows consume the same dynamic resource while the existing window-level opacity slider remains authoritative.

**Tech Stack:** .NET 8, WPF XAML, WPF-UI, xUnit STA integration tests

---

### Task 1: Shared badge geometry

**Files:**
- Modify: `src/ScreenTranslator.App/App.xaml`
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `src/ScreenTranslator.App/Pages/AppearancePage.xaml`
- Test: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`

- [ ] **Step 1: Write the failing badge style test**

Create `VisualStyleTests` that initializes `App`, reads `CompactBadgeStyle`, and asserts its `CornerRadius` setter equals `new CornerRadius(5)` rather than `999`.

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --no-restore --filter FullyQualifiedName~VisualStyleTests
```

Expected: failure because `CompactBadgeStyle` does not exist.

- [ ] **Step 3: Add and apply the shared style**

Add to `App.xaml`:

```xml
<Style x:Key="CompactBadgeStyle" TargetType="{x:Type Border}">
    <Setter Property="Background" Value="{DynamicResource AccentFillColorSecondaryBrush}" />
    <Setter Property="CornerRadius" Value="5" />
    <Setter Property="Padding" Value="10,4" />
</Style>
```

Replace `PillStyle` on the shortcut badge and the inline `CornerRadius="999"` preview badge with `CompactBadgeStyle`.

- [ ] **Step 4: Run the focused test and verify it passes**

Expected: `VisualStyleTests` passes and both pages compile.

### Task 2: Opaque theme-aware translation surface

**Files:**
- Create: `src/ScreenTranslator.App/Services/Appearance/TranslationSurfacePalette.cs`
- Modify: `src/ScreenTranslator.App/App.xaml`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml`
- Modify: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml`
- Test: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`

- [ ] **Step 1: Add failing palette and window-surface assertions**

Test that light and dark palette brushes have alpha `255`, and that `SidePanelWindow.RootCard` plus `TextOverlayWindow.RootCard` use `Opacity=1`.

- [ ] **Step 2: Run the focused test and verify it fails**

Expected: missing `TranslationSurfacePalette` and existing root opacity values `0.97`/`0.96`.

- [ ] **Step 3: Implement the palette**

Create:

```csharp
public static class TranslationSurfacePalette
{
    public static SolidColorBrush CreateBrush(ApplicationTheme theme)
    {
        var color = theme == ApplicationTheme.Dark
            ? Color.FromRgb(32, 32, 32)
            : Color.FromRgb(250, 250, 250);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
```

Define a default `TranslationSurfaceBrush` in `App.xaml`. In `ApplyTheme`, replace that resource with `TranslationSurfacePalette.CreateBrush(theme)` after applying the WPF-UI theme.

- [ ] **Step 4: Apply the shared surface**

Set both `RootCard` backgrounds and the overlay action bar background to `{DynamicResource TranslationSurfaceBrush}`. Set both root border opacities to `1`. Keep `ApplicationController` window `Opacity = settings.OverlayOpacity`, so the existing slider still controls 72–100%.

- [ ] **Step 5: Run the focused test and verify it passes**

Expected: palette alpha is 255, both root cards have opacity 1, and XAML compiles.

### Task 3: Regression, visual check, and package

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Document the corrected opacity behavior**

State that 100% uses an opaque translation surface and lower values use the configured window opacity.

- [ ] **Step 2: Run full validation**

Run Release build, 80 core tests, all integration tests, extension tests, `dotnet format --verify-no-changes`, `git diff --check`, and the secret scan.

- [ ] **Step 3: Render or launch a visual smoke check**

Verify the two badges are compact rounded rectangles and dark-theme overlay surfaces are solid enough to separate translated text from page text.

- [ ] **Step 4: Publish a new unique win-x64 ZIP**

Publish self-contained single-file output into `artifacts/ScreenTranslator-win-x64-2026-07-30-ui-surface-fix` and include `browser-extension`.

- [ ] **Step 5: Commit the implementation**

Stage only the files in this plan and commit with a focused UI-fix message.
