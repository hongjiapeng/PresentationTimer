# Known issues

## Windows theme changes can close the compact timer

SlidePace uses a custom WinUI 3 desktop acrylic backdrop for the Compact Timer and Presenter HUD. On affected Windows App SDK versions, changing the Windows app theme between Light and Dark while SlidePace is running can raise a native `Microsoft.UI.Xaml.dll` failure (`E_INVALIDARG`) and close the app.

This is an upstream WinUI system-backdrop issue rather than a managed exception in SlidePace. It is tracked by the Windows App SDK project:

- [Custom SystemBackdrop crashes when the theme changes (WindowsAppSDK #3570)](https://github.com/microsoft/WindowsAppSDK/issues/3570)
- [SystemBackdrop theme-change discussion (WindowsAppSDK #4103)](https://github.com/microsoft/WindowsAppSDK/discussions/4103)

Until the upstream behavior is fixed, close SlidePace before changing the Windows Light/Dark theme, then reopen it after the theme change. The app reads the selected theme at startup.

---

# 已知问题

## 切换 Windows 深浅主题可能关闭紧凑计时器

SlidePace 的紧凑计时器和演讲者 HUD 使用自定义 WinUI 3 桌面亚克力背景。在受影响的 Windows App SDK 版本中，如果 SlidePace 正在运行，切换 Windows 应用的浅色/深色主题可能触发 `Microsoft.UI.Xaml.dll` 原生异常（`E_INVALIDARG`），导致应用关闭。

这是 WinUI 系统背景的上游问题，并非 SlidePace 中可捕获的托管异常。相关记录：

- [WindowsAppSDK #3570：自定义 SystemBackdrop 在主题切换时崩溃](https://github.com/microsoft/WindowsAppSDK/issues/3570)
- [WindowsAppSDK #4103：SystemBackdrop 主题切换讨论](https://github.com/microsoft/WindowsAppSDK/discussions/4103)

在上游修复前，请先关闭 SlidePace，再切换 Windows 深浅主题，之后重新打开应用。应用会在启动时读取当前系统主题。
