# 项目结构说明

## 📁 目录结构

```
ETBBS/
├── docs/                          # 文档目录
│   ├── INDEX.md                   # 文档索引
│   ├── PROJECT_OVERVIEW.md        # 项目概览
│   ├── QUICK_REFERENCE.md         # 快速参考
│   ├── MOBILE_USER_GUIDE.md       # 移动端用户指南
│   ├── DEPLOYMENT_GUIDE.md        # 部署指南
│   ├── DEADLOCK_FIX.md           # 死锁修复说明
│   └── ...                        # 其他技术文档
│
├── ETBBS/                         # 核心库项目
│   ├── Game/                      # 游戏逻辑
│   ├── DSL/                       # 领域特定语言
│   ├── Net.cs                     # 网络协议
│   └── ETBBS.csproj
│
├── LB_FATE/                       # 控制台应用
│   ├── Program.Main.cs
│   └── LB_FATE.csproj
│
├── LB_FATE.Mobile/                # MAUI 移动应用
│   ├── Views/                     # 视图
│   ├── ViewModels/                # 视图模型
│   ├── Services/                  # 服务层
│   ├── Resources/                 # 资源文件
│   ├── Platforms/                 # 平台特定代码
│   ├── DEPLOYMENT_GUIDE.md        # Windows 打包部署指南
│   ├── export_certificate.ps1    # 证书导出脚本
│   ├── LB_FATE_Certificate.pfx   # Windows 签名证书 (不提交)
│   ├── linbei.keystore           # Android 签名密钥 (谨慎管理)
│   └── LB_FATE.Mobile.csproj
│
├── ETBBS.LbrValidator/            # LBR 文件验证工具
│   └── README.md
│
├── README.md                      # 项目主文档 (英文)
├── README.zh-CN.md                # 项目主文档 (中文)
├── SECURITY.md                    # 安全说明
├── PROJECT_STRUCTURE.md           # 本文件
├── cleanup.ps1                    # 清理脚本
└── .gitignore                     # Git 忽略配置
```

## 📄 主要文件说明

### 根目录

- **README.md / README.zh-CN.md** - 项目说明文档
- **SECURITY.md** - 安全最佳实践和注意事项
- **cleanup.ps1** - 清理构建产物的脚本
- **.gitignore** - Git 忽略规则

### 文档目录 (docs/)

所有技术文档和用户指南的集中存放位置。详见 `docs/INDEX.md`。

### ETBBS/ (核心库)

游戏引擎核心逻辑，包含：
- 游戏规则实现
- DSL 解析器
- 网络协议
- 可被其他项目引用

### LB_FATE/ (控制台版本)

基于 ETBBS 库的命令行界面游戏客户端/服务器。

### LB_FATE.Mobile/ (移动应用)

跨平台 MAUI 应用，支持：
- ✅ Android
- ✅ Windows
- ⏳ iOS (需要 Mac 构建)
- ⏳ macOS (需要 Mac 构建)

**关键文件：**
- `DEPLOYMENT_GUIDE.md` - Windows MSIX 打包和分发指南
- `export_certificate.ps1` - 导出证书供用户安装
- `LB_FATE_Certificate.pfx` - Windows 自签名证书 (密码: LB_FATE_2025)

## 🔧 构建和清理

### 清理项目

```bash
# Windows PowerShell
.\cleanup.ps1

# 或手动
dotnet clean
```

### 构建项目

```bash
# 构建所有项目
dotnet build

# 构建 Windows 版本
dotnet publish LB_FATE.Mobile -f net10.0-windows10.0.19041.0 -c Release

# 构建 Android 版本
dotnet publish LB_FATE.Mobile -f net10.0-android -c Release
```

## 🔐 安全注意事项

详见 `SECURITY.md`。

关键点：
- ⚠️ 不要提交 `.pfx` 文件到公共仓库
- ⚠️ Android keystore 密码已硬编码（建议改用环境变量）
- ✅ 构建产物和证书已在 .gitignore 中忽略

## 📦 发布产物

构建后的产物位置：

**Windows:**
```
LB_FATE.Mobile/bin/Release/net10.0-windows10.0.19041.0/win-x64/AppPackages/
```

**Android:**
```
LB_FATE.Mobile/bin/Release/net10.0-android/
├── com.lbfate.mobile.apk            # 未签名
└── com.lbfate.mobile-Signed.apk     # 已签名
```

## 🚀 快速开始

1. **克隆项目**
   ```bash
   git clone <repository-url>
   cd ETBBS
   ```

2. **还原依赖**
   ```bash
   dotnet restore
   ```

3. **运行控制台版本**
   ```bash
   dotnet run --project LB_FATE
   ```

4. **运行移动应用 (Windows)**
   ```bash
   dotnet build LB_FATE.Mobile -f net10.0-windows10.0.19041.0
   dotnet run --project LB_FATE.Mobile -f net10.0-windows10.0.19041.0
   ```

## 📚 更多文档

- 游戏规则和使用方法：`README.md`
- 移动端指南：`docs/MOBILE_USER_GUIDE.md`
- 打包部署：`LB_FATE.Mobile/DEPLOYMENT_GUIDE.md`
- 技术文档索引：`docs/INDEX.md`
