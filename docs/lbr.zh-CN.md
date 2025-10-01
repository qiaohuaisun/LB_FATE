# 角色与技能定义（.lbr / 技能 DSL）

本文面向关卡/角色作者，介绍一个 `.lbr` 文本文件中如何定义角色，以及技能脚本 DSL 的语法要点与运行规则。

- 扩展名：`.lbr`
- 编码：UTF-8（无 BOM 推荐）
- 注释：支持 `# ...`、`// ...`、`/* ... */`
- 分隔：语句与列表项可用 `;` 或 `,` 分隔（末尾分隔可省略）

## 1. 文件结构

一个角色文件包含一个 `role` 块，内含以下可选子段：`description`（或 `desc`）、`vars`、`tags`、`skills`、`quotes`。

```
role "<名称>" id "<id>" {
  description "在 info 命令中显示的角色说明"; # 或 desc "..."
  vars { "<键>" = <值>; ... }
  tags { "tag1", "tag2", ... }
  skills {
    skill "<技能名>" { <技能脚本> }
    ...
  }
  quotes {
    on_turn_start ["台词1", "台词2", ...]
    on_skill "<技能名>" ["台词1", "台词2", ...]
    ...
  }
}
```

- `<名称>` 与 `<id>` 任意文本；`id` 在加载到注册表时需全局唯一。
- `description/desc` 在游戏中通过 `info` 命令显示。

## 2. 变量（vars）

常用（非强制）键与类型（未列出者可按需自定义）：

- 基础与资源
  - `hp` (int) 当前生命
  - `max_hp` (int) 最大生命
  - `mp` (int | double) 当前魔力（支持 0.5 等小数）
  - `max_mp` (int | double) 最大魔力；未显式设置时会在初始化时以当前 `mp` 记录为 `max_mp`
- 攻防/法强/法抗/范围/速度
  - `atk` (int)、`def` (int)
  - `matk` (int)、`mdef` (int)
  - `resist_physical` (double 0..1) 物理减伤系数
  - `resist_magic` (double 0..1) 魔法减伤系数
  - `range` (int) 攻击/展示范围（用于 UI/提示）
  - `speed` (int | double) 行动或位移相关（示例游戏用于每回合可移动格数等）
- 回合状态与每回合效果
  - `shield_value` (double | int) 护盾值（先于 HP 扣除）
  - `undying_turns` (int) >0 时生命不会降到 0 以下
  - `mp_regen_per_turn` (int | double) 每回合恢复魔力（封顶 `max_mp`）
  - `hp_regen_per_turn` (int | double) 每回合恢复生命（封顶 `max_hp`）
  - 通用自增（新增，通用化）：`per_turn_add:<key>` (double) 每回合为变量 `<key>` 增加指定数值；可选 `per_turn_max:<key>` 或 `max_<key>` 用于上限
    - 示例：`per_turn_add:resist_magic = 0.05`（默认按 [0,1] 夹取，因为以 `resist_` 开头）；`per_turn_max:resist_magic = 0.6`
- 控制类（由系统在回合结算中衰减/移除）
  - `stunned_turns`、`silenced_turns`、`rooted_turns` (int)
  - `status_immune_turns` (int) >0 时新施加的眩晕/噤声/定身会被免疫
- 持续伤害
  - `bleed_turns`/`bleed_per_turn` (int)
  - `burn_turns`/`burn_per_turn` (int)
- 例外与被动
  - `auto_heal_below_half` (int) 首次跌破 50% 血量时自动回复的固定值
  - `auto_heal_below_half_used` (bool) 内部标记

数值在运行中统一按 int/long/double 处理，内部会做必要的统一和四舍五入。

## 3. 标签（tags）

自定义任意字符串标签；系统常用：
- `stunned`（眩晕，禁止行动）
- `silenced`（噤声，禁止施法）
- `rooted`/`frozen`（定身/冻结，禁止移动）
- `undying`、`duel` 等可拓展标签

系统在回合结算中会：
- 将 `*_turns` 每回合 -1，降到 0 时移除对应效果
- 应用 `mp_regen_per_turn`、`undying_turns` 等

## 4. 技能 DSL 概览

