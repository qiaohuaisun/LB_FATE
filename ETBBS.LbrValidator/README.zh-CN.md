以下是您提供的 `README.md` 文件内容的中文翻译：

---

# ETBBS LBR 验证器

一个用于验证 ETBBS 游戏系统中 `.lbr`（角色定义）文件语法的命令行工具。

## 概述

LBR 验证器会扫描指定目录中的 `.lbr` 文件，并使用 ETBBS 核心解析器验证其语法。它提供详细的错误报告、统计信息以及多种详细程度（verbosity）级别，帮助在运行前发现语法错误。

## 功能

- **递归扫描**：扫描子目录中的 `.lbr` 文件  
- **批量验证**：一次运行验证多个文件  
- **详细报告**：显示角色详情，包括名称、ID、技能和标签  
- **彩色输出**：使用视觉标识符（✓/✗）表示成功/失败  
- **统计信息**：汇总通过/失败数量及执行时间  
- **退出码**：支持 CI/CD 的返回码  
- **多级详细模式**：安静模式、普通模式、详细模式和详情模式

## 安装

构建验证器工具：
```bash
dotnet build ETBBS.LbrValidator
```

或发布为独立可执行文件：
```bash
dotnet publish ETBBS.LbrValidator -c Release -o publish/validator
```

## 使用方法

### 基本语法
```bash
ETBBS.LbrValidator [目录] [选项]
```

### 参数
- `目录`：要扫描 `.lbr` 文件的目录（默认为当前目录）

### 选项
- `-r, --recursive`：递归扫描子目录  
- `-v, --verbose`：显示每个文件的详细处理进度  
- `-d, --details`：显示角色详情（隐含启用 `--verbose`）  
- `-q, --quiet`：最小化输出（仅显示汇总信息）  
- `-h, --help`：显示帮助信息

### 示例

验证当前目录：
```bash
ETBBS.LbrValidator
```

验证指定目录：
```bash
ETBBS.LbrValidator roles
```

递归扫描并启用详细输出：
```bash
ETBBS.LbrValidator roles -r -v
```

显示详细角色信息：
```bash
ETBBS.LbrValidator D:\path\to\roles --details
```

安静模式（仅汇总）：
```bash
ETBBS.LbrValidator roles -q
```

## 输出示例

### 成功（详细模式）
```
═══════════════════════════════════════════════════════
  ETBBS LBR 验证器 - 角色文件语法检查器
═══════════════════════════════════════════════════════

找到 9 个 .lbr 文件待验证

正在验证: artoria.lbr ... ✓ 通过
正在验证: beast_florence.lbr ... ✓ 通过
正在验证: enkidu.lbr ... ✓ 通过
...

───────────────────────────────────────────────────────
  验证汇总
───────────────────────────────────────────────────────

总文件数:    9
通过:        9
耗时:        0.12 秒

✓ 所有文件均通过验证
```

### 失败（详细模式）
```
正在验证: broken_role.lbr ... ✗ 失败
  错误: DSL 解析错误，第 15 行：缺少关键字 'do'

───────────────────────────────────────────────────────
  验证汇总
───────────────────────────────────────────────────────

总文件数:    10
通过:        9
失败:        1
耗时:        0.15 秒

失败文件:

✗ broken_role.lbr
  DSL 解析错误，第 15 行：缺少关键字 'do'

✗ 验证失败（1 个文件存在错误）
```

### 详情模式
```
正在验证: beast_florence.lbr ... ✓ 通过
  角色: 兽之四候补 – 弗洛伦斯 (ID: beast_florence)
  技能数量: 9
  标签: beast, grand
```

## 退出码

- `0`：所有文件均通过验证  
- `1`：一个或多个文件验证失败，或发生其他错误

## 与 CI/CD 集成

验证器可集成到持续集成流水线中：

### GitHub Actions 示例
```yaml
- name: 验证 LBR 文件
  run: dotnet run --project ETBBS.LbrValidator -- roles -r -v
  continue-on-error: false
```

### Azure DevOps 示例
```yaml
- script: dotnet run --project ETBBS.LbrValidator -- $(Build.SourcesDirectory)/roles -r
  displayName: '验证角色文件'
  failOnStderr: true
```

## 常见用例

### 提交前钩子（Pre-commit Hook）
在提交前验证所有角色文件：
```bash
#!/bin/bash
dotnet run --project ETBBS.LbrValidator -- roles -q
if [ $? -ne 0 ]; then
    echo "LBR 验证失败。请修复语法错误后再提交。"
    exit 1
fi
```

