# 屏幕翻译软件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一款 Windows 10/11 原生桌面软件，支持全局快捷键框选屏幕、本地 OCR、DeepSeek V4 翻译，以及原位覆盖和原文旁显两种结果模式。

**Architecture:** 采用 .NET 8 WPF 与 WPF UI 4.3.0。纯业务模型、DeepSeek 客户端、布局算法和会话编排放入 `ScreenTranslator.Core`；Windows 捕获、OCR、快捷键、托盘和窗口放入 `ScreenTranslator.App`；单元与集成测试分别验证纯逻辑和 HTTP/会话边界。

**Tech Stack:** .NET SDK 8.0.423、C# 12、WPF、WPF UI 4.3.0、CommunityToolkit.Mvvm 8.4.2、Microsoft.Extensions.Hosting 8.0.0、Windows.Graphics.Capture、Windows.Media.Ocr、xUnit

---

## 文件结构

```text
ScreenTranslator.sln
global.json
Directory.Build.props
Directory.Packages.props
src/
  ScreenTranslator.Core/
    ScreenTranslator.Core.csproj
    Models/
      Geometry.cs
      CaptureModels.cs
      OcrModels.cs
      TranslationModels.cs
    Abstractions/
      IOcrEngine.cs
      ITranslationProvider.cs
      ISecretStore.cs
      ISettingsStore.cs
    Translation/
      DeepSeekOptions.cs
      DeepSeekTranslationProvider.cs
      TranslationOrchestrator.cs
      TranslationResponseValidator.cs
    Layout/
      TextLayoutService.cs
      PanelPlacementService.cs
    Sessions/
      TranslationSession.cs
      TranslationSessionCoordinator.cs
    Settings/
      AppSettings.cs
  ScreenTranslator.App/
    ScreenTranslator.App.csproj
    App.xaml
    App.xaml.cs
    app.manifest
    Assets/
      app.ico
    Infrastructure/
      ServiceRegistration.cs
      SingleInstanceGuard.cs
    Windows/
      MainWindow.xaml
      MainWindow.xaml.cs
      SelectionOverlayWindow.xaml
      SelectionOverlayWindow.xaml.cs
      SidePanelWindow.xaml
      SidePanelWindow.xaml.cs
      TextOverlayWindow.xaml
      TextOverlayWindow.xaml.cs
    Pages/
      GeneralPage.xaml
      TranslationPage.xaml
      AppearancePage.xaml
      HotkeyPage.xaml
      PrivacyPage.xaml
      AboutPage.xaml
    ViewModels/
      MainWindowViewModel.cs
      TranslationSettingsViewModel.cs
      SelectionOverlayViewModel.cs
      TranslationResultViewModel.cs
    Services/
      Capture/
        IScreenCaptureService.cs
        WindowsGraphicsCaptureService.cs
        GdiScreenCaptureService.cs
        FallbackScreenCaptureService.cs
      Ocr/
        WindowsOcrEngine.cs
        SoftwareBitmapConverter.cs
      Hotkeys/
        GlobalHotkeyService.cs
      Overlay/
        OverlayManager.cs
        WindowStyleService.cs
      Settings/
        JsonSettingsStore.cs
        DpapiSecretStore.cs
      Tray/
        TrayIconService.cs
      CaptureCoordinator.cs
    Interop/
      NativeMethods.cs
      GraphicsCaptureItemInterop.cs
  ScreenTranslator.Package/
    Package.appxmanifest
    ScreenTranslator.Package.wapproj
tests/
  ScreenTranslator.Core.Tests/
    ScreenTranslator.Core.Tests.csproj
    Translation/
      TranslationResponseValidatorTests.cs
      TranslationOrchestratorTests.cs
    Layout/
      TextLayoutServiceTests.cs
      PanelPlacementServiceTests.cs
    Sessions/
      TranslationSessionCoordinatorTests.cs
  ScreenTranslator.IntegrationTests/
    ScreenTranslator.IntegrationTests.csproj
    DeepSeekTranslationProviderTests.cs
    FakeDeepSeekServer.cs
README.md
```

## Task 1：安装工作区 SDK 并建立解决方案

**Files:**
- Create: `.tools/dotnet-install.ps1`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `ScreenTranslator.sln`
- Create: `src/ScreenTranslator.Core/ScreenTranslator.Core.csproj`
- Create: `src/ScreenTranslator.App/ScreenTranslator.App.csproj`
- Create: `tests/ScreenTranslator.Core.Tests/ScreenTranslator.Core.Tests.csproj`
- Create: `tests/ScreenTranslator.IntegrationTests/ScreenTranslator.IntegrationTests.csproj`

- [ ] **Step 1：下载并安装工作区本地 .NET SDK**

Run:

