# 屏幕翻译

一款面向 Windows 10/11 的原生桌面翻译工具。按下全局快捷键后框选屏幕区域，软件在本地执行 OCR，再调用 DeepSeek V4 翻译，并把结果覆盖在原文位置或显示在原文旁边。

## 功能

- 可录制和保存的全局快捷键（默认 `Alt + Shift + T`）
- 多显示器与混合 DPI 框选
- Windows 本地 OCR
- DeepSeek `deepseek-v4-flash` / `deepseek-v4-pro`
- 原位覆盖与旁边浮窗
- 原位译文支持右键清除此条或清除全部；切换到其他应用时自动隐藏，切回来源窗口后恢复
- Chrome / Edge 普通网页原位译文滚动跟随
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

## 启用 Chrome / Edge 网页跟随

1. 启动屏译，在“常规”页确认“网页译文跟随”已开启。
2. 点击“打开扩展文件夹”。
3. 在 `chrome://extensions` 或 `edge://extensions` 开启开发者模式。
4. 选择“加载已解压的扩展”，加载发布目录中的 `browser-extension` 文件夹。
5. 浏览器状态显示“已连接”后，在普通网页上使用“原位覆盖”翻译。

网页滚动只移动已经生成的译文，不会重新截图、OCR 或请求 DeepSeek。切换标签页、导航、缩放、最小化或改变浏览器窗口大小时，旧译文会自动失效。浏览器内置 PDF、内部页面和禁止内容脚本的页面继续使用静态翻译。

桌面程序刚启动而扩展仍在重连时，译文会先立即显示，再在后台自动接入网页跟随，无需重新框选。

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
- 浏览器跟随仅支持安装配套扩展后的 Chrome / Edge 普通 `http://`、`https://` 页面。
