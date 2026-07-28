# 屏幕翻译

一款面向 Windows 10/11 的原生桌面翻译工具。按下全局快捷键后框选屏幕区域，软件在本地执行 OCR，再调用 DeepSeek V4 翻译，并把结果覆盖在原文位置或显示在原文旁边。

## 功能

- 默认快捷键：`Alt + Shift + T`
- 多显示器与混合 DPI 框选
- Windows 本地 OCR
- DeepSeek `deepseek-v4-flash` / `deepseek-v4-pro`
- 原位覆盖与旁边浮窗
- Windows 11 Fluent、Mica、浅色和深色主题
- API Key 使用当前 Windows 用户范围的 DPAPI 加密
- 默认不上传截图、不保存截图、不保存翻译历史

## 开发环境

仓库使用工作区本地 .NET SDK，避免修改系统全局环境：

```powershell
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 restore ScreenTranslator.sln --configfile NuGet.Config
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 build ScreenTranslator.sln --no-restore
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 test ScreenTranslator.sln --no-build
```

## 配置 DeepSeek

1. 启动软件并打开“翻译”设置。
2. 输入 DeepSeek API Key。
3. 选择 `deepseek-v4-flash` 或 `deepseek-v4-pro`。
4. 点击“测试连接”。
5. 保存后使用 `Alt + Shift + T` 开始框选。

默认接口地址为 `https://api.deepseek.com`。短文本翻译使用非思考模式和 JSON 输出，以降低延迟并保持 OCR 文本块与译文一一对应。

## 隐私

截图只存在于当前进程内存。默认只把 OCR 识别出的文本发送给 DeepSeek；程序不会把截图上传到翻译 API。API Key 不会写入普通配置文件或日志。

## 发布

```powershell
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 publish src\ScreenTranslator.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\ScreenTranslator-win-x64
```

自包含程序位于 `dist\ScreenTranslator-win-x64\ScreenTranslator.exe`，目标电脑无需另装 .NET。

## 已知限制

- 屏幕捕获当前使用 GDI 兼容后端；Windows 图形捕获的 D3D11 单帧后端尚未启用。
- UAC 安全桌面、DRM 视频和部分受保护窗口可能无法截图。
- 当前版本的全局快捷键固定为 `Alt + Shift + T`。
