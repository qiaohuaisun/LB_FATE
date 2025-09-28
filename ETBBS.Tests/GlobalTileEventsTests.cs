using ETBBS;
using System;
using System.Collections.Generic;
using Xunit;

public class GlobalTileEventsTests
{
    private static WorldState EmptyWorld(int w = 6, int h = 6)
        => WorldState.CreateEmpty(w, h);

    [Fact]
    public void Global_Var_Change_Event_On_SetGlobalVar()
    {
        var s = EmptyWorld();
        var bus = new EventBus();
        VarChangedEvent? evt = null; int count = 0;
        using var d = bus.Subscribe(EventTopics.GlobalVarChanged, o => { evt = (VarChangedEvent)o!; count++; });

        var se = new SkillExecutor();
        (s, _) = se.Execute(s, new AtomicAction[] { new SetGlobalVar("gk", 42) }, events: bus);

        Assert.Equal(1, count);
        Assert.NotNull(evt);
        Assert.Equal("global", evt!.Scope);
        Assert.Null(evt.UnitId);
        Assert.Null(evt.Pos);
        Assert.Null(evt.Before);
        Assert.Equal(42, Convert.ToInt32(evt.After));
        Assert.Equal(42, Convert.ToInt32(s.Global.Vars["gk"]));
    }

    [Fact]
    public void Global_Tag_Add_Remove_Events()
    {
        var s = EmptyWorld();
        var bus = new EventBus();
        int added = 0, removed = 0; string? lastAdd = null, lastRemove = null;
        using var d1 = bus.Subscribe(EventTopics.GlobalTagAdded, o => { added++; lastAdd = (string)o!; });
        using var d2 = bus.Subscribe(EventTopics.GlobalTagRemoved, o => { removed++; lastRemove = (string)o!; });

        var se = new SkillExecutor();
        (s, _) = se.Execute(s, new AtomicAction[] { new AddGlobalTag("night"), new RemoveGlobalTag("night") }, events: bus);

        Assert.Equal(1, added);
        Assert.Equal(1, removed);
        Assert.Equal("night", lastAdd);
        Assert.Equal("night", lastRemove);
        Assert.DoesNotContain("night", s.Global.Tags);
    }

    [Fact]
    public void Tile_Var_Change_Events_On_Set_And_Modify()
    {
        var s = EmptyWorld();
        var pos = new Coord(1, 2);
        var bus = new EventBus();
        var events = new List<VarChangedEvent>();
        using var d = bus.Subscribe(EventTopics.TileVarChanged, o => events.Add((VarChangedEvent)o!));

        var se = new SkillExecutor();

        // Set tile var
        (s, _) = se.Execute(s, new AtomicAction[] { new SetTileVar(pos, "haz", 3) }, events: bus);
        Assert.Single(events);
        Assert.Equal("tile", events[0].Scope);
        Assert.Null(events[0].Before);
        Assert.Equal(3, Convert.ToInt32(events[0].After));
        Assert.True(events[0].Pos.HasValue);
        Assert.Equal(pos, events[0].Pos!.Value);

        // Modify tile var
        events.Clear();
        (s, _) = se.Execute(s, new AtomicAction[] { new ModifyTileVar(pos, "haz", v => v is int i ? i + 1 : 1) }, events: bus);
        Assert.Single(events);
        Assert.Equal(3, Convert.ToInt32(events[0].Before));
        Assert.Equal(4, Convert.ToInt32(events[0].After));
        Assert.Equal(4, Convert.ToInt32(s.Tiles[pos.X, pos.Y].Vars["haz"]));
    }

    [Fact]
    public void Tile_Tag_Add_Remove_Events()
    {
        var s = EmptyWorld();
        var pos = new Coord(2, 3);
        var bus = new EventBus();
        TileTagEvent? addEvt = null, remEvt = null;
        using var d1 = bus.Subscribe(EventTopics.TileTagAdded, o => addEvt = (TileTagEvent)o!);
        using var d2 = bus.Subscribe(EventTopics.TileTagRemoved, o => remEvt = (TileTagEvent)o!);

        var se = new SkillExecutor();
        (s, _) = se.Execute(s, new AtomicAction[] { new AddTileTag(pos, "smoke"), new RemoveTileTag(pos, "smoke") }, events: bus);

        Assert.NotNull(addEvt);
        Assert.NotNull(remEvt);
        Assert.Equal(pos, addEvt!.Pos);
        Assert.True(addEvt!.Added);
        Assert.Equal("smoke", addEvt!.Tag);
        Assert.Equal(pos, remEvt!.Pos);
        Assert.False(remEvt!.Added);
        Assert.Equal("smoke", remEvt!.Tag);
    }
}

