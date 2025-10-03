namespace LB_FATE.Mobile.Models;

/// <summary>
/// 网格交互模式
/// </summary>
public enum InteractionMode
{
    /// <summary>
    /// 普通模式 - 点击查看信息
    /// </summary>
    Normal,

    /// <summary>
    /// 移动模式 - 点击空格移动
    /// </summary>
    Move,

    /// <summary>
    /// 攻击模式 - 点击敌人攻击
    /// </summary>
    Attack,

    /// <summary>
    /// 技能模式 - 点击释放技能
    /// </summary>
    CastSkill
}

/// <summary>
/// 交互状态
/// </summary>
public class InteractionState
{
    /// <summary>
    /// 当前模式
    /// </summary>
    public InteractionMode Mode { get; set; } = InteractionMode.Normal;

    /// <summary>
    /// 选中的单位
    /// </summary>
    public GridUnit? SelectedUnit { get; set; }

    /// <summary>
    /// 当前技能名称（技能模式时使用）
    /// </summary>
    public string? CurrentSkill { get; set; }

    /// <summary>
    /// 高亮的格子坐标列表（可移动/可攻击范围）
    /// </summary>
    public List<(int x, int y)> HighlightedCells { get; set; } = new();

    /// <summary>
    /// 重置状态
    /// </summary>
    public void Reset()
    {
        Mode = InteractionMode.Normal;
        SelectedUnit = null;
        CurrentSkill = null;
        HighlightedCells.Clear();
    }
}
