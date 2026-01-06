# Seewo MCU Controller

一个用于控制 Seewo 设备上 MCU（微控制器单元）的命令行工具。

## 功能特性

- **设备检测**：自动检测连接的 MCU 设备
- **音量控制**：支持增加和减少系统音量
- **HDMI切换**：支持切换到 HDMI1 输入
- **触控笔控制**：支持启用和禁用安卓触控笔
- **设备信息查询**：获取设备的主板名称、IP 地址、UID、触摸屏尺寸和 MCU 版本
- **交互式模式**：提供交互式命令行界面
- **命令行模式**：支持直接执行特定命令

## 安装说明

### 环境要求

- .NET 8.0 运行时

### 编译

```bash
dotnet build -c Release
```

### 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## 使用指南

### 交互式模式

直接运行程序即可进入交互式模式：

```bash
SeewoMCUController.exe
```

### 命令行模式

直接在命令行中指定参数执行操作：

```bash
# 增加音量
SeewoMCUController.exe -vol +1

# 减少音量
SeewoMCUController.exe -vol -1

# 切换到 HDMI1
SeewoMCUController.exe -hdmi

# 启用触控笔
SeewoMCUController.exe -pen on

# 禁用触控笔
SeewoMCUController.exe -pen off

# 显示设备信息
SeewoMCUController.exe -info

# 列出所有检测到的设备
SeewoMCUController.exe -list

# 指定设备路径
SeewoMCUController.exe -d "\\?\hid#vid_1ff7&pid_0f21&mi_00&col02#8&362c00b8&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}" -vol +1
```

### 交互式命令

在交互式模式下，可以使用以下命令：

- `vol +<数值>` - 增加音量 (例: vol +1, vol +5)
- `vol -<数值>` - 减少音量 (例: vol -1, vol -3)
- `vol up` - 增加音量 1 级
- `vol down` - 减少音量 1 级
- `hdmi` - 切换到 HDMI1
- `pen on` - 启用触控笔
- `pen off` - 禁用触控笔
- `info` - 重新显示设备信息
- `list` - 列出所有检测到的MCU设备
- `help / ?` - 显示帮助信息
- `exit / quit` - 退出程序

### 设备路径参数

使用 `-d` 或 `--device-path` 参数可以指定特定的设备路径：

```bash
SeewoMCUController.exe --device-path "\\?\hid#vid_1ff7&pid_0f21&mi_00&col02#8&362c00b8&0&0001#{4d1e55b2-f16f-11cf-88cb-001111000030}" -vol +1
```

## 架构概述

项目采用分层架构：

- `Program.cs` - 应用程序入口点，处理命令行参数和交互式模式
- `McuController.cs` - MCU 控制器，提供高级控制接口
- `Mcu.cs` - MCU 设备控制类，提供底层控制功能
- `McuHunter.cs` - MCU 设备发现和连接管理
- `McuCommand.cs` - MCU 命令定义

## 贡献指南

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 发起 Pull Request

## 许可证

请参阅项目中的 LICENSE 文件。