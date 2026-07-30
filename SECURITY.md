# 安全政策

## 报告漏洞

请不要通过公开 Issue 报告可能危及用户隐私、API Key 或本机安全的问题。

请在本仓库的 GitHub 页面中使用 **Security → Report a vulnerability** 私下提交报告，并包含：

- 受影响版本或提交
- 复现步骤
- 可能造成的影响
- 可行的缓解或修复建议（如有）

在问题得到确认和修复前，请避免公开漏洞细节。

## API Key 安全

屏幕翻译使用 Windows DPAPI 在当前用户范围内保护 DeepSeek API Key。请勿提交真实 Key、解密后的配置、诊断日志或包含敏感信息的截图。若 Key 曾被公开，请立即在 DeepSeek 控制台撤销并重新生成。