```powershell
New-Item -ItemType Directory -Force .tools
Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile .tools/dotnet-install.ps1
powershell -ExecutionPolicy Bypass -File .tools/dotnet-install.ps1 -Version 8.0.423 -InstallDir .tools/dotnet
```

Expected: `.tools/dotnet/dotnet.exe --version` 输出 `8.0.423`。下载脚本保留用于可重复构建，`.tools/dotnet/` 加入 `.gitignore`。

- [ ] **Step 2：锁定 SDK 和通用编译设置**

Create `global.json`:

```json
{
  "sdk": {
    "version": "8.0.423",
    "rollForward": "latestPatch"
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageVersion Include="WPF-UI" Version="4.3.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3：创建解决方案与项目**

Run:

```powershell
.\.tools\dotnet\dotnet.exe new sln -n ScreenTranslator
.\.tools\dotnet\dotnet.exe new classlib -n ScreenTranslator.Core -o src/ScreenTranslator.Core -f net8.0
.\.tools\dotnet\dotnet.exe new wpf -n ScreenTranslator.App -o src/ScreenTranslator.App -f net8.0
.\.tools\dotnet\dotnet.exe new xunit -n ScreenTranslator.Core.Tests -o tests/ScreenTranslator.Core.Tests -f net8.0
.\.tools\dotnet\dotnet.exe new xunit -n ScreenTranslator.IntegrationTests -o tests/ScreenTranslator.IntegrationTests -f net8.0
```

Modify `ScreenTranslator.App.csproj` target:

```xml
<TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
<UseWPF>true</UseWPF>
<SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
```

Add package references:

```xml
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" />
  <PackageReference Include="Microsoft.Extensions.Hosting" />
  <PackageReference Include="WPF-UI" />
  <ProjectReference Include="..\ScreenTranslator.Core\ScreenTranslator.Core.csproj" />
</ItemGroup>
```

- [ ] **Step 4：连接项目并验证空解决方案**

Run:

```powershell
.\.tools\dotnet\dotnet.exe sln add src/ScreenTranslator.Core/ScreenTranslator.Core.csproj
.\.tools\dotnet\dotnet.exe sln add src/ScreenTranslator.App/ScreenTranslator.App.csproj
.\.tools\dotnet\dotnet.exe sln add tests/ScreenTranslator.Core.Tests/ScreenTranslator.Core.Tests.csproj
.\.tools\dotnet\dotnet.exe sln add tests/ScreenTranslator.IntegrationTests/ScreenTranslator.IntegrationTests.csproj
.\.tools\dotnet\dotnet.exe add tests/ScreenTranslator.Core.Tests reference src/ScreenTranslator.Core
.\.tools\dotnet\dotnet.exe add tests/ScreenTranslator.IntegrationTests reference src/ScreenTranslator.Core
.\.tools\dotnet\dotnet.exe restore
.\.tools\dotnet\dotnet.exe build --no-restore
```

Expected: `Build succeeded.` 且 0 warnings、0 errors。

- [ ] **Step 5：提交工具链骨架**

```powershell
git add global.json Directory.Build.props Directory.Packages.props ScreenTranslator.sln src tests .gitignore
git commit -m "build: bootstrap Windows translation solution"
```

## Task 2：定义领域模型和接口

**Files:**
- Create: `src/ScreenTranslator.Core/Models/Geometry.cs`
- Create: `src/ScreenTranslator.Core/Models/CaptureModels.cs`
- Create: `src/ScreenTranslator.Core/Models/OcrModels.cs`
- Create: `src/ScreenTranslator.Core/Models/TranslationModels.cs`
- Create: `src/ScreenTranslator.Core/Abstractions/IOcrEngine.cs`
- Create: `src/ScreenTranslator.Core/Abstractions/ITranslationProvider.cs`
- Create: `src/ScreenTranslator.Core/Abstractions/ISecretStore.cs`
- Create: `src/ScreenTranslator.Core/Abstractions/ISettingsStore.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Models/GeometryTests.cs`

- [ ] **Step 1：先写几何模型失败测试**

```csharp
[Fact]
public void Intersect_Clips_Rectangle_To_Monitor()
{
    var selection = new PixelRect(-20, 10, 100, 80);
    var monitor = new PixelRect(0, 0, 1920, 1080);

    Assert.Equal(new PixelRect(0, 10, 80, 80), selection.Intersect(monitor));
}
```

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter GeometryTests
```

Expected: FAIL，因为 `PixelRect` 尚未定义。

- [ ] **Step 2：实现不可变领域类型**

`Geometry.cs`:

```csharp
namespace ScreenTranslator.Core.Models;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsUsable => Width >= 8 && Height >= 8;

    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right <= left || bottom <= top
            ? new PixelRect(left, top, 0, 0)
            : new PixelRect(left, top, right - left, bottom - top);
    }
}
```

