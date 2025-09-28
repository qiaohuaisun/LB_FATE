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
        WorldStateOps.WithUnit(state, TargetId, u =>
        {
            var hp = 0;
            if (u.Vars.TryGetValue(Keys.Hp, out var v))
            {
                hp = v switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            }
            var dmg = Math.Max(0, Amount);
            // consume shield first
            if (u.Vars.TryGetValue(Keys.ShieldValue, out var sv))
            {
                double shield = sv is double dd ? dd : (sv is int ii ? ii : 0);
                var after = shield - dmg;
                if (after >= 0)
                {
                    return u with { Vars = u.Vars.SetItem(Keys.ShieldValue, after) };
                }
                else
                {
                    dmg = (int)Math.Max(0, Math.Round(-after));
                    u = u with { Vars = u.Vars.SetItem(Keys.ShieldValue, 0) };
                }
            }
            var nhp = Math.Max(0, hp - dmg);
            // auto-heal below half (first time)
            int maxHp = u.Vars.TryGetValue(Keys.MaxHp, out var mhv) ? (mhv is int mi ? mi : (mhv is long ml ? (int)ml : (mhv is double md ? (int)Math.Round(md) : 0))) : 0;
            if (u.Vars.TryGetValue(Keys.AutoHealBelowHalf, out var ahv)
                && (u.Vars.TryGetValue(Keys.AutoHealBelowHalfUsed, out var used) ? !(used is bool ub && ub) : true)
                && maxHp > 0 && hp > maxHp / 2 && nhp <= maxHp / 2)
            {
                int heal = ahv is int ai ? ai : (ahv is long al ? (int)al : (ahv is double ad ? (int)Math.Round(ad) : 0));
                nhp = Math.Min(maxHp, nhp + Math.Max(0, heal));
                u = u with { Vars = u.Vars.SetItem(Keys.AutoHealBelowHalfUsed, true) };
            }
            if (u.Vars.TryGetValue(Keys.UndyingTurns, out var ud) && ud is int turns && turns > 0)
            {
                if (nhp <= 0) nhp = 1; // cannot die while undying
            }
            return u with { Vars = u.Vars.SetItem(Keys.Hp, nhp) };
        });

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
        WorldStateOps.WithUnit(state, TargetId, u =>
        {
            var hp = 0;
            if (u.Vars.TryGetValue(Keys.Hp, out var v))
                hp = v switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            var nhp = hp + Math.Max(0, Amount);
            if (u.Vars.TryGetValue(Keys.MaxHp, out var mh))
            {
                var maxHp = mh switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
                if (maxHp > 0)
                {
                    nhp = Math.Min(maxHp, nhp);
                }
            }
            return u with { Vars = u.Vars.SetItem(Keys.Hp, nhp) };
        });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record PhysicalDamage(string AttackerId, string TargetId, int Power, double IgnoreDefenseRatio = 0.0) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, TargetId, t =>
        {
            var atk = 0; var def = 0; var hp = 0;
            if (state.Units.TryGetValue(AttackerId, out var au) && au.Vars.TryGetValue(Keys.Atk, out var av))
                atk = av switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            if (t.Vars.TryGetValue(Keys.Def, out var dv))
                def = dv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            if (t.Vars.TryGetValue(Keys.Hp, out var hv))
                hp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            var effDef = (int)Math.Round(def * Math.Max(0.0, 1.0 - IgnoreDefenseRatio));
            var raw = Math.Max(0, Power + atk - effDef);
            // apply physical resistance if present
            if (t.Vars.TryGetValue(Keys.ResistPhysical, out var rp) && rp is double rpd)
            {
                var factor = 1.0 - Math.Clamp(rpd, 0.0, 1.0);
                raw = (int)Math.Max(0, Math.Round(raw * factor));
            }
            // Duel mode: if both sides have duel tag, increase damage 3x (approx 'attack +200%')
            var attackerHasDuel = state.Units.TryGetValue(AttackerId, out var au2) && au2.Tags.Contains(Tags.Duel);
            var targetHasDuel = t.Tags.Contains(Tags.Duel);
            if (attackerHasDuel && targetHasDuel)
                raw = (int)Math.Round(raw * 3.0);
            // consume shield
            if (t.Vars.TryGetValue(Keys.ShieldValue, out var sv))
            {
                double shield = sv is double dd ? dd : (sv is int ii ? ii : 0);
                var after = shield - raw;
                if (after >= 0)
                {
                    return t with { Vars = t.Vars.SetItem(Keys.ShieldValue, after) };
                }
                else
                {
                    raw = (int)Math.Max(0, Math.Round(-after));
                    t = t with { Vars = t.Vars.SetItem(Keys.ShieldValue, 0) };
                }
            }
            var nhp = Math.Max(0, hp - raw);
            // auto-heal below half
            int maxHp = t.Vars.TryGetValue(Keys.MaxHp, out var mhv) ? (mhv is int mi ? mi : (mhv is long ml ? (int)ml : (mhv is double md ? (int)Math.Round(md) : 0))) : 0;
            if (t.Vars.TryGetValue(Keys.AutoHealBelowHalf, out var ahv)
                && (t.Vars.TryGetValue(Keys.AutoHealBelowHalfUsed, out var used) ? !(used is bool ub && ub) : true)
                && maxHp > 0 && hp > maxHp / 2 && nhp <= maxHp / 2)
            {
                int heal = ahv is int ai ? ai : (ahv is long al ? (int)al : (ahv is double ad ? (int)Math.Round(ad) : 0));
                nhp = Math.Min(maxHp, nhp + Math.Max(0, heal));
                t = t with { Vars = t.Vars.SetItem(Keys.AutoHealBelowHalfUsed, true) };
            }
            if (t.Vars.TryGetValue(Keys.UndyingTurns, out var ud) && ud is int turns && turns > 0)
            {
                if (nhp <= 0) nhp = 1;
            }
            return t with { Vars = t.Vars.SetItem(Keys.Hp, nhp) };
        });

    public override ImmutableHashSet<string> ReadVars => ImmutableHashSet<string>.Empty
        .Add(UnitVarKey(AttackerId, Keys.Atk))
        .Add(UnitVarKey(TargetId, Keys.Def))
        .Add(UnitVarKey(TargetId, Keys.Hp));

    public override ImmutableHashSet<string> WriteVars => ImmutableHashSet<string>.Empty.Add(UnitVarKey(TargetId, Keys.Hp));
}

