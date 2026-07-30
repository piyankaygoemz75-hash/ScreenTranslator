# 屏幕翻译

一款面向 Windows 10/11 的原生桌面翻译工具。按下全局快捷键后框选屏幕区域，软件在本地执行 OCR，再调用 DeepSeek V4 翻译，并把结果覆盖在原文位置或显示在原文旁边。

> 当前项目仍在积极开发中。首次使用前需要自行申请并配置 DeepSeek API Key。

## 安装

普通用户建议到 [Releases](https://github.com/piyankaygoemz75-hash/ScreenTranslator/releases) 下载最新版 `ScreenTranslator-Setup-x64.exe`，双击即可安装，不需要管理员权限，也不需要另外安装 .NET。

安装程序会自动注册 Chrome / Edge 与桌面程序之间的连接组件。受浏览器安全策略限制，扩展本身仍需在软件“常规”页按引导确认加载一次。详细步骤、便携版用法和卸载说明见 [安装指南](docs/installation.md)。

当前公开构建尚未购买代码签名证书，因此 Windows SmartScreen 可能显示“未知发布者”。请只从本仓库 Release 下载，并用同一页面的 `SHA256SUMS.txt` 核对文件。

## 功能

- 可录制和保存的全局快捷键（默认 `Alt + Shift + T`）
- 多显示器与混合 DPI 框选
- Windows 本地 OCR
- DeepSeek `deepseek-v4-flash` / `deepseek-v4-pro`
- 原位覆盖与旁边浮窗
- 译文底板在 100% 设置下完全不透明，降低与复杂页面文字混叠
- 原位译文支持右键清除此条或清除全部；切换到其他应用时自动隐藏，切回来源窗口后恢复
- Chrome / Edge 普通网页原位译文滚动跟随
- 连续框选：不中断地框选多个区域，按顺序识别和翻译，最多排队 5 条
- 可拖动、可滚轮滚动且操作栏固定可见的旁边浮窗
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

快捷键可在“快捷键”设置页直接录制修改；如果新组合被其他软件占用，屏译会恢复上一个可用组合。

默认接口地址为 `https://api.deepseek.com`。短文本翻译使用非思考模式和 JSON 输出，以降低延迟并保持 OCR 文本块与译文一一对应。

## 连续框选

普通快捷键打开框选界面后默认为单条框选，按 `Tab` 可随时切换为多条框选；再次按 `Tab` 可切回单条，并在当前选择完成后结束本轮。也可以在“常规”页点击“连续框选”，或在托盘菜单选择“连续框选翻译”，直接以多条模式开始。

每次松开鼠标后，当前区域会立即进入队列，OCR 和 DeepSeek 请求按框选顺序逐条处理。多条模式的总框选次数不限，但尚未完成的任务最多积压 5 条；真正达到积压上限时，本轮框选会停止并显示提醒，已经排队的内容仍会翻译完成。按 `Esc` 或右键也只结束继续框选，不取消队列。旁显模式会把多条结果汇总到一个可滚动面板，覆盖模式则保留每一条译文。

## 启用 Chrome / Edge 网页跟随

1. 启动屏译，在“常规”页确认“网页译文跟随”已开启。
2. 点击“安装到 Chrome”或“安装到 Edge”；程序会自动打开扩展页和扩展目录，并复制目录路径。
3. 在扩展页开启开发者模式。
4. 选择“加载已解压的扩展”，选中程序打开的 `browser-extension` 文件夹。
5. 浏览器状态显示“已连接”后，在普通网页上使用“原位覆盖”翻译。

网页滚动只移动已经生成的译文，不会重新截图、OCR 或请求 DeepSeek。切换标签页、导航、缩放、最小化或改变浏览器窗口大小时，旧译文会自动失效。浏览器内置 PDF、内部页面和禁止内容脚本的页面继续使用静态翻译。

桌面程序刚启动而扩展仍在重连时，译文会先立即显示，再在后台自动接入网页跟随，无需重新框选。

网页跟随只记录连接、标签页数字 ID 和滚动增量等诊断状态，不记录网址、网页正文、截图或 API Key。诊断文件位于 `%LOCALAPPDATA%\ScreenTranslator\browser-follow.log`，超过 1 MiB 会自动从头记录。

## 隐私

截图只存在于当前进程内存。默认只把 OCR 识别出的文本发送给 DeepSeek；程序不会把截图上传到翻译 API。API Key 不会写入普通配置文件或日志。

## 从源码构建发布包

```powershell
powershell -ExecutionPolicy Bypass -File eng\build-release.ps1 -Version 0.2.2
```

脚本会生成自包含程序、便携 ZIP 和浏览器扩展 ZIP。安装器还需要 Inno Setup 6；正式 tag 的 GitHub Actions 会自动构建安装器、执行静默安装/卸载测试、生成 SHA-256 并发布 Release。

## 已知限制

- 屏幕捕获当前使用 GDI 兼容后端；Windows 图形捕获的 D3D11 单帧后端尚未启用。
- UAC 安全桌面、DRM 视频和部分受保护窗口可能无法截图。
- 浏览器跟随仅支持安装配套扩展后的 Chrome / Edge 普通 `http://`、`https://` 页面。

## 参与贡献

欢迎提交 Issue 和 Pull Request。开始前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)；安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。

## 开源许可

本项目使用 [MIT License](LICENSE) 开源。