`OcrModels.cs` and `TranslationModels.cs`:

```csharp
namespace ScreenTranslator.Core.Models;

public sealed record OcrBlock(
    string Id,
    string Text,
    double Confidence,
    PixelRect BoundsInCapturePixels,
    int ReadingOrder);

public enum TranslationStyle { Natural, Literal, Learning }

public sealed record TranslationRequest(
    string SourceLanguage,
    string TargetLanguage,
    TranslationStyle Style,
    string Context,
    IReadOnlyList<OcrBlock> Blocks);

public sealed record TranslatedBlock(string Id, string SourceText, string Translation, PixelRect Bounds);
public sealed record TranslationResult(IReadOnlyList<TranslatedBlock> Blocks);
```

Interfaces:

```csharp
public interface IOcrEngine
{
    Task<IReadOnlyList<OcrBlock>> RecognizeAsync(CapturedBitmap bitmap, string? languageTag, CancellationToken cancellationToken);
}

public interface ITranslationProvider
{
    Task<string> TranslateRawAsync(TranslationRequest request, CancellationToken cancellationToken);
}
```

- [ ] **Step 3：运行模型测试**

Run: `.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter GeometryTests`

Expected: PASS。

- [ ] **Step 4：提交模型边界**

```powershell
git add src/ScreenTranslator.Core tests/ScreenTranslator.Core.Tests/Models
git commit -m "feat: define translation domain model"
```

## Task 3：实现 DeepSeek V4 客户端

**Files:**
- Create: `src/ScreenTranslator.Core/Translation/DeepSeekOptions.cs`
- Create: `src/ScreenTranslator.Core/Translation/DeepSeekTranslationProvider.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/FakeDeepSeekServer.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/DeepSeekTranslationProviderTests.cs`

- [ ] **Step 1：写出 HTTP 契约失败测试**

```csharp
[Fact]
public async Task Provider_Sends_V4_Flash_NonThinking_Json_Request()
{
    await using var server = await FakeDeepSeekServer.StartAsync("""
        {"choices":[{"message":{"content":"{\"blocks\":[]}"}}]}
        """);
    var provider = server.CreateProvider(apiKey: "test-key");

    await provider.TranslateRawAsync(TestRequests.OneBlock(), CancellationToken.None);

    Assert.Equal("/chat/completions", server.LastRequest.Path);
    Assert.Equal("Bearer test-key", server.LastRequest.Authorization);
    Assert.Contains("\"model\":\"deepseek-v4-flash\"", server.LastRequest.Body);
    Assert.Contains("\"thinking\":{\"type\":\"disabled\"}", server.LastRequest.Body);
    Assert.Contains("\"response_format\":{\"type\":\"json_object\"}", server.LastRequest.Body);
}
```

Expected: FAIL，因为 provider 尚未实现。

- [ ] **Step 2：实现 DeepSeek 配置**

```csharp
public sealed record DeepSeekOptions
{
    public Uri BaseUri { get; init; } = new("https://api.deepseek.com/");
    public string Model { get; init; } = "deepseek-v4-flash";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
```

- [ ] **Step 3：实现请求与错误映射**

`DeepSeekTranslationProvider` 必须：

1. 从 `ISecretStore.GetAsync("deepseek-api-key")` 获取密钥。
2. POST 到 `chat/completions`。
3. 发送 `thinking.type=disabled`、`stream=false`、`response_format.type=json_object`。
4. 系统提示词同时包含 `JSON`、block ID 保留规则和完整输出例子。
5. 401/403 映射为 `TranslationAuthenticationException`。
6. 429 映射为 `TranslationRateLimitException`。
7. 404 映射为 `TranslationConfigurationException`。
8. 5xx 和超时映射为 `TranslationUnavailableException`。
9. 返回 `choices[0].message.content`，空内容作为格式错误处理。

核心请求对象：

```csharp
var payload = new
{
    model = options.Model,
    messages = new object[]
    {
        new { role = "system", content = SystemPrompt },
        new { role = "user", content = JsonSerializer.Serialize(request, JsonOptions) }
    },
    thinking = new { type = "disabled" },
    response_format = new { type = "json_object" },
    stream = false
};
```

