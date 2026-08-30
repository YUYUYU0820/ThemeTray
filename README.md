# ThemeTray

ThemeTray 是一个 Windows 托盘应用，用于手动或按计划切换系统与应用的浅色、深色主题。界面为简体中文，程序不显示主窗口。

## 功能说明

- **托盘操作**：程序常驻系统通知区域，左键或右键单击托盘图标即可打开菜单；菜单会显示当前主题、自动切换状态和当前生效的时间方案。
- **手动切换主题**：可立即切换为浅色或深色主题，同时更新 Windows 系统和应用主题设置，并同步刷新托盘图标。
- **固定时间自动切换**：支持 `标准`（07:00 / 19:00）、`办公`（08:00 / 18:00）、`夜间`（09:00 / 22:00）预设，也可以设置自定义浅色和深色时间。支持跨午夜时间段，程序启动时会立即检查，之后每 60 秒检查一次。
- **日出日落自动切换**：通过 Windows 当前定位获取经纬度，并从 `api.sunrise-sunset.org` 获取当天日出、日落时间，再转换为本地 Windows 时区使用。定位权限或网络不可用时，会临时回退到固定时间。
- **地点与数据缓存**：可保存常用地点并在下次刷新时复用附近地点；最多保留 10 个地点和 3 组日出日落数据，最新使用和最新日期优先。获取失败原因会保存在设置中，便于在菜单中查看。
- **Windows 通知**：启动、实际主题变化以及日出日落数据刷新结果会显示为 Toast 通知；3 秒内连续产生的通知会合并显示。Toast 不可用时会自动回退到托盘气泡提示。
- **开机自启**：可通过托盘菜单启用或停用当前用户开机自启，不需要管理员权限。
- **单实例运行**：同一用户会话中只允许一个 ThemeTray 实例运行，重复启动不会打开第二个托盘图标。

## 运行环境

- Windows 10 1809（17763）或更高版本，x64。
- 按当前源码构建的版本需要安装 x64 .NET 8 Windows Desktop Runtime、x64 Windows App Runtime 2.3 和 x64 Visual C++ 2015-2022 Redistributable。
- `ThemeTray.v1.05.zip` 是当前最新的框架依赖多文件版本，包含自动切换重试和 Toast 注册修复。
- `ThemeTray.v1.04.zip`、`ThemeTray.v1.03.zip` 和 `ThemeTray.v1.02.zip` 是保留的历史版本；`ThemeTray.v1.01.zip` 是保留的自包含版本，不依赖预装 .NET。

## 使用

1. 解压发布包并运行 `ThemeTray.exe`。
2. 在系统托盘中左键或右键单击图标打开菜单。
3. 可手动切换主题，或在“自动切换”菜单中启用固定时间、日出日落自动切换和开机自启。

日出日落模式会请求 Windows 定位权限，并通过 `api.sunrise-sunset.org` 获取当天数据；定位或网络不可用时，程序会使用已配置的固定时间。

## 构建与发布

在安装了 Windows Desktop 工作负载的 .NET 8 SDK 的 Windows 环境中运行：

```powershell
dotnet build .\ThemeTray.csproj
dotnet publish .\ThemeTray.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false
```

当前版本是框架依赖的多文件应用。发布包应包含 publish 目录中的全部文件，以及对应的中文说明和更新日志；不要只复制 `ThemeTray.exe`。不要覆盖 `Release` 中已有的历史版本 ZIP。

## 数据与权限

- 配置保存在 `%APPDATA%\ThemeTray\settings.json`。
- 主题与开机自启仅写入当前用户的 Windows 注册表，不需要管理员权限。
- 启动、实际主题变化及日出日落获取结果会通过 Windows 通知中心显示，并使用当前托盘主题图标；3 秒内连续产生的通知会合并为一个 Toast。通知不可用时会回退到托盘气泡。
- 日出日落数据使用 HTTPS 请求获取；Windows 定位权限由系统控制，应用不会写入机器级配置。
- 应用未进行代码签名；Windows SmartScreen 可能显示未知发布者提示。