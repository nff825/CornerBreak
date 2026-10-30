# Mac 实验包的发送与发布

版本：0.2.0-beta.1。Windows 稳定版仍为0.2.0。

只请一位用户测试时，可以直接发送与芯片对应的 experimental.zip、同名 .sha256 和测试说明，不必先公开发布。M 系列使用 arm64，Intel 使用 x64，首批目标为 macOS 14 及以上。

两份包均位于项目根目录 release 中。包内有 CornerBreak.app 与测试说明，运行环境已包含。没有 Mac 实机验收、Developer ID 身份签名或 Apple 公证，应明确标为受邀实验测试版。

## 如需更新 GitHub

1. 打开本地 release/CornerBreak-v0.2.0-with-macos-beta.1-source，进入目录内部，全选其中内容，上传到现有 CornerBreak 仓库。
2. README.md 应直接位于仓库根目录，macOS 与 assets、tests 等目录并列。该副本排除了个人设置、SDK、NuGet 缓存和构建输出。
3. 提交标题可以用 Add experimental macOS 0.2.0-beta.1 build。
4. 创建单独 Release，标签用 v0.2.0-macos-beta.1，标题用 CornerBreak macOS 0.2.0-beta.1（实验内测版）。
5. 勾选 This is a pre-release。把两份 experimental.zip 与各自 .sha256 放在 Release 附件区域。manifest.json 为详细的静态检查记录，也可附上。
6. 版本说明应说明支持系统、架构、现有功能、未实机验证及无开发者身份签名和公证，附上 macOS/TESTING.md 的测试方式。

Mac ZIP 约46—48 MiB，应作为 Release 附件上传。仓库 Code 页使用源码展开目录，避免把运行包当成源码文件上传。

## 维护者重新打包

Build-Mac.ps1 会检查原生代码页哈希和 ZIP 执行权限，不覆盖已有同名输出。重复构建可以指定一个新的 -OutputDirectory；对外发新代码前应增加 macOS/VERSION 和桌面项目中的测试版本。

打包器的检查范围是架构、原生代码页哈希、文件字节、CRC、UNIX 权限和应用目录结构；不能代替 Mac 启动、Gatekeeper、菜单栏及全屏测试。

[GitHub 文件上传说明](https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository)

[GitHub Release 说明](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
