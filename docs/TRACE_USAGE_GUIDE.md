# SkillTrace 使用指南

## 概述

SkillTrace 是一个强大的调试工具，可以追踪 LBR 技能的完整执行过程，包括条件判断、选择器执行、动作执行等所有关键步骤。

## 快速开始

### 1. 启用追踪

```csharp
// 创建追踪器
var trace = new SkillTrace(enabled: true);

// 设置为当前追踪器
TraceExtensions.CurrentTrace = trace;

// 执行技能...

// 输出追踪信息
Console.WriteLine(trace.FormatTrace(verbose: true));
```

### 2. 基本使用示例

```csharp
// 创建世界状态
var world = WorldState.CreateEmpty(10, 10);

// 添加单位
world = WorldStateOps.WithUnit(world, "hero", u => new UnitState(
    ImmutableDictionary<string, object>.Empty
        .Add(Keys.Hp, 100)
        .Add(Keys.Mp, 10)
        .Add(Keys.Pos, new Coord(5, 5)),
    ImmutableHashSet<string>.Empty
));

// 启用追踪
var trace = new SkillTrace(enabled: true);
TraceExtensions.CurrentTrace = trace;

// 从 LBR 创建并执行技能
var script = @"
    for each enemies within 3 do {
        if it hp < 50 then {
            deal physical 20 damage to it from caster
        }
    }
";
var skill = TextDsl.FromTextUsingGlobals("AttackSkill", script);
var executor = new SkillExecutor();
(world, _) = executor.ExecutePlan(world, skill.BuildPlan(new Context(world)), validator: null);

// 输出追踪
Console.WriteLine(trace.FormatTrace(verbose: true));
```

## 追踪功能详解

### 可追踪的事件类型

#### 1. 选择器执行 (Selector)

自动记录选择器执行和选中的单位：

```lbr
for each random 2 enemies within 5 do { ... }
```

追踪输出：
```
[1] Selector: random enemies in range 5 {count=2, units=E1, E2}
```

#### 2. 条件判断 (Condition)

记录所有条件的判断结果：

```lbr
if caster hp < 50 then { ... }
```

追踪输出：
```
[2] Condition: condition {result=true}
```

对于概率判断：
```lbr
chance 30% then { ... }
```

追踪输出：
```
[3] Condition: chance 30% {result=true, roll=0.245}
```

#### 3. 伤害 (Damage)

记录所有伤害事件：

```lbr
deal physical 10 damage to target from caster
```

追踪输出：
```
[4] Damage: hero → enemy {amount=10, type=physical}
```

#### 4. 治疗 (Heal)

记录治疗事件：

```lbr
heal 20 to target
```

追踪输出：
```
[5] Heal: → ally {amount=20}
```

#### 5. 变量修改 (Variable)

记录变量的修改：

```lbr
set unit(caster) var "atk" = var "atk" of caster + 5
```

追踪输出：
```
[6] Variable: hero.atk changed {from=10, to=15}
```

#### 6. 动作 (Action)

记录其他动作：

```lbr
add tag "stunned" to target
```

追踪输出：
```
[7] Action: add tag {tag=stunned, target=enemy}
```

#### 7. 作用域 (Scope)

记录执行的作用域层次：

```lbr
for each enemies do { ... }
```

追踪输出：
```
[8] Scope: Enter: iteration: E1
  [9] Damage: hero → E1 {amount=10, type=physical}
[10] Scope: Exit: iteration: E1
```

## 高级功能

### 1. 详细输出模式

```csharp
// 非详细模式（跳过作用域）
Console.WriteLine(trace.FormatTrace(verbose: false));

// 详细模式（包含所有信息）
Console.WriteLine(trace.FormatTrace(verbose: true));
```

### 2. 清空追踪

```csharp
trace.Clear(); // 清空所有追踪记录
```

### 3. 禁用追踪

```csharp
// 创建禁用的追踪器（零开销）
var trace = new SkillTrace(enabled: false);

// 或直接设置为 null
TraceExtensions.CurrentTrace = null;
```

### 4. 访问原始追踪数据

```csharp
foreach (var entry in trace.Entries)
{
    Console.WriteLine($"Step {entry.Step}: {entry.Type}");
    Console.WriteLine($"Message: {entry.Message}");
    Console.WriteLine($"Depth: {entry.Depth}");

    foreach (var (key, value) in entry.Data)
    {
        Console.WriteLine($"  {key}: {value}");
    }
}
```

## 实战示例

### 示例1：调试复杂的条件逻辑

```lbr
skill "智能攻击" {
    targeting enemies; range 5;

    for each enemies within 5 do {
        if it hp < 30 then {
            # 斩杀低血量敌人
            deal physical 50 damage to it from caster
        } else {
            if caster var "combo" >= 3 then {
                # 连击加成
                deal physical 25 damage to it from caster;
                set unit(caster) var "combo" = 0
            } else {
                # 普通攻击
                deal physical 15 damage to it from caster;
                set unit(caster) var "combo" = var "combo" of caster + 1
            }
        }
    }
}
```

通过追踪可以清楚看到：
- 选择了哪些敌人
- 每个敌人触发了哪个分支
- 连击数的变化
- 最终造成的伤害

### 示例2：追踪随机性

```lbr
skill "随机打击" {
    targeting self;

    for each random 3 enemies within 6 do {
        chance 50% then {
            deal physical 30 damage to it from caster
        } else {
            deal physical 10 damage to it from caster
        }
    }
}
```

追踪输出会显示：
- 随机选择了哪3个敌人
- 每次概率判断的结果和随机数
- 实际造成的伤害

### 示例3：性能分析

```csharp
var trace = new SkillTrace(enabled: true);
TraceExtensions.CurrentTrace = trace;

var stopwatch = System.Diagnostics.Stopwatch.StartNew();
// 执行技能...
stopwatch.Stop();

Console.WriteLine($"Total steps: {trace.Entries.Count}");
Console.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Average per step: {stopwatch.ElapsedMilliseconds / (double)trace.Entries.Count:F2}ms");
```

## 最佳实践

### ✅ 推荐

1. **开发阶段启用追踪**
   ```csharp
   #if DEBUG
   TraceExtensions.CurrentTrace = new SkillTrace(enabled: true);
   #endif
   ```

2. **使用详细模式排查问题**
   ```csharp
   if (skillFailed)
   {
       Console.WriteLine(trace.FormatTrace(verbose: true));
   }
   ```

3. **单元测试中验证追踪**
   ```csharp
   var damageEntries = trace.Entries.Where(e => e.Type == "Damage").ToList();
   Assert.Equal(expectedDamageCount, damageEntries.Count);
   ```

### ❌ 避免

1. **生产环境长期启用**
   - 追踪会消耗内存和CPU
   - 仅在需要时临时启用

2. **忘记清理追踪器**
   ```csharp
   // 错误：追踪器一直累积数据
   for (int i = 0; i < 1000; i++)
   {
       ExecuteSkill();
   }

   // 正确：每次清空
   for (int i = 0; i < 1000; i++)
   {
       trace.Clear();
       ExecuteSkill();
   }
   ```

3. **在热路径中记录过多信息**
   - 追踪系统已经优化，但仍有开销
   - 对性能敏感的代码考虑禁用追踪

## 总结

SkillTrace 提供了强大而灵活的技能执行追踪能力，帮助开发者：

- 🐛 快速定位和修复 bug
- 📊 理解技能的执行流程
- 🎲 调试随机性和概率逻辑
- ⚡ 分析性能瓶颈
- ✅ 编写更可靠的测试

通过合理使用追踪功能，可以大幅提升 LBR 技能开发的效率和质量。
