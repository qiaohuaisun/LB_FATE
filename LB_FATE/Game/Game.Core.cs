using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ETBBS;

namespace LB_FATE;

partial class Game
{
    private int width = 15;
    private int height = 9;
    private readonly Random rng = new();
    private readonly Dictionary<string, string> teamOf = new();
    private readonly Dictionary<string, ClassType> classOf = new();
    private readonly Dictionary<string, char> symbolOf = new();
    private readonly Dictionary<ClassType, ClassDef> defs = new()
    {
        [ClassType.Saber] = new ClassDef(36, 3, 8, 6, 1, 3),
        [ClassType.Rider] = new ClassDef(34, 3, 7, 5, 1, 4),
        [ClassType.Archer] = new ClassDef(28, 4, 6, 4, 3, 2),
        [ClassType.Lancer] = new ClassDef(32, 3, 7, 5, 2, 3),
        [ClassType.Caster] = new ClassDef(26, 6, 5, 3, 2, 2),
        [ClassType.Berserker] = new ClassDef(34, 2, 9, 3, 1, 3),
        [ClassType.Assassin] = new ClassDef(28, 3, 6, 4, 1, 4),
    };

    private readonly char[] symbols = new[] { '1','2','3','4','5','6','7' };
    private readonly string[] playerIds;
    private readonly Dictionary<string, IPlayerEndpoint> endpoints = new();

    private WorldState state = null!;
    private EventBus events = new();
    private readonly RoleRegistry registry = new();
    private readonly Dictionary<string, RoleDefinition> roleOf = new();
    private readonly ICooldownStore cooldowns = new InMemoryCooldownStore();
    private readonly string? rolesDirArg;
    private readonly List<string> recentLog = new();
    private HashSet<Coord>? highlightCells = null;
    private char highlightChar = 'o';

    public Game(string? rolesDir = null, int playerCount = 7, Dictionary<string, IPlayerEndpoint>? endpointMap = null, int? mapWidth = null, int? mapHeight = null)
    {
        rolesDirArg = rolesDir;
        if (mapWidth is int mw && mw > 0) this.width = mw;
        if (mapHeight is int mh && mh > 0) this.height = mh;
        var count = Math.Max(1, Math.Min(playerCount, symbols.Length));
        playerIds = Enumerable.Range(1, count).Select(i => $"P{i}").ToArray();
        if (endpointMap is not null)
            foreach (var kv in endpointMap) endpoints[kv.Key] = kv.Value;
    }
}