出现在 `skill { ... }` 块中。

- 元信息（可选，影响 UI 与校验）：
  - `cost mp <N>;`
  - `range <N>;`
  - `cooldown <N>;`
  - `sealed_until <T>;`（旧语义：按内部“回合索引”判定，0 基；一般不推荐）
  - `sealed_until day <D> [phase <P>];`（新推荐：在“第 D 天 第 P 阶段”之前不可用）
    - 天数从 1 开始；阶段 1..5；省略 phase 时表示在“第 D 天开始”解封。
  - `ends_turn;`（可选）：使用该技能后立即结束当前回合。
  - `targeting any|enemies|allies|self;`
- 行动（部分）：
  - 伤害/治疗：
    - `deal <N> damage to <unit>`（真实伤害）
    - `deal physical <N> damage to <unit> [from <unit>] [ignore defense <P>%]`
    - `deal magic <N> damage to <unit> [from <unit>] [ignore resist <P>%]`
    - `heal <N> to <unit>`
  - 位移/线性范围：
    - `move <unit> to (<x>,<y>)`
    - `dash towards <unit> up to <N>`
    - `line physical <P> to <unit> length <L> [radius <R>] [ignore defense <X>%]`
    - `line magic <P> to <unit> length <L> [radius <R>] [ignore resist <X>%]`
    - `line <P> to <unit> length <L> [radius <R>]`（真实伤害）
  - 标签/变量/资源：
    - `add tag "<tag>" to <unit>` / `remove tag "<tag>" from <unit>`
    - `set unit(<unit>) var "<key>" = <value>`
    - `set tile(<x>,<y>) var "<key>" = <value>` / `set global var "<key>" = <value>`
    - `consume mp = <number>`（默认指向施法者）
- 结构：
  - `if <cond> then <stmt> [else <stmt>]`
  - `repeat <N> times <stmt>`
  - `parallel { <stmt>; <stmt>; }`
  - `for each <selector> [in parallel] do { <stmt> }`
  - `chance <P>% then <stmt> [else <stmt>]`（新增：按概率执行 then 分支，否则执行 else 分支；P 为 0..100）
- 单位引用：`caster | target | it | unit id "<id>"`

## 5. 选择器与条件

### 选择器

**基础语法：**
- `enemies [子句...]` - 选择敌方单位
- `allies [子句...]` - 选择友方单位
- `nearest [N] enemies|allies of <unit|point>` - 选择最近的 N 个单位
- `farthest [N] enemies|allies of <unit|point>` - 选择最远的 N 个单位
- `units with tag "<tag>"` / `units with var "<key>" OP <int>` - 选择所有符合条件的单位

**可用子句**（可以**任意顺序**出现）：
- `of <unit>` - 指定参照单位（通常是 `caster` 或 `target`）。对于基于范围的选择器是**可选的**。
- `in range <R> of <unit|point>` - 筛选范围 R 内的单位
- `with tag "<tag>"` - 筛选带有特定标签的单位
- `with var "<key>" OP <value>` - 根据变量条件筛选单位
- `order by var "<key>" [asc|desc]` - 按变量排序（默认：asc 升序）
- `limit <N>` - 限制结果数量为 N 个单位

**示例：**
```lbr
# 所有子句 - 任意顺序都可以！
enemies of caster in range 4 of caster with tag "stunned" limit 2

# 与上面相同，不同顺序
enemies in range 4 of caster of caster limit 2 with tag "stunned"

# 按 HP 排序，最低的优先
enemies of caster in range 10 of caster order by var "hp" asc limit 1

# 简化写法：使用范围时可以省略 "of caster"
enemies in range 3 of target

# 最近的单位（of 子句在 enemies 后的 "of" 中隐含）
nearest 3 enemies of caster

# 范围内的所有盟友
allies in range 5 of caster
```

### 条件

- `<unit> has tag "<tag>"` - 检查单位是否有标签
- `<unit> mp OP <int>` - 比较 MP 值（OP: `>=, >, <=, <, ==, !=`）

## 6. 值与表达式（新增）

