using System.Collections.Immutable;

namespace ETBBS;

public sealed record SetUnitVar(string Id, string Key, object Value) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u => u with { Vars = u.Vars.SetItem(Key, Value) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Key));
}

public sealed record ModifyUnitVar(string Id, string Key, Func<object, object> Modifier) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u =>
        {
            var old = u.Vars.TryGetValue(Key, out var v) ? v : default!;
            var nv = Modifier(old);
            return u with { Vars = u.Vars.SetItem(Key, nv) };
        });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Key));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Key));
}

public sealed record AddTileTag(Coord Pos, string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithTile(state, Pos, t => t with { Tags = t.Tags.Add(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(TileTagKey(Pos, Tag));
}

public sealed record RemoveTileTag(Coord Pos, string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithTile(state, Pos, t => t with { Tags = t.Tags.Remove(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(TileTagKey(Pos, Tag));
}

public sealed record SetTileVar(Coord Pos, string Key, object Value) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithTile(state, Pos, t => t with { Vars = t.Vars.SetItem(Key, Value) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(TileVarKey(Pos, Key));
}

public sealed record AddUnitTag(string Id, string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u => u with { Tags = u.Tags.Add(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitTagKey(Id, Tag));
}

public sealed record RemoveUnitTag(string Id, string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u => u with { Tags = u.Tags.Remove(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitTagKey(Id, Tag));
}

public sealed record Damage(string TargetId, int Amount) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        // Reverse damage globally -> treat as heal
        if (state.Global.Vars.TryGetValue(Keys.ReverseDamageTurnsGlobal, out var rdt)
            && TypeConversion.ToInt(rdt) > 0)
        {
            return new Heal(TargetId, Math.Max(0, Amount)).Compile()(state);
        }

        return WorldStateOps.WithUnit(state, TargetId, u =>
        {
            // Guaranteed evasion also blocks generic damage once
            int evadeCharges = u.GetIntVar(Keys.EvadeCharges);
            if (evadeCharges > 0)
            {
                return u with { Vars = u.Vars.SetItem(Keys.EvadeCharges, evadeCharges - 1)
                                                .SetItem(Keys.NextAttackMultiplier, GameConstants.EvadeCounterMultiplier) };
            }

            // Apply damage with full pipeline (shield, heal triggers, undying)
            var result = DamageCalculation.ApplyDamage(u, Amount);
            return result.ModifiedUnit;
        });
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record Move(string Id, Coord To) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u => u with { Vars = u.Vars.SetItem(Keys.Pos, To) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Keys.Pos));
}

public sealed record Heal(string TargetId, int Amount) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        // Global heal reversal (generic toggle): if reverse_heal_turns > 0, convert heal to damage
        int reverseTurns = TypeConversion.GetIntFrom(state.Global.Vars, Keys.ReverseHealTurnsGlobal);
        if (reverseTurns > 0)
        {
            return new Damage(TargetId, Math.Max(0, Amount)).Compile()(state);
        }

        // Unit is currently unhealable -> no-op
        var tu = state.GetUnitOrNull(TargetId);
        if (tu is not null && tu.GetIntVar(Keys.NoHealTurns) > 0)
        {
            return state; // ignore heal
        }

        return WorldStateOps.WithUnit(state, TargetId, u =>
        {
            int hp = u.GetIntVar(Keys.Hp);
            int newHp = hp + Math.Max(0, Amount);

            int maxHp = u.GetIntVar(Keys.MaxHp);
            if (maxHp > 0)
            {
                newHp = Math.Min(maxHp, newHp);
            }

            return u with { Vars = u.Vars.SetItem(Keys.Hp, newHp) };
        });
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record PhysicalDamage(string AttackerId, string TargetId, int Power, double IgnoreDefenseRatio = 0.0) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        var tu = state.GetUnitOrNull(TargetId);
        if (tu is null) return state;

        // Check evasion with all bonuses
        var evasionResult = EvasionCalculation.CheckEvasion(state, AttackerId, TargetId);
        if (evasionResult.Evaded)
            return evasionResult.ModifiedState;

        state = evasionResult.ModifiedState;
        double atkMult = evasionResult.AttackMultiplier;

        var au = state.GetUnitOrNull(AttackerId);
        tu = state.GetUnitOrNull(TargetId); // refresh after state modification
        if (tu is null) return state;

        // Stats
        int atk = au?.GetIntVar(Keys.Atk) ?? 0;
        int def = tu.GetIntVar(Keys.Def);
        int hp = tu.GetIntVar(Keys.Hp);

        // Low HP ignore defense (attacker passive)
        double ignoreBelow = au?.GetDoubleVar(Keys.LowHpIgnoreDefRatio) ?? 0.0;
        ignoreBelow = Math.Clamp(ignoreBelow, 0.0, 1.0);
        int maxHp = tu.GetIntVar(Keys.MaxHp);

        bool ignoreAllDef = false;
        // Force ignore defense during turns
        if (au is not null && au.GetIntVar(Keys.ForceIgnoreDefTurns) > 0)
            ignoreAllDef = true;
        if (maxHp > 0 && ignoreBelow > 0.0 && hp <= (int)Math.Round(maxHp * ignoreBelow))
            ignoreAllDef = true;

        int effDef = ignoreAllDef ? 0 : (int)Math.Round(def * Math.Max(0.0, 1.0 - IgnoreDefenseRatio));
        int raw = Math.Max(0, Power + atk - effDef);

        // Apply physical resistance if present
        double resistance = tu.GetDoubleVar(Keys.ResistPhysical);
        if (resistance > 0)
        {
            double factor = 1.0 - Math.Clamp(resistance, GameConstants.MinResistanceCap, GameConstants.MaxResistanceCap);
            raw = (int)Math.Max(0, Math.Round(raw * factor));
        }

        // Apply attack multiplier
        raw = (int)Math.Max(0, Math.Round(raw * atkMult));

        // Duel mode
        bool attackerHasDuel = au?.Tags.Contains(Tags.Duel) ?? false;
        bool targetHasDuel = tu.Tags.Contains(Tags.Duel);
        if (attackerHasDuel && targetHasDuel)
            raw = (int)Math.Round(raw * GameConstants.DuelDamageMultiplier);

        // Apply damage with full pipeline
        return WorldStateOps.WithUnit(state, TargetId, t =>
        {
            var result = DamageCalculation.ApplyDamage(t, raw);
            return result.ModifiedUnit;
        });
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty
        .Add(UnitVarKey(AttackerId, Keys.Atk))
        .Add(UnitVarKey(TargetId, Keys.Def))
        .Add(UnitVarKey(TargetId, Keys.Hp));

    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record MagicDamage(string AttackerId, string TargetId, int Power, double IgnoreResistRatio = 0.0) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        var tu = state.GetUnitOrNull(TargetId);
        if (tu is null) return state;

        // Check evasion with all bonuses
        var evasionResult = EvasionCalculation.CheckEvasion(state, AttackerId, TargetId);
        if (evasionResult.Evaded)
            return evasionResult.ModifiedState;

        state = evasionResult.ModifiedState;
        double atkMult = evasionResult.AttackMultiplier;

        var au = state.GetUnitOrNull(AttackerId);
        tu = state.GetUnitOrNull(TargetId); // refresh after state modification
        if (tu is null) return state;

        int matk = 0;
        if (au is not null && au.Vars.TryGetValue(Keys.MAtk, out var av))
            matk = TypeConversion.ToInt(av);
        else if (au is not null && au.Vars.TryGetValue(Keys.Atk, out var av2))
            matk = TypeConversion.ToInt(av2);

        int mdef = tu.GetIntVar(Keys.MDef);
        int hp = tu.GetIntVar(Keys.Hp);

        var effRes = (int)Math.Round(mdef * Math.Max(0.0, 1.0 - IgnoreResistRatio));
        var raw = Math.Max(0, Power + matk - effRes);

        // Apply magic resistance
        double resistance = tu.GetDoubleVar(Keys.ResistMagic);
        if (resistance > 0)
        {
            var factor = 1.0 - Math.Clamp(resistance, GameConstants.MinResistanceCap, GameConstants.MaxResistanceCap);
            raw = (int)Math.Max(0, Math.Round(raw * factor));
        }

        raw = (int)Math.Max(0, Math.Round(raw * atkMult));

        // Apply damage with full pipeline
        return WorldStateOps.WithUnit(state, TargetId, t =>
        {
            var result = DamageCalculation.ApplyDamage(t, raw);
            return result.ModifiedUnit;
        });
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty
        .Add(UnitVarKey(AttackerId, Keys.MAtk))
        .Add(UnitVarKey(TargetId, Keys.MDef))
        .Add(UnitVarKey(TargetId, Keys.Hp));

    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record SetGlobalVar(string Key, object Value) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.SetItem(Key, Value) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(GlobalVarKey(Key));
}

public sealed record ModifyGlobalVar(string Key, Func<object, object> Modifier) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithGlobal(state, g =>
        {
            var old = g.Vars.TryGetValue(Key, out var v) ? v : default!;
            var nv = Modifier(old);
            return g with { Vars = g.Vars.SetItem(Key, nv) };
        });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(GlobalVarKey(Key));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(GlobalVarKey(Key));
}

public enum DamageFlavor { Physical, Magic, True }

public sealed record LineAoeDamage(string AttackerId, string TargetId, int Power, int Length, int Radius, DamageFlavor Flavor, double IgnoreRatio = 0.0) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        var au = state.GetUnitOrNull(AttackerId);
        var tu = state.GetUnitOrNull(TargetId);
        if (au is null || tu is null) return state;

        if (!au.Vars.TryGetValue(Keys.Pos, out var ap) || ap is not Coord aPos) return state;
        if (!tu.Vars.TryGetValue(Keys.Pos, out var tp) || tp is not Coord tPos) return state;

        int steps = Math.Max(0, Length);
        int rad = Math.Max(0, Radius);
        int dx = Math.Sign(tPos.X - aPos.X);
        int dy = Math.Sign(tPos.Y - aPos.Y);
        var path = new List<Coord>();
        var cur = aPos;
        for (int i = 0; i < steps; i++)
        {
            // Stop only when we have reached the target cell; allows including tiles up to 'steps' away
            if (cur.Equals(tPos)) break;
            Coord next = cur;
            if (cur.X != tPos.X) next = new Coord(cur.X + dx, cur.Y);
            else if (cur.Y != tPos.Y) next = new Coord(cur.X, cur.Y + dy);
            cur = next; path.Add(cur);
        }

        // teams map (optional)
        var teams = state.Global.Vars.TryGetValue(DslRuntime.TeamsKey, out var tv) && tv is IReadOnlyDictionary<string, string> tm ? tm : null;
        string? atkTeam = null; if (teams != null && teams.TryGetValue(AttackerId, out var tteam)) atkTeam = tteam;

        var curState = state;
        foreach (var (id, u) in state.Units)
        {
            if (id == AttackerId) continue;
            if (teams != null && atkTeam != null && teams.TryGetValue(id, out var t0) && t0 == atkTeam) continue; // skip allies
            if (!u.Vars.TryGetValue(Keys.Pos, out var pv) || pv is not Coord pos) continue;
            bool inRange = path.Any(c => Math.Abs(c.X - pos.X) + Math.Abs(c.Y - pos.Y) <= rad);
            if (!inRange) continue;

            switch (Flavor)
            {
                case DamageFlavor.True:
                    curState = new Damage(id, Power).Compile()(curState);
                    break;
                case DamageFlavor.Physical:
                    {
                        int atk = au.GetIntVar(Keys.Atk);
                        int def = u.GetIntVar(Keys.Def);
                        int effDef = (int)Math.Round(def * Math.Max(0.0, 1.0 - IgnoreRatio));
                        int raw = Math.Max(0, Power + atk - effDef);

                        // Apply physical resistance
                        double resistance = u.GetDoubleVar(Keys.ResistPhysical);
                        if (resistance > 0)
                        {
                            var factor = 1.0 - Math.Clamp(resistance, GameConstants.MinResistanceCap, GameConstants.MaxResistanceCap);
                            raw = (int)Math.Max(0, Math.Round(raw * factor));
                        }

                        curState = new Damage(id, raw).Compile()(curState);
                        break;
                    }
                case DamageFlavor.Magic:
                    {
                        int matk = au.Vars.TryGetValue(Keys.MAtk, out var av) ? TypeConversion.ToInt(av)
                                  : au.GetIntVar(Keys.Atk);
                        int mdef = u.GetIntVar(Keys.MDef);
                        int effRes = (int)Math.Round(mdef * Math.Max(0.0, 1.0 - IgnoreRatio));
                        int raw = Math.Max(0, Power + matk - effRes);

                        // Apply magic resistance
                        double resistance = u.GetDoubleVar(Keys.ResistMagic);
                        if (resistance > 0)
                        {
                            var factor = 1.0 - Math.Clamp(resistance, GameConstants.MinResistanceCap, GameConstants.MaxResistanceCap);
                            raw = (int)Math.Max(0, Math.Round(raw * factor));
                        }

                        curState = new Damage(id, raw).Compile()(curState);
                        break;
                    }
            }
        }
        return curState;
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty
        .Add(UnitVarKey(AttackerId, Keys.Pos))
        .Add(UnitVarKey(AttackerId, Keys.Atk))
        .Add(UnitVarKey(AttackerId, Keys.MAtk))
        .Add(UnitVarKey(TargetId, Keys.Pos));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty; // damage emits inner Damage writes
}

// Dash towards target up to MaxSteps along Manhattan shortest path.
public sealed record DashTowards(string Id, string TargetId, int MaxSteps) : AtomicAction
{
    public override Effect Compile() => state =>
    {
        if (!state.Units.TryGetValue(Id, out var u) || !state.Units.TryGetValue(TargetId, out var t)) return state;
        if (!u.Vars.TryGetValue(Keys.Pos, out var up) || up is not Coord upos) return state;
        if (!t.Vars.TryGetValue(Keys.Pos, out var tp) || tp is not Coord tpos) return state;
        int steps = Math.Max(0, MaxSteps);
        var cur = upos;
        int dx = Math.Sign(tpos.X - cur.X);
        int dy = Math.Sign(tpos.Y - cur.Y);
        for (int i = 0; i < steps; i++)
        {
            // stop if adjacent
            if (Math.Abs(cur.X - tpos.X) + Math.Abs(cur.Y - tpos.Y) <= 1) break;
            // choose axis to move (greedy)
            Coord next = cur;
            if (cur.X != tpos.X) next = new Coord(cur.X + dx, cur.Y);
            else if (cur.Y != tpos.Y) next = new Coord(cur.X, cur.Y + dy);
            // block if occupied by another alive unit
            bool blocked = false;
            foreach (var (oid, ou) in state.Units)
            {
                if (oid == Id) continue;
                if (ou.Vars.TryGetValue(Keys.Hp, out var hv) && hv is int hi && hi <= 0) continue;
                if (ou.Vars.TryGetValue(Keys.Pos, out var ov) && ov is Coord oc && oc.Equals(next)) { blocked = true; break; }
            }
            if (blocked) break;
            cur = next;
        }
        return WorldStateOps.WithUnit(state, Id, uu => uu with { Vars = uu.Vars.SetItem(Keys.Pos, cur) });
    };

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty
        .Add(UnitVarKey(Id, Keys.Pos))
        .Add(UnitVarKey(TargetId, Keys.Pos));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Keys.Pos));
}

public sealed record AddGlobalTag(string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithGlobal(state, g => g with { Tags = g.Tags.Add(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(GlobalTagKey(Tag));
}

public sealed record RemoveGlobalTag(string Tag) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithGlobal(state, g => g with { Tags = g.Tags.Remove(Tag) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(GlobalTagKey(Tag));
}

public sealed record ModifyTileVar(Coord Pos, string Key, Func<object, object> Modifier) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithTile(state, Pos, t =>
        {
            var old = t.Vars.TryGetValue(Key, out var v) ? v : default!;
            var nv = Modifier(old);
            return t with { Vars = t.Vars.SetItem(Key, nv) };
        });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(TileVarKey(Pos, Key));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(TileVarKey(Pos, Key));
}

// Remove variable entries
public sealed record RemoveUnitVar(string Id, string Key) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, Id, u => u with { Vars = u.Vars.Remove(Key) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(Id, Key));
}

public sealed record RemoveTileVar(Coord Pos, string Key) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithTile(state, Pos, t => t with { Vars = t.Vars.Remove(Key) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(TileVarKey(Pos, Key));
}

public sealed record RemoveGlobalVar(string Key) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithGlobal(state, g => g with { Vars = g.Vars.Remove(Key) });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty;
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(GlobalVarKey(Key));
}



