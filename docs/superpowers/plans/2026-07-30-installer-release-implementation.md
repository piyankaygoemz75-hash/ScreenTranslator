# Installer and Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce reproducible per-user Windows installer, portable, extension, and checksum artifacts and publish them as GitHub Release `v0.2.0`.

**Architecture:** A PowerShell release script publishes the self-contained app and creates portable/extension archives. Inno Setup consumes the publish directory to build a current-user installer. A tag-triggered GitHub Actions workflow runs tests, builds all artifacts, verifies an install/uninstall smoke test, and uploads the exact files to GitHub Release.

**Tech Stack:** .NET 8 publish, PowerShell 7, Inno Setup 6, GitHub Actions

---

### Task 1: Version and uninstall command

**Files:**
- Modify: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Modify: `src/ScreenTranslator.App/App.xaml.cs`
- Modify: `src/ScreenTranslator.App/Services/Browser/NativeMessagingRegistrationService.cs`

- [ ] Set assembly/package version to `0.2.0`.
- [ ] Add a `--unregister-browser-host` command that runs registration cleanup without opening WPF UI.
- [ ] Add tests for argument recognition and cleanup.
- [ ] Run tests and commit with `chore: prepare version 0.2.0`.

### Task 2: Reproducible release builder

**Files:**
- Create: `eng/build-release.ps1`
- Modify: `.gitignore`

- [ ] Make the script accept `-Version`, `-Runtime`, and `-OutputRoot`.
- [ ] Clean only the exact output version directory after resolving it under `artifacts`.
- [ ] Publish self-contained single-file x64 app.
- [ ] Copy license and release README.
- [ ] Create portable and extension ZIP files with deterministic names.
- [ ] Generate `SHA256SUMS.txt` using `Get-FileHash`.
- [ ] Validate every expected file and fail on an unexpected API key/config file.
- [ ] Run locally and commit with `build: add reproducible release packaging`.

### Task 3: Inno Setup installer

**Files:**
- Create: `installer/ScreenTranslator.iss`
- Create: `installer/LICENSE-zh-CN.txt`

- [ ] Configure `PrivilegesRequired=lowest`, `DefaultDirName={localappdata}\Programs\ScreenTranslator`, x64 mode, app icon, compression, and Chinese/English wizard languages.
- [ ] Install the published app and bundled extension to stable paths.
- [ ] Create Start menu and optional desktop shortcuts.
- [ ] Offer optional current-user startup.
- [ ] Close the running app during upgrade and launch the new app after install.
- [ ] Call `ScreenTranslator.exe --unregister-browser-host` during uninstall before file removal.
- [ ] Offer a final uninstall checkbox to delete `%LOCALAPPDATA%\ScreenTranslator`; leave it unchecked by default.
- [ ] Build with `ISCC.exe` and commit with `build: add per-user Windows installer`.

### Task 4: Installer smoke verification

**Files:**
- Create: `eng/test-installer.ps1`

- [ ] Install silently into a temporary explicit directory.
- [ ] Assert the executable, extension manifest, license, start menu entry, and uninstaller exist.
- [ ] Launch the registration command and assert Chrome/Edge current-user host keys point to the installed manifest.
- [ ] Run the uninstaller silently.
- [ ] Assert installed files and host registrations are removed while a seeded settings file remains.
- [ ] Run locally/CI and commit with `test: verify installer lifecycle`.

### Task 5: GitHub release workflow

**Files:**
- Create: `.github/workflows/release.yml`
- Modify: `.github/workflows/ci.yml`

- [ ] Trigger release workflow on tags matching `v*`.
- [ ] Restore, build Release, run all .NET and Node tests.
- [ ] Install pinned Inno Setup 6 on `windows-latest`.
- [ ] Run `eng/build-release.ps1`, compile the installer, and run installer smoke verification.
- [ ] Upload the four release artifacts and checksum as workflow artifacts.
- [ ] Create the GitHub Release with generated notes and the Chinese installation/SmartScreen notice.
- [ ] Add a CI job that compiles the installer script on ordinary `main` pushes without creating a release.
- [ ] Commit with `ci: publish verified Windows releases`.

### Task 6: Documentation and final release

**Files:**
- Modify: `README.md`
- Create: `docs/installation.md`
- Modify: `SECURITY.md`

- [ ] Put installer download first, portable download second, and source build later in README.
- [ ] Document one-time Chrome/Edge extension confirmation, connection repair, upgrade, uninstall, configuration retention, and unsigned SmartScreen warning.
- [ ] Run secret scan, Release build, all .NET tests, all extension tests, release packaging, and installer smoke test.
- [ ] Push `main`, wait for CI success, create annotated tag `v0.2.0`, and push it.
- [ ] Wait for release workflow completion and verify GitHub Release contains installer, portable ZIP, extension ZIP, and SHA-256 checksums.
- [ ] Commit documentation with `docs: add installation and release guide`.