- [ ] **Step 4：验证所有状态码**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter DeepSeekTranslationProviderTests
```

Expected: 成功、401、404、429、500、超时和空内容测试全部 PASS。

- [ ] **Step 5：提交 DeepSeek 客户端**

```powershell
git add src/ScreenTranslator.Core/Translation tests/ScreenTranslator.IntegrationTests
git commit -m "feat: add DeepSeek V4 translation client"
```

## Task 4：实现结构化响应验证和翻译编排

**Files:**
- Create: `src/ScreenTranslator.Core/Translation/TranslationResponseValidator.cs`
- Create: `src/ScreenTranslator.Core/Translation/TranslationOrchestrator.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Translation/TranslationResponseValidatorTests.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Translation/TranslationOrchestratorTests.cs`

- [ ] **Step 1：写 block ID 验证失败测试**

```csharp
[Theory]
[InlineData("""{"blocks":[{"id":"b1","translation":"甲"},{"id":"b1","translation":"乙"}]}""")]
[InlineData("""{"blocks":[{"id":"unknown","translation":"甲"}]}""")]
[InlineData("""{"blocks":[]}""")]
public void Validate_Rejects_Missing_Duplicate_Or_Unknown_Ids(string json)
{
    var expected = new[] { "b1" };
    Assert.Throws<TranslationFormatException>(() => validator.Parse(json, expected));
}
```

- [ ] **Step 2：实现严格解析**

```csharp
public IReadOnlyDictionary<string, string> Parse(string json, IReadOnlyCollection<string> expectedIds)
{
    var document = JsonSerializer.Deserialize<DeepSeekTranslationEnvelope>(json, JsonOptions)
        ?? throw new TranslationFormatException("DeepSeek 返回了空 JSON。");
    var duplicate = document.Blocks.GroupBy(x => x.Id).FirstOrDefault(x => x.Count() > 1);
    if (duplicate is not null)
        throw new TranslationFormatException($"DeepSeek 返回了重复 ID：{duplicate.Key}");
    var actual = document.Blocks.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
    if (!actual.SetEquals(expectedIds))
        throw new TranslationFormatException("DeepSeek 返回的文本块与 OCR 文本块不一致。");
    return document.Blocks.ToDictionary(x => x.Id, x => x.Translation, StringComparer.Ordinal);
}
```

- [ ] **Step 3：实现一次格式修复**

`TranslationOrchestrator.TranslateAsync` 首次解析失败时，构造同一批 block 的修复请求，并把原始响应放入上下文；第二次失败直接抛出 `TranslationFormatException`。取消请求不得触发修复。

- [ ] **Step 4：验证正常、乱序、修复和取消**

Run: `.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter Translation`

Expected: 所有 Translation 测试 PASS。

- [ ] **Step 5：提交编排层**

```powershell
git add src/ScreenTranslator.Core/Translation tests/ScreenTranslator.Core.Tests/Translation
git commit -m "feat: validate structured translation responses"
```

## Task 5：实现设置存储和 DPAPI 密钥

**Files:**
- Create: `src/ScreenTranslator.Core/Settings/AppSettings.cs`
- Create: `src/ScreenTranslator.App/Services/Settings/JsonSettingsStore.cs`
- Create: `src/ScreenTranslator.App/Services/Settings/DpapiSecretStore.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Settings/AppSettingsTests.cs`

- [ ] **Step 1：写默认设置测试**

```csharp
[Fact]
public void Defaults_Match_Product_Decisions()
{
    var settings = new AppSettings();
    Assert.Equal("zh-CN", settings.TargetLanguage);
    Assert.Equal("deepseek-v4-flash", settings.DeepSeekModel);
    Assert.Equal(DisplayMode.SidePanel, settings.DisplayMode);
    Assert.False(settings.SaveHistory);
    Assert.False(settings.StartWithWindows);
}
```

- [ ] **Step 2：实现配置模型和原子写入**

配置路径固定为 `%LocalAppData%\ScreenTranslator\settings.json`。写入先保存 `settings.json.tmp`，成功后替换正式文件。损坏 JSON 重命名为 `settings.corrupt-<UTC时间>.json` 后恢复默认配置。

- [ ] **Step 3：实现当前用户 DPAPI**

```csharp
var encrypted = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(secret),
    optionalEntropy: null,
    DataProtectionScope.CurrentUser);
