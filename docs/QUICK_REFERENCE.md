# LBR DSL 快速参考卡

## 🎯 技能声明

```lbr
skill "技能名" {
  range 5;                    # 射程
  targeting enemies;          # enemies|allies|self|any|tile|point
  cost mp 2;                  # MP消耗
  cooldown 3;                 # 冷却
  min_range 2;                # 最小射程
  distance manhattan;         # manhattan|chebyshev|euclidean
  ends_turn;                  # 结束回合

  # 技能内容...
}
```

## 🔍 选择器速查

### 基础
```lbr
enemies                      # 所有敌人
allies of target             # 目标的盟友
units                        # 所有单位
```

### 智能
```lbr
random 2 enemies             # 随机
healthiest allies            # HP最高
weakest 3 enemies            # HP最低
nearest 2 enemies            # 最近
farthest ally                # 最远
```

### 几何形状
```lbr
enemies in circle 5 of caster                        # 圆形
allies in cross 3 of point                           # 十字
enemies in line length 8 width 1 of caster dir "up"  # 直线
units in cone radius 6 angle 90 of caster dir "right"# 扇形
```

### 范围
```lbr
enemies within 5             # 简写
enemies in range 5 of target # 完整
```

### 子句
```lbr
enemies
  in range 5
  with tag "stunned"
  with var "hp" < 50
  order by var "atk" desc
  limit 3
```

## 🔢 表达式

### 运算符
```lbr
+ - * / %                    # 加减乘除模
( )                          # 分组
```

### 函数
```lbr
min(a, b)                    # 最小值
max(a, b)                    # 最大值
abs(x)                       # 绝对值
floor(x)                     # 向下取整
ceil(x)                      # 向上取整
round(x)                     # 四舍五入
```

### 变量引用
```lbr
var "key" of caster          # 单位变量
var "key" of global          # 全局变量
```

## 🎮 动作

### 伤害
```lbr
deal 10 damage to target
deal physical 15 damage to target from caster
deal magic 20 damage to target from caster
deal physical 10 damage to target from caster ignore defense 50%
```

### AOE
```lbr
line physical aoe to target from caster damage 15 range 5 width 1
line magic aoe to target from caster damage 20 range 6 width 2
line true aoe to target damage 25 range 4 width 1
```

### 治疗
```lbr
heal 20 to target
```

### 移动
```lbr
move target to (3, 5)
dash towards target up to 3
```

### 标签
```lbr
add tag "stunned" to target
remove tag "invisible" from caster
add global tag "event"
remove tile tag "water" at (5, 5)
```

### 变量
```lbr
set unit(caster) var "atk" = var "atk" of caster + 5
set global var "turn" = var "turn" of global + 1
set tile var "damage" = 10 at (5, 5)

remove unit var "temp" from caster
remove global var "cache"
```

### MP
```lbr
consume mp = 2.5
```

## 🔀 控制流

### 条件
```lbr
if caster hp < 50 then { ... }
if caster has tag "berserk" then { ... }
if caster var "combo" >= 3 then { ... } else { ... }
```

### 概率
```lbr
chance 30% then { ... } else { ... }
```

### 循环
```lbr
for each enemies within 5 do { ... }
for each allies in parallel do { ... }
repeat 3 times { ... }
```

### 并行
```lbr
parallel {
  { ... }
  { ... }
}
```

## 🐛 调试追踪

```csharp
// 启用
var trace = new SkillTrace(enabled: true);
TraceExtensions.CurrentTrace = trace;

// 执行技能...

// 输出
Console.WriteLine(trace.FormatTrace(verbose: true));

// 清理
trace.Clear();
```

## 📏 距离度量

```lbr
distance manhattan;          # |x1-x2| + |y1-y2|
distance chebyshev;          # max(|x1-x2|, |y1-y2|)
distance euclidean;          # sqrt((x1-x2)² + (y1-y2)²)
```

## 🔤 单位引用

```lbr
caster                       # 施放者
target                       # 目标
it                           # 循环中当前单位
unit id "unit_id"            # 特定单位
```

## 📦 作用域

```lbr
unit(...)                    # 单位作用域
global                       # 全局作用域
tile                         # 地图格子作用域
```

## 💡 常用模式

### 百分比伤害
```lbr
set global var "damage" = round(var "hp" of target * 0.3);
deal var "damage" of global damage to target
```

### 治疗最弱盟友
```lbr
for each weakest allies within 5 do {
  heal 20 to it
}
```

### 连击系统
```lbr
if caster var "combo" >= 3 then {
  deal physical var "combo" of caster * 10 damage to target from caster;
  set unit(caster) var "combo" = 0
} else {
  deal physical 15 damage to target from caster;
  set unit(caster) var "combo" = var "combo" of caster + 1
}
```

### AOE + 条件
```lbr
for each enemies in circle 5 of caster do {
  if it hp < var "hp" of caster / 2 then {
    deal physical 30 damage to it from caster
  } else {
    deal physical 15 damage to it from caster
  }
}
```

### 随机多重打击
```lbr
for each random 3 enemies within 6 do {
  chance 50% then {
    deal physical 25 damage to it from caster
  } else {
    deal physical 10 damage to it from caster
  }
}
```

---

📖 完整文档: `docs/lbr.zh-CN.md`
🔧 项目概览: `PROJECT_OVERVIEW.md`
🐛 追踪指南: `TRACE_USAGE_GUIDE.md`
