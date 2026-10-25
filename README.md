# CornerBreak · 角落小憩

给忙碌的工作留一点休息时间。CornerBreak 是轻量的 Windows 桌面休息提醒工具，在屏幕角落提醒你休息眼睛、喝水和起身活动，并用逐渐放大的卡片与可切换的休息伙伴提醒你暂时离开工位。

当前版本：**0.1.0**。程序使用 C# / Windows Forms，运行时无需 PowerShell 或 VBScript。

## 图文介绍

六张介绍图展示 CornerBreak 的使用方式。图片由 AI 生成，其中的软件界面和尺寸变化为示意；[下方的实际界面](#实际界面)来自 EXE 预览模式的真实截图。点击图片可查看大图。

<table>
  <tr><th>01 · 提醒会慢慢长大</th><th>02 · 三项休息提醒</th></tr>
  <tr>
    <td><a href="docs/images/promo-01-cover.png"><img src="docs/images/promo-01-cover.png" alt="再坐一会儿，提醒就要长大了：CornerBreak 项目介绍" width="360"></a></td>
    <td><a href="docs/images/promo-02-reminders.png"><img src="docs/images/promo-02-reminders.png" alt="眼睛休息、喝水、起身活动，三项提醒分别开关" width="360"></a></td>
  </tr>
  <tr><th>03 · 越接近整点越醒目</th><th>04 · 选择休息伙伴</th></tr>
  <tr>
    <td><a href="docs/images/promo-03-growth.png"><img src="docs/images/promo-03-growth.png" alt="同一桌面中，提醒窗口从剩余十分钟到剩余一分钟逐渐变大" width="360"></a></td>
    <td><a href="docs/images/promo-04-companions.png"><img src="docs/images/promo-04-companions.png" alt="猫猫探头、蓝色大肥鱼、二次元伙伴可供选择" width="360"></a></td>
  </tr>
  <tr><th>05 · 灵活暂停与跳过</th><th>06 · 本地 EXE 使用</th></tr>
  <tr>
    <td><a href="docs/images/promo-05-pause.png"><img src="docs/images/promo-05-pause.png" alt="支持限时暂停、手动恢复、跳过本轮和关闭自动放大" width="360"></a></td>
    <td><a href="docs/images/promo-06-local-app.png"><img src="docs/images/promo-06-local-app.png" alt="Windows 桌面小工具，解压后双击运行，无需账号，设置本地保存" width="360"></a></td>
  </tr>
</table>

## 下载与运行

1. 在本仓库的 **Releases** 页面下载 `CornerBreak-v0.1.0-windows.zip`。
2. 完整解压到有写入权限的文件夹。
3. 双击 **CornerBreak.exe**。程序会在通知区域保留托盘图标，到休息时段自动显示卡片。

运行环境：Windows，**.NET Framework 4.7.2 或兼容更新版本**。无需安装开发工具，不需要管理员权限。

三张角色图片已嵌入 EXE，无需额外放置 `assets` 文件夹。请保留随附的 `CornerBreak.exe.config`。首次保存设置时，程序会在 EXE 同目录创建 `Data/settings.json`；搬迁或升级时保留 `Data` 文件夹即可保留个人设置。

重复启动会打开已有程序的状态窗口。退出请右击托盘图标并选择“退出 CornerBreak”。

## 功能

- **三项提醒**：眼睛休息、喝点水、起身活动，可以分别开关。
- **固定休息时段**：每小时的 50 分到下一个整点，例如 14:50—15:00；中途打开也会显示实际剩余时间。
- **渐进放大**：休息时段开始时保持常态，随后卡片、角色、文字逐渐变大，越接近整点越明显。
- **三种休息伙伴**：猫猫探头、蓝色大肥鱼、二次元伙伴，也可选择不显示角色。
- **暂停与跳过**：暂停 30 分钟、1 小时、2 小时或至手动恢复；可以跳过本轮提醒。
- **本地保存**：设置、角色、暂停及本轮跳过状态保存在本机；程序不联网，不自动设置开机启动。

## 实际界面

以下程序截图均通过 EXE 预览模式生成。

<p align="center"><img src="docs/images/normal.png" alt="CornerBreak 提醒窗口真实截图" width="420"></p>

## 选择休息伙伴

在“提醒设置”中查看预览并保存，或通过托盘的“休息伙伴”菜单立即切换。取消设置不会改变原选择。

<table>
  <tr><th>猫猫探头</th><th>蓝色大肥鱼</th><th>二次元伙伴</th></tr>
  <tr>
    <td><img src="docs/images/cat.png" alt="猫猫探头提醒卡片" width="240"></td>
    <td><img src="docs/images/fish.png" alt="蓝色大肥鱼提醒卡片" width="240"></td>
    <td><img src="docs/images/companion.png" alt="二次元伙伴提醒卡片" width="240"></td>
  </tr>
</table>

## 倒计时渐进放大

卡片固定右下角，向左上方扩展。上限约为当前屏幕工作区宽度的一半、高度的 80%，并始终受屏幕边界限制。鼠标停留、拖动窗口、打开菜单或设置时暂缓放大，操作结束后平滑继续。暂停、跳过本轮或休息结束后恢复常态。

<table>
  <tr><th>剩余 10:00</th><th>剩余 01:00</th></tr>
  <tr>
    <td valign="bottom"><img src="docs/images/growth-start.png" alt="倒计时开始时的窗口" width="240"></td>
    <td valign="bottom"><img src="docs/images/growth-late.png" alt="临近休息结束时放大的窗口" width="437"></td>
  </tr>
</table>

也可在设置中关闭“随倒计时逐渐放大”，使用固定尺寸提醒。

<details>
  <summary>查看提醒设置</summary>
  <p><img src="docs/images/settings.png" alt="提醒设置和休息伙伴预览" width="420"></p>
</details>

## 使用提示

- 点击“本轮不再提示”或关闭提醒窗口，会收起卡片并跳过当前休息时段。跳过状态在重启后仍然有效，到本轮结束时失效。
- 托盘“查看状态”不会取消本轮跳过。
- 限时暂停到期后，程序按正常休息时段规则恢复提醒。
- 选择“不显示角色”会收起角色区域，保留文字与倒计时。
- 迁移原脚本版时，将新程序和配置文件放在原目录，保留原有 `Data/settings.json` 即可继续使用原设置。

完整操作说明见 [使用说明.txt](使用说明.txt)。

## 从源码构建

开发构建需要 Windows PowerShell 5.1 和 .NET Framework 4.7.2 或兼容更新版本。构建脚本使用系统自带的 .NET Framework C# 编译器，无需 NuGet 包。

在项目目录打开 PowerShell：

```powershell
# 编译程序并运行原生 C# 测试
.\Build.ps1 -Test

# 启动构建后的程序
.\dist\CornerBreak.exe

# 构建、测试并生成发布 ZIP 与 SHA256 文件
.\Package.ps1
```

默认构建输出为 `dist/CornerBreak.exe`，发布包输出到 `release/`。已有经过验证的构建时，可执行 `./Package.ps1 -SkipBuild` 直接打包。发布 ZIP 仅包含 EXE、运行配置、文档、文档图片，以及仓库中已有的 `LICENSE`（如有）；不包含个人 `Data`、测试输出、开发脚本或历史备份。

`Build.ps1` 编译根目录 C# 源码为独立的 Windows 桌面程序，并将 `assets/mascots` 下的三个 PNG 嵌入 EXE。运行 EXE 不会调用 PowerShell 或 VBScript。

源码目录中可能存在用于本机日常运行的 `CornerBreak.exe`，它和 `dist/` 都是本地构建产物，已被 Git 忽略。根目录的 `CornerBreak.exe.config` 是构建所需的源文件，应提交到仓库。

## 开发预览

预览使用独立实例和内存设置，不修改日常使用的 `Data/settings.json`。

```powershell
.\dist\CornerBreak.exe --preview
.\dist\CornerBreak.exe --preview --previewmascot fish
.\dist\CornerBreak.exe --preview --previewat "2026-10-25T12:59:00" --renderpreview "preview.png"
.\dist\CornerBreak.exe --preview --rendersettings "settings.png"
```

`--previewmascot` 可选 `cat`、`fish`、`companion`、`none`。`--previewscenario` 可选 `Active`、`Waiting`、`Skipped`、`Paused`、`Disabled`。导出截图需要同时使用 `--preview`。
