using ETBBS;
using Xunit;
using System;
using System.Collections.Immutable;

namespace ETBBS.Tests;

public class QuotesSystemTests
{
    [Fact]
    public void CanParseRoleWithQuotes()
    {
        var lbr = """
role "Test Boss" id "test_boss" {
  vars { "hp" = 100; "max_hp" = 100; }
  tags { "boss" }

  skills {
    skill "Attack" {
      range 1; targeting enemies;
      deal physical 5 damage to target from caster;
    }
  }

  quotes {
    on_turn_start ["Quote 1", "Quote 2"]
    on_turn_end ["End quote"]
    on_skill "Attack" ["Attack quote 1", "Attack quote 2"]
    on_hp_below 0.5 ["Half HP quote"]
    on_hp_below 0.2 ["Low HP quote"]
    on_victory ["Victory!"]
    on_defeat ["Defeated..."]
  }
}
""";

        var role = LbrLoader.Load(lbr);

        Assert.Equal("Test Boss", role.Name);
        Assert.Equal("test_boss", role.Id);

        // Check quotes
        Assert.Equal(2, role.Quotes.OnTurnStart.Length);
        Assert.Contains("Quote 1", role.Quotes.OnTurnStart);
        Assert.Contains("Quote 2", role.Quotes.OnTurnStart);

        Assert.Single(role.Quotes.OnTurnEnd);
        Assert.Contains("End quote", role.Quotes.OnTurnEnd);

        Assert.True(role.Quotes.OnSkill.ContainsKey("Attack"));
        Assert.Equal(2, role.Quotes.OnSkill["Attack"].Length);

        Assert.True(role.Quotes.OnHpBelow.ContainsKey(0.5));
        Assert.Single(role.Quotes.OnHpBelow[0.5]);
        Assert.Contains("Half HP quote", role.Quotes.OnHpBelow[0.5]);

        Assert.True(role.Quotes.OnHpBelow.ContainsKey(0.2));
        Assert.Single(role.Quotes.OnHpBelow[0.2]);

        Assert.Single(role.Quotes.OnVictory);
        Assert.Single(role.Quotes.OnDefeat);
    }

    [Fact]
    public void RoleWithoutQuotes_HasEmptyQuotes()
    {
        var lbr = """
role "Simple Role" id "simple" {
  vars { "hp" = 50; }
  skills {
    skill "Basic" {
      range 1; targeting self;
      heal 5 to caster;
    }
  }
}
""";

        var role = LbrLoader.Load(lbr);

        Assert.Empty(role.Quotes.OnTurnStart);
        Assert.Empty(role.Quotes.OnTurnEnd);
        Assert.Empty(role.Quotes.OnSkill);
        Assert.Empty(role.Quotes.OnDamage);
        Assert.Empty(role.Quotes.OnHpBelow);
        Assert.Empty(role.Quotes.OnVictory);
        Assert.Empty(role.Quotes.OnDefeat);
    }

    [Fact]
    public void GetRandom_WithEmptyList_ReturnsNull()
    {
        var rng = new Random(42);
        var empty = System.Collections.Immutable.ImmutableArray<string>.Empty;

        var result = RoleQuotes.GetRandom(empty, rng);

        Assert.Null(result);
    }

    [Fact]
    public void GetRandom_WithOneItem_ReturnsThatItem()
    {
        var rng = new Random(42);
        var single = new[] { "Only quote" }.ToImmutableArray();

        var result = RoleQuotes.GetRandom(single, rng);

        Assert.Equal("Only quote", result);
    }

    [Fact]
    public void GetRandom_WithMultipleItems_ReturnsOne()
    {
        var rng = new Random(42);
        var quotes = new[] { "Quote 1", "Quote 2", "Quote 3" }.ToImmutableArray();

        var result = RoleQuotes.GetRandom(quotes, rng);

        Assert.NotNull(result);
        Assert.Contains(result, quotes);
    }
}