- 变量引用：`var "<key>" of <unit>`
- 简单加减表达式（新增支持）：`<primary> (('+'|'-') <primary>)*`
  - `<primary>` 为数字、字符串或 `var "<key>" of <unit>`
  - 示例：`set unit(caster) var "atk" = var "atk" of caster + 2;`
- 数值会在运行时求值并按需要转换为 int 或 double。
 - 注：更复杂的表达式（乘除/括号）当前未支持。

通用回合增量（per_turn_add）规则
- 任意 `vars` 中的 `per_turn_add:<key>` 每回合结算时会把 `<key>` 增加指定值。
- 上限优先顺序：`per_turn_max:<key>` 或 `max_<key>`；若 `<key>` 为 `hp`/`mp`，也会使用 `max_hp`/`max_mp`；以 `resist_` 开头的变量默认夹取 [0,1]。

## 7. 台词系统（quotes）

角色可以定义在特定事件发生时播放的台词，增强游戏沉浸感。每个事件类型可以定义多条台词，系统会随机选择一条播放。

### 支持的事件类型

```
quotes {
  # 回合开始时
  on_turn_start ["台词1", "台词2", ...]

  # 回合结束时
  on_turn_end ["台词1", "台词2", ...]

  # 使用特定技能时（可为每个技能单独定义）
  on_skill "<技能名>" ["台词1", "台词2", ...]

  # 受到伤害时
  on_damage ["台词1", "台词2", ...]

  # HP低于指定百分比时（0-1之间的小数）
  on_hp_below 0.5 ["台词1", "台词2", ...]
  on_hp_below 0.2 ["台词1", "台词2", ...]

  # 胜利时
  on_victory ["台词1", "台词2", ...]

  # 失败时
  on_defeat ["台词1", "台词2", ...]
}
```

### 注意事项

- HP阈值台词只会触发一次（每个阈值）
- 从高到低检查阈值，首次满足条件时触发
- 台词为可选项，不定义台词块不会影响角色功能
- 当前版本主要支持Boss角色的台词显示

### 示例

```
role "兽之王" id "boss_beast" {
  vars { "hp" = 105; "max_hp" = 105; }

  skills {
    skill "毁灭咆哮" {
      range 4; targeting enemies;
      deal magic 6 damage to target from caster;
    }
  }

  quotes {
    on_turn_start [
      "愚蠢的人类，颤抖吧！",
      "毁灭的时刻到来了..."
    ]

    on_skill "毁灭咆哮" [
      "聆听毁灭的咆哮！",
      "湮灭吧——毁灭咆哮！"
    ]

    on_hp_below 0.5 [
      "哼...还不错，但还不够！",
      "你们成功激怒我了！"
    ]

    on_hp_below 0.2 [
      "不可能...我怎么会...",
      "可恶...区区人类..."
    ]
  }
}
```

## 8. 运行时上下文

脚本执行时会自动绑定：
- `$caster`（string）：施法者 id
- `$target`（string|null）：部分指令上下文中的目标 id
- `$teams`（只读映射）：`unitId -> teamId`，用于 allies/enemies 判定

## 8. 示例（摘录）

冲锋 + 邻格溅射（当标签 `windrealm` 存在时对目标 1 格内群体伤害）：
```
skill "强袭冲锋" {
  range 3; targeting enemies; cooldown 1;
  dash towards target up to 3;
  deal physical 8 damage to target from caster;
  if caster has tag "windrealm" then {
    for each enemies of caster in range 1 of target in parallel do {
      deal physical 4 damage to it from caster
    }
  };
  consume mp = 1.0;
}
```

自我强化（加攻击 + 可能施加眩晕；展示加法表达式与注释）：
```
skill "战意迸发" {
  range 0; targeting self; cooldown 2;
  add tag "attack_up" to caster; // 攻击强化标记
  set unit(caster) var "atk" = var "atk" of caster + 2; /* 新增：支持 + */
  if caster has tag "windrealm" then {
    add tag "stunned" to target;
    set unit(target) var "stunned_turns" = 1
  };
  consume mp = 0.5;
}
```

