using System;
using System.Text.RegularExpressions;
using LB_FATE.AvaloniaClient.Models;
using ETBBS;

namespace LB_FATE.AvaloniaClient.Services;

/// <summary>
/// Parses server messages and updates the game state.
/// </summary>
public static class GameStateParser
{
    // Pattern: === LB_FATE | Day 1 | Phase 2 ===
    private static readonly Regex HeaderPattern = new(@"=== LB_FATE \| Day (\d+) \| Phase (\d+) ===");

    // Pattern: P0: P0 Saber      HP[##########](100/100) MP=8 Pos=(2,3)
    private static readonly Regex UnitPattern = new(
        @"^\s*([!0-9]):\s*(\w+)\s+(\w+)\s+HP\[([#.]+)\]\((\d+)/(\d+)\)\s+MP=([\d.]+)\s+Pos=\((\d+),(\d+)\)(.*?)$"
    );

    // Pattern: [n] Skill Name (mp:2, range:3, cd:5 (2 left), tgt:enemies)
    private static readonly Regex SkillPattern = new(
        @"^\s*\[(\d+)\]\s+(.+?)\s+\(mp:(\d+),\s*range:(\d+),\s*cd:(\d+)\s*\((\d+)\s+left\),\s*tgt:(\w+).*?\).*$"
    );

    /// <summary>
    /// Parse a server message line and update the game state.
    /// </summary>
    public static void ParseLine(GameState state, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Parse header (Day/Phase)
        var headerMatch = HeaderPattern.Match(line);
        if (headerMatch.Success)
        {
            state.Day = int.Parse(headerMatch.Groups[1].Value);
            state.Phase = int.Parse(headerMatch.Groups[2].Value);
            return;
        }

        // Parse unit status
        var unitMatch = UnitPattern.Match(line);
        if (unitMatch.Success)
        {
            var symbol = unitMatch.Groups[1].Value[0];
            var id = unitMatch.Groups[2].Value;
            var className = unitMatch.Groups[3].Value;
            var hp = int.Parse(unitMatch.Groups[5].Value);
            var maxHp = int.Parse(unitMatch.Groups[6].Value);
            var mp = double.Parse(unitMatch.Groups[7].Value);
            var x = int.Parse(unitMatch.Groups[8].Value);
            var y = int.Parse(unitMatch.Groups[9].Value);
            var suffix = unitMatch.Groups[10].Value;
            var isOffline = suffix.Contains("offline");

            if (!state.Units.TryGetValue(id, out var unit))
            {
                unit = new UnitInfo { Id = id };
                state.Units[id] = unit;
            }

            unit.Symbol = symbol;
            unit.ClassName = className;
            unit.Hp = hp;
            unit.MaxHp = maxHp;
            unit.Mp = mp;
            unit.Position = new Coord(x, y);
            unit.IsOffline = isOffline;
            return;
        }

        // Parse skill
        var skillMatch = SkillPattern.Match(line);
        if (skillMatch.Success)
        {
            var skillInfo = new SkillInfo
            {
                Index = int.Parse(skillMatch.Groups[1].Value),
                Name = skillMatch.Groups[2].Value.Trim(),
                MpCost = int.Parse(skillMatch.Groups[3].Value),
                Range = int.Parse(skillMatch.Groups[4].Value),
                Cooldown = int.Parse(skillMatch.Groups[5].Value),
                CooldownLeft = int.Parse(skillMatch.Groups[6].Value),
                Targeting = skillMatch.Groups[7].Value
            };

            // Store skill for current player (context-dependent parsing)
            // This requires maintaining context about which player's skills are being listed
            return;
        }

        // Parse PROMPT (indicates player's turn)
        if (line.Trim() == "PROMPT")
        {
            state.IsMyTurn = true;
            return;
        }

        // Parse logs
        if (line.StartsWith(" - "))
        {
            state.RecentLogs.Add(line.Substring(3));
            if (state.RecentLogs.Count > 20)
                state.RecentLogs.RemoveAt(0);
            return;
        }

        // Parse GAME OVER
        if (line.Contains("GAME OVER"))
        {
            state.RecentLogs.Add("=== GAME OVER ===");
            return;
        }
    }

    /// <summary>
    /// Parse board grid to extract unit positions (fallback method).
    /// </summary>
    public static void ParseBoardGrid(GameState state, string[] gridLines)
    {
        if (gridLines.Length < 2) return;

        for (int y = 0; y < Math.Min(gridLines.Length, state.Height); y++)
        {
            var line = gridLines[y];
            if (line.Length < 2) continue;

            // Skip border characters
            var content = line.Trim('|', '+', '-');

            for (int x = 0; x < Math.Min(content.Length, state.Width); x++)
            {
                var ch = content[x];
                if (char.IsDigit(ch))
                {
                    var id = "P" + ch;
                    if (state.Units.TryGetValue(id, out var unit))
                    {
                        unit.Position = new Coord(x, y);
                    }
                }
            }
        }
    }
}