```

密钥路径固定为 `%LocalAppData%\ScreenTranslator\secrets\deepseek-api-key.bin`。读取失败返回明确的 `SecretStoreException`，不把密钥内容写入异常。

- [ ] **Step 4：运行设置测试**

Run: `.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter Settings`

Expected: 默认值、往返序列化和损坏恢复测试 PASS。

- [ ] **Step 5：提交设置与密钥**

```powershell
git add src/ScreenTranslator.Core/Settings src/ScreenTranslator.App/Services/Settings tests/ScreenTranslator.Core.Tests/Settings
git commit -m "feat: persist settings and protect API key"
```

## Task 6：实现文本布局和旁显定位

**Files:**
- Create: `src/ScreenTranslator.Core/Layout/TextLayoutService.cs`
- Create: `src/ScreenTranslator.Core/Layout/PanelPlacementService.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Layout/TextLayoutServiceTests.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Layout/PanelPlacementServiceTests.cs`

- [ ] **Step 1：写阅读顺序与边缘避让测试**

```csharp
[Fact]
public void Place_Uses_Left_When_Right_Does_Not_Fit()
{
    var workArea = new PixelRect(0, 0, 1920, 1080);
    var selection = new PixelRect(1700, 100, 200, 100);
    Assert.Equal(
        new PixelRect(1268, 100, 420, 360),
        service.Place(selection, new PixelSize(420, 360), workArea, gap: 12));
}
```

- [ ] **Step 2：实现稳定阅读顺序**

按块中心点 Y 排序；中心点 Y 差小于较小块高度的 40% 时视为同行，再按 X 排序。空文本被移除，连续空白压缩为一个空格。

- [ ] **Step 3：实现右、左、下、上的定位优先级**

面板与选区间距 12 有效像素，最终矩形必须完全位于当前显示器工作区。四个方向都放不下时，把面板限制到工作区内并允许内容滚动。

- [ ] **Step 4：运行布局测试**

Run: `.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter Layout`

Expected: PASS。

- [ ] **Step 5：提交布局算法**

```powershell
git add src/ScreenTranslator.Core/Layout tests/ScreenTranslator.Core.Tests/Layout
git commit -m "feat: order OCR text and place result panels"
```

## Task 7：实现显示器捕获和 DPI 坐标

**Files:**
- Create: `src/ScreenTranslator.App/Services/Capture/IScreenCaptureService.cs`
- Create: `src/ScreenTranslator.App/Services/Capture/GdiScreenCaptureService.cs`
- Create: `src/ScreenTranslator.App/Services/Capture/WindowsGraphicsCaptureService.cs`
- Create: `src/ScreenTranslator.App/Services/Capture/FallbackScreenCaptureService.cs`
- Create: `src/ScreenTranslator.App/Interop/GraphicsCaptureItemInterop.cs`
- Modify: `src/ScreenTranslator.App/Interop/NativeMethods.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Models/MonitorTransformTests.cs`

- [ ] **Step 1：写混合 DPI 转换测试**

```csharp
[Fact]
public void Physical_To_Dip_Uses_Target_Monitor_Scale()
{
    var transform = new MonitorTransform(new PixelRect(1920, 0, 2560, 1440), 1.5);
    Assert.Equal(new DipRect(1280, 0, 640, 480),
        transform.ToDip(new PixelRect(1920, 0, 960, 720)));
}
```

- [ ] **Step 2：实现 GDI 后备捕获**

使用 `Graphics.CopyFromScreen` 按每个显示器物理边界捕获 `PixelFormat.Format32bppPArgb` 位图，立即转换为冻结的 `BitmapSource`，并在 `finally` 中释放 GDI 对象。

- [ ] **Step 3：实现 Windows.Graphics.Capture 首选捕获**

通过 `IGraphicsCaptureItemInterop.CreateForMonitor` 创建 `GraphicsCaptureItem`；使用 D3D11 frame pool 获取单帧，复制到 CPU 可读表面并转换为 `BitmapSource`。捕获会话只存在到首帧完成，超时 2 秒后取消并触发后备。

- [ ] **Step 4：实现回退策略**

`FallbackScreenCaptureService` 先调用 WGC；遇到 `PlatformNotSupportedException`、授权错误、设备创建失败或 2 秒超时则调用 GDI。用户取消必须直接传播，不得启动 GDI。

- [ ] **Step 5：运行坐标测试并做本机截图冒烟测试**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.Core.Tests --filter MonitorTransform
.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App
```

Expected: 测试 PASS，构建成功；调试命令输出所有显示器物理边界和截图尺寸，两者一致。

- [ ] **Step 6：提交捕获层**

```powershell
git add src/ScreenTranslator.App/Services/Capture src/ScreenTranslator.App/Interop tests/ScreenTranslator.Core.Tests/Models
git commit -m "feat: capture monitors with Windows fallback"
```

## Task 8：实现 Windows 本地 OCR

**Files:**
- Create: `src/ScreenTranslator.App/Services/Ocr/SoftwareBitmapConverter.cs`
- Create: `src/ScreenTranslator.App/Services/Ocr/WindowsOcrEngine.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/WindowsOcrEngineTests.cs`
- Create: `tests/ScreenTranslator.IntegrationTests/Fixtures/english.png`
- Create: `tests/ScreenTranslator.IntegrationTests/Fixtures/chinese.png`

- [ ] **Step 1：建立固定图片 OCR 测试**

英文 fixture 包含 `Hello screen`，中文 fixture 包含 `屏幕翻译`。测试要求返回至少一个 block、非空文本、合法正尺寸坐标。

- [ ] **Step 2：实现 BitmapSource 到 SoftwareBitmap 转换**

使用 `PngBitmapEncoder` 写入 `InMemoryRandomAccessStream`，通过 `BitmapDecoder` 解码为 `SoftwareBitmap`，并统一转换为 `BitmapPixelFormat.Bgra8` 与 `BitmapAlphaMode.Premultiplied`。

