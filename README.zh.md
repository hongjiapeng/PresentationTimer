# PresentationTimer

[中文](./README.zh.md) · **[English](./README.md)**

PresentationTimer 是一个以本地优先为原则、基于 .NET 10 和 WinUI 3 的 Windows 演讲者工作台。它将精准的倒计时/超时计时器、PowerPoint 文件选择与控制、演讲者备注监测，以及通过可信局域网连接的手机浏览器遥控器整合在一起。

## 截图

展开视图设计稿展示了演讲者工作台的整体层级；紧凑计时器和演讲者 HUD 截图展示了专注计时场景。

<p>
  <img src="docs/design/presentation-timer-expanded-ui-v2.png" alt="PresentationTimer 展开视图设计稿" width="720">
</p>
<p>
  <img src="docs/screenshots/compact-timer.png" alt="PresentationTimer 紧凑计时器" width="660">
  <img src="docs/screenshots/presenter-hud.png" alt="PresentationTimer 演讲者 HUD" width="440">
</p>

## 前置条件

- Windows 10 22H2 或 Windows 11（x64）
- .NET 10 SDK
- 安装了 WinUI 应用开发工作负载的 Visual Studio 2026
- Microsoft PowerPoint 桌面版（用于 PowerPoint 集成检查）
- 已启用开发者模式（用于未打包 Debug 启动）

PowerPoint 控制功能要求 Windows 桌面版 Microsoft PowerPoint，以及已注册的 `PowerPoint.Application` COM 类。当前适配器不支持 WPS Office（包括 `KWPP.Application` COM 类）、网页版 PowerPoint 或 Microsoft 365/Office 门户应用。

如果 PowerPoint 卡片显示“未检测到桌面版 PowerPoint（COM）”或英文对应提示，请安装或修复 Microsoft Office 桌面应用，然后重启 PresentationTimer。仅选择文件不能绕过这一要求。

仓库提供了引导式检查脚本：它会打开微软官方安装页面，并在安装后重新检查 COM 注册状态：

```powershell
.\scripts\Ensure-PowerPointDesktop.ps1 -OpenInstaller -WaitAfterOpening
```

如果系统提供 `winget`，也可以通过 Windows 包管理器安装 Microsoft 桌面套件：

```powershell
winget install --id Microsoft.Office `
  --source winget `
  --accept-source-agreements `
  --accept-package-agreements
