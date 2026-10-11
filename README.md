# Seewo MCU Controller

Seewo MCU Controller 是一个运行在 Windows 上的命令行控制工具，用于通过 HID 设备连接并控制适用 Remote6 的希沃 OPS 与安卓板之间的控制单元（不支持使用 UDI 的机型）。它同时提供：

- 交互式 TUI，适合现场操作和查看实时事件；
- 分类式 CLI，适合脚本、远程调用和一次性执行命令；
- HID 设备发现、连接、握手、帧封装探测和重连；
- 双向报文解析、设备状态缓存和部分事件自动应答；
- 原始帧发送能力，便于调试或使用尚未加入菜单的命令。

本项目适用于搭配其他程序控制设备输入源、音视频状态、锁屏、设备信息和系统动作的。程序不会修改系统配置，也不会在后台常驻；执行单条 CLI 命令后会退出，直接启动程序则进入 TUI。

## 特别说明

为了避免有人没看完 README 就使用，这里特别强调一下：

> [!CAUTION]
> 本程序的锁 Android 屏功能会触发 Android 内的屏幕锁，不是熄灭屏幕！不是 U 盘锁！不是触控锁定！这是一个很危险的功能，如果你只是无聊，请不要动它！
> 为防止误操作，TUI 中已设置二次确认，在未设置密码的前提下只能锁屏 10s，如果您使用 CLI 执行锁屏命令，会一直锁屏，直到执行解锁命令或依照锁屏页面流程（如使用密码、U 盘）解锁屏幕。

## 功能概览

### 控制功能

- 音量绝对值设置、增减音量、静音和取消静音；
- 声音模式切换：标准、教室、影视、会议；
- 显示模式切换：标准、明亮、鲜艳、柔和；
- PC、TV、HDMI、VGA、Android 通道切换；
- 默认通道设置、当前通道上报和全通道状态请求；
- Android 屏幕锁定、解锁及口令相关应答；
- 锁屏口令的设置（两次输入确认）、清除与 MD5 校验；
- 时间同步、关机通知、音视频模式声明、WOL、WOL 服务名注册；
- 打开网络设置菜单、线屏插入通知、触控笔开关；
- U 盘解锁通知、解锁文件回传请求、SN 接收确认以及多种状态应答；
- 自定义主命令、子命令和负载的原始帧发送。

### 查询和监控

- 查询板卡名、Android SN、Android MAC、机型、IP、UID 和固件版本；
- 缓存并展示 USB 机型、Android/MCU/触摸固件、触摸屏尺寸、当前通道等状态；
- 查看最近的上下行报文；
- 查看 MCU 主命令事件字典；
- 接收请求 PC 时间、口令校验、SN、文件和 U 盘状态等事件，并按配置自动应答；
- 枚举当前检测到的 HID 设备并显示 VID、PID、设备类型和路径。

### 连接和链路

- 支持无头和带头两种 64 字节 HID 报告封装；
- 默认自动探测帧模式，也可使用命令行或 TUI 手动指定；
- 连接后默认执行握手，也可关闭；
- 可关闭自动应答、调整查询超时和帧间延迟；
- TUI 支持独立实时日志窗口；在无法创建独立窗口的环境中自动退化为内嵌日志面板；
- 命令注册表由 TUI 和 CLI 共用，因此两种入口的动作名称、参数和行为保持一致。

## 环境要求

- Windows 10 或 Windows 11；
- .NET 8.0 SDK（从源码构建，运行不需要安装）；
- 可访问目标 HID 设备。

项目使用 .NET 8，并启用 Native AOT 发布配置。程序依赖 Windows 控制台和 HID 设备发现 API，不以 Linux 或 macOS 为目标平台。

## 构建和发布

在仓库根目录执行：

```powershell
dotnet build -c Release
```

发布为 Windows x64 独立程序：

```powershell
dotnet publish -c Release -r win-x64 --self-contained
```

发布文件位于 `SeewoMCUController/bin/Release/net8.0/win-x64/publish/`。也可以直接运行构建输出目录中的 `SeewoMCUController.exe`。

