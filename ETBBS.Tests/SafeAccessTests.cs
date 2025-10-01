using ETBBS;
using System.Collections.Immutable;
using Xunit;

public class SafeAccessTests
{
    [Fact]
    public void World_Bounds_And_Tiles_Are_Safe()
    {
        var s = WorldState.CreateEmpty(3, 2);
        Assert.True(s.IsValidPosition(new Coord(0, 0)));
        Assert.True(s.IsValidPosition(new Coord(2, 1)));
        Assert.False(s.IsValidPosition(new Coord(-1, 0)));
        Assert.False(s.IsValidPosition(new Coord(3, 0)));
        Assert.False(s.IsValidPosition(new Coord(0, 2)));

        Assert.NotNull(s.GetTileOrNull(new Coord(1, 1)));
        Assert.Null(s.GetTileOrNull(new Coord(10, 10)));
    }

    [Fact]
    public void Unit_Accessors_Are_Safe()
    {
        var s = WorldState.CreateEmpty(2, 2);
        Assert.False(s.HasUnit("U"));
        Assert.Null(s.GetUnitOrNull("U"));

        s = WorldStateOps.WithUnit(s, "U", _ => new UnitState(
            Vars: ImmutableDictionary<string, object>.Empty
                .Add(Keys.Hp, 10)
                .Add(Keys.Pos, new Coord(1, 1)),
            Tags: ImmutableHashSet<string>.Empty.Add("tag1")
        ));

        Assert.True(s.HasUnit("U"));
        var u = s.GetUnitOrNull("U");
        Assert.NotNull(u);

        Assert.True(s.IsUnitAlive("U"));
        Assert.True(s.UnitHasTag("U", "tag1"));
        Assert.Equal(new Coord(1, 1), s.GetUnitPosition("U"));

        // Missing/bad cases
        Assert.False(s.UnitHasTag("X", "tag1"));
        Assert.Equal(new Coord(9, 9), s.GetUnitPosition("X", new Coord(9, 9)));
    }

    [Fact]
    public void Var_And_Global_GetValueOrDefault_Work()
    {
        var unit = new UnitState(
            Vars: ImmutableDictionary<string, object>.Empty.Add("x", 5),
            Tags: ImmutableHashSet<string>.Empty
        );

        Assert.Equal(5, unit.GetIntVar("x", 0));
        Assert.Equal(0, unit.GetIntVar("y", 0));

        var g = new GlobalState(0, ImmutableDictionary<string, object>.Empty.Add("gkey", 42), ImmutableHashSet<string>.Empty);
        Assert.Equal(42, g.GetVarOrDefault("gkey", -1));
        Assert.Equal(-1, g.GetVarOrDefault("other", -1));

        var dict = ImmutableDictionary<string, object>.Empty.Add("a", 1);
        Assert.Equal(1, dict.GetValueOrDefault("a", 7));
        Assert.Equal(7, dict.GetValueOrDefault("b", 7));
    }
}

