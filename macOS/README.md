# CornerBreak for macOS · 内测版

版本：**0.2.0-beta.1**。首批目标系统：**macOS 14 或更新版本**。

这个版本把 Windows 版的休息规则和常驻小挂件带到 macOS：平时显示伙伴和下次休息倒计时，每小时 50 分展开提醒，到整点回到小挂件。眼睛休息、喝水、起身活动三项可以分别开关。

## 当前验证范围

应用由 Windows 环境交叉构建。发布包内包含 .NET 运行环境，使用者无需安装 .NET、SDK 或开发工具。

**尚未在 Mac 实机上验证，也未使用 Developer ID 签名或通过 Apple 公证。** 这是发给愿意协助验证的用户的实验性内测包。Mac 上能否顺利启动、浮窗是否保持置顶、菜单栏和全屏切换是否符合预期，都需要实机反馈；此版本不能作为已验证的正式版发布。

## 选择下载包

先在 Mac 的“关于本机”查看芯片或处理器：

| 设备 | 下载文件 |
|---|---|
| Apple M 系列芯片 | `CornerBreak-v0.2.0-beta.1-macos-arm64-experimental.zip` |
| Intel 处理器的 Mac | `CornerBreak-v0.2.0-beta.1-macos-x64-experimental.zip` |

本地测试包位于项目根目录的 `release` 文件夹；公开下载地址以作者实际发布的 Release 为准。请使用与设备对应的包，不要求安装 Rosetta。下载时一并保留作者提供的 SHA256 校验文件。

## 启动与快速试用

1. 确认文件来自作者提供的地址，并与随附的 SHA256 值核对一致。校验只能确认文件是否一致，不能代替来源判断。
2. 完整解压 ZIP，双击 **CornerBreak.app**。保留整个应用包的结构，不需要进入应用包内部运行文件。
3. 正常情况下应出现一个小挂件。点击它可查看详情，按住拖动可移动，右击打开菜单。
4. 不必等到每小时 50 分：先通过菜单栏图标或挂件右键菜单中的 **“查看状态”**，检查详情与设置能否打开。
5. 从“提醒设置”切换猫猫探头、蓝色大肥鱼、二次元伙伴；也可以隐藏角色，只保留文字。
6. 结束试用时，从菜单栏图标或挂件菜单选择 **“退出 CornerBreak”**。

详细步骤和反馈模板见 [TESTING.md](TESTING.md)。

## 首次启动受到系统提示阻挡

这个内测包没有 Developer ID 签名和 Apple 公证，macOS 可能提示开发者身份无法验证。

请先确认来源和 SHA256，再参考 [Apple 官方“Safely open apps on your Mac”说明](https://support.apple.com/en-us/102445)，由你决定是否允许这一个应用启动。

如果提示 **“已损坏”**、**“会损坏电脑”**，或者应用立即退出，请先停止尝试，把提示截图、macOS 版本和芯片类型反馈给作者。无需关闭 Gatekeeper、修改全局安全设置、删除隔离属性或执行重新签名脚本。

## 提醒行为

- 固定在每小时 **50 分到下一个整点**提醒，例如 14:50—15:00；中途启动显示本轮实际剩余时间。
- 平时小挂件常驻，到休息时段展开详情；整点、暂停或跳过后回到小挂件。
- “收起详情”和关闭详情窗口只收起；**“本轮不再提示”才会记录跳过本轮**。
- 支持暂停 30 分钟、1 小时、2 小时或直到手动恢复；到期后按正常时段恢复提醒。
- 提醒卡片可随倒计时逐渐放大，有尺寸上限，也可以关闭放大。
- 可以切回定时弹出模式，等待期间不显示小挂件。
- 设置保存在本机，无需账号，程序不设置开机自启动。
- **跨 Spaces、其他应用全屏、多屏、睡眠唤醒和不抢焦点行为尚待 Mac 实机验证。**

## 设置与异常日志

Mac 的设置位于：

```text
~/Library/Application Support/CornerBreak/settings.json
```

异常日志位于：

```text
~/Library/Application Support/CornerBreak/CornerBreak.log
```

它们位于用户目录，**不在 CornerBreak.app 包内部**。替换应用时保留该目录即可保留设置。异常日志不记录任务正文；如日志文件不存在，说明程序尚未写入异常日志。遇到启动失败时，请先反馈系统提示与设备信息。

## 源码与维护构建

核心项目使用 .NET 10，桌面界面采用固定版本 Avalonia 11.3.19。Core 通过链接文件共用根目录的 `ReminderRules.cs`，时间与增长规则只有一份实现；Mac 设置文件与现有 Windows 程序数据隔离。

在 CornerBreak 项目根目录，先将 `$dotnetPath`、`$pythonPath` 设置为本机可执行文件的完整路径，然后运行：

```powershell
.\macOS\Build-Mac.ps1 -Architecture all `
  -DotNetPath $dotnetPath `
  -PythonPath $pythonPath
```

`all` 生成 Apple Silicon 与 Intel 两份包。脚本输出路径和检查结果以实际运行日志为准。

核心规则测试可单独运行：

```powershell
& $dotnetPath run --project .\macOS\Tests\CornerBreak.Core.Tests.csproj -c Release
```

构建和核心测试成功仍不能代替 Mac 实机验收；签名、公证及正式分发需要后续单独完成。