```

这会安装完整的 Microsoft 365 Apps for enterprise 套件，而不只是 PowerPoint。安装后请先启动 PowerPoint 完成登录和激活，再重启 PresentationTimer。

脚本不会绕过 Microsoft 登录、许可证、用户账户控制或 Office 激活流程。

## 构建与测试

```powershell
dotnet restore PresentationTimer.sln
dotnet build PresentationTimer.sln -c Debug -p:Platform=x64
dotnet test PresentationTimer.sln -c Debug -p:Platform=x64
```

完成 Debug x64 构建后运行桌面 UI 冒烟检查：

```powershell
.\scripts\ui-smoke.ps1 -AppPath .\src\PresentationTimer.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\PresentationTimer.App.exe
```

## 使用方法

1. 在 PowerPoint 卡片中选择 **Open presentation**，打开 `.ppt`、`.pptx`、`.pptm`、`.pps` 或 `.ppsx` 文件；PresentationTimer 会以只读方式打开文件并启动幻灯片放映。也可以先在 PowerPoint 中启动放映，PresentationTimer 会自动连接。它不会关闭演示文稿或 PowerPoint。
2. 输入 `15:00` 这样的时长，然后选择 **Start**。暂停、继续和重置仍然是桌面端本地命令。
3. 选择 **Start remote**。使用同一可信 Wi-Fi/LAN 中的手机扫描显示的二维码。
4. 使用浏览器中的上一页/下一页按钮，并查看权威的幻灯片编号、纯文本备注以及剩余/超时时长。
5. 演讲结束后立即选择 **End session**。本次会话的二维码令牌和所有浏览器 Cookie 都会失效。

## PowerPoint 测试夹具

[`tests/fixtures/PresentationTimer.PowerPointFixture.pptx`](tests/fixtures/PresentationTimer.PowerPointFixture.pptx) 是一个原创、以程序生成的 6 页演示文稿，用于人工验证。它覆盖空备注、多行备注、必须保持为纯文本的类 HTML 文本、真正隐藏的幻灯片，以及末页边界行为。它不包含外部图片、图表、事实性声明或第三方素材。

这个 PPT 适合提交到 GitHub，定位为测试夹具或人工验证 sample。请在文件名和文档中明确这一用途；它不适合作为项目的产品宣传演示文稿。

## 可信局域网安全边界

MVP 使用局域网内的 HTTP 鉴权，而不是 HTTPS。一个 256 位的一次性配对令牌会被交换为独立的 `HttpOnly`、`SameSite=Strict` 浏览器 Cookie；凭据只保存在内存中，并在会话结束时撤销。这可以避免未经授权的误操作，但无法防御能够监听或篡改恶意/公共 Wi-Fi 流量的攻击者。请使用可信的私有网络，并在演讲结束后结束会话。

## 手机无法连接

- 确认电脑和手机连接到同一个 Wi-Fi/LAN，并且可以直接互相访问。
- 暂时禁用 VPN，并检查接入点是否启用了客户端/AP 隔离。
- 如果 Windows 防火墙弹出提示，请在 Windows 安全中心允许 PresentationTimer 使用“专用网络”。应用不会自行提升权限，也不会修改防火墙或网络设置。
- 如果电脑更换了网络或 IP 地址，请等待新的适配器选项，选择手机可访问的 URL，并扫描新的二维码。
- 企业设备策略可能会禁止局域网入站监听；请联系管理员允许此应用访问本地子网。

当存在多个可用的网络适配器时，请选择标注为 Ethernet、Wi-Fi 或其他、且能够到达手机的候选地址。如果演讲期间电脑地址发生变化，PresentationTimer 会撤回旧二维码，重新绑定主机并发布替代选项。只要端点仍可访问，已连接手机可能会自动恢复；否则请扫描新的二维码。

## PowerPoint 无法打开所选文件

- 确认安装的是 Microsoft PowerPoint 桌面版，而不是只有 WPS Office 或 Microsoft 365 门户。
- 启动 PowerPoint 一次，完成 Office 修复/激活提示，然后重启 PresentationTimer。
- 文件必须使用 `.ppt`、`.pptx`、`.pptm`、`.pps` 或 `.ppsx` 扩展名；不支持、缺失或不可读的文件会安全报错，不会改变当前演示状态。
- WPS 支持需要单独的适配器，目前尚未启用。

## 本地日志与隐私

PresentationTimer 将结构化 Serilog 事件写入 `%LOCALAPPDATA%\PresentationTimer\Logs`。日志按天及 5 MiB 滚动，最多保留七个文件七天，并在正常关闭时刷新。演讲者备注、配对令牌、浏览器 Cookie，以及完整的含令牌配对 URL 都会被刻意排除。若不再需要排查记录，请先关闭应用，再删除日志文件。

应用组合使用 `Microsoft.Extensions.DependencyInjection` 和构造函数注入，并启用构建时作用域验证。进程拥有单一的计时器、PowerPoint 控制器、远程主机和演示会话服务；窗口与页面通过 DI 接收这些服务，而不是使用全局服务定位器。

## 便携发布

使用 NativeAOT 并关闭裁剪，创建未打包、自包含的 x64 dogfood 输出：

```powershell
dotnet publish .\src\PresentationTimer.App\PresentationTimer.App.csproj -c Release -p:Platform=x64 -r win-x64 -o .\artifacts\publish\win-x64
```

直接从该目录启动 `PresentationTimer.App.exe`。卸载/回滚只需关闭应用，然后删除或替换便携目录；MVP 不保存演示文稿、令牌或账户数据。除非单独删除，否则本地诊断日志仍保留在 `%LOCALAPPDATA%\PresentationTimer\Logs`。分享构建版本前，请参阅 [docs/manual-verification.md](docs/manual-verification.md) 和 [PowerPoint 检查清单](docs/powerpoint-manual-checklist.md)。

## Windows 安装程序

GitHub Releases 提供 Windows 安装程序（`PresentationTimer-<version>-win-x64-Setup.exe`）和便携 ZIP（`presentationtimer-<tag>-win-x64.zip`）。安装程序：

- 按用户安装到 `%LOCALAPPDATA%\Programs\PresentationTimer`，不需要管理员权限
- 支持英文、简体中文和繁体中文界面
- 可选创建桌面快捷方式，并在登录 Windows 时启动 PresentationTimer
- 卸载时保留 `%LOCALAPPDATA%\PresentationTimer\Logs` 下的诊断日志

本地构建安装程序（需要安装 Inno Setup 6）：

```powershell
.\scripts\build-installer.ps1 -Version 0.1.0
```

提交并推送所有预期变更后，可运行 `.\scripts\release.ps1 0.1.0` 发布版本。脚本会运行测试套件、创建并推送带注释的 `v*` 标签；随后 GitHub Actions（`.github/workflows/release.yml`）会构建安装程序和便携 ZIP，并创建或更新 GitHub Release。

## 许可证与第三方声明

仓库中的第三方组件和声明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