线性范围真实/魔法伤害的组合：
```
skill "终结一击" {
  range 3; targeting enemies; cooldown 5;
  line physical 9 to target length 3 radius 0;
  line 3 to target length 3 radius 0; # 真实伤害
  if caster has tag "windrealm" then { line magic 5 to target length 3 radius 0 };
  consume mp = 2.5;
}
```

## 9. 角色加载（LB_FATE）

- 默认目录：可执行目录下的 `roles`（`AppContext.BaseDirectory/roles`）
- 环境变量：`LB_FATE_ROLES_DIR`（可递归加载）
- 启动参数：`--roles <目录>`（可递归加载）
- 示例运行：`dotnet run --project LB_FATE/LB_FATE.csproj -- --roles roles`

## 10. 常见问题（FAQ）

- 没写 `max_mp` 怎么办？
  - 初始化时会以当前 `mp` 记为 `max_mp`，并在回合开始按 `max_mp` 回满（示例规则）。
- 魔法相关属性不生效？
  - 引擎支持 `mdef` 与 `resist_magic`；请确保在 `vars` 中提供。
- 表达式精度与类型？
  - 简单加减在运行时按 double 计算；若两侧均为整数且结果为整数，最终写回为 int。
- 注释在哪里可用？
  - 现在在 LBR 各段与技能脚本中均可使用 `#`、`//`、`/*...*/` 注释。

## 11. 常见语法错误

### 错误 1：`for each` 缺少 `of` 子句

**错误示例：**
```lbr
for each enemies in range 2 of caster do { ... }
```

**错误信息：**
```
DSL parse error at X: keyword 'do' expected
```

**正确写法：**
```lbr
for each enemies of caster in range 2 of caster do { ... }
```

**说明：** `of <unit>` 子句是**必需的**，必须紧跟在 `enemies`/`allies` 后面。

### 错误 2：`order by` 必须在 `in range` 之后

**错误示例：**
```lbr
for each enemies of caster order by var "hp" desc in range 100 of caster do { ... }
```

**错误信息：**
```
DSL parse error at X: keyword 'do' expected
```

**正确写法：**
```lbr
for each enemies of caster in range 100 of caster order by var "hp" desc do { ... }
```

**说明：** 解析器要求子句顺序为：`of <unit>` → `in range` → `order by` → `limit` → `do`

### 错误 3：`in parallel` 位置错误

**正确写法：**
```lbr
for each enemies of caster in range 2 of caster in parallel do { ... }
```

**说明：** `in parallel` 修饰符必须在所有筛选条件之后、`do` 之前。

### 使用验证工具

在运行游戏前，可使用验证工具检查语法：

```bash
dotnet run --project ETBBS.LbrValidator -- roles -v
```

详见 `ETBBS.LbrValidator/README.md`。

## 9. 常见语法错误

### 错误 1：无效的单位引用

**错误写法：**
```lbr
deal physical 10 damage to enemy
```

**错误消息：**
```
DSL parse error: unknown unit reference
Suggestion: Expected one of: caster, target, it, unit id "..."
```

**正确写法：**
```lbr
deal physical 10 damage to target from caster
```

### 错误 2：选择器中的重复子句

**错误写法：**
```lbr
for each enemies of caster in range 2 of caster limit 3 limit 5 do { ... }
```

**错误消息：**
```
DSL parse error: duplicate 'limit' clause in selector
```

**正确写法：**
```lbr
for each enemies of caster in range 2 of caster limit 3 do { ... }
```

### 错误 3：`in parallel` 位置错误

**错误写法：**
```lbr
for each enemies in parallel of caster in range 2 of caster do { ... }
```

**正确写法：**
```lbr
for each enemies of caster in range 2 of caster in parallel do { ... }
```

**说明：** `in parallel` 修饰符必须在所有选择器子句之后，紧接在 `do` 之前。

### 改进提示

从 v2.0 开始，选择器子句可以**任意顺序**出现，不再强制要求固定顺序。以下两种写法都是合法的：

```lbr
# 传统顺序
for each enemies of caster in range 4 of caster with tag "stunned" limit 2 do { ... }

# 任意顺序
for each enemies limit 2 with tag "stunned" in range 4 of caster of caster do { ... }
```

这使得 DSL 更加灵活和易用。

—— 完 ——
