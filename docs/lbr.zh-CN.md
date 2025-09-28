# 角色与技能定义（.lbr / 技能 DSL）

本文面向关卡/角色作者，介绍一个 `.lbr` 文本文件中如何定义角色，以及技能脚本 DSL 的语法要点与运行规则。

- 扩展名：`.lbr`
- 编码：UTF-8（无 BOM 推荐）
- 注释：支持 `# ...`、`// ...`、`/* ... */`
- 分隔：语句与列表项可用 `;` 或 `,` 分隔（末尾分隔可省略）

## 1. 文件结构

一个角色文件包含一个 `role` 块，内含以下可选子段：`description`（或 `desc`）、`vars`、`tags`、`skills`。

```
role "<名称>" id "<id>" {
  description "在 info 命令中显示的角色说明"; # 或 desc "..."
  vars { "<键>" = <值>; ... }
  tags { "tag1", "tag2", ... }
  skills {
    skill "<技能名>" { <技能脚本> }
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
  - `sealed_until <T>;`（新增：在第 T 回合（天）开始之前不可用，达到/超过 T 后可用）
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

- 选择器：
  - `enemies|allies [of <unit>] [in range <R> of <unit>] [with tag "<tag>"] [with var "<key>" OP <int>] [limit <N>]`
  - `units with tag "<tag>"` / `units with var "<key>" OP <int>`
  - `nearest|farthest [N] enemies|allies of <unit>`
- 条件：
  - `<unit> has tag "<tag>"`
  - `<unit> mp OP <int>`（OP: `>=, >, <=, <, ==, !=`）

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

## 7. 运行时上下文

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
    for each enemies in range 1 of target in parallel do {
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

—— 完 ——
