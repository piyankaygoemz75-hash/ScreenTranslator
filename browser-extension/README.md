# 屏译网页跟随扩展

这个 Manifest V3 扩展把普通网页的滚动几何信息发送给屏译桌面程序，使已经显示的原位译文跟随页面移动。它同时支持 Chrome 和 Edge，固定扩展 ID 为 `plpgmkbadcfnkmolbeecggbbopilajed`。

## 安装

先安装并运行屏译桌面程序。桌面程序负责注册 Native Messaging Host；只加载扩展而没有运行桌面程序时，扩展会在后台限速重连，不影响网页使用。

### Chrome

1. 打开 `chrome://extensions`。
2. 开启右上角的“开发者模式”。
3. 点击“加载已解压的扩展程序”。
4. 选择发布目录中的 `browser-extension` 文件夹。
5. 确认扩展 ID 是 `plpgmkbadcfnkmolbeecggbbopilajed`。

### Edge

1. 打开 `edge://extensions`。
2. 开启左侧的“开发人员模式”。
3. 点击“加载解压缩的扩展”。
4. 选择发布目录中的 `browser-extension` 文件夹。
5. 确认扩展 ID 是 `plpgmkbadcfnkmolbeecggbbopilajed`。

## 支持范围

- 支持普通 `http://` 和 `https://` 页面。
- 支持页面根滚动以及普通嵌套滚动容器。
- Chrome/Edge 内部页面、扩展页面、浏览器内置 PDF 阅读器和其他禁止内容脚本的页面不受支持；屏译会保留静态翻译行为。
- 子框架滚动会被标记为子框架事件，由桌面程序使当前跟随会话失效，而不会把框架内坐标误当成屏幕坐标。

## 隐私

扩展只发送随机文档标识、标签页和浏览器窗口数字 ID、窗口边界、设备像素比、滚动增量及滚动容器矩形。它不读取或发送网页正文、输入内容、页面标题、URL、Cookie 或浏览历史。OCR 图片和 DeepSeek 请求均由桌面程序按其自身隐私设置处理。

## 开发验证

在仓库根目录运行：

```powershell
node --test browser-extension/tests/content.test.js
node --check browser-extension/background.js
node --check browser-extension/scroll-accumulator.js
node --check browser-extension/content.js
```
