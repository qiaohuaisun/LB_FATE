ETBBS LBR 工具
===============

为 ETBBS 的 `.lbr` 角色文件提供 VS Code 语言支持与创作辅助。

**功能概览**
- 语言标识：识别并激活 `lbr` 语言模式
- 语法高亮：角色段落与技能 DSL 关键字
- 片段模板：`role`、`skill`、控制流与常用动作
- 补全与悬浮：顶层段落、`vars`/`tags`、技能元信息与动作，内置变量/关键字说明
- 折叠与大纲：基于花括号折叠；大纲显示 `role`/`skill`，并展示技能摘要（range/targeting/cooldown/mp）
- 诊断：括号匹配、role 头部缺失、`vars` 条目格式；工作区级重复 `role id` 检测
- 格式化：命令与“格式化文档”，按花括号层级缩进

**快速开始**
- 新建或打开 `.lbr` 文件
- 输入 `role` 回车生成角色骨架；在 `skills {}` 内输入 `skill` 生成技能骨架
- 常用片段：`if`、`foreach`、`deal`、`line`、`move`、`set`、`mp` 等
- 运行命令：
  - “ETBBS LBR: Validate File” 重新执行当前文件诊断
  - “ETBBS LBR: Create New Role” 交互创建新角色文件
  - “ETBBS LBR: Format Document” 或编辑器“格式化文档”

**命令**
- ETBBS LBR: Validate File（校验当前文件）
- ETBBS LBR: Create New Role（创建角色骨架）
- ETBBS LBR: Format Document（格式化文档）

**设置**
- `etbbs-lbr.format.indentSize`：缩进空格数（默认 2，范围 1–8）
- `etbbs-lbr.validate.workspaceDuplicates`：是否在工作区检测重复 `role id`（默认开启）

**语法要点**
- 注释以 `#` 开头，直到行尾
- 字符串使用双引号 `"..."`
- 分隔符 `;` 与 `,` 均可使用；允许末尾分隔符

**常用变量（示例）**
- hp, max_hp, mp, max_mp, atk, def, matk, mdef
- resist_physical, resist_magic, range, speed
- shield_value, undying_turns, mp_regen_per_turn
- stunned_turns, silenced_turns, rooted_turns, status_immune_turns
- bleed_turns, bleed_per_turn, burn_turns, burn_per_turn
- auto_heal_below_half, auto_heal_below_half_used

**常见标签（示例）**
- stunned, silenced, rooted, frozen, undying, duel, windrealm, charged

**扩展开发**
- 纯 JavaScript（无需打包构建）
- 入口：`extension.js`
- 语法：`syntaxes/lbr.tmLanguage.json`
- 配置：`language-configuration.json`
- 片段：`snippets/lbr.code-snippets.json`
- 命令：validate、createRole、formatDocument

