# Codex Project Tool Release

注意！！！请在使用前确保会话对话已完成，且工具缓存数据无留存必要！！！该指令会解除本次进程的Chatgpt app tools占用！！！故使用后会导致tools无法被调用！！！需要重启codex后才能重新正常使用完整的Chatgpt app tools！！！
本指令的推荐使用场景：尝试在项目中新开本地会话却遇到报错（即下文相关报错）时使用。

Windows 小工具，用于结束占用 Codex 项目目录的辅助进程，临时解决新建本地会话失败（`Could not use this project for a local chat`）的问题。

> [!NOTE]
> 非官方实验性工具。它只能临时解除占用，无法从根本上修复客户端的问题：辅助进程重新启动后，目录可能再次被占用。

## 问题背景

Codex 会在本地保存一份项目副本，位于 `%USERPROFILE%\.codex\.chatgpt-projects\g-p-*`。在项目中新建本地会话时，客户端会先同步项目资料，再用新生成的目录替换旧的项目副本。如果此时还有辅助进程以项目副本为工作目录，Windows 会因共享冲突拒绝替换，会话随即创建失败。

相关报错：

| 出现位置 | 信息 |
| --- | --- |
| 客户端界面 | `Could not use this project for a local chat`（中文界面为“无法将此项目用于本地聊天”） |
| 客户端日志 | `ChatGPT project context sync failed ... stage=filesystem` |
| 检查目录时 | `Win32 32 / ERROR_SHARING_VIOLATION` |

界面和日志中的报错也可能由权限问题等其他原因引起，仅凭报错无法确定是进程占用。遇到这类问题时，可以先运行 `Check-Only.cmd` 检查。

## 使用方法

无需另外安装 Python 或 Node.js。下载并解压仓库，保持目录结构不变即可。

| 文件 | 作用 |
| --- | --- |
| `Check-Only.cmd` | 列出匹配的进程并检查目录状态，不结束任何进程 |
| `Release-All-Project-Tools.cmd` | 结束匹配的进程，然后重新检查目录状态 |
| `Install-StartMenu.cmd` | 为当前用户创建开始菜单快捷方式 |

1. 双击 `Check-Only.cmd`，查看哪些进程占用了项目副本。
2. 确认这些项目中没有正在执行的任务，再双击 `Release-All-Project-Tools.cmd`。
3. 目录状态变为 `available` 后，回到 Codex 重新创建本地会话。

> [!WARNING]
> `Release-All-Project-Tools.cmd` 启动后立即执行，没有二次确认。它会结束**所有项目**中匹配的辅助进程（不限于当前项目），也不会判断任务是否空闲。正在进行的工具调用会被中断，内存中的变量和计算结果会丢失，文件写入可能无法完成，浏览器自动化也可能需要重新连接。

脚本不会结束 Codex 主程序或本地服务，不会删除项目资料和聊天记录，也不会修改 Codex 配置。它不联网，不常驻后台，每次运行一轮后即退出。通常不需要管理员权限，脚本也不会主动请求提权。

### 开始菜单快捷方式

运行 `Install-StartMenu.cmd` 后，在开始菜单搜索 `Codex Project Tool Release` 即可找到快捷方式。安装只创建快捷方式，不会执行释放。

安装后不要移动工具文件夹；如果移动了，删除旧快捷方式后重新运行 `Install-StartMenu.cmd`。卸载时，删除快捷方式和工具文件夹即可。

### 输出说明

| 状态 | 含义 |
| --- | --- |
| `check-only` | 只做了检查，未结束进程 |
| `released` | 进程已结束，并已确认退出 |
| `already-exited` | 处理时进程已经退出 |
| `unavailable-or-exited` | 无法访问或进程已退出，未能确认释放结果 |
| `identity-changed-skipped` | 结束前复核发现进程身份已变化，已跳过 |
| `available` | 目录访问检查通过，可以回到 Codex 重试 |
| `BLOCKED / Win32 32` | 目录仍有共享冲突 |

