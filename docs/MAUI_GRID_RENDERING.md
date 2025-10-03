# MAUI 网格渲染系统

本文档介绍 LB_FATE.Mobile 中使用 Microsoft.Maui.Graphics 实现的 2D 网格渲染系统。

## 📐 架构概览

```
GridBoardView (控件)
    ↓
GridBoardDrawable (绘制逻辑)
    ↓
Microsoft.Maui.Graphics.ICanvas (渲染接口)
```

## 🔧 核心组件

### 1. GridBoardDrawable - 绘制引擎

**位置**: `LB_FATE.Mobile/Views/Controls/GridBoardDrawable.cs`

**功能**:
- 使用 `Microsoft.Maui.Graphics.ICanvas` 进行 2D 绘制
- 绘制网格线
- 绘制单位（圆形图标 + 生命值条 + 标签）
- 坐标标签显示
- 屏幕坐标转换为网格坐标

**关键方法**:
```csharp
public void Draw(ICanvas canvas, RectF dirtyRect)
{
    // 1. 清空背景
    // 2. 绘制网格线
    // 3. 绘制单位
    // 4. 绘制坐标标签
}

private void DrawUnit(ICanvas canvas, GridUnit unit, float offsetX, float offsetY)
{
    // 绘制单位圆形、边框、生命值条、标签
}

public (int x, int y) ScreenToGrid(PointF screenPoint, RectF canvasRect)
{
    // 将屏幕点击坐标转换为网格坐标
}
```

**颜色方案**:
- 背景色: `#1A1A1A` (深灰)
- 网格线: `#3A3A3A` (中灰)
- 当前玩家: `#4AE290` (绿色)
- 友军: `#4A90E2` (蓝色)
- 敌军: `#E24A4A` (红色)

### 2. GridBoardView - 可视化控件

**位置**: `LB_FATE.Mobile/Views/Controls/GridBoardView.cs`

**功能**:
- 封装 `GraphicsView` 控件
- 提供数据绑定支持
- 处理触摸交互
- 缩放功能

**可绑定属性**:
```csharp
public GridData GridData { get; set; }  // 网格数据
public float CellSize { get; set; }      // 单元格大小 (10-100)
```

**事件**:
```csharp
public event EventHandler<GridCellTappedEventArgs> CellTapped;
```

**使用示例 (XAML)**:
```xaml
<controls:GridBoardView x:Name="GridBoard"
                       GridData="{Binding GridData}"
                       CellSize="30"
                       Margin="5"/>
```

### 3. 数据模型

#### GridData
**位置**: `LB_FATE.Mobile/Models/GridUnit.cs`

```csharp
public class GridData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<GridUnit> Units { get; set; }

    public GridUnit? GetUnitAt(int x, int y);
    public void AddOrUpdateUnit(GridUnit unit);
    public void RemoveUnit(string id);
}
```

#### GridUnit
```csharp
public class GridUnit
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public float MP { get; set; }
    public float MaxMP { get; set; }
    public bool IsAlly { get; set; }
    public bool IsCurrentPlayer { get; set; }
    public Color Color { get; set; }
}
```

## 🎮 交互功能

### 触摸交互

**点击空格子**:
- 自动填充移动命令: `move x y`
- 在消息日志显示: `点击空格: (x, y)`

**点击己方单位**:
- 自动填充 `info` 命令
- 显示单位详情

**点击敌方单位**:
- 自动填充攻击命令: `attack 单位ID`
- 显示单位信息

### 缩放功能

```csharp
GridBoard.ZoomIn();   // 单元格大小 +5
GridBoard.ZoomOut();  // 单元格大小 -5
```

实现位置: `GamePage.xaml.cs` 中的 `OnZoomClicked` 方法

## 📊 渲染细节

### 单元格绘制

1. **网格线**:
   - 颜色: `#3A3A3A`
   - 线宽: 1px
   - 垂直线和水平线交叉形成网格

2. **单位圆形**:
   - 半径: `cellSize * 0.35`
   - 填充颜色: 根据单位类型（友军/敌军/自己）
   - 白色边框，线宽 2px

3. **生命值条**:
   - 宽度: `cellSize * 0.8`
   - 高度: 4px
   - 颜色:
     - HP > 60%: 绿色
     - HP 30-60%: 橙色
     - HP < 30%: 红色

4. **单位标签**:
   - 字体: Bold
   - 大小: `cellSize * 0.25`
   - 颜色: 白色
   - 内容: 单位名称首字母或ID

5. **坐标标签**:
   - 顶部显示 X 坐标 (0, 1, 2...)
   - 左侧显示 Y 坐标
   - 字体大小: 10px
   - 颜色: `#777777`

