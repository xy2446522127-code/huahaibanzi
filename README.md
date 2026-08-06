# 花海剪贴板

花海剪贴板是一款面向 Windows 10 / 11 的轻量桌面剪贴板工具。界面采用玫瑰紫液态玻璃设计，支持文本、链接、图片和文件历史，所有数据默认只保存在当前电脑，不需要登录账号。

## 主要功能

- 右键双击唤出，也可以录入自定义键盘或鼠标快捷方式。
- 在鼠标指针附近显示，并在可见期间保持置顶；隐藏后继续在托盘后台运行。
- 文本、链接、图片和文件分类，支持搜索、置顶、收藏和单条删除。
- 点击历史记录后立即复制并隐藏面板。
- 3 天、7 天或 30 天自动清理普通历史，收藏和置顶内容不受影响。
- 五套主题、背景透明度、等比例缩放、背景花瓣和减少动态效果设置。
- 密码管理器、浏览器无痕窗口以及用户排除列表中的应用不会写入历史。
- 可选开机自启、后台运行和 GitHub Release 更新检查。

## 安装

正式安装程序为：

```text
dist/HuahaiClipboard-Setup.exe
```

安装程序支持选择安装位置，并按照当前产品要求拒绝安装到 C 盘。安装后会创建桌面、开始菜单和 Windows“应用和功能”卸载入口。

当前安装程序没有商业代码签名证书，Windows SmartScreen 可能显示未知发布者提示。请从本仓库或可信发布页面获取安装包，并核对发布说明中的 SHA-256。

## 本机数据与隐私

默认数据目录：

```text
%LOCALAPPDATA%\HuahaiClipboard
```

设置、历史、图片缓存和窗口位置只保存在本机。文本历史使用当前 Windows 用户范围的 DPAPI 保护。卸载程序只删除安装目录、快捷方式和本应用拥有的开机启动项，默认保留上述用户数据。

## 从源码构建

要求：Windows 10/11 x64、.NET 8 SDK、Visual Studio 2022 Build Tools（Windows App SDK 生成组件）、PowerShell 5.1 或更高版本。

```powershell
dotnet test tests\HuahaiClipboard.Core.Tests\HuahaiClipboard.Core.Tests.csproj -c Release

& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' `
  src\HuahaiClipboard.App\HuahaiClipboard.App.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 /p:SelfContained=false /p:Version=1.1.1 `
  /p:OutDir="$PWD\dist\webview-build-1.1.1\" /restore /m

.\installer\Fetch-Prerequisites.ps1 -Destination dist\prerequisites
.\installer\Build-Installer.ps1 `
  -PublishRoot dist\webview-build-1.1.1 `
  -PrerequisiteRoot dist\prerequisites `
  -OutputPath dist\HuahaiClipboard-Setup.exe
```

正式桌面入口位于 `src/HuahaiClipboard.App`，使用 WinUI 3 + WebView2 离线加载 `Assets/Web/product-shell.html`；安装后的程序不依赖 localhost 或开发服务器。`experiments/HuahaiClipboard.NativeUiSpike` 仅保留为 1.1.0 原生 WPF 回滚实现。

## 开源许可

本项目使用 [MIT License](LICENSE)。