public sealed record MagicDamage(string AttackerId, string TargetId, int Power, double IgnoreResistRatio = 0.0) : AtomicAction
{
    public override Effect Compile() => state =>
        WorldStateOps.WithUnit(state, TargetId, t =>
        {
            var matk = 0; var mdef = 0; var hp = 0;
            if (state.Units.TryGetValue(AttackerId, out var au) && au.Vars.TryGetValue(Keys.MAtk, out var av))
                matk = av switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            else if (state.Units.TryGetValue(AttackerId, out var au2) && au2.Vars.TryGetValue(Keys.Atk, out var av2))
                matk = av2 switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            if (t.Vars.TryGetValue(Keys.MDef, out var dv))
                mdef = dv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            if (t.Vars.TryGetValue(Keys.Hp, out var hv))
                hp = hv switch { int i => i, long l => (int)l, double d => (int)Math.Round(d), _ => 0 };
            var effRes = (int)Math.Round(mdef * Math.Max(0.0, 1.0 - IgnoreResistRatio));
            var raw = Math.Max(0, Power + matk - effRes);
            // apply magic resistance if present
            if (t.Vars.TryGetValue(Keys.ResistMagic, out var rm) && rm is double rmd)
            {
                var factor = 1.0 - Math.Clamp(rmd, 0.0, 1.0);
                raw = (int)Math.Max(0, Math.Round(raw * factor));
            }
            // consume shield
            if (t.Vars.TryGetValue(Keys.ShieldValue, out var sv))
            {
                double shield = sv is double dd ? dd : (sv is int ii ? ii : 0);
                var after = shield - raw;
                if (after >= 0)
                {
                    return t with { Vars = t.Vars.SetItem(Keys.ShieldValue, after) };
                }
                else
                {
                    raw = (int)Math.Max(0, Math.Round(-after));
                    t = t with { Vars = t.Vars.SetItem(Keys.ShieldValue, 0) };
                }
            }
            var nhp = Math.Max(0, hp - raw);
            // auto-heal below half
            int maxHp = t.Vars.TryGetValue(Keys.MaxHp, out var mhv) ? (mhv is int mi ? mi : (mhv is long ml ? (int)ml : (mhv is double md ? (int)Math.Round(md) : 0))) : 0;
            if (t.Vars.TryGetValue(Keys.AutoHealBelowHalf, out var ahv)
                && (t.Vars.TryGetValue(Keys.AutoHealBelowHalfUsed, out var used) ? !(used is bool ub && ub) : true)
                && maxHp > 0 && hp > maxHp / 2 && nhp <= maxHp / 2)
            {
                int heal = ahv is int ai ? ai : (ahv is long al ? (int)al : (ahv is double ad ? (int)Math.Round(ad) : 0));
                nhp = Math.Min(maxHp, nhp + Math.Max(0, heal));
                t = t with { Vars = t.Vars.SetItem(Keys.AutoHealBelowHalfUsed, true) };
            }
            if (t.Vars.TryGetValue(Keys.UndyingTurns, out var ud) && ud is int turns && turns > 0)
            {
                if (nhp <= 0) nhp = 1;
            }
            return t with { Vars = t.Vars.SetItem(Keys.Hp, nhp) };
        });

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
        if (!state.Units.TryGetValue(AttackerId, out var au) || !state.Units.TryGetValue(TargetId, out var tu)) return state;
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
            if (Math.Abs(cur.X - tPos.X) + Math.Abs(cur.Y - tPos.Y) <= 1) break;
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
                    int atk = au.Vars.TryGetValue(Keys.Atk, out var av) ? (av is int ai ? ai : (av is long al ? (int)al : (av is double ad ? (int)Math.Round(ad) : 0))) : 0;
                    int def = u.Vars.TryGetValue(Keys.Def, out var dv) ? (dv is int di ? di : (dv is long dl ? (int)dl : (dv is double dd ? (int)Math.Round(dd) : 0))) : 0;
                    int effDef = (int)Math.Round(def * Math.Max(0.0, 1.0 - IgnoreRatio));
                    int raw = Math.Max(0, Power + atk - effDef);
                    // resist_physical
                    if (u.Vars.TryGetValue(Keys.ResistPhysical, out var rp) && rp is double rpd)
                    {
                        var factor = 1.0 - Math.Clamp(rpd, 0.0, 1.0);
                        raw = (int)Math.Max(0, Math.Round(raw * factor));
                    }
                    curState = new Damage(id, raw).Compile()(curState);
                    break;
                }
                case DamageFlavor.Magic:
                {
                    int matk = au.Vars.TryGetValue(Keys.MAtk, out var av) ? (av is int ai ? ai : (av is long al ? (int)al : (av is double ad ? (int)Math.Round(ad) : 0)))
                              : (au.Vars.TryGetValue(Keys.Atk, out var av2) ? (av2 is int ai2 ? ai2 : (av2 is long al2 ? (int)al2 : (av2 is double ad2 ? (int)Math.Round(ad2) : 0))) : 0);
                    int mdef = u.Vars.TryGetValue(Keys.MDef, out var dv) ? (dv is int di ? di : (dv is long dl ? (int)dl : (dv is double dd ? (int)Math.Round(dd) : 0))) : 0;
                    int effRes = (int)Math.Round(mdef * Math.Max(0.0, 1.0 - IgnoreRatio));
                    int raw = Math.Max(0, Power + matk - effRes);
                    // resist_magic
                    if (u.Vars.TryGetValue(Keys.ResistMagic, out var rm) && rm is double rmd)
                    {
                        var factor = 1.0 - Math.Clamp(rmd, 0.0, 1.0);
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
 