### 布局计算

```csharp
// 居中显示
float offsetX = Math.Max(0, (canvasWidth - gridWidth * cellSize) / 2);
float offsetY = Math.Max(0, (canvasHeight - gridHeight * cellSize) / 2);

// 单位位置
float unitX = offsetX + unit.X * cellSize;
float unitY = offsetY + unit.Y * cellSize;
```

## 🔗 集成到游戏页面

### GamePage.xaml 布局

```xaml
<Grid RowDefinitions="3*,2*,Auto,Auto">
    <!-- Row 0: 网格视图 -->
    <controls:GridBoardView GridData="{Binding GridData}"/>

    <!-- Row 1: 消息日志 -->
    <CollectionView ItemsSource="{Binding GameMessages}"/>

    <!-- Row 2: 快捷按钮 -->
    <Grid ColumnDefinitions="*,*,*,*,*">
        <Button Text="info"/>
        <Button Text="help"/>
        <Button Text="pass"/>
        <Button Text="缩放"/>
        <Button Text="退出"/>
    </Grid>

    <!-- Row 3: 命令输入 -->
    <Entry Text="{Binding CommandInput}"/>
</Grid>
```

### GameViewModel 集成

```csharp
[ObservableProperty]
private GridData _gridData = new();

public void OnGridCellTapped(int x, int y, GridUnit? unit)
{
    if (unit != null)
    {
        if (unit.IsCurrentPlayer)
            CommandInput = "info";
        else if (!unit.IsAlly)
            CommandInput = $"attack {unit.Id}";
    }
    else
    {
        CommandInput = $"move {x} {y}";
    }
}
```

## 🚀 使用示例

### 初始化网格

```csharp
GridData = new GridData
{
    Width = 25,
    Height = 15
};

// 添加单位
GridData.AddOrUpdateUnit(new GridUnit
{
    Id = "P1",
    Name = "玩家",
    X = 12,
    Y = 7,
    HP = 100,
    MaxHP = 100,
    IsCurrentPlayer = true
});
```

### 更新单位

```csharp
var unit = GridData.GetUnitAt(12, 7);
if (unit != null)
{
    unit.HP -= 20;  // 受到伤害
    GridBoard.Invalidate();  // 刷新显示
}
```

### 处理点击

```csharp
GridBoard.CellTapped += (sender, e) =>
{
    if (e.Unit != null)
    {
        Console.WriteLine($"点击单位: {e.Unit.Name} at ({e.GridX}, {e.GridY})");
    }
    else
    {
        Console.WriteLine($"点击空格: ({e.GridX}, {e.GridY})");
    }
};
```

## 🎨 自定义样式

### 修改颜色

在 `GridBoardDrawable.cs` 中修改颜色常量:

```csharp
private readonly Color _allyColor = Color.FromArgb("#YOUR_COLOR");
private readonly Color _enemyColor = Color.FromArgb("#YOUR_COLOR");
```

### 调整单元格大小

```csharp
// 默认大小
GridBoard.CellSize = 30;

// 放大
GridBoard.CellSize = 50;

// 缩小
GridBoard.CellSize = 20;
```

### 修改生命值条样式

在 `DrawHealthBar` 方法中自定义:

```csharp
float barHeight = 6f;  // 增加高度
float barY = y + 2;     // 调整位置
```

## 📱 性能优化

1. **按需刷新**: 只在数据变化时调用 `Invalidate()`
2. **批量更新**: 使用 `BeginInvokeOnMainThread` 避免频繁UI更新
3. **限制渲染范围**: 只绘制可见区域的单位
4. **缓存计算**: 预计算偏移量和单元格位置

## 🐛 故障排除

### 网格不显示
- 检查 `GridData.Width` 和 `Height` 是否设置
- 确保 `CellSize` 在有效范围 (10-100)
- 验证 `GridBoardView` 有足够的空间

### 点击无响应
- 确保已订阅 `CellTapped` 事件
- 检查 `IsEnabled` 属性是否为 true
- 验证触摸手势识别器已添加

### 颜色显示不正确
- 确认 `GridUnit.Color` 已设置
- 检查 `IsAlly` 和 `IsCurrentPlayer` 标志
- 验证颜色常量格式正确 (如 `#RRGGBB`)

## 🔮 未来扩展

- [ ] 添加单位移动动画
- [ ] 实现技能范围高亮显示
- [ ] 支持自定义单位图标/头像
- [ ] 添加攻击特效和粒子系统
- [ ] 实现缩放/平移手势支持
- [ ] 地图障碍物渲染
- [ ] 多层图层支持（地形、单位、特效）

---

**文档更新**: 2025-10-01
**版本**: v1.0