`available` 只代表检查那一刻目录没有被占用，不代表本地会话一定能创建成功。

如果状态仍是 `BLOCKED`，可能还有本工具处理范围以外的进程在占用目录，或者辅助进程已经重新启动。这时反复运行本工具通常没有帮助，可以在**资源监视器 → CPU → 关联的句柄**中搜索 `.chatgpt-projects`，查看还有哪些进程打开了项目副本。

## 兼容性与限制

| 类别 | 要求 |
| --- | --- |
| 系统 | Windows x64，不支持 ARM64，也不处理 32 位进程 |
| 运行环境 | 64 位 Windows PowerShell 5.1 及系统自带的 .NET |
| 客户端 | Microsoft Store 版 Codex，安装路径和进程结构需与脚本的识别规则一致 |
| 数据目录 | 默认的 `%USERPROFILE%\.codex`，暂不支持自定义 `CODEX_HOME` |

- 只处理 Codex 自带 `cua_node` 运行时中的 `node.exe` 和 `node_repl.exe`。Python、终端、其他插件，以及普通代码项目中的 Node 进程都不在处理范围内。
- 每次运行只释放一轮，之后新启动的辅助进程仍可能占用目录。
- 客户端更新、安装布局变化或权限不足，都可能导致进程无法被识别。
- 目前只在一台 Windows x64 设备上验证过扫描，以及手动结束指定进程后的效果。当前版本的脚本尚未经过多设备、长期使用的测试，也未分别在 Windows 10 和 Windows 11 上验证。

## 工作原理

进程同时满足以下条件时，才会被列为释放目标：

1. 可执行文件为 `node.exe` 或 `node_repl.exe`，且位于当前用户的 `OpenAI\Codex\runtimes\cua_node\<version>\bin` 目录下。
2. 工作目录位于某个项目副本内。
3. 父进程链能追溯到预期路径下的 Codex 本地服务 `codex.exe`，以及 Store 包中的 `ChatGPT.exe`。

结束进程前，脚本会重新核对进程的创建时间、映像路径、工作目录和父进程链，复核与终止使用同一个进程句柄，以免 PID 被复用时误杀其他进程。无法确认身份的进程会被跳过。

进程的工作目录通过只读访问进程环境块（PEB）获取，不注入代码，也不修改进程内存。PEB 属于 Windows 内部结构，系统更新后这部分实现可能需要适配。

## 开发与测试

在仓库目录中打开 64 位 PowerShell，运行以下命令。

运行测试：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-Guard.ps1
```

测试覆盖路径边界判断、进程类型筛选（排除普通 Node 进程和 Codex 本地服务）以及身份校验失败时的拒绝逻辑。测试不会结束真实的辅助进程，但也不能代替完整释放流程的测试。

只读扫描：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Release-ProjectTools.ps1 -NoPause
```

上述命令和 `.cmd` 启动器中的 `-ExecutionPolicy Bypass` 只对当次 PowerShell 进程生效，不会修改系统的执行策略。

## 相关资料

- [openai/codex#44736](https://github.com/openai/codex/issues/44736)：辅助进程占用项目副本导致同步失败的分析，以及通过配置工作目录规避的方法
- [openai/codex#42215](https://github.com/openai/codex/issues/42215)：项目上下文同步在 filesystem 阶段失败的报告
- [Codex 配置参考](https://learn.chatgpt.com/docs/config-file/config-reference)：`mcp_servers.<id>.cwd` 配置项说明

另一种规避思路是用 `mcp_servers.<id>.cwd` 让辅助进程在项目副本以外的目录启动。这样做会改变相对路径的解析基准，而且客户端启动或插件更新时可能覆盖该配置。本工具不修改这些设置。

## 问题反馈

提交 Issue 时，请提供：

- 系统版本与架构
- Codex 版本
- 操作步骤
- 脚本输出（可能包含项目 ID，请先脱敏）

请勿提交访问令牌、原始配置文件、聊天记录或项目资料。

## License

[MIT](LICENSE)
