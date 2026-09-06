# Startup and Tray Options Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persisted controls for silent Windows startup and tray-icon visibility, then publish the change as a new GitHub release.

**Architecture:** Extend `AppSettings` and the existing general-settings view model. The Windows Run registration will append `--startup-silent` when requested, and `App.xaml.cs` will skip opening the settings window for that argument. `TrayIconService` will expose a visibility setter so the controller can apply the persisted `ShowTrayIcon` value immediately and react to later changes.

**Tech Stack:** .NET 8 WPF, WPF-UI, Windows Forms `NotifyIcon`, System.Text.Json, xUnit, GitHub Actions and Inno Setup.

---

### Task 1: Add persisted settings and startup-argument coverage

**Files:**
- Modify: `src/ScreenTranslator.Core/Settings/AppSettings.cs`
- Create: `src/ScreenTranslator.App/Infrastructure/StartupCommand.cs`
- Modify: `tests/ScreenTranslator.Core.Tests/Settings/AppSettingsTests.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Infrastructure/MaintenanceCommandTests.cs`

- [ ] **Step 1: Write failing tests**

Assert that new defaults are `StartSilently == false` and `ShowTrayIcon == true`, that version-one JSON migrates to the current version with those defaults, and that `StartupCommand.IsSilentStartup` recognizes `--startup-silent` case-insensitively while ignoring normal arguments.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run `dotnet test tests/ScreenTranslator.Core.Tests/ScreenTranslator.Core.Tests.csproj -c Release --filter AppSettingsTests` and `dotnet test tests/ScreenTranslator.IntegrationTests/ScreenTranslator.IntegrationTests.csproj -c Release --filter "MaintenanceCommandTests|StartupCommandTests"`; expect compile or assertion failures because the properties and parser do not exist yet.

- [ ] **Step 3: Implement the minimal settings and parser**

Raise `AppSettings.CurrentVersion` to `3`; add `StartSilently` with a false default and `ShowTrayIcon` with a true default. Add `StartupCommand.SilentArgument` and an `IsSilentStartup(IReadOnlyList<string>)` helper that validates non-null input and performs an ordinal-ignore-case argument comparison.

- [ ] **Step 4: Run the focused tests and verify they pass**

Repeat both commands and expect all focused tests to pass.

### Task 2: Wire the settings page, startup registration, and tray visibility

**Files:**
- Modify: `src/ScreenTranslator.App/ViewModels/SettingsViewModels.cs`
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `src/ScreenTranslator.App/Services/Tray/TrayIconService.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.cs`
- Modify: `src/ScreenTranslator.App/App.xaml.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`

- [ ] **Step 1: Add view-model properties and UI regression assertions**

Expose `StartSilently` and `ShowTrayIcon` on `GeneralSettingsViewModel`, add two toggle cards below the existing startup/tray settings, and assert their binding paths and labels from the parsed XAML.

- [ ] **Step 2: Add the tray visibility API**

Add `SetVisible(bool)` and an `IsVisible` read-only property to `TrayIconService`; `SetVisible` updates `NotifyIcon.Visible` unless disposed, and `Dispose` leaves the state false.

- [ ] **Step 3: Apply and persist the settings**

Load and collect both values in `ApplicationController`. Register startup with `ApplyStartupRegistration(startWithWindows, startSilently)`, writing the executable path plus ` --startup-silent` only when both are enabled. React to either property changing, and call `_tray.SetVisible(GeneralSettings.ShowTrayIcon)` after loading and whenever the toggle changes.

- [ ] **Step 4: Honor silent startup**

In `App.OnStartup`, compute `StartupCommand.IsSilentStartup(e.Args)` after maintenance/native-host handling and call `Controller.ShowSettings()` only when the flag is absent.

- [ ] **Step 5: Run integration tests**

Run the focused integration tests and verify the UI and startup behavior tests pass.

### Task 3: Version, package, publish, and verify

**Files:**
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Modify: `eng/build-release.ps1`
- Modify: `README.md`

- [ ] **Step 1: Update version and user documentation**

Bump the app and build-script defaults to `0.2.6`; document that silent startup applies to Windows auto-start and that hiding the tray icon removes tray-menu access while hotkeys continue to work.

- [ ] **Step 2: Run all local checks and build release artifacts**

Run core tests, integration tests excluding the known native-pipe test when the installed app owns the pipe, browser extension tests, `git diff --check`, the release build, Inno Setup compilation, and checksum generation. Confirm installer, portable ZIP, extension ZIP, and SHA256 files exist.

- [ ] **Step 3: Commit and push**

Stage the implementation, documentation, and tests; commit with `feat: add silent startup and tray visibility options`; push `main`.

- [ ] **Step 4: Verify CI, tag, and Release**

Wait for the main CI run to succeed, create and push annotated tag `v0.2.6`, wait for the Release workflow, and verify all four release assets are uploaded and public.