- [ ] **Step 3：实现 WindowsOcrEngine**

未指定语言时调用 `OcrEngine.TryCreateFromUserProfileLanguages()`；指定 BCP-47 标签时使用 `TryCreateFromLanguage(new Language(tag))`。把每一行单词合并为一个 `OcrBlock`，边界取该行所有 `OcrWord.BoundingRect` 的并集，置信度在系统未提供时使用 `1.0`。

- [ ] **Step 4：处理语言包和无文本**

创建引擎失败时抛出 `OcrLanguageUnavailableException`，包含语言标签但不包含系统路径。无文本返回空列表，不抛异常。

- [ ] **Step 5：运行 OCR 集成测试**

Run: `.\.tools\dotnet\dotnet.exe test tests/ScreenTranslator.IntegrationTests --filter WindowsOcrEngineTests`

Expected: 当前系统存在对应 OCR 语言时 PASS；缺失语言测试验证异常类型和消息。

- [ ] **Step 6：提交 OCR**

```powershell
git add src/ScreenTranslator.App/Services/Ocr tests/ScreenTranslator.IntegrationTests
git commit -m "feat: recognize selected text with Windows OCR"
```

## Task 9：实现全局快捷键和框选窗口

**Files:**
- Create: `src/ScreenTranslator.App/Services/Hotkeys/GlobalHotkeyService.cs`
- Create: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml`
- Create: `src/ScreenTranslator.App/Windows/SelectionOverlayWindow.xaml.cs`
- Create: `src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs`
- Modify: `src/ScreenTranslator.App/Interop/NativeMethods.cs`

- [ ] **Step 1：实现 RegisterHotKey 生命周期**

默认注册 `Alt + Shift + T`，窗口销毁和服务释放时调用 `UnregisterHotKey`。注册失败抛出 `HotkeyConflictException`。WM_HOTKEY 只发布 `CaptureRequested` 事件。

- [ ] **Step 2：实现每显示器冻结框选层**

窗口使用显示器物理截图作背景，叠加 55% 黑色遮罩。鼠标按下记录起点，移动更新矩形，松开返回 `PixelRect`。选区内部显示原图，边框使用系统强调色，右下角显示物理像素尺寸。

- [ ] **Step 3：实现取消与限制**

`Esc` 完成 `TaskCompletionSource` 的取消；小于 8×8 像素返回取消；拖拽限制在开始显示器内。窗口关闭后解除鼠标捕获并释放截图。

- [ ] **Step 4：本机手工验证**

验证单屏、双屏、125% 和 150% 缩放；选区坐标与裁剪结果误差不得超过 1 物理像素。

- [ ] **Step 5：提交快捷键和框选**

```powershell
git add src/ScreenTranslator.App/Services/Hotkeys src/ScreenTranslator.App/Windows/SelectionOverlayWindow* src/ScreenTranslator.App/ViewModels/SelectionOverlayViewModel.cs src/ScreenTranslator.App/Interop
git commit -m "feat: select screen regions from a global hotkey"
```

## Task 10：实现 Windows 11 Fluent 设置界面

**Files:**
- Modify: `src/ScreenTranslator.App/App.xaml`
- Create: `src/ScreenTranslator.App/Windows/MainWindow.xaml`
- Create: `src/ScreenTranslator.App/Windows/MainWindow.xaml.cs`
- Create: `src/ScreenTranslator.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/ScreenTranslator.App/ViewModels/TranslationSettingsViewModel.cs`
- Create: `src/ScreenTranslator.App/Pages/GeneralPage.xaml`
- Create: `src/ScreenTranslator.App/Pages/TranslationPage.xaml`
- Create: `src/ScreenTranslator.App/Pages/AppearancePage.xaml`
- Create: `src/ScreenTranslator.App/Pages/HotkeyPage.xaml`
- Create: `src/ScreenTranslator.App/Pages/PrivacyPage.xaml`
- Create: `src/ScreenTranslator.App/Pages/AboutPage.xaml`

- [ ] **Step 1：加载 Fluent 主题资源**

```xml
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <ui:ThemesDictionary Theme="System" />
      <ui:ControlsDictionary />
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>
```

- [ ] **Step 2：实现 FluentWindow 与 NavigationView**

导航固定为“常规、翻译、外观、快捷键、隐私、关于”。设置入口放底部。窗口使用 Mica，宽 920、高 640、最小宽 760、最小高 520；Windows 10 和透明效果关闭时使用主题纯色。

- [ ] **Step 3：实现 DeepSeek 设置页**

包含遮罩 API Key 输入框、模型下拉框、连接测试、状态提示和高级 Base URL 展开区域。模型选项只有 `deepseek-v4-flash` 与 `deepseek-v4-pro`。测试按钮运行期间禁用，成功显示耗时，失败显示脱敏错误。

- [ ] **Step 4：实现主题和可访问性**

字体使用 Segoe UI Variable，正文 14、辅助文字 12、标题 Semibold。主题支持跟随系统、浅色、深色；强调色跟随系统。高对比度时禁用 Mica/Acrylic，动画在系统减少动画时关闭。

- [ ] **Step 5：运行构建并视觉检查**

Run: `.\.tools\dotnet\dotnet.exe build src/ScreenTranslator.App`

Expected: 0 warnings、0 errors。手工检查浅色、深色、125%/150% DPI、键盘 Tab 导航和高对比度。

- [ ] **Step 6：提交 Fluent UI**

```powershell
git add src/ScreenTranslator.App/App.xaml src/ScreenTranslator.App/Windows/MainWindow* src/ScreenTranslator.App/Pages src/ScreenTranslator.App/ViewModels
git commit -m "feat: add Windows 11 Fluent settings UI"
```

## Task 11：实现旁显和原位覆盖

**Files:**
- Create: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml`
- Create: `src/ScreenTranslator.App/Windows/SidePanelWindow.xaml.cs`
- Create: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml`
- Create: `src/ScreenTranslator.App/Windows/TextOverlayWindow.xaml.cs`
- Create: `src/ScreenTranslator.App/ViewModels/TranslationResultViewModel.cs`
- Create: `src/ScreenTranslator.App/Services/Overlay/OverlayManager.cs`
- Create: `src/ScreenTranslator.App/Services/Overlay/WindowStyleService.cs`

- [ ] **Step 1：实现旁显面板**

旁显窗口无任务栏图标、不抢焦点、Acrylic 后备纯色，最大宽 420、最大高为工作区 70%。按钮包含查看原文、复制、固定、切换覆盖、重译、关闭。

- [ ] **Step 2：实现原位文本块**

每个 `TranslatedBlock` 创建独立窗口，位置由物理坐标转换为目标显示器 DIP。背景取截图块边缘平均色后叠加 88% 不透明度；圆角 8；字体从原框高度估算并限制在 12–32 DIP。

- [ ] **Step 3：实现窗口样式**

使用 Win32 扩展样式 `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT`。控制柄展开时移除 `WS_EX_TRANSPARENT`，关闭后恢复。按住 Alt 时隐藏全部原位覆盖，松开后恢复。

- [ ] **Step 4：实现固定和模式切换**

非固定窗口在下一会话成功后关闭；固定窗口保留。模式切换复用同一 `TranslationResult`，不得重新调用 DeepSeek。

- [ ] **Step 5：手工验证覆盖交互**

验证文本块位置、鼠标穿透、Alt 对照、复制、固定、关闭、屏幕边缘避让和浅深主题。

- [ ] **Step 6：提交显示层**

```powershell
git add src/ScreenTranslator.App/Windows/SidePanelWindow* src/ScreenTranslator.App/Windows/TextOverlayWindow* src/ScreenTranslator.App/ViewModels/TranslationResultViewModel.cs src/ScreenTranslator.App/Services/Overlay
git commit -m "feat: show translations beside or over source text"
```

## Task 12：连接托盘、单实例和完整会话

**Files:**
- Modify: `src/ScreenTranslator.App/App.xaml.cs`
- Create: `src/ScreenTranslator.App/Infrastructure/ServiceRegistration.cs`
- Create: `src/ScreenTranslator.App/Infrastructure/SingleInstanceGuard.cs`
- Create: `src/ScreenTranslator.App/Services/Tray/TrayIconService.cs`
- Create: `src/ScreenTranslator.App/Services/CaptureCoordinator.cs`
- Create: `src/ScreenTranslator.Core/Sessions/TranslationSession.cs`
- Create: `src/ScreenTranslator.Core/Sessions/TranslationSessionCoordinator.cs`
- Test: `tests/ScreenTranslator.Core.Tests/Sessions/TranslationSessionCoordinatorTests.cs`

- [ ] **Step 1：写迟到响应测试**

```csharp
[Fact]
public async Task Older_Session_Cannot_Publish_After_New_Session_Starts()
{
    var first = coordinator.Start();
    var second = coordinator.Start();

    Assert.True(first.CancellationToken.IsCancellationRequested);
    Assert.False(coordinator.TryPublish(first.Id));
    Assert.True(coordinator.TryPublish(second.Id));
}
```

- [ ] **Step 2：实现会话状态机**

状态为 `Idle -> Selecting -> Ocr -> Translating -> Displayed`，任意处理中状态可转 `Cancelled` 或 `Failed`。新会话原子替换旧会话并取消旧 token。

- [ ] **Step 3：实现 CaptureCoordinator**

顺序固定为：隐藏非固定窗口、捕获显示器、显示框选层、裁剪、OCR、布局、DeepSeek 翻译、验证当前 session、显示结果。每个阶段检查取消令牌。无 OCR 文本显示轻提示并结束。

- [ ] **Step 4：实现托盘和单实例**

命名 Mutex 为 `ScreenTranslator.Singleton.v1`。第二实例通过命名事件通知首实例打开设置后退出。托盘菜单包含开始翻译、显示/隐藏译文、设置、暂停快捷键、退出。

- [ ] **Step 5：注册所有服务**

`ServiceRegistration` 使用 `Host.CreateDefaultBuilder` 注册单例服务；`HttpClient` 使用 DeepSeek Base URL 和 15 秒超时；UI 服务只在 Dispatcher 线程调用。

- [ ] **Step 6：运行全部自动测试**

Run:

```powershell
.\.tools\dotnet\dotnet.exe test ScreenTranslator.sln
```

Expected: 全部 PASS，0 skipped；需要语言包的 OCR 环境测试单独归入显式 `OcrEnvironment` 分类，不影响核心 CI。

- [ ] **Step 7：提交完整流程**

```powershell
git add src tests
git commit -m "feat: connect capture OCR translation and overlays"
```

## Task 13：MSIX 打包、隐私检查和发布验证

**Files:**
- Create: `src/ScreenTranslator.Package/ScreenTranslator.Package.wapproj`
- Create: `src/ScreenTranslator.Package/Package.appxmanifest`
- Create: `src/ScreenTranslator.App/app.manifest`
- Create: `src/ScreenTranslator.App/Assets/app.ico`
- Create: `README.md`
- Create: `docs/testing/manual-test-matrix.md`

- [ ] **Step 1：配置清单**

包标识使用 `ScreenTranslator.Desktop`，架构 x64，最低版本 Windows 10 19041。清单启用包身份所需桌面入口，不声明摄像头、麦克风、位置、文档库或图片库能力。

- [ ] **Step 2：生成自包含发布**

Run:

```powershell
.\.tools\dotnet\dotnet.exe publish src/ScreenTranslator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

