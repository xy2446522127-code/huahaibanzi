# 花海剪贴板

花海剪贴板是一款面向 Windows 10 / 11 的轻量桌面剪贴板工具。界面采用玫瑰紫液态玻璃设计，支持文本、链接、图片和文件历史，所有数据默认只保存在当前电脑，不需要登录账号。

## 主要功能

- 右键双击唤出，也可以录入自定义键盘或鼠标快捷方式。
- 在鼠标指针附近显示，并在可见期间保持置顶；隐藏后继续在托盘后台运行。
- 文本、链接、图片和文件分类，支持搜索、置顶、收藏和单条删除。
- 点击历史记录后立即复制并隐藏面板；这次程序自身写入不会重新进入历史，原记录位置和时间保持不变。
- 3 天、7 天或 30 天自动清理普通历史，收藏和置顶内容不受影响。
- 五套主题、背景透明度、等比例缩放、背景花瓣和减少动态效果设置。
- 密码管理器、浏览器无痕窗口以及用户排除列表中的应用不会写入历史。
- 可选开机自启、后台运行、点击面板外自动隐藏和 GitHub Release 更新检查。
- 面板隐藏期间持续更新内存历史；再次唤出时先同步最新状态再显示，不展示旧列表或加载过程。

## 更新与联网说明

- 默认启用“自动检查更新”时，程序后台启动后立即检查一次，之后每 5 分钟检查一次。`v1.1.13` 及后续版本会先通过 HTTPS 读取本仓库固定的静态更新清单；清单不可用、无效或不是新版本时，才回退到 GitHub API（`api.github.com`）和 Release 页面。发现新版会通过 Windows 通知、托盘更新入口和面板红点提醒，可在“关于与更新”中关闭或稍后提醒 24 小时。
- 请求不会上传剪贴板历史、图片、排除列表、设置或账号数据。公网 IP、User-Agent 和当前版本号属于正常 HTTPS 请求元数据。
- Git 仓库中的代码提交不会直接更新已安装用户。维护者必须创建新的 GitHub Release，并上传固定名称的 `HuahaiClipboard-Setup.exe` 及对应 SHA-256 文件。
- 只有用户点击“立即更新”后，程序才会下载安装包；下载完成后校验资产大小、SHA-256 和固定发布者证书指纹，任一不一致都会拒绝启动安装并保留当前版本。

### 发布静态更新清单

`update-manifest.json` 是 `v1.1.13` 及后续版本的无令牌更新发现入口。不要在没有正式安装包时提交它，也不要用估算值填写大小或 SHA-256。每次发布按以下顺序操作：

1. 生成并使用固定发布者证书签名 `HuahaiClipboard-Setup.exe`，创建对应的 GitHub Release 并上传该文件。
2. 从已上传的同一安装包取得精确字节数和 SHA-256；将根目录 `update-manifest.json` 更新为 `version`、`releaseUrl`、`installerUrl`、`size` 和小写 `sha256` 五个字段。
3. `releaseUrl` 和 `installerUrl` 必须使用 HTTPS `github.com` 地址，安装包文件名必须精确为 `HuahaiClipboard-Setup.exe`；将清单提交并推送到 `master`。

客户端只接受固定的 `https://raw.githubusercontent.com/xy2446522127-code/huahaibanzi/master/update-manifest.json` 地址。清单内容即使遭到错误修改，下载内容仍必须通过 SHA-256 和固定发布者证书校验才会启动安装。

## 安装

正式安装程序为：

```text
dist/HuahaiClipboard-Setup.exe
```

安装程序支持选择安装位置，并按照当前产品要求拒绝安装到 C 盘。安装后会创建桌面、开始菜单和 Windows“应用和功能”卸载入口。

正式 Release 安装包使用项目维护者的自签名开源发布证书进行 Authenticode 签名，应用内更新会固定核对该证书指纹。该证书不是付费商业信任证书，因此 Windows SmartScreen 仍可能显示未知发布者提示；请从本仓库或可信发布页面获取安装包，并核对发布说明中的 SHA-256。私钥只保存在维护者本机证书库，不进入 Git。

## 本机数据与隐私

默认数据目录：

```text
<安装目录>\Data\<Windows 用户 SID>
```

设置、历史、图片缓存和窗口位置只保存在本机。文本历史使用当前 Windows 用户范围的 DPAPI 保护。卸载程序只删除安装目录、快捷方式和本应用拥有的开机启动项，默认保留上述用户数据。

## 从源码构建

要求：Windows 10/11 x64、.NET 8 SDK、Visual Studio 2022 Build Tools（Windows App SDK 生成组件）、PowerShell 5.1 或更高版本。

```powershell
dotnet test tests\HuahaiClipboard.Core.Tests\HuahaiClipboard.Core.Tests.csproj -c Release

& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' `
  src\HuahaiClipboard.App\HuahaiClipboard.App.csproj `
  /t:Build /p:Configuration=Release /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 /p:SelfContained=false /p:WindowsAppSDKSelfContained=true /p:Version=1.1.12 `
  /p:OutDir="$PWD\dist\webview-build-1.1.12\" /restore /m

.\installer\Fetch-Prerequisites.ps1 -Destination dist\prerequisites
.\installer\Build-Installer.ps1 `
  -PublishRoot dist\webview-build-1.1.12 `
  -PrerequisiteRoot dist\prerequisites `
  -OutputPath dist\HuahaiClipboard-Setup.exe `
  -SigningThumbprint CD06B727BD8811C3B59CE0A4F9384D68EC7431C2
```

未持有发布私钥的贡献者可以省略 `-SigningThumbprint` 构建本地调试安装包；正式 GitHub Release 必须使用上述固定发布者证书签名。

正式桌面入口位于 `src/HuahaiClipboard.App`，使用 WinUI 3 + WebView2 离线加载 `Assets/Web/product-shell.html`；安装后的程序不依赖 localhost 或开发服务器。`experiments/HuahaiClipboard.NativeUiSpike` 仅保留为 1.1.0 原生 WPF 回滚实现。

## 开源许可

本项目使用 [MIT License](LICENSE)。
