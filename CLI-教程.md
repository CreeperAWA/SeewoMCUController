# Seewo MCU Controller CLI 使用教程

本教程将详细介绍如何使用 Seewo MCU Controller 命令行工具。

## 目录

1. [基本概念](#基本概念)
2. [安装与准备](#安装与准备)
3. [基本用法](#基本用法)
4. [详细命令说明](#详细命令说明)
5. [交互式模式](#交互式模式)
6. [设备路径指定](#设备路径指定)
7. [常见问题](#常见问题)

## 基本概念

Seewo MCU Controller 是一个用于控制 Seewo 设备上 MCU（微控制器单元）的命令行工具。它支持两种工作模式：

- **交互式模式**：程序启动后进入命令提示符，可以连续执行多个命令
- **命令行模式**：直接在命令行中指定参数执行单个操作

## 安装与准备

### 环境要求

- Windows 操作系统
- .NET 8.0 运行时

### 设备要求

- Seewo 设备（支持的 MCU 设备）
- 设备已正确连接到计算机

## 基本用法

### 启动交互式模式

```bash
SeewoMCUController.exe
```

程序将自动检测并连接 MCU 设备，然后显示交互式提示符。

### 执行单个命令

```bash
SeewoMCUController.exe [选项]
```

例如：
```bash
# 增加音量 1 级
SeewoMCUController.exe -vol +1

# 切换到 HDMI1
SeewoMCUController.exe -hdmi

# 显示设备信息
SeewoMCUController.exe -info
```

## 详细命令说明

### 音量控制

#### 增加音量

```bash
# 增加 1 级音量
SeewoMCUController.exe -vol +1

# 增加 5 级音量
SeewoMCUController.exe -vol +5

# 增加 1 级音量（简化命令）
SeewoMCUController.exe -vol up
```

#### 减少音量

```bash
# 减少 1 级音量
SeewoMCUController.exe -vol -1

# 减少 3 级音量
SeewoMCUController.exe -vol -3

# 减少 1 级音量（简化命令）
SeewoMCUController.exe -vol down
```

### HDMI切换

```bash
# 切换到 HDMI1
SeewoMCUController.exe -hdmi
```

### 触控笔控制

```bash
# 启用触控笔
SeewoMCUController.exe -pen on

# 禁用触控笔
SeewoMCUController.exe -pen off
```

### 设备信息查询

```bash
# 显示设备信息（主板名称、IP、UID、触摸屏尺寸、MCU版本等）
SeewoMCUController.exe -info

# 列出所有检测到的MCU设备
SeewoMCUController.exe -list
```

### 获取帮助

```bash
# 显示命令行帮助信息
SeewoMCUController.exe -h
SeewoMCUController.exe --help
```

## 交互式模式

进入交互式模式后，程序会显示设备信息并提供命令提示符。

### 可用命令

在交互式模式下，可以使用以下命令：

#### 音量控制
- `vol +<数值>` - 增加音量 (例: `vol +1`, `vol +5`)
- `vol -<数值>` - 减少音量 (例: `vol -1`, `vol -3`)
- `vol up` - 增加音量 1 级
- `vol down` - 减少音量 1 级

#### HDMI控制
- `hdmi` - 切换到 HDMI1

#### 触控笔控制
- `pen on` - 启用触控笔
- `pen off` - 禁用触控笔

#### 信息查询
- `info` - 重新显示设备信息
- `list` - 列出所有检测到的MCU设备

#### 系统命令
- `help` 或 `?` - 显示帮助信息
- `exit` 或 `quit` - 退出程序

### 交互式模式示例

```
==========================================
    Seewo MCU Controller - 交互式模式
==========================================

正在查找 MCU 设备...
MCU 设备已连接

==========================================
           MCU 设备信息
==========================================
开始获取设备信息...
获取主板名称: T.HV551.73E
获取IP:
获取UID:
获取触摸屏尺寸: 82
获取MCU版本: 82-X82G15_20P,7972_20190620-P-20P
获取设备连接信息: VID=0x1FF7, PID=0x0F21, Path=\\?\hid#vid_1ff7&pid_0f21&mi_00&col02#8&362c00b8&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}
开始打印设备信息...
主板名称: T.HV551.73E
设备 IP:
设备 UID: 未知
触摸屏:   82 英寸
MCU 版本: 82-X82G15_20P,7972_20190620-P-20P
设备 VID: 0x1FF7
设备 PID: 0x0F21
设备路径: \\?\hid#vid_1ff7&pid_0f21&mi_00&col02#8&362c00b8&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}
==========================================

可用命令:
  vol +<数值>     - 增加音量 (例: vol +1, vol +5)
  vol -<数值>     - 减少音量 (例: vol -1, vol -3)
  vol up          - 增加音量 1 级
  vol down        - 减少音量 1 级
  hdmi            - 切换到 HDMI1
  pen on          - 启用触控笔
  pen off         - 禁用触控笔
  info            - 重新显示设备信息
  list            - 列出所有检测到的MCU设备
  help / ?        - 显示此帮助信息
  exit / quit     - 退出程序

等待输入命令...
> vol +1
音量增加成功
> hdmi
已切换到 HDMI1
> exit
正在退出...
```

## 设备路径指定

当计算机连接了多个 MCU 设备时，可以使用 `-d` 或 `--device-path` 参数指定特定的设备：

```bash
# 指定设备路径执行音量增加
SeewoMCUController.exe -d "\\?\hid#vid_1fe7&pid_0004#6&314b457f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}" -vol +1

# 使用长格式参数
SeewoMCUController.exe --device-path "\\?\hid#vid_1fe7&pid_0004#6&314b457f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}" -vol +1
```

要查看所有可用的设备路径，可以使用：

```bash
SeewoMCUController.exe -list
```

## 常见问题

### Q: 如何确定正确的设备路径？

A: 使用以下命令列出所有检测到的设备：
```bash
SeewoMCUController.exe -list
```

## 高级用法示例

### 批处理脚本示例

创建一个批处理脚本 `mcu_control.bat`：

```batch
@echo off
echo 正在执行MCU控制序列...
SeewoMCUController.exe -vol +2
timeout /t 1 /nobreak >nul
SeewoMCUController.exe -hdmi
timeout /t 1 /nobreak >nul
SeewoMCUController.exe -info
echo MCU控制序列执行完成
pause
```

### PowerShell 脚本示例

```powershell
Write-Host "正在执行MCU控制序列..."
.\SeewoMCUController.exe -vol +2
Start-Sleep -Seconds 1
.\SeewoMCUController.exe -hdmi
Start-Sleep -Seconds 1
.\SeewoMCUController.exe -info
Write-Host "MCU控制序列执行完成"
```

## 故障排除

如果遇到问题，请尝试以下步骤：

1. 确认 .NET 8.0 运行时已安装
2. 重新连接 MCU 设备
3. 检查设备管理器中是否有错误的设备
4. 以管理员身份运行程序
5. 查看程序输出的调试信息