Expected: `src/ScreenTranslator.App/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/ScreenTranslator.App.exe` 存在。

- [ ] **Step 3：生成 MSIX**

使用已安装的 Windows SDK MSBuild 构建：

```powershell
msbuild src/ScreenTranslator.Package/ScreenTranslator.Package.wapproj /restore /p:Configuration=Release /p:Platform=x64 /p:AppxBundle=Never
```

Expected: `AppPackages` 下生成 x64 MSIX。若本机没有完整 MSIX 构建工具，保留自包含发布目录作为可运行交付物，并在 README 写明生成签名 MSIX 需要 Windows SDK/Visual Studio Build Tools。

- [ ] **Step 4：执行安全扫描**

Run:

```powershell
Get-ChildItem -Recurse src,tests | Select-String -Pattern 'sk-[A-Za-z0-9]|Authorization:\s*Bearer\s+[A-Za-z0-9]'
```

Expected: 没有真实密钥。日志测试验证请求正文、Authorization 和截图不被记录。

- [ ] **Step 5：执行手工验收矩阵**

逐项记录 Windows 11 浅色/深色、100%/125%/150%/200% DPI、双屏负坐标、浏览器/PDF/图片/无边框游戏、DeepSeek 两模型、断网/401/429/超时、覆盖/旁显/固定/取消。

