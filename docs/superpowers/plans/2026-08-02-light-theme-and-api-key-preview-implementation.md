# Light Theme and API Key Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the four reported light-theme text collisions and show a privacy-safe preview for a saved DeepSeek API Key.

**Architecture:** Keep the theme fix local to the two primary-button labels and two accent badges. Model saved-key presence separately from replacement input, derive only a masked preview, and resolve connection-test credentials from new input first and DPAPI storage second.

**Tech Stack:** .NET 8, WPF, WPF UI 4.3, CommunityToolkit.Mvvm, xUnit, DPAPI secret storage, GitHub Actions/Inno Setup.

---

### Task 1: Lock down the four light-theme labels

**Files:**
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `src/ScreenTranslator.App/Pages/TranslationPage.xaml`
- Modify: `src/ScreenTranslator.App/Pages/AppearancePage.xaml`

- [ ] **Step 1: Add a failing XAML assertion**

Add a test that loads the three pages, finds `PrimaryCaptureButtonText`, `SaveButtonText`, `HotkeyBadgeText`, and `PreviewBadgeText`, and asserts:

```csharp
Assert.Equal(
    "{DynamicResource TextOnAccentFillColorPrimaryBrush}",
    (string?)element.Attribute("Foreground"));
```

- [ ] **Step 2: Run the focused visual-style test and confirm it fails**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests\ScreenTranslator.IntegrationTests\ScreenTranslator.IntegrationTests.csproj -c Release --filter FullyQualifiedName~VisualStyleTests
```

Expected: the new assertion fails because the named labels do not exist yet.

- [ ] **Step 3: Give only the four labels an accent-compatible foreground**

Replace the two raw primary-button strings with explicit content and mark the two badge labels:

```xml
<TextBlock x:Name="PrimaryCaptureButtonText"
           Text="开始框选"
           Foreground="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
```

Use the same resource for `SaveButtonText`, `HotkeyBadgeText`, and `PreviewBadgeText`. Do not modify the global implicit `TextBlock` style.

- [ ] **Step 4: Run `VisualStyleTests` and confirm it passes**

Expected: all visual-style tests pass.

### Task 2: Model a saved Key without retaining it in UI state

**Files:**
- Create: `src/ScreenTranslator.App/ViewModels/ApiKeyMasker.cs`
- Modify: `src/ScreenTranslator.App/ViewModels/TranslationSettingsViewModel.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/ViewModels/ApiKeyMaskerTests.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/ViewModels/TranslationSettingsViewModelTests.cs`

- [ ] **Step 1: Add failing mask and state tests**

Cover a normal key, a four-character key, an empty key, applying a saved key, entering replacement mode, cancellation, and test-command availability with only a saved key. The normal-key expectation is:

```csharp
Assert.Equal("************abd4", ApiKeyMasker.Mask("sk-1234567890abd4"));
```

- [ ] **Step 2: Run the two test classes and confirm they fail**

Expected: compilation fails because `ApiKeyMasker` and the saved-key properties do not exist.

- [ ] **Step 3: Implement the masker and ViewModel state**

`ApiKeyMasker.Mask` returns twelve asterisks plus the final four characters for normal keys, masks all characters for keys of length four or less, and returns an empty string for blank input.

Add these ViewModel members:

```csharp
public bool HasSavedApiKey { get; private set; }
public string SavedApiKeyMask { get; private set; } = string.Empty;
public bool IsEditingApiKey { get; private set; }
public bool ShowSavedApiKey => HasSavedApiKey && !IsEditingApiKey;
public bool ShowApiKeyEditor => !ShowSavedApiKey;
public IRelayCommand BeginApiKeyEditCommand { get; }
public IRelayCommand CancelApiKeyEditCommand { get; }
public void ApplySavedApiKey(string? apiKey);
```

`ApplySavedApiKey` derives the mask then drops the supplied full value. Beginning or cancelling replacement clears `ApiKey`; cancelling restores the saved preview. Notify the test command whenever saved-key availability changes.

- [ ] **Step 4: Run the focused tests and confirm they pass**

Expected: masker and ViewModel tests pass.

### Task 3: Wire secure loading, replacement, and connection testing

**Files:**
- Create: `src/ScreenTranslator.App/Services/Settings/ApiKeyResolver.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/Pages/TranslationPage.xaml`
- Create: `tests/ScreenTranslator.IntegrationTests/Settings/ApiKeyResolverTests.cs`

- [ ] **Step 1: Add failing resolver tests**

Verify that a nonblank replacement Key is returned without reading storage, while blank input reads `DeepSeekTranslationProvider.ApiKeyName` from `ISecretStore`.

- [ ] **Step 2: Run the resolver tests and confirm they fail**

Expected: compilation fails because `ApiKeyResolver` does not exist.

- [ ] **Step 3: Implement credential resolution**

```csharp
public static Task<string?> ResolveAsync(
    string? candidate,
    ISecretStore secretStore,
    CancellationToken cancellationToken = default)
```

Return trimmed replacement input when supplied; otherwise call the secure store. Never log either value.

- [ ] **Step 4: Add the saved/read-only and replacement/password UI states**

Use `ShowSavedApiKey` and `ShowApiKeyEditor` with `BooleanToVisibilityConverter`. The saved state contains a read-only `TextBox` bound to `SavedApiKeyMask` and a “更换” button. The editor contains the existing `PasswordBox` and a conditional “取消” button.

- [ ] **Step 5: Load and refresh the preview through the controller**

At initialization, read the DPAPI Key once, call `ApplySavedApiKey`, and convert read failures into a visible status. On save, write only nonblank replacement input, then call `ApplySavedApiKey` so the input is cleared and the mask refreshes.

- [ ] **Step 6: Make connection testing fall back to the stored Key**

Call `ApiKeyResolver.ResolveAsync(request.ApiKey, _secretStore, cancellationToken)`. Return “请先配置 DeepSeek API Key” if neither source contains a Key; otherwise construct the existing in-memory test provider with the resolved value.

- [ ] **Step 7: Run focused and full tests**

Expected: resolver tests, all .NET tests, and all 11 browser-extension tests pass.

### Task 4: Prepare and publish v0.2.3

**Files:**
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Modify: `eng/build-release.ps1`
- Modify: `README.md`

- [ ] **Step 1: Set the product and release-script version to `0.2.3`**

Update `<Version>0.2.3</Version>`, the default release script version, and the README build example.

- [ ] **Step 2: Run release validation**

Run Release build, all .NET tests, browser tests, secret scan, `git diff --check`, and `eng/build-release.ps1 -Version 0.2.3`. Compile the Inno Setup installer and generate SHA-256 checksums.

- [ ] **Step 3: Commit and push `main`**

Commit the implementation with `fix: repair light theme and key preview`, push to `origin/main`, and wait for CI to pass including silent installer verification.

- [ ] **Step 4: Publish the verified tag**

Create and push annotated tag `v0.2.3`, wait for the Release workflow, and verify the installer, portable ZIP, browser-extension ZIP, and `SHA256SUMS.txt` assets on the GitHub Release.
