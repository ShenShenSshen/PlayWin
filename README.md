# PlayWin（游窗）

<img src="AllLive.WinUI/Assets/PlayWin.png" width="80" alt="PlayWin 图标" />

简洁的 Windows 游戏直播客户端，只看哔哩哔哩和抖音，不显示弹幕。使用 WinUI 3，栏位采用磨砂材质，内容保持清晰。

**当前版本：v1.0.0 · Windows x64 · MIT**

## 下载

- [安装版 PlayWin-1.0.0-x64-Setup.exe](https://github.com/ShenShenSshen/PlayWin/releases/download/v1.0.0/PlayWin-1.0.0-x64-Setup.exe)
- [便携版 PlayWin-x64.zip](https://github.com/ShenShenSshen/PlayWin/releases/download/v1.0.0/PlayWin-x64.zip)
- [所有发布版本](https://github.com/ShenShenSshen/PlayWin/releases)

安装版默认安装到当前用户目录，创建开始菜单入口，默认创建桌面快捷方式，可取消。便携版解压后运行 `PlayWin.exe`，请保留整个目录。

支持 Windows 10 1903（18362）及以上版本和 Windows 11 x64。抖音登录与接口请求需要 WebView2 Runtime。程序自带 .NET 和 Windows App SDK 运行依赖，无需安装开发 SDK。

## 功能

- **两个平台，游戏优先**：按哔哩哔哩 / 抖音切换首页和游戏分类；搜索、关注及历史统一管理。
- **浏览记忆**：恢复最后的平台和各平台游戏大类；每个平台可手动固定最多 6 个常看游戏到首页。
- **清晰的搜索结果**：显示平台标识和接口已提供的开播状态，直播标题可悬停查看全文。
- **轻量播放器**：清晰度与线路切换、截图、全屏、置顶小窗、独立窗口播放；双击全屏、空格暂停、滚轮调音量，控件自动隐藏。
- **有限断流恢复**：最多重试 3 次，重新检查房间及播放地址；确认停播时停止，离开房间取消。
- **固定比例与位置记忆**：16:9 播放区域尺寸预设，适配显示器工作区；主窗口和独立窗口分别记住位置。
- **关注与历史**：日常关注页只显示确认开播的房间，管理入口可查看和移除停播主播；观看历史保留。支持 JSON 导入导出。
- **账号登录**：哔哩哔哩扫码登录、抖音官方网页登录；区分登录失效、网络失败与平台接口异常。
- **外观与更新**：浅色 / 深色主题、磨砂导航栏；设置中手动检测更新，软件内后台下载安装包，校验后由用户启动安装器。

没有弹幕、礼物效果或跨设备同步。


## 数据与升级

发布包不包含个人登录、关注、历史或浏览器缓存。为兼容已有版本；安装版和便携版会使用同一份本机数据。

覆盖升级保留数据；**卸载安装版会清空登录、关注、历史、设置、日志、下载文件及浏览器缓存，全部不保留**。程序和安装包暂未进行代码签名，Windows 可能显示未知发布者。

平台接口变化、平台登录要求和网络状况可能影响直播获取。视频保持原比例，不拉伸或裁切；直播源自身的黑边不会被去除。

## 构建与验证

需要 .NET 10 SDK、Windows SDK 10.0.26100.0；安装包另外需要 Inno Setup。

```powershell
.\build-winui.ps1
dotnet run --project tests/WinUIDataCheck/WinUIDataCheck.csproj -c Release
.\artifacts\PlayWin-win-x64\PlayWin.exe --smoke-test
.\build-installer.ps1 -CompilerPath '编译器目录\ISCC.exe'
```

解决方案：`PlayWin.slnx`。发布目录：`artifacts/PlayWin-win-x64`。

- `AllLive.Core`：两个平台的接口与核心模型，项目文件 `PlayWin.Core.csproj`。
- `AllLive.WinUI`：WinUI 3 客户端，项目文件 `PlayWin.csproj`。
- `tests`：数据、自检、图标和隔离安装验证。
- `installer/PlayWin.iss`：当前用户安装与卸载配置。

构建缓存、个人数据和发布产物不纳入源码。

自检使用隔离测试数据，不等于真实直播、多显示器和全部 DPI 环境的人工验证。详见 [客户端说明](AllLive.WinUI/README.md)。

## 来源与许可证

原项目：[https://github.com/xiaoyaocz/AllLive](https://github.com/xiaoyaocz/AllLive)。感谢原作者及项目依赖的维护者。

采用 [MIT 许可证](LICENSE)，保留原作者版权声明。与哔哩哔哩、抖音平台无官方关联。
