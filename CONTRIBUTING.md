# 参与贡献

感谢你愿意改进屏幕翻译。欢迎提交错误报告、功能建议和代码贡献。

## 提交问题

- 先搜索现有 Issue，避免重复提交。
- 错误报告请附上 Windows 版本、复现步骤、预期结果和实际结果。
- 涉及界面或译文错位时，建议附上截图，并说明显示缩放比例、浏览器和页面类型。
- 不要在 Issue、日志或截图中公开 DeepSeek API Key。

## 本地开发

项目需要 Windows 10/11，仓库内的脚本会使用工作区本地 .NET SDK：

```powershell
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 restore ScreenTranslator.sln --configfile NuGet.Config
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 build ScreenTranslator.sln --no-restore
powershell -ExecutionPolicy Bypass -File eng\dotnet.ps1 test ScreenTranslator.sln --no-build
```

浏览器扩展的测试：

```powershell
node --test browser-extension\tests\content.test.js browser-extension\tests\document-state.test.js
```

## 提交代码

1. 从 `main` 创建一个主题分支。
2. 保持改动聚焦，并为行为变化补充测试。
3. 提交前运行完整构建和测试。
4. 在 Pull Request 中说明问题、解决方式和验证结果。

提交贡献即表示你同意贡献内容按仓库的 MIT 许可证发布。
