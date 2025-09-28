using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ETBBS;

namespace LB_FATE;

partial class Game
{
    private void InitWorld()
    {
        state = WorldState.CreateEmpty(width, height);
        // load roles (default -> env -> arg)
        var defaultRoles = System.IO.Path.Combine(AppContext.BaseDirectory, "roles");
        var envDir = Environment.GetEnvironmentVariable("LB_FATE_ROLES_DIR");
        if (System.IO.Directory.Exists(defaultRoles)) registry.LoadDirectory(defaultRoles, recursive: false);
        if (!string.IsNullOrWhiteSpace(envDir) && System.IO.Directory.Exists(envDir!)) registry.LoadDirectory(envDir!, recursive: true);
        if (!string.IsNullOrWhiteSpace(rolesDirArg) && System.IO.Directory.Exists(rolesDirArg!)) registry.LoadDirectory(rolesDirArg!, recursive: true);

        // Assign unique classes randomly
        var classes = Enum.GetValues<ClassType>().ToList();
        classes = classes.OrderBy(_ => rng.Next()).ToList();
        for (int i = 0; i < playerIds.Length; i++)
        {
            var pid = playerIds[i];
            var ct = classes[i];
            classOf[pid] = ct;
            teamOf[pid] = pid; // everyone is enemy to each other
            symbolOf[pid] = symbols[i];

            var pos = RandomEmpty();
            var variants = GetRoleVariants(ct);
            if (variants.Count > 0)
            {
                var role = variants[rng.Next(variants.Count)];
                state = UnitFactory.AddUnit(state, pid, role);
                state = WorldStateOps.WithUnit(state, pid, u => u with { Vars = u.Vars.SetItem(Keys.Pos, pos).SetItem("class", ct.ToString()) });
                roleOf[pid] = role;
                // If role defines mp, capture it as max_mp for later refills
                var u0 = state.Units[pid];
                if (u0.Vars.TryGetValue(Keys.Mp, out var mp0) && !u0.Vars.ContainsKey(Keys.MaxMp))
                    state = WorldStateOps.WithUnit(state, pid, u => u with { Vars = u.Vars.SetItem(Keys.MaxMp, mp0) });
            }
            else
            {
                var def = defs[ct];
                state = WorldStateOps.WithUnit(state, pid, u => new UnitState(
                    Vars: ImmutableDictionary<string, object>.Empty
                        .Add(Keys.Hp, def.MaxHp)
                        .Add(Keys.MaxHp, def.MaxHp)
                        .Add(Keys.Mp, def.Mp)
                        .Add(Keys.MaxMp, def.Mp)
                        .Add(Keys.Atk, def.Atk)
                        .Add(Keys.Def, def.Def)
                        .Add(Keys.Range, def.Range)
                        .Add(Keys.Speed, def.Speed)
                        .Add("class", ct.ToString())
                        .Add(Keys.Pos, pos),
                    Tags: ImmutableHashSet<string>.Empty
                ));
            }
        }
        state = WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.SetItem(DslRuntime.TeamsKey, teamOf) });
    }

    private void RefillAllMpToMax()
    {
        foreach (var (id, u) in state.Units)
        {
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            if (u.Vars.TryGetValue(Keys.MaxMp, out var mm))
            {
                state = WorldStateOps.WithUnit(state, id, uu => uu with { Vars = uu.Vars.SetItem(Keys.Mp, mm) });
            }
        }
    }

    private Coord RandomEmpty()
    {
        while (true)
        {
            var p = new Coord(rng.Next(width), rng.Next(height));
            if (!Occupied(p)) return p;
        }
    }

    private bool Occupied(Coord pos)
    {
        foreach (var (id, u) in state.Units)
        {
            if (GetInt(id, Keys.Hp, 0) <= 0) continue;
            if (u.Vars.TryGetValue(Keys.Pos, out var v) && v is Coord c)
                if (c.Equals(pos)) return true;
        }
        return false;
    }

    private List<string> Alive()
    {
        var list = new List<string>();
        foreach (var (id, u) in state.Units)
        {
            int hp = 0;
            if (u.Vars.TryGetValue(Keys.Hp, out var v))
            {
                if (v is int i) hp = i;
                else if (v is long l) hp = (int)l;
                else if (v is double d) hp = (int)Math.Round(d);
            }
            if (hp > 0) list.Add(id);
        }
        return list;
    }

    private List<RoleDefinition> GetRoleVariants(ClassType ct)
    {
        var classStr = ct.ToString().ToLowerInvariant();
        var list = new List<RoleDefinition>();
        foreach (var role in registry.All())
        {
            var idLower = role.Id.ToLowerInvariant();
            var nameLower = role.Name.ToLowerInvariant();
            bool idMatch = idLower == classStr || idLower.StartsWith(classStr + "_");
            bool nameMatch = nameLower == classStr || nameLower.StartsWith(classStr + " ");
            bool varMatch = role.Vars.TryGetValue("class", out var v) && v is string s && s.Equals(ct.ToString(), StringComparison.OrdinalIgnoreCase);
            if (idMatch || nameMatch || varMatch) list.Add(role);
        }
        return list;
    }
}