## 快速开始

### 启动 TUI

不带参数运行程序：

```powershell
SeewoMCUController.exe
```

程序会发现设备、建立连接、自动探测帧模式并执行握手，然后进入分类菜单。按菜单提示输入序号即可进入对应分类，也可以直接输入 CLI 风格的命令，例如：

```text
audio volume 60
channel switch hdmi 1
info show
q
```

### 执行单条 CLI 命令

CLI 的基本格式是：

```text
SeewoMCUController.exe <分类> <动作> [参数...]
```

动作名称在所有分类中唯一时，可以省略分类：

```text
SeewoMCUController.exe <动作> [参数...]
```

例如：

```powershell
SeewoMCUController.exe audio volume 60
SeewoMCUController.exe audio mute
SeewoMCUController.exe video mode soft
SeewoMCUController.exe channel switch hdmi 1
SeewoMCUController.exe info version
SeewoMCUController.exe system time
SeewoMCUController.exe mute
```

单条命令默认会连接设备并执行握手。命令结束后程序断开连接并退出。

## CLI 选项

CLI 选项可以放在动作前或动作后。需要带值的选项使用空格或 `=` 连接均可，例如 `--timeout 3000` 和 `--timeout=3000`。

| 选项 | 说明 |
| --- | --- |
| `-h`, `--help` | 显示用法、选项和分类概览 |
| `-d`, `--device-path <路径>`、`--device <路径>` | 指定 HID 设备路径；不指定时自动选择检测到的设备 |
| `--frame <模式>`、`--frame-mode <模式>` | 指定帧模式：`auto`、`raw` 或 `framed`；默认是 `auto` |
| `--no-handshake` | 连接后不执行握手序列 |
| `--no-auto-reply` | 关闭后台事件的自动应答；**不影响锁屏口令校验** |
| `--no-connect` | 保留给本地动作使用；不会替代需要设备连接的动作 |
| `--timeout <毫秒>` | 查询类动作等待回包的时间；默认 `2000` |
| `--hold`、`--wait-unlock` | 动作执行完后驻留，持续应答上行请求（口令校验等）。`lock lock` 与 `lock set-password` 本来就会驻留 |
| `--hold-seconds <秒>` | 驻留时限；**默认不限时**，给 `0` 同样是不限时 |
| `--password <口令>`、`--pwd <口令>` | 本次进程内设置锁屏口令；隐含 `--hold` 与 `--verbose` |
| `-v`, `--verbose` | 把 MCU 上行事件逐条打印到标准输出 |
| `--dry-run` | 只校验动作和参数并打印将执行的命令，不连接设备 |
| `--selftest` | 构造并校验全部命令帧，不连接设备 |
| `--categories` | 列出全部 CLI 分类 |
| `--commands [分类]` | 列出全部动作，或只列出指定分类的动作 |
| `--devices` | 枚举检测到的 HID 设备 |

查看帮助和动作列表：

```powershell
SeewoMCUController.exe --help
SeewoMCUController.exe --categories
SeewoMCUController.exe --commands
SeewoMCUController.exe --commands audio
SeewoMCUController.exe --devices
SeewoMCUController.exe --selftest
```

推荐先使用 `--commands` 查看当前版本的精确动作、参数和对应功能，再将命令用于脚本。CLI 返回码约定如下：

- `0`：执行成功或查询型本地动作成功；
- `1`：设备连接失败、发送失败、查询超时或设备返回错误；
- `2`：参数、动作或选项无效。

## CLI 命令参考

下面列出所有功能分类及常用动作。动作可能有别名；完整别名和当前版本的参数说明以 `--commands` 输出为准。

### `audio` 音频设置

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `volume` | `audio volume <0-100>` | 设置绝对音量 |
| `up` | `audio up [级数]` | 增加音量，默认 1 级 |
| `down` | `audio down [级数]` | 减少音量，默认 1 级 |
| `mute` | `audio mute` | 静音 |
| `unmute` | `audio unmute` | 取消静音 |
| `mode` | `audio mode <standard\|classroom\|movie\|meeting>` | 设置声音模式 |
| `modes` | `audio modes` | 显示声音模式映射 |

