using System.Collections.Generic;
using ETBBS;

namespace LB_FATE.AvaloniaClient.Models;

/// <summary>
/// Client-side representation of the game state.
/// </summary>
public class GameState
{
    public int Day { get; set; }
    public int Phase { get; set; }
    public int Width { get; set; } = 10;
    public int Height { get; set; } = 10;
    public Dictionary<string, UnitInfo> Units { get; set; } = new();
    public List<string> RecentLogs { get; set; } = new();
    public string? CurrentPlayerId { get; set; }
    public bool IsMyTurn { get; set; }
}

/// <summary>
/// Client-side unit information.
/// </summary>
public class UnitInfo
{
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public double Mp { get; set; }
    public double MaxMp { get; set; }
    public Coord Position { get; set; }
    public char Symbol { get; set; }
    public bool IsOffline { get; set; }
    public List<SkillInfo> Skills { get; set; } = new();
}

/// <summary>
/// Client-side skill information.
/// </summary>
public class SkillInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MpCost { get; set; }
    public int Range { get; set; }
    public int Cooldown { get; set; }
    public int CooldownLeft { get; set; }
    public string Targeting { get; set; } = "any";
    public bool IsSealed { get; set; }
}