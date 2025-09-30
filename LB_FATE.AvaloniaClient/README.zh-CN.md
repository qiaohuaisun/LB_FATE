# LB_FATE Avalonia GUI 客户端

一款基于 Avalonia UI 构建的现代化跨平台 LB_FATE 游戏图形客户端。

## 功能特性

### ✨ 当前功能

- **现代化图形界面**：简洁的深色主题界面，采用标签页布局
- **实时游戏棋盘**：可视化 10×10 网格，以不同颜色表示单位
- **交互式控制**：
  - 左键点击空白格子进行移动
  - 右键点击敌方单位进行攻击
  - 提供命令输入框，支持高级指令
- **单位状态显示**：实时显示所有单位的 HP/MP 血条
- **技能面板**：列出可用技能并追踪冷却时间
- **战斗日志**：可滚动查看最近的游戏事件
- **连接管理**：简易的服务器连接界面
- **跨平台支持**：可在 Windows、Linux 和 macOS 上运行

### 🎮 操作说明

#### 鼠标控制
- **左键点击**空白格子 → 移动至该位置
- **右键点击**敌方单位 → 攻击该单位

#### 命令输入框
直接输入以下命令：
- `move x y` - 移动至坐标 (x, y)
- `attack P#` - 攻击 ID 为 P# 的玩家
- `use n P#` - 对目标 P# 使用第 n 号技能
- `use n x y` - 在坐标 (x, y) 处使用第 n 号技能
- `use n up/down/left/right` - 向指定方向使用技能
- `pass` - 结束当前回合
- `skills` - 列出可用技能
- `info` - 显示角色信息
- `help` - 显示帮助信息

#### 快捷按钮
- **发送（Send）** - 执行输入框中的命令
- **跳过（Pass）** - 立即结束当前回合
- **技能（Skills）** - 刷新技能列表
- **帮助（Help）** - 显示快速帮助

## 构建与运行

### 先决条件
- .NET 8.0 SDK
- ETBBS 核心库（自动引用）

### 构建
```bash
dotnet build LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj -c Release
```

### 运行
```bash
dotnet run --project LB_FATE.AvaloniaClient/LB_FATE.AvaloniaClient.csproj
```

或在 Windows 上：
```cmd
LB_FATE.AvaloniaClient\bin\Release\net8.0\LB_FATE.AvaloniaClient.exe
```

## 使用指南

### 1. 启动服务器

首先，使用启动脚本之一启动 LB_FATE 服务器：

**Windows**：
```cmd
cd publish
runServer.cmd
```

选择所需的日志级别并配置：
- 玩家数量（例如：2）
- 游戏模式（ffa 或 boss）
- 端口（默认：35500）

### 2. 启动客户端

运行 Avalonia 客户端应用程序。

### 3. 连接服务器

1. 在 **连接（Connection）** 标签页中，输入：
   - 服务器主机：`127.0.0.1`（本地服务器）
   - 服务器端口：`35500`（或自定义端口）
2. 点击 **连接服务器（Connect to Server）**
3. 等待显示“已连接（Connected）”状态

### 4. 开始游戏

1. 切换到 **游戏（Game）** 标签页（连接成功后自动启用）
2. 等待轮到自己（出现绿色“YOUR TURN”提示）
3. 查看游戏棋盘：
   - 不同颜色代表不同职业
   - 血条显示单位生命值
   - 数字显示单位 ID
4. 执行操作：
   - 点击格子进行移动
   - 右键点击敌人进行攻击
   - 通过按钮或命令使用技能
5. 点击 **跳过（Pass）** 按钮或输入 `pass` 命令结束回合

## 架构设计

### 项目结构

```
LB_FATE.AvaloniaClient/
├── Models/
│   └── GameState.cs           - 客户端游戏状态
├── ViewModels/
│   ├── ViewModelBase.cs       - 基础 ViewModel
│   ├── MainWindowViewModel.cs - 主窗口 ViewModel
│   └── GameViewModel.cs       - 游戏逻辑 ViewModel
├── Views/
│   ├── MainWindow.axaml       - 主窗口布局
│   ├── MainWindow.axaml.cs
│   ├── GameView.axaml         - 游戏界面布局
│   └── GameView.axaml.cs
├── Controls/
│   ├── GameBoardControl.axaml - 自定义游戏棋盘控件
│   └── GameBoardControl.axaml.cs
├── Services/
│   ├── GameClient.cs          - 服务器 TCP 通信客户端
│   └── GameStateParser.cs     - 解析服务器消息
└── Assets/                     - 图标和资源文件
```