### `video` 视频设置

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `mode` | `video mode <standard\|brightness\|gorgeous\|soft>` | 设置显示模式 |
| `modes` | `video modes` | 显示显示模式映射 |

### `channel` 通道设置

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `switch` | `channel switch <pc\|tv\|hdmi\|vga\|android> [param]` | 切换通道 |
| `default` | `channel default <通道> [param]` | 设置默认通道 |
| `report` | `channel report <通道>` | 上报当前通道 |
| `all` | `channel all` | 请求所有通道状态 |
| `list` | `channel list` | 查看通道编号和参数规则 |

对于 HDMI 和 VGA，`param` 是从 1 开始的输入序号。常用通道名称也支持数字和部分别名，具体可用 `channel list` 查看。

### `lock` 锁屏及相关应答

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `lock` | `lock lock` | 锁定 Android 屏幕 |
| `unlock` | `lock unlock` | 解锁 Android 屏幕 |
| `set-password` | `lock set-password <口令>` | 设置锁屏口令；TUI 中会要求输入两遍确认 |
| `clear-password` | `lock clear-password` | 清除锁屏口令，回到任何口令都能解锁 |
| `pwd-status` | `lock pwd-status` | 查看当前口令是否已设置 |
| `pwd` | `lock pwd <口令>` | 返回口令校验结果；通过时按配置自动解锁 |
| `file-back` | `lock file-back` | 请求回传解锁文件数据 |
| `sn-ack` | `lock sn-ack` | 确认收到 SN |
| `notice` | `lock notice [tag]` | 发送无负载通知 |
| `ack14` | `lock ack14 [tag] [负载]` | 发送状态或凭据应答 |
| `ack16` | `lock ack16 [tag] [负载]` | 发送开关或弹窗状态应答 |
| `ack18` | `lock ack18 [tag] [负载]` | 发送二进制负载应答 |
| `ack35` | `lock ack35 <v1> [v2 v3]` | 发送状态应答 |

#### 锁屏口令

锁 Android 屏后，安卓板把用户在锁屏页输入的**明文口令**放进 `(0x36,0x30)` 负载，PC 侧校验后回
`(0x36,0x31)` + 1 字节（`1` 通过 / `0` 不通过），为 `1` 时再发 `(0x36,0x12)` 解锁。

本程序的校验方式与官方 `SeewoUSBLockMonitor.exe` 一致：对**原始负载字节**做 MD5，
得到 32 位小写十六进制后与本机保存的值比对（官方 `DevLockService::VerifyLockPwdByLocalCache`）。
口令只存在于本进程内存中，退出即失效，不写盘、不修改系统配置。

| 状态 | 行为 |
| --- | --- |
| 未设置口令 | 任何口令都判定为正确，保持原有行为 |
| 已设置口令 | 只有口令完全一致（区分大小写与符号）才回 `1` 并解锁 |

口令校验始终自动应答，`--no-auto-reply` 与 `link auto-reply off` 都不会关掉它。

口令可用大小写字母、数字和符号，建议不超过 48 位。该数值仅用于兼容性提示，不限制实际输入；仍不建议使用过长或含大量特殊字符的口令：
部分 Android 输入法无法正确输入这类字符，会导致锁屏之后再也解不开。TUI 的锁屏菜单会常驻显示这条提示。

在 TUI 中执行 `set-password` 时会要求输入两遍，两次不一致则放弃设置、原口令保持不变。
命令行直接给出口令则跳过确认：

```powershell
SeewoMCUController.exe lock set-password 'Passw0rd'
SeewoMCUController.exe lock pwd-status
SeewoMCUController.exe lock clear-password
```

#### 命令行锁屏会自动驻留

安卓板是在收到 `(0x36,0x10)` 之后才弹出锁屏页的，用户输完口令才会上报 `(0x36,0x30)`。
**PC 侧必须一直在运行才能应答**，否则安卓板的上报无人受理，屏幕再也解不开。

