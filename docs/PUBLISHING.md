# CornerBreak v0.2.0：GitHub 网页上传与发布

适用日期：2026-10-26。按当前项目结构整理，本次更新先上传 v0.2.0 源码，再发布新的 Windows 程序包；保留旧版 Release。

## 1. 本地文件

源码上传目录：

```text
D:\Project_Creative\Tool\CornerBreak\release\CornerBreak-v0.2.0-source
```

这个目录中直接包含 README.md、C# 源文件、构建脚本、assets、docs 和 tests。README 已引用 docs/images 下的六张宣传图与真实截图。上传时进入该目录，选择里面的全部内容，不要把外层目录一起套入仓库。

程序发布附件：

- CornerBreak-v0.2.0-windows.zip
- CornerBreak-v0.2.0-windows.zip.sha256

两份附件位于 D:\Project_Creative\Tool\CornerBreak\release。ZIP 保持压缩状态，稍后上传到 Release 附件区域。

源码上传目录已排除个人 Data、历史 backups、output、dist、release、根目录 EXE、生成提示词和测试输出。网页拖拽上传时不要依赖 .gitignore 代替文件筛选。

## 2. 打开现有仓库或创建仓库

打开 [GitHub 新建仓库](https://github.com/new)，登录账号后填写：

| 项目 | 内容 |
|---|---|
| Repository name | CornerBreak |
| Description | Windows 桌面休息提醒工具：眼睛休息、喝水、起身活动，支持渐进放大和伙伴切换。 |
| Visibility | Public |
| Add README | 不勾选，使用已准备的 README |
| Add .gitignore | 不额外选择，使用源码目录中的文件 |
| Choose a license | 本次保留为空，当前项目尚未添加 LICENSE |

点击 Create repository。如果已创建 CornerBreak 仓库，直接使用已有仓库，继续下一步。

操作依据：[GitHub 创建仓库说明](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository)。

## 3. 上传源码和 README 图片

1. 在空仓库页面点击 uploading an existing file；已有文件时使用 Add file → Upload files。
2. 在电脑上打开前述 CornerBreak-v0.2.0-source 目录。
3. 进入目录内部，按 Ctrl+A，选中里面全部文件与文件夹，拖进网页。
4. 等待上传完成。README.md 应直接位于仓库根目录；图片应位于 docs/images。
5. 提交标题填写：

   ```text
   Prepare CornerBreak v0.2.0 with illustrated README
   ```

6. 页面允许直接提交时，选择默认分支（通常是 main），点击 Commit changes。若要求新分支，则提交后创建 Pull Request，合并到默认分支再发布。
7. 回到仓库首页，检查六张介绍图和实际截图都能显示；打开 VERSION，内容应为 0.2.0。

若仓库首页仅显示一个 CornerBreak-v0.2.0-source 文件夹，表示多传了一层；应让 README.md、Build.ps1 等直接出现在仓库根目录。

操作依据：[GitHub 上传文件说明](https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository)。

## 4. 发布 Windows 程序

1. 在仓库主页点击右侧 Releases，再点击 Create a new release 或 Draft a new release。
2. Choose a tag 输入 v0.2.0，并选择 Create new tag。
3. Target 选择刚才上传源码的默认分支，通常是 main。
4. Release title 填写 CornerBreak v0.2.0：常驻小挂件。
5. 打开源码目录内 docs/RELEASE_NOTES.md，把正文复制到 Describe this release。
6. 在附件区域上传 CornerBreak-v0.2.0-windows.zip 和 CornerBreak-v0.2.0-windows.zip.sha256，保持原文件名。
7. 等待两份附件上传完成。按正式版本发布时不勾选 This is a pre-release；如显示 Set as latest release，可勾选。
8. 检查后点击 Publish release。Save draft 只保存草稿。

GitHub 自动附带的 Source code ZIP 是源码快照；给普通用户使用的下载项是你上传的 CornerBreak-v0.2.0-windows.zip。

操作依据：[GitHub Release 说明](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)、[关于 Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)。

## 5. 发布后检查

1. 打开仓库首页，确认六张宣传图与实际截图显示正常。
2. 下载一次 Windows ZIP，完整解压，进入 CornerBreak 文件夹，双击 CornerBreak.exe。
3. 确认可以打开状态窗口、设置和伙伴切换。
4. 在小红书正文补充实际的仓库或 Release 地址；本地保存的帖子文案位于 output/xiaohongshu-20261025/小红书帖子.txt。

以后修改代码、README 或图片后，应重新准备源码上传目录和程序 ZIP，避免上传旧文件。

