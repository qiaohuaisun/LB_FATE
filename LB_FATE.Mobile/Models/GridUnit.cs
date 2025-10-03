namespace LB_FATE.Mobile.Models;

/// <summary>
/// 网格上的单位信息
/// </summary>
public class GridUnit
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";  // 网格显示符号（1-9, B, !等）
    public int X { get; set; }
    public int Y { get; set; }
    public int HP { get; set; }
    public int MaxHP { get; set; }
    public float MP { get; set; }
    public float MaxMP { get; set; }
    public bool IsAlly { get; set; }
    public bool IsCurrentPlayer { get; set; }
    public Color Color { get; set; } = Colors.Gray;
    public List<string> Tags { get; set; } = new();
    public bool IsOffline { get; set; } = false;
}

/// <summary>
/// 网格数据模型
/// </summary>
public class GridData
{
    public int Width { get; set; } = 25;
    public int Height { get; set; } = 15;
    public List<GridUnit> Units { get; set; } = new();

    /// <summary>
    /// 获取指定位置的单位
    /// </summary>
    public GridUnit? GetUnitAt(int x, int y)
    {
        return Units.FirstOrDefault(u => u.X == x && u.Y == y);
    }

    /// <summary>
    /// 添加或更新单位
    /// </summary>
    public void AddOrUpdateUnit(GridUnit unit)
    {
        var existing = Units.FirstOrDefault(u => u.Id == unit.Id);
        if (existing != null)
        {
            Units.Remove(existing);
        }
        Units.Add(unit);
    }

    /// <summary>
    /// 移除单位
    /// </summary>
    public void RemoveUnit(string id)
    {
        Units.RemoveAll(u => u.Id == id);
    }

    /// <summary>
    /// 清空所有单位
    /// </summary>
    public void Clear()
    {
        Units.Clear();
    }
}