因此 `lock lock` 和 `lock set-password` 在命令行下**执行完都不会退出**，会自动驻留等待校验，
不需要额外加 `--hold`，且**默认不限时**——屏幕还锁着，进程就没有自行退出的道理。

驻留期间会出现 `驻留>` 提示符，可以继续输入命令：

| 输入 | 行为 |
| --- | --- |
| `unlock` / `lock unlock` | 解锁 Android 屏，随后自动结束驻留 |
| `exit` / `quit` / `q` | 退出。**屏幕仍锁着时会被拒绝**，先解锁才能退 |
| `help` | 显示驻留模式可用命令 |
| 其它命令 | 与单次 CLI 用法相同，例如 `info show`、`lock pwd-status` |

驻留结束条件（满足任一即可）：

- 口令校验通过并已发出 `(0x36,0x12)` → 立即退出；
- 执行了 `unlock` → 退出；
- 输入 `exit` 且屏幕未锁 → 退出；
- 显式给了 `--hold-seconds` 且到达时限 → 退出；
- 按 Ctrl+C → 强制退出（操作系统级中断，不做拦截）。

```powershell
# 直接锁屏，自动驻留；未设口令时任何口令都能解锁
SeewoMCUController.exe lock lock

# 设口令 + 锁屏 + 驻留（--password 隐含 --hold 与 --verbose）
SeewoMCUController.exe lock lock --password 'Passw0rd'

# 等价写法：先设口令，再锁屏，两步合成一次调用
SeewoMCUController.exe lock set-password 'Passw0rd'

# 显式声明不限时（本来就是默认）
SeewoMCUController.exe lock lock --hold-seconds 0

# 指定驻留时限（不指定则不限时）
SeewoMCUController.exe lock lock --password 'Passw0rd' --hold-seconds 120
```

驻留期间在 `驻留>` 提示符下可以继续下命令：

```text
驻留> unlock        # 解锁并退出驻留
驻留> exit          # 屏幕已解锁时退出
驻留> exit          # 屏幕还锁着 → [ERR] 屏幕仍处于锁屏状态，不能退出
```

> `lock set-password` 是纯本地动作，本身不发送 MCU 命令。为了让「设口令」在命令行下
> 真正有意义，它执行后会自动补发一帧锁屏，再进入驻留；TUI 菜单里两者仍是独立动作。

#### 口令校验不受自动应答开关影响

`--no-auto-reply` / `link auto-reply off` 只关闭对时、SN 确认、文件回传、U 盘弹窗这几类自动应答。
**锁屏口令校验 `(0x36,0x30)` 始终自动应答**，不受该开关影响：漏掉这一帧就等于告诉安卓板
「不解锁」，屏幕会一直锁着。因此即使加了 `--no-auto-reply`，口令解锁依然可用。


### `info` 设备信息

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `show` | `info show` | 显示当前缓存的设备信息 |
| `board-name` | `info board-name` | 查询板卡名并等待结果 |
| `sn` | `info sn` | 查询 Android SN |
| `mac` | `info mac` | 查询 Android MAC |
| `machine` | `info machine` | 查询机型字符串 |
| `ip` | `info ip` | 查询设备 IP |
| `uid` | `info uid` | 查询设备 UID |
| `version` | `info version` | 查询 Android、MCU 和触摸固件版本 |
| `android-info` | `info android-info` | 发送 Android 信息请求，不等待回包 |
| `base-info` | `info base-info` | 发送基础信息请求 |
| `all-channel` | `info all-channel` | 请求所有通道状态 |

### `system` 系统与电源

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `handshake` | `system handshake` | 发送握手首包 |
| `handshake-seq` | `system handshake-seq` | 执行完整握手序列 |
| `time` | `system time` | 将本机当前时间同步到 Android 板 |
| `poweroff` | `system poweroff` | 发送关机通知 |
| `av-mode` | `system av-mode <0\|1\|2>` | 声明音视频模式 |
| `wol` | `system wol` | 发送 WOL 广播 |
| `wol-register` | `system wol-register [名称]` | 注册 WOL 服务名 |
| `network-menu` | `system network-menu` | 打开网络设置菜单 |
| `cable` | `system cable` | 通知线屏已插入 |