### 开发期间批量验证
快速检查多个目录中的所有角色：
```bash
for dir in roles roles_custom roles_test; do
    echo "正在验证 $dir..."
    ETBBS.LbrValidator $dir -v
done
```

### 定位特定错误
使用详细模式找出哪些文件有问题：
```bash
ETBBS.LbrValidator roles -v 2>&1 | grep "FAILED"
```

## 常见错误

### 语法错误：缺少 'do' 关键字
```
错误: DSL 解析错误，第 80 行：缺少关键字 'do'
```
**原因**：`for each` 语句缺少必要子句（如 `of caster`、`in range`）  
**修复**：确保语法完整，例如：
```lbr
for each enemies of caster in range 4 of caster do { ... }
```

### 文件未找到
```
错误: 目录未找到: /path/to/roles
```
**原因**：指定的目录不存在  
**修复**：检查路径，在 Windows 上建议使用绝对路径（如 `D:\path\to\roles`）

## 技术细节

- **解析器**：使用 ETBBS 核心库中的 `LbrLoader.LoadFromFile()`  
- **文件匹配模式**：仅匹配 `*.lbr` 文件  
- **编码**：支持 UTF-8（含 BOM）  
- **性能**：在典型硬件上每文件约 50–100 毫秒

## 故障排查

**Q：验证器显示“未找到文件”**  
A：请确认当前目录正确，且存在 `.lbr` 文件。如需搜索子目录，请使用 `-r` 参数。

**Q：在 Windows 上出现“无法访问”错误**  
A：请以管理员权限运行，或检查文件/目录的访问权限。

**Q：输出中没有颜色**  
A：某些终端不支持 ANSI 颜色。请尝试更换终端，或使用 `-q` 获取纯文本输出。

## 参见

- [LBR 语法指南](../docs/LBR_SYNTAX.md)  
- [角色创建教程](../docs/ROLE_CREATION.md)  
- [ETBBS 核心文档](../ETBBS/README.md)

--- 

如需进一步本地化（如术语统一、风格调整等），请告知！# ETBBS LBR 验证器

一个用于验证 ETBBS 游戏系统中 `.lbr`（角色定义）文件语法的命令行工具。

## 概述

LBR 验证器会扫描指定目录中的 `.lbr` 文件，并使用 ETBBS 核心解析器验证其语法。它提供详细的错误报告、统计信息以及多种详细程度（verbosity）级别，帮助在运行前发现语法错误。

## 功能

- **递归扫描**：扫描子目录中的 `.lbr` 文件  
- **批量验证**：一次运行验证多个文件  
- **详细报告**：显示角色详情，包括名称、ID、技能和标签  
- **彩色输出**：使用视觉标识符（✓/✗）表示成功/失败  
- **统计信息**：汇总通过/失败数量及执行时间  
- **退出码**：支持 CI/CD 的返回码  
- **多级详细模式**：安静模式、普通模式、详细模式和详情模式

## 安装

构建验证器工具：
```bash
dotnet build ETBBS.LbrValidator
```

或发布为独立可执行文件：
```bash
dotnet publish ETBBS.LbrValidator -c Release -o publish/validator
```

## 使用方法

### 基本语法
```bash
ETBBS.LbrValidator [目录] [选项]
```

### 参数
- `目录`：要扫描 `.lbr` 文件的目录（默认为当前目录）

### 选项
- `-r, --recursive`：递归扫描子目录  
- `-v, --verbose`：显示每个文件的详细处理进度  
- `-d, --details`：显示角色详情（隐含启用 `--verbose`）  
- `-q, --quiet`：最小化输出（仅显示汇总信息）  
- `-h, --help`：显示帮助信息

### 示例

验证当前目录：
```bash
ETBBS.LbrValidator
```

验证指定目录：
```bash
ETBBS.LbrValidator roles
```

递归扫描并启用详细输出：
```bash
ETBBS.LbrValidator roles -r -v
```

显示详细角色信息：
```bash
ETBBS.LbrValidator D:\path\to\roles --details
```

安静模式（仅汇总）：
```bash
ETBBS.LbrValidator roles -q
```

## 输出示例

### 成功（详细模式）
```
═══════════════════════════════════════════════════════
  ETBBS LBR 验证器 - 角色文件语法检查器
═══════════════════════════════════════════════════════

找到 9 个 .lbr 文件待验证

正在验证: artoria.lbr ... ✓ 通过
正在验证: beast_florence.lbr ... ✓ 通过
正在验证: enkidu.lbr ... ✓ 通过
...

───────────────────────────────────────────────────────
  验证汇总
───────────────────────────────────────────────────────

总文件数:    9
通过:        9
耗时:        0.12 秒

✓ 所有文件均通过验证
```

