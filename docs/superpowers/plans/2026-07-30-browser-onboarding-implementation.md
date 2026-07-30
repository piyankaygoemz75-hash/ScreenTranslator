# Browser Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically register and repair the desktop bridge, detect installed Chrome/Edge browsers, and guide the single browser-required extension confirmation with live connection feedback.

**Architecture:** Add testable browser detection and native-registration removal/repair services, model each browser with an explicit installation/connection state, then update the settings page commands to open the extension manager, reveal and copy the stable extension folder, and wait for the existing bridge connection event.

**Tech Stack:** C# 12, WPF, Windows Registry, Native Messaging, xUnit

---

### Task 1: Browser installation detection

**Files:**
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserInstallationDetector.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserInstallationDetectorTests.cs`

- [ ] Add tests using an injected file system and registry reader for Chrome present, Edge present, both absent, and nonstandard per-user install paths.
- [ ] Implement detection using App Paths and standard Program Files/LocalAppData locations without launching browsers.
- [ ] Run focused tests and commit with `feat: detect installed browsers`.

### Task 2: Repairable Native Messaging registration

**Files:**
- Modify: `src/ScreenTranslator.App/Services/Browser/NativeMessagingRegistrationService.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Browser/NativeMessagingRegistrationServiceTests.cs`

- [ ] Extend `INativeMessagingRegistry` with read and delete operations.
- [ ] Add `GetStatusAsync`, idempotent `RepairAsync`, and `UnregisterAsync`.
- [ ] Ensure repair rewrites a stale executable path and both browser registrations.
- [ ] Ensure unregister deletes registry values and the generated manifest but not the extension directory.
- [ ] Run focused tests and commit with `feat: repair browser bridge registration`.

### Task 3: Explicit browser setup states

**Files:**
- Modify: `src/ScreenTranslator.App/ViewModels/BrowserIntegrationViewModel.cs`
- Modify: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserIntegrationViewModelTests.cs`

- [ ] Define `BrowserSetupState` values `NotDetected`, `ExtensionNotConnected`, `WaitingForConnection`, `Connected`, and `BridgeError`.
- [ ] Replace string-comparison availability logic with state-derived properties.
- [ ] Add per-browser install commands and a repair command.
- [ ] Test all state transitions and command events.
- [ ] Commit with `feat: model browser onboarding states`.

### Task 4: Guided installation actions

**Files:**
- Create: `src/ScreenTranslator.App/Services/Browser/BrowserExtensionOnboardingService.cs`
- Modify: `src/ScreenTranslator.App/Services/ApplicationController.Browser.cs`
- Test: `tests/ScreenTranslator.IntegrationTests/Browser/BrowserExtensionOnboardingServiceTests.cs`

- [ ] Inject process launcher and clipboard adapters for testability.
- [ ] On install action, validate the bundled extension folder, copy its full path, open the browser extension manager, reveal the folder in Explorer, and set state to waiting.
- [ ] Reuse bridge connection events to mark the matching browser connected immediately.
- [ ] On repair, call `RepairAsync` and refresh states without restarting the app.
- [ ] Add clear error messages for missing folder, launch failure, clipboard failure, and registration failure.
- [ ] Run focused tests and commit with `feat: guide browser extension setup`.

### Task 5: Windows 11 browser setup UI

**Files:**
- Modify: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Modify: `tests/ScreenTranslator.IntegrationTests/Windows/VisualStyleTests.cs`
- Modify: `README.md`
- Modify: `browser-extension/README.md`

- [ ] Replace generic buttons with per-browser cards, state indicators, “安装到 Chrome/Edge”, “修复连接”, and “打开扩展目录”.
- [ ] Keep all controls as rounded rectangles rather than pill shapes.
- [ ] Document that the desktop bridge is automatic and browser confirmation is required once by Chrome/Edge security policy.
- [ ] Run full tests and commit with `feat: add browser connection onboarding`.

