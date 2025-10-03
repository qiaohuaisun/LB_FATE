# LB_FATE.Mobile

LB_FATE 游戏的 .NET MAUI 移动客户端，支持 Android、iOS、Windows 和 macOS 平台。

## 📱 项目概述

这是一个跨平台的移动客户端，用于连接 LB_FATE 游戏服务器并进行回合制战术游戏。

### 支持平台

- ✅ Android (API 21+)
- ✅ iOS (15.0+)
- ✅ macOS (Catalyst 15.0+)
- ✅ Windows (10.0.19041.0+)

## 🏗️ 项目结构

```
LB_FATE.Mobile/
├── Services/               # 业务服务层
│   ├── NetworkService.cs           # TCP 网络连接管理
│   └── GameProtocolHandler.cs      # 游戏协议解析
│
├── ViewModels/            # 视图模型 (MVVM)
│   ├── MainViewModel.cs            # 主页面 ViewModel
│   └── GameViewModel.cs            # 游戏页面 ViewModel
│
├── Views/                 # UI 页面
│   ├── GamePage.xaml               # 游戏界面
│   └── GamePage.xaml.cs
│
├── Models/                # 数据模型
│   └── GameState.cs                # 游戏状态数据
│
├── Converters/            # 值转换器
│   ├── InvertedBoolConverter.cs
│   └── BoolToColorConverter.cs
│
├── MainPage.xaml          # 连接页面
├── AppShell.xaml          # Shell 导航配置
├── MauiProgram.cs         # 依赖注入配置
└── README.md
```

## 🚀 快速开始

### 前置要求

- .NET 8.0+ SDK 或 .NET 10.0+ SDK
- Visual Studio 2022 (Windows) 或 Visual Studio for Mac
- 对于 Android 开发：Android SDK
- 对于 iOS 开发：Xcode (仅限 macOS)

### 构建项目

```bash
# 还原依赖
dotnet restore LB_FATE.Mobile/LB_FATE.Mobile.csproj

# 构建所有平台
dotnet build LB_FATE.Mobile/LB_FATE.Mobile.csproj

# 仅构建 Android
dotnet build LB_FATE.Mobile/LB_FATE.Mobile.csproj -f net10.0-android

# 仅构建 Windows
dotnet build LB_FATE.Mobile/LB_FATE.Mobile.csproj -f net10.0-windows10.0.19041.0
```

### 运行项目

#### 在 Windows 上运行

```bash
dotnet run --project LB_FATE.Mobile --framework net10.0-windows10.0.19041.0
```

#### 在 Android 模拟器上运行

```bash
dotnet build -t:Run -f net10.0-android
```

#### 使用 Visual Studio

1. 打开 `ETBBS.sln`
2. 将 `LB_FATE.Mobile` 设置为启动项目
3. 选择目标平台（Android Emulator / Windows / iOS Simulator）
4. 按 F5 运行

## 🎮 使用说明

### 连接服务器

1. 启动应用后，进入连接页面
2. 输入服务器地址（默认: `127.0.0.1`）
3. 输入端口号（默认: `35500`）
4. 点击"连接"按钮

### 游戏界面

连接成功后，会自动跳转到游戏界面：

- **消息日志区域**：显示游戏消息和回合信息
- **快捷命令按钮**：
  - `info` - 查看单位详情
  - `help` - 显示帮助信息
  - `pass` - 结束回合
  - `退出` - 退出游戏
- **命令输入框**：输入游戏命令

### 支持的命令

```
move x y          - 移动到坐标 (x, y)
attack P#         - 攻击玩家 P# (如: attack P1)
cast <技能名>     - 使用技能
pass             - 结束回合
info             - 显示单位详情
help             - 显示帮助
```

## 🔧 技术栈

- **.NET MAUI** - 跨平台 UI 框架
- **CommunityToolkit.Mvvm** - MVVM 工具包
- **ETBBS Core** - 游戏核心库
- **TCP Socket** - 网络通信

## 📐 架构说明

### MVVM 模式

项目采用 MVVM (Model-View-ViewModel) 架构：

- **Model**: 数据模型 (`GameState`, `ServerConfig`)
- **View**: XAML 页面 (`MainPage`, `GamePage`)
- **ViewModel**: 业务逻辑 (`MainViewModel`, `GameViewModel`)

### 依赖注入

在 `MauiProgram.cs` 中配置服务和页面的依赖注入：

```csharp
// ViewModels
builder.Services.AddSingleton<MainViewModel>();
builder.Services.AddTransient<GameViewModel>();

// Pages
builder.Services.AddSingleton<MainPage>();
builder.Services.AddTransient<GamePage>();
```

### 导航

使用 Shell 导航在页面间切换：

```csharp
// 跳转到游戏页面
await Shell.Current.GoToAsync("//GamePage");

// 返回主页面
await Shell.Current.GoToAsync("//MainPage");
```

## 🛠️ 扩展开发

### 添加新功能

1. **添加新服务**: 在 `Services/` 文件夹创建服务类
2. **添加新页面**:
   - 在 `Views/` 创建 XAML 页面
   - 在 `ViewModels/` 创建对应的 ViewModel
   - 在 `MauiProgram.cs` 注册依赖
   - 在 `AppShell.xaml` 添加路由

### 自定义控件

计划添加的控件：

- [ ] `GridBoardView` - 2D 网格渲染控件（显示游戏地图）
- [ ] `UnitInfoView` - 单位信息卡片
- [ ] `SkillListView` - 技能列表控件

## 📝 待办事项

- [ ] 实现 2D 网格可视化渲染
- [ ] 添加触摸交互（点击移动/攻击）
- [ ] 实现断线重连机制
- [ ] 添加音效和动画
- [ ] 优化移动端 UI 布局
- [ ] 支持横屏/竖屏自适应
- [ ] 添加游戏设置页面
- [ ] 实现本地 AI 对战模式

## 🐛 已知问题

- 当前仅支持文本命令输入
- 网格渲染尚未实现
- 断线重连功能待完善

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE.txt](../LICENSE.txt)

---

**开发者**: 与 Claude Code 协作开发
**项目地址**: [GitHub](https://github.com/qiaohuaisun/LB_FATE)