### `device` 外设与工具

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `pen` | `device pen <on\|off>` | 开启或关闭触控笔 |
| `usb-unlock` | `device usb-unlock` | 通知设备已通过 U 盘完成解锁 |
| `raw` | `device raw <main> <sub> [负载]` | 发送自定义原始帧 |

`raw` 的 `main` 和 `sub` 支持十进制或 `0x` 十六进制写法。负载会优先按连续十六进制解析，否则按 UTF-8 文本解析。例如：

```powershell
SeewoMCUController.exe device raw 0x2B 0x72 68656C6C6F
SeewoMCUController.exe device raw 43 114 hello
```

### `log` 事件与报文

这些动作只读取或修改当前进程内的日志，不需要设备连接：

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `show` | `log show [条数]` | 显示最近报文，默认 20 条 |
| `clear` | `log clear` | 清空报文日志 |
| `events` | `log events` | 显示事件字典 |

### `link` 连接与链路

这些动作用于查看或调整当前进程的连接行为：

| 动作 | 用法 | 说明 |
| --- | --- | --- |
| `devices` | `link devices` | 枚举 HID 设备 |
| `frame-mode` | `link frame-mode [auto\|raw\|framed]` | 查看或切换帧模式 |
| `auto-reply` | `link auto-reply [on\|off]` | 查看或开关自动应答（不影响锁屏口令校验） |
| `inter-frame` | `link inter-frame [毫秒]` | 查看或设置帧间延迟 |
| `disconnect` | `link disconnect` | 断开当前连接 |
| `reconnect` | `link reconnect` | 重连并重新握手 |

## TUI 使用说明

直接启动程序即可进入 TUI。主菜单包含以下分类：

| 分类 | 内容 |
| --- | --- |
| 音频设置 | 音量、静音和声音模式 |
| 视频设置 | 显示模式 |
| 通道设置 | 输入源切换、默认通道和通道状态 |
| 锁屏设置 | 锁屏、解锁、口令设置与校验及其他应答（菜单顶部常驻显示口令状态与兼容性提示） |
| 设备信息 | 设备状态和查询 |
| 系统与电源 | 握手、时间、电源、WOL 和系统菜单 |
| 外设与工具 | 触控笔、U 盘通知和原始帧 |
| 事件与报文 | 报文日志和事件字典 |
| 连接与链路 | 设备、帧模式、自动应答和重连 |

### 快捷键和直接输入

- `h` 或 `help`：分页显示动作帮助；
- `d`：重新执行握手；
- `c`：切换彩色和单色显示；
- `Esc`：清空当前输入；
- `q`：退出程序；
- 方向键和翻页键：滚动较长的动作列表。

在菜单中可以直接输入完整动作，例如 `audio volume 60`、`mute` 或 `channel switch hdmi 1`，不必逐级选择菜单。

### 日志窗口

TUI 默认将操作台和实时日志分为两个窗口：

- 操作台窗口负责菜单、连接状态和输入；
- 实时日志窗口显示时间、方向、分类、事件、详情、自动应答和链路状态。

日志窗口由程序自动启动，不需要手动执行内部启动参数。关闭任意一个窗口都会结束整个 TUI 会话。若当前环境无法创建独立控制台，日志会显示在操作台底部。

日志最多保留 1024 条记录；方向、分类、事件和详情完全相同的相邻内容会合并计数。通过设置环境变量 `NO_COLOR`，可以默认关闭颜色：

```powershell
$env:NO_COLOR = "1"
SeewoMCUController.exe
```

## 帧模式和连接行为

程序使用 64 字节 HID 报告传输业务帧，并支持两种封装方式：

- `raw`：直接使用业务帧；
- `framed`：在业务帧前加入固定的带头封装；
- `auto`：连接时探测，适合不确定设备模式的情况。

通常不需要手动选择模式。如果设备已经确认使用某一种模式，才建议显式指定：

```powershell
SeewoMCUController.exe --frame raw audio volume 50
SeewoMCUController.exe --frame framed info show
```