- [ ] **Step 6：最终构建与提交**

Run:

```powershell
.\.tools\dotnet\dotnet.exe clean ScreenTranslator.sln -c Release
.\.tools\dotnet\dotnet.exe restore ScreenTranslator.sln
.\.tools\dotnet\dotnet.exe test ScreenTranslator.sln -c Release --no-restore
.\.tools\dotnet\dotnet.exe publish src/ScreenTranslator.App -c Release -r win-x64 --self-contained true --no-restore
```

Expected: 所有测试 PASS，发布目录包含可运行程序。

```powershell
git add src README.md docs/testing
git commit -m "release: package screen translator for Windows"
```

## 完成定义

- 快捷键可以在单屏和多屏环境中启动框选。
- 框选坐标、OCR 坐标和覆盖坐标在混合 DPI 下保持一致。
- DeepSeek V4 Flash/Pro 均可测试连接和完成结构化翻译。
- 默认不上传截图、不保存截图、不保存历史。
- API Key 只以当前用户 DPAPI 密文保存。
- 覆盖与旁显可切换、固定、复制、重译和取消。
- 设置界面符合 Windows 11 Fluent 视觉规范并支持浅色、深色、高对比度后备。
- `dotnet test ScreenTranslator.sln -c Release` 全部通过。
- 生成 win-x64 自包含发布目录；具备 MSIX 工具链时同时生成 MSIX。