### MVVM 模式

本客户端遵循 **Model-View-ViewModel (MVVM)** 模式：

- **Models**：数据结构（`GameState`、`UnitInfo`、`SkillInfo`）
- **ViewModels**：业务逻辑与状态管理（`GameViewModel`）
- **Views**：XAML UI 定义（`MainWindow`、`GameView`）

### 核心组件

#### GameClient
处理与服务器的 TCP 连接：
- 异步连接/断开
- 发送命令
- 后台线程接收消息
- 事件驱动架构

#### GameStateParser
将服务器文本消息解析为结构化数据：
- 棋盘状态（天数/阶段）
- 单位信息（HP/MP/位置）
- 技能详情
- 战斗日志

#### GameBoardControl
用于渲染游戏棋盘的自定义 Avalonia 控件：
- 基于 Canvas 绘制
- 网格线和坐标
- 按职业着色的单位可视化
- HP 血条
- 鼠标交互

#### GameViewModel
核心 ViewModel，负责管理：
- 连接状态
- 游戏状态更新
- 用户命令
- UI 绑定的可观察集合

## 配色方案

### 职业颜色
- **剑士（Saber）**：青色 (#00CED1)
- **弓兵（Archer）**：绿色 (#32CD32)
- **枪兵（Lancer）**：蓝色 (#4169E1)
- **骑兵（Rider）**：金色 (#FFD700)
- **术士（Caster）**：洋红色 (#FF00FF)
- **刺客（Assassin）**：深青色 (#008B8B)
- **狂战士（Berserker）**：红色 (#DC143C)

### UI 颜色
- 背景：深色 (#1e1e1e, #2b2b2b)
- 网格线：灰色 (#444444)
- HP 血条：绿色 (#00FF00) / 深红色 (#8B0000)
- 当前回合：青柠绿
- 日志：浅灰色

## 网络协议

客户端通过基于文本的 TCP 协议与服务器通信：

### 客户端 → 服务器
- 命令字符串（例如："move 5 3"、"attack P1"、"pass"）

### 服务器 → 客户端
- 棋盘状态文本（由 `GameStateParser` 解析）
- 单位状态行
- 战斗日志消息
- 玩家回合时发送 "PROMPT"
- 游戏结束时发送 "GAME OVER"

## 未来增强计划

### 计划功能
- [ ] 移动和攻击动画
- [ ] 技能范围预览
- [ ] 音效和背景音乐
- [ ] 战斗回放系统
- [ ] 多服务器配置文件
- [ ] 聊天系统
- [ ] 统计仪表板
- [ ] 设置/偏好面板
- [ ] 自定义主题
- [ ] 移动端触控优化

### 技术改进
- [ ] 更完善的错误处理和重连机制
- [ ] 消息队列实现更流畅的更新
- [ ] 单位精灵/头像系统
- [ ] 技能粒子特效
- [ ] 大型棋盘的小地图
- [ ] 性能优化
- [ ] 国际化支持 (i18n)

## 依赖项

- **Avalonia**：11.3.6 - 跨平台 UI 框架
- **CommunityToolkit.Mvvm**：8.2.1 - MVVM 辅助工具
- **ETBBS**：核心游戏库（项目引用）

## 开发指南

### 添加新视图

1. 在 `Views/` 目录创建 XAML 文件
2. 在 `ViewModels/` 目录创建对应的 ViewModel
3. 在 XAML 或代码后台绑定 DataContext

### 扩展 GameClient

`GameClient` 类通过事件实现可扩展性：
```csharp
client.MessageReceived += (sender, msg) => { /* 处理消息 */ };
client.ConnectionStatusChanged += (sender, connected) => { /* 处理状态变化 */ };
client.ErrorOccurred += (sender, error) => { /* 处理错误 */ };
```

### 自定义控件

将自定义控件添加到 `Controls/` 目录并在视图中引用：
```xaml
xmlns:controls="using:LB_FATE.AvaloniaClient.Controls"
<controls:YourControl />
```

## 故障排除

### 连接失败
- 确保服务器正在运行
- 检查防火墙设置
- 验证正确的 IP/端口
- 查看服务器日志中的错误

### 棋盘未更新
- 检查连接状态
- 确保服务器正在发送棋盘更新
- 查看日志中的解析错误

### UI 无响应
- 检查控制台中的异常
- 重启客户端
- 重新构建项目

## 许可证

与父项目（ETBBS）相同。

## 贡献指南

1. Fork 代码仓库
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

---

**使用 ❤️ 基于 Avalonia UI 和 .NET 8 构建**