### 失败（详细模式）
```
正在验证: broken_role.lbr ... ✗ 失败
  错误: DSL 解析错误，第 15 行：缺少关键字 'do'

───────────────────────────────────────────────────────
  验证汇总
───────────────────────────────────────────────────────

总文件数:    10
通过:        9
失败:        1
耗时:        0.15 秒

失败文件:

✗ broken_role.lbr
  DSL 解析错误，第 15 行：缺少关键字 'do'

✗ 验证失败（1 个文件存在错误）
```

### 详情模式
```
正在验证: beast_florence.lbr ... ✓ 通过
  角色: 兽之四候补 – 弗洛伦斯 (ID: beast_florence)
  技能数量: 9
  标签: beast, grand
```

## 退出码

- `0`：所有文件均通过验证  
- `1`：一个或多个文件验证失败，或发生其他错误

## 与 CI/CD 集成

验证器可集成到持续集成流水线中：

### GitHub Actions 示例
```yaml
- name: 验证 LBR 文件
  run: dotnet run --project ETBBS.LbrValidator -- roles -r -v
  continue-on-error: false
```

### Azure DevOps 示例
```yaml
- script: dotnet run --project ETBBS.LbrValidator -- $(Build.SourcesDirectory)/roles -r
  displayName: '验证角色文件'
  failOnStderr: true
```

## 常见用例

### 提交前钩子（Pre-commit Hook）
在提交前验证所有角色文件：
```bash
#!/bin/bash
dotnet run --project ETBBS.LbrValidator -- roles -q
if [ $? -ne 0 ]; then
    echo "LBR 验证失败。请修复语法错误后再提交。"
    exit 1
fi
```

### 开发期间批量验证
快速检查多个目录中的所有角色：
```bash
for dir in roles roles_custom roles_test; do
    echo "正在验证 $dir..."
    ETBBS.LbrValidator $dir -v
done
```

### 定位特定错误
使用详细模式找出哪些文件有问题：
```bash
ETBBS.LbrValidator roles -v 2>&1 | grep "FAILED"
```

## 常见错误

### 语法错误：缺少 'do' 关键字
```
错误: DSL 解析错误，第 80 行：缺少关键字 'do'
```
**原因**：`for each` 语句缺少必要子句（如 `of caster`、`in range`）  
**修复**：确保语法完整，例如：
```lbr
for each enemies of caster in range 4 of caster do { ... }
```

### 文件未找到
```
错误: 目录未找到: /path/to/roles
```
**原因**：指定的目录不存在  
**修复**：检查路径，在 Windows 上建议使用绝对路径（如 `D:\path\to\roles`）

## 技术细节

- **解析器**：使用 ETBBS 核心库中的 `LbrLoader.LoadFromFile()`  
- **文件匹配模式**：仅匹配 `*.lbr` 文件  
- **编码**：支持 UTF-8（含 BOM）  
- **性能**：在典型硬件上每文件约 50–100 毫秒

## 故障排查

**Q：验证器显示“未找到文件”**  
A：请确认当前目录正确，且存在 `.lbr` 文件。如需搜索子目录，请使用 `-r` 参数。

**Q：在 Windows 上出现“无法访问”错误**  
A：请以管理员权限运行，或检查文件/目录的访问权限。

**Q：输出中没有颜色**  
A：某些终端不支持 ANSI 颜色。请尝试更换终端，或使用 `-q` 获取纯文本输出。

## 参见

- [LBR 语法指南](../docs/LBR_SYNTAX.md)  
- [角色创建教程](../docs/ROLE_CREATION.md)  
- [ETBBS 核心文档](../ETBBS/README.md)

## 新增选项

- `--json`：以 JSON 输出结果（无标题/无颜色），便于 CI 解析
- `--lang=<en|zh-CN>`：本地化消息（默认英文）

示例：
```bash
ETBBS.LbrValidator roles --json
ETBBS.LbrValidator roles --lang=zh-CN
```

### JSON 输出示例
```json
{
  "summary": { "total": 10, "passed": 9, "failed": 1, "seconds": 0.15 },
  "results": [
    {
      "file": "broken_role.lbr",
      "path": "roles/broken_role.lbr",
      "success": false,
      "error": "DSL parse error at line 15, column 8: keyword 'do' expected\n  for each enemies of caster in range 2 {\n         ^",
      "role": null,
      "id": null,
      "skills": 0,
      "warnings": []
    }
  ]
}
```

## 语义检查

除语法错误外，验证器还会报告常见语义问题：

- 角色 `name` 或 `id` 为空（警告）
- 角色内部技能名称重复（警告）
- 不同文件中角色 ID 重复（作为错误处理）