每次发送之间默认保留 50 毫秒间隔，可在 TUI 中使用 `link inter-frame` 调整；单条 CLI 命令则可通过 TUI 会话或代码接口调整。查询类命令默认等待 2000 毫秒，可以按设备响应速度调整：

```powershell
SeewoMCUController.exe --timeout 5000 info version
```

指定设备路径的示例：

```powershell
SeewoMCUController.exe --device-path "\\?\hid#..." info show
```

建议先执行 `--devices`，复制目标设备的完整路径，避免连接到错误设备。

## 常见场景

### 调整音量并切换输入源

```powershell
SeewoMCUController.exe audio volume 60
SeewoMCUController.exe channel switch hdmi 1
```

### 查询设备状态

```powershell
SeewoMCUController.exe info show
SeewoMCUController.exe info version
SeewoMCUController.exe info ip
```

### 关闭握手执行控制动作

当设备已经由其他程序初始化，或需要避免重复握手时：

```powershell
SeewoMCUController.exe --no-handshake audio mute
```

### 只检查命令和参数

不会连接设备，也不会发送报文：

```powershell
SeewoMCUController.exe --dry-run channel switch hdmi 1
SeewoMCUController.exe --dry-run device raw 0x2B 0x72 68656C6C6F
```

## 故障排查

### 找不到设备

1. 确认设备已连接并已被 Windows 识别；
2. 执行 `SeewoMCUController.exe --devices` 查看枚举结果；
3. 使用 `--device-path` 指定完整设备路径；
4. 关闭可能独占 HID 设备的其他控制程序后重试。

### 发送成功但设备无反应

1. 先尝试 `--frame auto`，再根据日志确认模式；
2. 使用 `--frame raw` 或 `--frame framed` 手动切换；
3. 确认通道名称和 HDMI/VGA 的 `param` 是否正确；
4. 使用 `--no-handshake` 或不使用该选项分别测试；
5. 对查询命令增加 `--timeout`，并通过 `info show` 或 `log show` 检查状态和回包。

### CLI 报参数错误

使用以下命令查看当前版本的动作、别名和参数格式：

```powershell
SeewoMCUController.exe --commands
SeewoMCUController.exe --commands <分类>
```

选项值必须放在选项后，例如 `--frame raw`、`--timeout 3000`。十六进制字节可以使用 `0x` 前缀；原始负载也可以直接写成连续十六进制字符串。

## 项目结构

```text
SeewoMCUController/
├─ Cli/                 CLI 参数解析、命令执行和日志窗口入口
├─ Commands/            TUI 与 CLI 共用的动作注册表
├─ Mcu/                 设备连接、HID 发现、状态和链路
│  └─ Protocol/         帧编解码、出站组包和入站事件解析
├─ Ui/                  TUI、日志表格、终端布局和颜色主题
├─ Program.cs           程序入口、取消处理和顶层异常处理
└─ SeewoMCUController.csproj
```

新增一个可通过菜单和 CLI 使用的动作时，应在 `Commands/McuActionRegistry.cs` 注册，并按需要补充出站组包、入站解析或设备状态处理。注册表会自动驱动 `--commands` 输出和 TUI 菜单。

## 贡献

欢迎提交问题报告和 Pull Request。建议的流程如下：

1. Fork 项目；
2. 创建独立功能分支；
3. 保持 CLI、TUI 和设备状态行为一致；
4. 在真实设备或可用的本地环境中验证变更；
5. 提交清晰的变更说明并发起 Pull Request。

提交问题时，请附上 Windows 版本、程序版本、交互智能平板型号（通常在机身蓝色标签上标明，如 `F82EC`、`F86EC`，亦可在工厂菜单查看）、OPS（可在“运行”中输入 `msinfo32`，查看「系统型号」，通常是以 MT 开头的字符串，如 `MT41A SHB`）、执行命令、退出码，以及在不包含敏感设备信息的前提下提供相关日志。

## 许可证

本项目使用 MIT License，详情见 [`LICENSE`](LICENSE)。
