# 屏译安装指南

## 推荐：一键安装

1. 打开项目的 [GitHub Releases](https://github.com/piyankaygoemz75-hash/ScreenTranslator/releases)。
2. 下载最新版 `ScreenTranslator-Setup-x64.exe`。
3. 可选：下载 `SHA256SUMS.txt`，在 PowerShell 中执行：

   ```powershell
   Get-FileHash .\ScreenTranslator-Setup-x64.exe -Algorithm SHA256
   ```

   输出应与校验文件中安装器对应的一行完全一致。
4. 双击安装器并完成安装。程序默认安装到当前用户目录，不需要管理员权限。
5. 打开“翻译”设置，填写自己的 DeepSeek API Key 并测试连接。

公开构建暂未进行商业代码签名。Windows SmartScreen 可能显示“未知发布者”；请确认文件来自本项目 Release 且哈希一致。

## 连接 Chrome / Edge

安装器会自动安装桌面连接组件，但浏览器安全策略要求用户确认加载扩展一次：

1. 打开屏译“常规”页。
2. 点击“安装到 Chrome”或“安装到 Edge”。
3. 软件会自动打开浏览器扩展管理页、扩展目录，并复制目录路径。
4. 开启扩展页的“开发者模式”。
5. 点击“加载已解压的扩展”，选择刚才打开的 `browser-extension` 文件夹。
6. 回到屏译，等待状态显示“已连接”。

如果浏览器更新、移动安装目录或连接异常，点击“修复连接”即可重新注册桌面组件。扩展无需重复加载。

## 便携版

`ScreenTranslator-Portable-x64.zip` 适合不希望安装的用户。解压后直接运行 `ScreenTranslator.exe`，目标电脑无需另装 .NET。首次启动会尝试注册浏览器连接组件；扩展仍按上方步骤确认加载一次。

`ScreenTranslator-Browser-Extension.zip` 是单独提供的扩展副本，通常无需另外下载，因为安装版和便携版都已包含扩展目录。

## 卸载

可在 Windows“设置 > 应用 > 已安装的应用”中卸载屏译。默认卸载会保留 API Key、快捷键和界面设置，方便以后重装；如果希望彻底清除，可在卸载窗口勾选“同时删除 API Key、快捷键和个人设置”。

卸载程序会同时移除 Chrome / Edge 的桌面连接注册，不会修改浏览器内的其他扩展。
