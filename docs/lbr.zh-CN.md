# LBR 角色与技能定义语言 - 完整指南

**语言**: 中文 | [English](lbr.en.md)

LBR (Little Battle Role) 是ETBBS的领域特定语言(DSL)，用于定义回合制战斗中的角色和技能。

---

## 📚 目录

1. [快速入门](#1-快速入门)
2. [文件结构](#2-文件结构)
3. [角色属性](#3-角色属性)
4. [技能元信息](#4-技能元信息)
5. [表达式系统](#5-表达式系统)
6. [动作系统](#6-动作系统)
7. [选择器系统](#7-选择器系统)
8. [控制流](#8-控制流)
9. [条件系统](#9-条件系统)
10. [高级特性](#10-高级特性)
11. [台词系统](#11-台词系统)
12. [完整示例](#12-完整示例)
13. [最佳实践](#13-最佳实践)
14. [常见错误](#14-常见错误)
15. [工具和调试](#15-工具和调试)

---

## 1. 快速入门

### 基本信息

- **文件扩展名**: `.lbr`
- **编码**: UTF-8 (推荐无 BOM)
- **注释**: 支持 `#`、`//`、`/* ... */`
- **分隔符**: 语句和列表项使用 `;` 或 `,` 分隔（末尾分隔可省略）

### 5分钟示例

创建一个简单的战士角色：

```lbr
role "勇敢的战士" id "warrior" {
  description "一位勇敢的剑士";

  vars {
    "hp" = 100; "max_hp" = 100;
    "mp" = 5.0; "max_mp" = 5.0;
    "atk" = 12; "def" = 8;
    "range" = 2; "speed" = 3;
  }

  tags { "melee", "physical" }

  skills {
    skill "重斩" {
      range 2;
      targeting enemies;
      cost mp 1;
      cooldown 1;

      deal physical 15 damage to target from caster;
    }

    skill "回复术" {
      range 0;
      targeting self;
      cost mp 2;
      cooldown 3;

      heal 30 to caster;
    }
  }
}
```

**就是这样！** 你已经创建了一个完整的角色。

---

## 2. 文件结构

### 角色定义

一个 `.lbr` 文件包含一个 `role` 块：

```lbr
role "<角色名称>" id "<唯一ID>" {
  description "<角色描述>";  # 或使用 desc

  vars {
    # 角色属性
  }

  tags {
    # 角色标签
  }

  skills {
    # 技能定义
  }

  quotes {
    # 台词（可选）
  }
}
```

### 字段说明

| 字段 | 必需 | 说明 |
|------|------|------|
| `role "<name>"` | ✅ | 角色显示名称 |
| `id "<id>"` | ✅ | 唯一标识符，全局唯一 |
| `description` | ❌ | 角色描述，游戏中显示 |
| `vars` | ❌ | 角色属性和变量 |
| `tags` | ❌ | 角色标签集合 |
| `skills` | ❌ | 技能列表 |
| `quotes` | ❌ | 台词和对话 |

---

## 3. 角色属性

### 3.1 基础属性

```lbr
vars {
  # 生命和魔力
  "hp" = 100;          # 当前生命值
  "max_hp" = 100;      # 最大生命值
  "mp" = 5.0;          # 当前魔力值（支持小数）
  "max_mp" = 5.0;      # 最大魔力值

  # 攻击和防御
  "atk" = 12;          # 物理攻击力
  "def" = 8;           # 物理防御力
  "matk" = 10;         # 魔法攻击力
  "mdef" = 6;          # 魔法防御力

  # 其他属性
  "range" = 2;         # 攻击范围
  "speed" = 3;         # 移动速度
}
```

### 3.2 减伤系数

```lbr
vars {
  "resist_physical" = 0.2;  # 物理减伤 20%
  "resist_magic" = 0.15;    # 魔法减伤 15%
}
```

**范围**: 0.0 ~ 1.0 (0% ~ 100%)

### 3.3 护盾和不死

```lbr
vars {
  "shield_value" = 50;      # 护盾值（先于HP扣除）
  "undying_turns" = 2;      # 不死回合数（HP不会降到0以下）
}
```

### 3.4 每回合效果

```lbr
vars {
  # 每回合恢复
  "hp_regen_per_turn" = 5;     # 每回合回复5点HP
  "mp_regen_per_turn" = 1.0;   # 每回合回复1点MP

  # 持续伤害
  "bleed_turns" = 3;           # 流血持续3回合
  "bleed_per_turn" = 4;        # 每回合流血伤害
  "burn_turns" = 2;            # 燃烧持续2回合
  "burn_per_turn" = 6;         # 每回合燃烧伤害
}
```

### 3.5 通用回合增量（高级）

```lbr
vars {
  # 为任意属性添加每回合增长
  "per_turn_add:atk" = 2;           # 每回合攻击力+2
  "per_turn_max:atk" = 30;          # 攻击力上限30

  "per_turn_add:resist_magic" = 0.05;  # 每回合魔抗+5%
  "per_turn_max:resist_magic" = 0.6;   # 魔抗上限60%
}
```

**规则**:
- `resist_*` 变量自动限制在 [0, 1]
- 优先使用 `per_turn_max:<key>`，否则使用 `max_<key>`
- HP/MP 使用 `max_hp`/`max_mp` 作为上限

### 3.6 状态控制

```lbr
vars {
  # 负面状态持续时间
  "stunned_turns" = 2;      # 眩晕2回合（无法行动）
  "silenced_turns" = 1;     # 沉默1回合（无法施法）
  "rooted_turns" = 3;       # 定身3回合（无法移动）

  # 状态免疫
  "status_immune_turns" = 2;  # 状态免疫2回合
}
```

**系统行为**: 每回合结束时，`*_turns` 自动 -1，降到 0 时效果消失。

### 3.7 特殊被动

```lbr
vars {
  # 低血量自动治疗（仅触发一次）
  "auto_heal_below_half" = 40;        # 首次低于50%血量时回复40点
  "auto_heal_below_half_used" = false; # 内部标记
}
```

---

## 4. 技能元信息

### 4.1 基本元信息

```lbr
skill "技能名称" {
  # 必需/常用信息
  range 5;                  # 施法范围
  targeting enemies;        # 目标类型

  # 可选信息
  cost mp 2;                # MP消耗
  cooldown 3;               # 冷却回合数
  min_range 2;              # 最小施法距离

  # 技能主体...
}
```

### 4.2 目标类型

| 类型 | 说明 | 示例 |
|------|------|------|
| `enemies` | 敌方单位 | 攻击技能 |
| `allies` | 友方单位 | 治疗技能 |
| `self` | 自己 | 增益技能 |
| `any` | 任意单位 | 通用技能 |
| `tile` | 地面格子 | 陷阱、AOE |
| `point` | 坐标点 | 传送、召唤 |

### 4.3 距离度量

```lbr
skill "技能" {
  range 5;
  distance manhattan;      # 曼哈顿距离（十字形）
  # distance chebyshev;    # 切比雪夫距离（方形）
  # distance euclidean;    # 欧几里得距离（圆形）

  # ...
}
```

**距离类型**:

| 类型 | 计算公式 | 形状 | 用途 |
|------|---------|------|------|
| `manhattan` | \|x1-x2\| + \|y1-y2\| | 十字/菱形 | 传统战棋 |
| `chebyshev` | max(\|x1-x2\|, \|y1-y2\|) | 正方形 | 国际象棋王移动 |
| `euclidean` | √((x1-x2)² + (y1-y2)²) | 圆形 | 真实距离 |

### 4.4 解锁条件

```lbr
skill "终极技" {
  # 旧语法（不推荐）
  sealed_until 10;          # 在全局回合10之前不可用

  # 新语法（推荐）
  sealed_until day 3 phase 2;  # 第3天第2阶段解锁
  sealed_until day 5;          # 第5天开始解锁

  # ...
}
```

**说明**:
- 天数从 1 开始
- 阶段范围: 1 ~ 5
- 省略 `phase` 表示该天开始解锁

### 4.5 特殊标记

```lbr
skill "爆发" {
  ends_turn;    # 使用后立即结束回合

  # ...
}
```

---

## 5. 表达式系统

### 5.1 基本表达式

```lbr
# 字面值
10                    # 整数
3.5                   # 小数
"key"                 # 字符串

# 变量引用
var "hp" of caster    # 读取施法者的HP
var "atk" of target   # 读取目标的攻击力
var "counter" of global  # 读取全局变量
```

### 5.2 算术运算

```lbr
# 加减乘除模
var "atk" of caster + 5
var "hp" of target - 10
var "damage" of global * 2
var "mp" of caster / 2
var "turn" of global % 3

# 复杂表达式（支持括号）
(var "atk" of caster + var "matk" of caster) * 2
var "max_hp" of target - (var "hp" of target / 2)
```

**运算符优先级**:
1. `*` `/` `%` (乘除模)
2. `+` `-` (加减)
3. `()` 括号最高

### 5.3 内置函数

```lbr
# min(a, b) - 返回最小值
heal min(50, var "max_hp" of target - var "hp" of target) to target

# max(a, b) - 返回最大值
set global var "damage" = max(0, var "atk" of caster - var "def" of target)

# abs(x) - 绝对值
set global var "distance" = abs(var "x" of caster - var "x" of target)

# floor(x) - 向下取整
set global var "half_mp" = floor(var "mp" of caster / 2)

# ceil(x) - 向上取整
set global var "damage" = ceil(var "atk" of caster * 1.5)

# round(x) - 四舍五入
set global var "percent_hp" = round(var "hp" of caster * 0.3)
```

### 5.4 比较运算符

用于条件判断：

| 运算符 | 说明 | 示例 |
|--------|------|------|
| `==` | 等于 | `var "combo" of caster == 3` |
| `!=` | 不等于 | `var "hp" of target != 0` |
| `<` | 小于 | `var "hp" of caster < 50` |
| `<=` | 小于等于 | `var "mp" of caster <= 2` |
| `>` | 大于 | `var "atk" of caster > 10` |
| `>=` | 大于等于 | `caster mp >= 5` |

---

## 6. 动作系统

### 6.1 伤害动作

#### 真实伤害

```lbr
deal 20 damage to target
deal var "damage" of global damage to it
```

#### 物理伤害

```lbr
# 基础物理伤害
deal physical 15 damage to target from caster

# 忽略部分防御
deal physical 20 damage to target from caster ignore defense 50%
```

**计算公式**:
```
实际伤害 = 基础伤害 * (1 - def_reduction) * (1 - resist_physical)
其中: def_reduction = def / (def + 100) * (1 - ignore%)
```

#### 魔法伤害

```lbr
# 基础魔法伤害
deal magic 25 damage to target from caster

# 忽略部分魔抗
deal magic 30 damage to target from caster ignore resist 30%
```

**计算公式**:
```
实际伤害 = 基础伤害 * (1 - mdef_reduction) * (1 - resist_magic) * (1 - ignore%)
```

### 6.2 治疗动作

```lbr
# 固定治疗
heal 30 to target

# 动态治疗（表达式）
heal var "heal_amount" of global to caster

# 治疗到满（使用min函数）
heal min(50, var "max_hp" of target - var "hp" of target) to target
```

### 6.3 AOE 线性伤害

```lbr
# 物理线性AOE
line physical aoe to target from caster damage 15 range 5 width 1

# 魔法线性AOE
line magic aoe to target from caster damage 20 range 6 width 2

# 真实伤害线性AOE
line true aoe to target damage 25 range 4 width 1
```

**参数说明**:
- `damage`: 伤害值
- `range` / `length`: 线的长度
- `width` / `radius`: 线的宽度（半径）

### 6.4 移动动作

#### 精确移动

```lbr
move target to (5, 8)
move caster to (var "x" of global, var "y" of global)
```

#### 冲刺

```lbr
# 向目标冲刺最多3格
dash towards target up to 3

# 向特定单位冲刺
dash towards unit id "boss" up to 5
```

#### 击退

```lbr
# 将目标从施法者位置击退2格
knockback target 2

# 从特定单位位置击退目标
knockback target 3 from unit id "boss"
```

**说明**:
- 击退方向由源单位指向目标单位计算
- 如果路径上有其他单位阻挡，提前停止
- 击退距离为实际移动的格数

#### 拉取

```lbr
# 将目标拉向施法者2格
pull target 2

# 将目标拉向特定单位
pull target 3 towards unit id "boss"
```

**说明**:
- 拉取方向由目标单位指向源单位计算
- 如果路径上有其他单位阻挡，提前停止
- 拉取距离为实际移动的格数

### 6.5 标签操作

#### 单位标签

```lbr
# 添加标签
add tag "stunned" to target
add tag "blessed" to caster

# 移除标签
remove tag "invisible" from caster
remove tag "marked" from target
```

#### 全局标签

```lbr
# 添加全局标签
add global tag "battle_started"
add global tag "boss_phase_2"

# 移除全局标签
remove global tag "preparation_phase"
```

#### 地图格子标签

```lbr
# 添加地面标签
add tile tag "burning" at (5, 5)
add tile tag "blessed_ground" at (3, 7)

# 移除地面标签
remove tile tag "water" at (3, 3)
```

### 6.6 变量操作

#### 单位变量

```lbr
# 设置变量
set unit(caster) var "atk" = var "atk" of caster + 5
set unit(target) var "combo" = 0
set unit(it) var "marked_by" = var "id" of caster

# 移除变量
remove unit var "temp_buff" from caster
```

#### 全局变量

```lbr
# 设置全局变量
set global var "turn_count" = var "turn_count" of global + 1
set global var "damage" = var "atk" of caster * 2

# 移除全局变量
remove global var "cached_value"
```

#### 地图格子变量

```lbr
# 设置格子变量
set tile var "damage" = 10 at (5, 5)
set tile var "heal_bonus" = 5 at (3, 3)

# 移除格子变量
remove tile var "marker" at (7, 7)
```

### 6.7 资源消耗

```lbr
# 消耗MP（默认消耗施法者的MP）
consume mp = 2.5
consume mp = var "cost" of global
```

---

## 7. 选择器系统

选择器是 LBR 最强大的特性之一，用于选择符合特定条件的单位集合。

### 7.1 基础选择器

```lbr
enemies                   # 所有敌人
allies                    # 所有盟友
units                     # 所有单位
enemies of target         # 目标的敌人
allies of caster          # 施法者的盟友
```

### 7.2 范围选择器

```lbr
# 完整语法
enemies in range 5 of caster
allies in range 3 of target
units in range 4 of point

# 简写语法
enemies within 5          # 等同于 enemies in range 5 of caster
allies around 3           # 等同于 allies in range 3 of caster
```

### 7.3 智能选择器

#### Random (随机)

```lbr
random 2 enemies           # 随机选择2个敌人
random 3 allies            # 随机选择3个盟友
random 1 enemies of caster # 随机选择1个敌人
```

#### Nearest/Farthest (最近/最远)

```lbr
nearest 3 enemies of caster      # 最近的3个敌人
nearest enemy of caster          # 最近的1个敌人
farthest 2 allies of target      # 最远的2个盟友
farthest ally of point           # 离点最远的盟友
```

#### Healthiest/Weakest (血量最高/最低)

```lbr
healthiest allies                # 血量最高的盟友
healthiest 2 enemies             # 血量最高的2个敌人
weakest 3 enemies                # 血量最低的3个敌人
weakest ally                     # 血量最低的盟友
```

### 7.4 几何形状选择器

#### Circle (圆形)

```lbr
# 以施法者为中心，欧几里得距离5范围内的敌人
enemies in circle 5 of caster

# 以目标为中心，半径3的圆形
allies in circle 3 of target
```

#### Cross (十字形)

```lbr
# 以施法者为中心的十字形（仅正交方向）
allies in cross 3 of caster

# 以点为中心的十字形
enemies in cross 4 of point
```

#### Line (直线)

```lbr
# 从施法者向上延伸的直线
enemies in line length 8 width 1 of caster dir "up"

# 支持的方向: "up", "down", "left", "right"
units in line length 6 width 2 of caster dir "right"
```

#### Cone (扇形/锥形)

```lbr
# 半径6，角度90度的扇形
units in cone radius 6 angle 90 of caster dir "right"

# 半径4，角度60度的扇形
enemies in cone radius 4 angle 60 of caster dir "up"
```

### 7.5 选择器子句

选择器子句可以**任意顺序**组合：

```lbr
# 所有子句
enemies
  of caster                    # 以谁为参照
  in range 5 of caster         # 范围过滤
  with tag "stunned"           # 标签过滤
  with var "hp" < 50           # 变量过滤
  order by var "hp" asc        # 排序
  limit 3                      # 限制数量

# 顺序可以任意调整（都是合法的）
enemies in range 5 of caster with tag "stunned" of caster limit 3
enemies limit 3 in range 5 of caster of caster with tag "stunned"
enemies with tag "stunned" limit 3 of caster in range 5 of caster
```

#### of 子句

```lbr
enemies of caster         # 施法者的敌人
allies of target          # 目标的盟友
enemies of it             # 当前迭代单位的敌人
```

#### in range 子句

```lbr
enemies in range 4 of caster      # 施法者4格范围内的敌人
allies in range 3 of target       # 目标3格范围内的盟友
units in range 5 of point         # 点5格范围内的所有单位
```

#### with tag 子句

```lbr
enemies with tag "stunned"        # 所有眩晕的敌人
allies with tag "blessed"         # 所有被祝福的盟友
units with tag "marked"           # 所有被标记的单位
```

#### with var 子句

```lbr
enemies with var "hp" < 50                    # HP低于50的敌人
allies with var "mp" >= 3                     # MP大于等于3的盟友
units with var "combo" == 3                   # 连击数为3的单位
enemies with var "atk" > var "def" of caster  # 攻击力高于施法者防御的敌人
```

#### order by 子句

```lbr
# 按HP升序（从低到高）
enemies order by var "hp" asc

# 按攻击力降序（从高到低）
allies order by var "atk" desc

# 按距离排序（需要先计算距离变量）
units order by var "distance" asc
```

#### limit 子句

```lbr
enemies limit 3           # 最多3个敌人
allies limit 1            # 最多1个盟友
units limit 5             # 最多5个单位
```

### 7.6 选择器组合示例

```lbr
# 示例1: 选择最弱的3个受伤敌人
for each enemies
  of caster
  in range 10 of caster
  with var "hp" < var "max_hp" of it
  order by var "hp" asc
  limit 3
do {
  deal magic 20 damage to it from caster
}

# 示例2: 选择最近的被眩晕盟友治疗
for each allies
  of caster
  in range 6 of caster
  with tag "stunned"
  limit 1
do {
  heal 30 to it;
  remove tag "stunned" from it
}

# 示例3: 选择圆形范围内血量低于30%的敌人
for each enemies
  in circle 5 of caster
  with var "hp" < (var "max_hp" of it * 0.3)
  limit 5
do {
  deal physical 15 damage to it from caster
}
```

---

## 8. 控制流

### 8.1 条件语句 (if-else)

```lbr
# 简单条件
if caster hp < 50 then {
  heal 20 to caster
}

# 带else分支
if target has tag "shielded" then {
  deal 10 damage to target
} else {
  deal 25 damage to target
}

# 嵌套条件
if caster mp >= 5 then {
  if target hp < 30 then {
    deal magic 40 damage to target from caster
  } else {
    deal magic 20 damage to target from caster
  }
}
```

### 8.2 概率分支 (chance)

```lbr
# 30%概率触发
chance 30% then {
  deal physical 40 damage to target from caster;
  add tag "critical" to caster
} else {
  deal physical 15 damage to target from caster
}

# 50%概率
chance 50% then {
  heal 25 to caster
}

# 可以嵌套
chance 60% then {
  chance 50% then {
    deal 50 damage to target
  } else {
    deal 25 damage to target
  }
}
```

### 8.3 循环 (for each)

#### 基本循环

```lbr
# 遍历所有敌人
for each enemies of caster in range 5 of caster do {
  deal physical 10 damage to it from caster
}

# 遍历所有盟友
for each allies of caster in range 3 of caster do {
  heal 15 to it
}
```

#### 并行循环

```lbr
# 并行执行（所有动作同时发生）
for each enemies of caster in range 4 of caster in parallel do {
  deal magic 12 damage to it from caster;
  add tag "marked" to it
}
```

**区别**:
- 普通循环：动作按顺序执行，每个单位处理完才处理下一个
- 并行循环：所有动作同时执行

#### 嵌套循环

```lbr
# 对每个敌人，治疗其周围的所有盟友
for each enemies of caster in range 10 of caster do {
  for each allies of caster in range 2 of it do {
    heal 10 to it
  }
}
```

#### 使用 it 引用

在循环中，`it` 代表当前迭代的单位：

```lbr
for each enemies within 5 do {
  # it 是当前敌人
  if it hp < 30 then {
    deal physical 20 damage to it from caster
  } else {
    deal physical 10 damage to it from caster
  }
}
```

### 8.4 重复 (repeat)

```lbr
# 重复3次
repeat 3 times {
  deal magic 5 damage to target from caster
}

# 重复N次（变量）
repeat var "combo" of caster times {
  deal physical 8 damage to target from caster
}

# 可以嵌套
repeat 2 times {
  repeat 3 times {
    deal 5 damage to target
  }
}
```

### 8.5 并行块 (parallel)

```lbr
# 同时执行多个动作
parallel {
  { deal 10 damage to target }
  { heal 5 to caster }
  { add tag "marked" to target }
}

# 更复杂的并行块
parallel {
  {
    for each enemies within 3 do {
      deal 8 damage to it
    }
  }
  {
    for each allies within 3 do {
      heal 6 to it
    }
  }
}
```

---

## 9. 条件系统

### 9.1 标签检查

```lbr
# 检查单位是否有标签
if caster has tag "berserk" then { ... }
if target has tag "stunned" then { ... }
if it has tag "marked" then { ... }
```

### 9.2 属性比较

```lbr
# MP比较（快捷语法）
if caster mp >= 5 then { ... }
if target mp < 2 then { ... }

# HP比较（快捷语法）
if caster hp < 30 then { ... }
if target hp <= 50 then { ... }
```

### 9.3 变量比较

```lbr
# 数值比较
if caster var "combo" == 3 then { ... }
if target var "shield" > 0 then { ... }
if it var "atk" >= var "def" of caster then { ... }

# 复杂条件
if caster var "hp" < (var "max_hp" of caster / 2) then {
  # HP低于50%
  heal 40 to caster
}
```

### 9.4 条件组合技巧

虽然DSL不直接支持 `and`/`or`，但可以使用嵌套实现：

```lbr
# AND 逻辑（嵌套if）
if caster mp >= 3 then {
  if target hp < 50 then {
    # MP >= 3 AND HP < 50
    deal magic 30 damage to target from caster
  }
}

# OR 逻辑（使用选择器）
for each enemies
  of caster
  in range 5 of caster
  with var "hp" < 20
do {
  # 对所有HP<20的敌人执行
  deal 40 damage to it
}

# 也可以OR（重复动作，但检查不同条件）
if caster has tag "blessed" then {
  heal 30 to caster
}
if caster hp < 20 then {
  heal 30 to caster
}
```

---

## 10. 高级特性

### 10.1 距离度量系统

不同的距离计算方式影响技能的范围形状：

```lbr
skill "十字攻击" {
  range 5;
  distance manhattan;    # 十字/菱形范围
  targeting enemies;

  deal physical 15 damage to target from caster;
}

skill "方形攻击" {
  range 3;
  distance chebyshev;    # 正方形范围
  targeting enemies;

  deal physical 12 damage to target from caster;
}

skill "圆形爆破" {
  range 4;
  distance euclidean;    # 圆形范围
  targeting enemies;

  deal magic 18 damage to target from caster;
}
```

**可视化对比** (range=3):

```
Manhattan (菱形):      Chebyshev (方形):     Euclidean (圆形):
      ·                    · · · · ·                ·
    · · ·                  · · · · ·              · · ·
  · · X · ·                · · X · ·            · · X · ·
    · · ·                  · · · · ·              · · ·
      ·                    · · · · ·                ·
```

### 10.2 完整示例：连击系统

```lbr
skill "连击斩" {
  range 2;
  targeting enemies;
  cost mp 1;
  cooldown 1;

  # 检查连击数
  if caster var "combo" >= 3 then {
    # 连击爆发
    deal physical (var "combo" of caster * 10) damage to target from caster;

    # 范围伤害
    for each enemies of caster in range 2 of target do {
      deal physical (var "combo" of caster * 5) damage to it from caster
    };

    # 重置连击
    set unit(caster) var "combo" = 0
  } else {
    # 累积连击
    deal physical 15 damage to target from caster;
    set unit(caster) var "combo" = var "combo" of caster + 1
  }
}
```

### 10.3 完整示例：光环技能

```lbr
skill "战争光环" {
  range 0;
  targeting self;
  cost mp 3;
  cooldown 5;

  # 为自己添加光环标记
  add tag "war_aura" to caster;
  set unit(caster) var "aura_turns" = 3;

  # 增强周围所有盟友
  for each allies of caster in range 3 of caster do {
    set unit(it) var "atk" = var "atk" of it + 5;
    set unit(it) var "def" = var "def" of it + 3;
    add tag "buffed" to it
  }
}
```

### 10.4 完整示例：位置交换

```lbr
skill "闪现交换" {
  range 5;
  targeting allies;
  cost mp 2;
  cooldown 3;

  # 保存坐标
  set global var "temp_x" = var "x" of caster;
  set global var "temp_y" = var "y" of caster;

  # 施法者移动到目标位置
  move caster to (var "x" of target, var "y" of target);

  # 目标移动到施法者原位置
  move target to (var "temp_x" of global, var "temp_y" of global)
}
```

### 10.5 完整示例：传染效果

```lbr
skill "疫病传播" {
  range 4;
  targeting enemies;
  cost mp 2;
  cooldown 2;

  # 对主目标造成伤害并施加疫病
  deal magic 10 damage to target from caster;
  add tag "diseased" to target;
  set unit(target) var "disease_turns" = 3;

  # 传染给周围敌人
  for each enemies of caster in range 2 of target do {
    # 50%概率传染
    chance 50% then {
      add tag "diseased" to it;
      set unit(it) var "disease_turns" = 2;
      deal magic 5 damage to it from caster
    }
  }
}
```

---

## 11. 台词系统

台词系统为角色添加语音和对话，增强游戏沉浸感。

### 11.1 事件类型

```lbr
quotes {
  # 回合开始
  on_turn_start ["台词1", "台词2", ...]

  # 回合结束
  on_turn_end ["台词1", "台词2", ...]

  # 使用技能（可为每个技能单独定义）
  on_skill "技能名" ["台词1", "台词2", ...]

  # 受到伤害
  on_damage ["台词1", "台词2", ...]

  # HP阈值（0-1之间的小数）
  on_hp_below 0.5 ["台词1", "台词2", ...]
  on_hp_below 0.2 ["台词1", "台词2", ...]

  # 胜利
  on_victory ["台词1", "台词2", ...]

  # 失败
  on_defeat ["台词1", "台词2", ...]
}
```

### 11.2 完整示例

```lbr
role "龙王" id "dragon_king" {
  vars {
    "hp" = 200; "max_hp" = 200;
    "mp" = 10.0; "max_mp" = 10.0;
    "atk" = 25; "matk" = 30;
  }

  tags { "boss", "dragon", "grand" }

  skills {
    skill "龙息吐息" {
      range 6; targeting enemies;
      cost mp 3; cooldown 2;

      deal magic 35 damage to target from caster;
      for each enemies in cone radius 5 angle 60 of caster dir "up" do {
        deal magic 20 damage to it from caster
      }
    }

    skill "龙之怒吼" {
      range 0; targeting self;
      cost mp 5; cooldown 5;

      for each enemies in circle 8 of caster do {
        deal magic 25 damage to it from caster;
        chance 40% then {
          add tag "stunned" to it;
          set unit(it) var "stunned_turns" = 1
        }
      }
    }
  }

  quotes {
    on_turn_start [
      "渺小的人类，感受龙之威压！",
      "颤抖吧，末日将至！",
      "你们的末日已经到来！"
    ]

    on_skill "龙息吐息" [
      "燃烧殆尽吧——龙息吐息！",
      "化为灰烬！",
      "尝尝这毁灭之炎！"
    ]

    on_skill "龙之怒吼" [
      "聆听毁灭的咆哮！",
      "龙之怒吼——震颤吧！",
      "无人能在我的怒吼中幸存！"
    ]

    on_hp_below 0.7 [
      "有趣...你们还挺强的。",
      "不错，已经很久没人伤到我了。"
    ]

    on_hp_below 0.4 [
      "可恶...竟敢伤我至此！",
      "你们...成功激怒我了！",
      "准备迎接真正的愤怒吧！"
    ]

    on_hp_below 0.15 [
      "这...不可能...",
      "区区人类...怎么会...",
      "我可是...龙王..."
    ]

    on_damage [
      "区区蚂蚁！",
      "这点伤害？",
      "毫无意义的抵抗！"
    ]

    on_victory [
      "就这？太弱了。",
      "人类，不过如此。",
      "下一个挑战者在哪里？"
    ]

    on_defeat [
      "我...竟然会输...",
      "记住...这个耻辱...",
      "总有一天...我会回来的..."
    ]
  }
}
```

### 11.3 台词规则

1. **随机选择**: 每个事件触发时，从该事件的台词列表中随机选择一条
2. **HP阈值**: 每个阈值只触发一次，从高到低检查
3. **技能台词**: 每个技能可以有独立的台词
4. **可选性**: 台词块完全可选，不影响角色功能

---

## 12. 完整示例

### 12.1 近战战士

```lbr
role "圣骑士" id "paladin" {
  description "神圣的战士，保护弱小，惩戒邪恶";

  vars {
    "hp" = 120; "max_hp" = 120;
    "mp" = 6.0; "max_mp" = 6.0;
    "atk" = 14; "def" = 12;
    "matk" = 8; "mdef" = 10;
    "range" = 2; "speed" = 3;
    "resist_physical" = 0.1;
  }

  tags { "melee", "holy", "tank" }

  skills {
    skill "圣光斩" {
      range 2;
      targeting enemies;
      cost mp 1;
      cooldown 1;

      deal physical 18 damage to target from caster;

      # 对不死生物额外伤害
      if target has tag "undead" then {
        deal magic 10 damage to target from caster
      }
    }

    skill "保护之盾" {
      range 0;
      targeting self;
      cost mp 2;
      cooldown 3;

      # 获得护盾
      set unit(caster) var "shield_value" = 30;
      add tag "shielded" to caster;

      # 治疗自己
      heal 20 to caster
    }

    skill "神圣冲锋" {
      range 4;
      targeting enemies;
      cost mp 3;
      cooldown 4;

      # 冲向敌人
      dash towards target up to 4;

      # 造成伤害
      deal physical 25 damage to target from caster;

      # AOE眩晕周围敌人
      for each enemies of caster in range 1 of target do {
        chance 50% then {
          add tag "stunned" to it;
          set unit(it) var "stunned_turns" = 1
        }
      }
    }

    skill "治疗光环" {
      range 0;
      targeting self;
      cost mp 4;
      cooldown 5;

      # 治疗周围所有盟友
      for each allies of caster in circle 4 of caster do {
        heal 25 to it;

        # 移除负面状态
        if it has tag "poisoned" then {
          remove tag "poisoned" from it
        }
      }
    }
  }
}
```

### 12.2 远程法师

```lbr
role "元素法师" id "elementalist" {
  description "掌握冰火雷电的强大法师";

  vars {
    "hp" = 80; "max_hp" = 80;
    "mp" = 10.0; "max_mp" = 10.0;
    "atk" = 6; "def" = 4;
    "matk" = 20; "mdef" = 8;
    "range" = 6; "speed" = 2;
    "resist_magic" = 0.15;
    "mp_regen_per_turn" = 1.5;
  }

  tags { "ranged", "mage", "elemental" }

  skills {
    skill "火球术" {
      range 6;
      targeting enemies;
      distance euclidean;
      cost mp 2;
      cooldown 1;

      deal magic 22 damage to target from caster;

      # 燃烧效果
      chance 40% then {
        add tag "burning" to target;
        set unit(target) var "burn_turns" = 2;
        set unit(target) var "burn_per_turn" = 5
      }
    }

    skill "冰冻新星" {
      range 0;
      targeting self;
      cost mp 4;
      cooldown 3;

      # 冻结周围所有敌人
      for each enemies in circle 3 of caster do {
        deal magic 15 damage to it from caster;
        add tag "frozen" to it;
        set unit(it) var "rooted_turns" = 2
      }
    }

    skill "连锁闪电" {
      range 5;
      targeting enemies;
      cost mp 5;
      cooldown 4;

      # 主目标
      deal magic 30 damage to target from caster;

      # 跳跃到附近3个敌人
      set global var "lightning_source" = target;
      set global var "jumps" = 3;

      for each nearest 3 enemies of caster do {
        if it != target then {
          deal magic 20 damage to it from caster
        }
      }
    }

    skill "元素精通" {
      range 0;
      targeting self;
      cost mp 3;
      cooldown 6;
      sealed_until day 2;

      # 增强魔法攻击
      set unit(caster) var "matk" = var "matk" of caster + 10;

      # 获得元素护盾
      set unit(caster) var "shield_value" = 40;
      set unit(caster) var "resist_magic" = 0.4;

      # 持续3回合
      add tag "元素精通" to caster
    }
  }
}
```

### 12.3 辅助治疗

```lbr
role "圣光祭司" id "priest" {
  description "虔诚的治疗者，守护生命之光";

  vars {
    "hp" = 90; "max_hp" = 90;
    "mp" = 12.0; "max_mp" = 12.0;
    "atk" = 5; "def" = 6;
    "matk" = 12; "mdef" = 12;
    "range" = 5; "speed" = 2;
    "mp_regen_per_turn" = 2.0;
  }

  tags { "support", "healer", "holy" }

  skills {
    skill "治疗术" {
      range 6;
      targeting allies;
      cost mp 2;
      cooldown 0;

      # 治疗目标
      heal 35 to target
    }

    skill "群体治疗" {
      range 0;
      targeting self;
      cost mp 4;
      cooldown 3;

      # 治疗所有受伤的盟友
      for each allies of caster in range 5 of caster do {
        if it var "hp" < var "max_hp" of it then {
          # 治疗缺失生命值的50%
          set global var "missing_hp" = var "max_hp" of it - var "hp" of it;
          heal round(var "missing_hp" of global * 0.5) to it
        }
      }
    }

    skill "复活术" {
      range 4;
      targeting allies;
      cost mp 8;
      cooldown 10;
      sealed_until day 3;
      ends_turn;

      # 复活死亡的盟友（假设hp=0为死亡）
      if target var "hp" <= 0 then {
        set unit(target) var "hp" = round(var "max_hp" of target * 0.4);
        add tag "revived" to target;

        # 移除所有负面状态
        remove tag "stunned" from target;
        remove tag "silenced" from target;
        remove tag "poisoned" from target
      }
    }

    skill "圣光庇护" {
      range 5;
      targeting allies;
      cost mp 3;
      cooldown 4;

      # 目标获得护盾
      set unit(target) var "shield_value" = 50;

      # 免疫负面状态
      set unit(target) var "status_immune_turns" = 2;
      add tag "blessed" to target
    }

    skill "驱散魔法" {
      range 6;
      targeting any;
      cost mp 2;
      cooldown 2;

      # 对盟友：移除负面效果
      if target is ally then {
        remove tag "stunned" from target;
        remove tag "silenced" from target;
        remove tag "poisoned" from target;
        remove tag "burning" from target;
        heal 15 to target
      } else {
        # 对敌人：移除增益效果并造成伤害
        remove tag "blessed" from target;
        remove tag "shielded" from target;
        set unit(target) var "shield_value" = 0;
        deal magic 20 damage to target from caster
      }
    }
  }
}
```

---

## 13. 最佳实践

### 13.1 代码风格

#### ✅ 推荐

```lbr
# 使用缩进提高可读性
skill "技能" {
  range 5;
  targeting enemies;

  if caster mp >= 3 then {
    deal magic 25 damage to target from caster
  } else {
    deal magic 10 damage to target from caster
  }
}

# 使用注释说明复杂逻辑
skill "连击" {
  # 检查连击数，达到3层时爆发
  if caster var "combo" >= 3 then {
    # 爆发伤害
    deal physical (var "combo" of caster * 15) damage to target from caster;
    # 重置连击
    set unit(caster) var "combo" = 0
  }
}

# 使用有意义的变量名
set global var "total_damage" = var "atk" of caster + var "matk" of caster
set global var "heal_amount" = round(var "max_hp" of target * 0.3)
```

#### ❌ 避免

```lbr
# 不要省略缩进
skill "技能" {
range 5;
if caster mp >= 3 then {
deal magic 25 damage to target from caster
}
}

# 不要使用无意义的变量名
set global var "x" = 10
set global var "temp" = var "a" of caster + 5

# 不要写过长的单行
for each enemies of caster in range 10 of caster with tag "marked" order by var "hp" asc limit 5 do { deal physical var "damage" of global damage to it from caster ignore defense 30% }
```

### 13.2 性能优化

```lbr
# ✅ 使用limit减少不必要的计算
for each enemies of caster in range 10 of caster limit 3 do {
  deal 20 damage to it
}

# ✅ 使用智能选择器
for each weakest 2 allies of caster do {
  heal 30 to it
}

# ❌ 避免不必要的嵌套循环
for each enemies within 10 do {
  for each allies within 10 do {  # 可能非常慢
    # ...
  }
}

# ✅ 优先使用几何选择器
for each enemies in circle 5 of caster do {
  deal 15 damage to it
}
```

### 13.3 可维护性

```lbr
# ✅ 将复杂计算存入变量
set global var "damage_bonus" = var "atk" of caster - var "def" of target;
set global var "final_damage" = max(10, var "damage_bonus" of global);
deal physical var "final_damage" of global damage to target from caster

# ✅ 分解复杂技能
skill "复杂技能" {
  # 第一阶段：准备
  set global var "power" = var "matk" of caster * 2;
  add tag "charging" to caster;

  # 第二阶段：执行
  for each enemies within 5 do {
    deal magic var "power" of global damage to it from caster
  };

  # 第三阶段：清理
  remove tag "charging" from caster;
  set global var "power" = 0
}
```

### 13.4 常见模式

#### 百分比伤害

```lbr
# 对目标当前HP造成30%伤害
set global var "percent_damage" = round(var "hp" of target * 0.3);
deal magic var "percent_damage" of global damage to target from caster
```

#### 治疗到满

```lbr
# 治疗缺失生命值
set global var "missing_hp" = var "max_hp" of target - var "hp" of target;
heal min(50, var "missing_hp" of global) to target
```

#### 条件AOE

```lbr
# 如果周围有3个以上敌人，使用AOE
for each enemies in range 3 of caster do {
  deal 10 damage to it
}
```

#### 连击系统

```lbr
# 累积连击数，达到阈值爆发
if caster var "combo" >= 5 then {
  deal physical (var "combo" of caster * 20) damage to target from caster;
  set unit(caster) var "combo" = 0
} else {
  deal physical 15 damage to target from caster;
  set unit(caster) var "combo" = var "combo" of caster + 1
}
```

---

## 14. 常见错误

### 14.1 语法错误

#### 错误1: 无效的单位引用

```lbr
# ❌ 错误
deal physical 10 damage to enemy

# ✅ 正确
deal physical 10 damage to target from caster
```

**错误信息**: `DSL parse error: unknown unit reference`
**建议**: 使用 `caster`, `target`, `it`, 或 `unit id "..."`

#### 错误2: 缺少必需的子句

```lbr
# ❌ 错误：for each 必须有 of 子句
for each enemies in range 3 of caster do { ... }

# ✅ 正确
for each enemies of caster in range 3 of caster do { ... }
```

#### 错误3: 重复的子句

```lbr
# ❌ 错误
for each enemies of caster limit 3 limit 5 do { ... }

# ✅ 正确
for each enemies of caster limit 3 do { ... }
```

**错误信息**: `DSL parse error: duplicate 'limit' clause in selector`

#### 错误4: in parallel 位置错误

```lbr
# ❌ 错误
for each enemies in parallel of caster in range 2 of caster do { ... }

# ✅ 正确
for each enemies of caster in range 2 of caster in parallel do { ... }
```

### 14.2 逻辑错误

#### 错误1: 除以零

```lbr
# ❌ 可能出错
set global var "result" = var "hp" of target / 0

# ✅ 安全做法
set global var "divisor" = max(1, var "def" of target);
set global var "result" = var "atk" of caster / var "divisor" of global
```

#### 错误2: 未初始化的变量

```lbr
# ❌ 错误：combo 可能未初始化
if caster var "combo" >= 3 then { ... }

# ✅ 正确：在角色 vars 中初始化
vars {
  "combo" = 0;
}
```

#### 错误3: 引用错误的作用域

```lbr
# ❌ 错误：在循环外使用 it
for each enemies within 5 do {
  set unit(it) var "marked" = 1
}
heal 20 to it  # it 不再有效！

# ✅ 正确：不要在循环外使用 it
for each enemies within 5 do {
  set unit(it) var "marked" = 1;
  heal 20 to it  # 在循环内使用
}
```

### 14.3 性能问题

#### 问题1: 过度嵌套

```lbr
# ❌ 性能差
for each enemies within 10 do {
  for each allies within 10 do {
    for each units within 10 do {
      # 可能执行数百次！
    }
  }
}

# ✅ 优化：使用限制
for each enemies within 10 limit 3 do {
  for each allies within 5 limit 2 do {
    # 最多执行 3 * 2 = 6 次
  }
}
```

#### 问题2: 不必要的全局变量

```lbr
# ❌ 创建太多全局变量
set global var "temp1" = 10;
set global var "temp2" = 20;
set global var "temp3" = 30;
# ... 可能忘记清理

# ✅ 及时清理或重用
set global var "temp" = 10;
# 使用 temp
remove global var "temp"
```

---

## 15. 工具和调试

### 15.1 LBR 验证器

在运行游戏前验证语法：

```bash
# 验证单个文件
dotnet run --project ETBBS.LbrValidator -- role.lbr

# 验证整个目录
dotnet run --project ETBBS.LbrValidator -- roles/ -r -v

# 安静模式（仅显示错误）
dotnet run --project ETBBS.LbrValidator -- roles/ -q
```

**输出示例**:
```
Validating: warrior.lbr ... ✓ OK
Validating: mage.lbr ... ✗ FAILED
  Line 15: DSL parse error: unknown unit reference
  Expected one of: caster, target, it, unit id "..."
```

### 15.2 VSCode 扩展

使用官方 VSCode 扩展获得完整 IDE 支持：

- ✅ 语法高亮
- ✅ 智能补全
- ✅ 实时错误检查
- ✅ 悬停文档
- ✅ 快速修复

**安装**:
```bash
cd vscode-lbr-extension
npm install && npm run compile
npm run package
code --install-extension lbr-language-support-*.vsix
```

### 15.3 技能追踪调试

使用技能追踪系统调试复杂技能：

```csharp
var trace = new SkillTrace(enabled: true);
TraceExtensions.CurrentTrace = trace;

// 执行技能...

// 查看追踪
Console.WriteLine(trace.FormatTrace(verbose: true));
```

**输出示例**:
```
=== Skill Execution Trace ===
[1] Selector: enemies in range 5 {count=3, units=E1, E2, E3}
[2] Scope: Enter: iteration: E1
  [3] Condition: it hp < 50 {result=true}
  [4] Damage: caster → E1 {amount=20, type=physical}
[5] Scope: Exit: iteration: E1
...
```

查看 [TRACE_USAGE_GUIDE.md](TRACE_USAGE_GUIDE.md) 了解更多。

---

## 附录

### A. 快速参考

查看 [QUICK_REFERENCE.md](QUICK_REFERENCE.md) 获取语法速查表。

### B. 完整文档

- [项目概览](PROJECT_OVERVIEW.md) - 架构和设计
- [LSP 文档](LSP.md) - 语言服务器
- [英文版本](lbr.en.md) - English version

### C. 获取帮助

1. **查看文档索引**: [docs/INDEX.md](INDEX.md)
2. **使用验证器**: `dotnet run --project ETBBS.LbrValidator -- roles/ -v`
3. **VSCode 扩展**: 实时语法检查和补全
4. **示例角色**: 查看 `publish/roles/` 中的示例

---

<p align="center">
  <strong>祝您创作愉快！🎮</strong>
</p>
