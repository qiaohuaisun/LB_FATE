using ETBBS;
using Xunit;
using System;
using System.Linq;

namespace ETBBS.Tests;

public class FlorenceQuotesTests
{
    [Fact]
    public void Florence_HasQuotesForAllMajorEvents()
    {
        var lbrPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "publish", "roles", "beast_florence.lbr"
        );

        // Skip test if file doesn't exist (CI environment)
        if (!System.IO.File.Exists(lbrPath))
        {
            return;
        }

        var role = LbrLoader.LoadFromFile(lbrPath);

        Assert.Equal("beast_florence", role.Id);

        // Check turn quotes
        Assert.NotEmpty(role.Quotes.OnTurnStart);
        Assert.NotEmpty(role.Quotes.OnTurnEnd);

        // Check skill quotes
        Assert.True(role.Quotes.OnSkill.ContainsKey("逆行天罚"));
        Assert.True(role.Quotes.OnSkill.ContainsKey("星水注射"));
        Assert.True(role.Quotes.OnSkill.ContainsKey("解剖刀舞"));

        // Check HP threshold quotes
        Assert.True(role.Quotes.OnHpBelow.ContainsKey(0.6));
        Assert.True(role.Quotes.OnHpBelow.ContainsKey(0.3));
        Assert.True(role.Quotes.OnHpBelow.ContainsKey(0.15));

        // Check victory/defeat
        Assert.NotEmpty(role.Quotes.OnVictory);
        Assert.NotEmpty(role.Quotes.OnDefeat);
    }

    [Fact]
    public void Florence_UltimateSkillQuote_ContainsKeyPhrase()
    {
        var lbrPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "publish", "roles", "beast_florence.lbr"
        );

        if (!System.IO.File.Exists(lbrPath))
        {
            return;
        }

        var role = LbrLoader.LoadFromFile(lbrPath);

        var ultimateQuotes = role.Quotes.OnSkill["逆行天罚"];

        // Should mention the skill name or key concept
        Assert.True(ultimateQuotes.Any(q => q.Contains("逆行天罚") || q.Contains("医院") || q.Contains("生与死")));
    }

    [Fact]
    public void Florence_LowHpQuotes_ShowDesperation()
    {
        var lbrPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "publish", "roles", "beast_florence.lbr"
        );

        if (!System.IO.File.Exists(lbrPath))
        {
            return;
        }

        var role = LbrLoader.LoadFromFile(lbrPath);

        var criticalQuotes = role.Quotes.OnHpBelow[0.15];

        // Critical HP quotes should show vulnerability/regret
        Assert.True(criticalQuotes.Any(q =>
            q.Contains("……") ||
            q.Contains("失败") ||
            q.Contains("错了") ||
            q.Contains("不可能")
        ));
    }

    [Fact]
    public void Florence_DefeatQuotes_ShowLiberation()
    {
        var lbrPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "publish", "roles", "beast_florence.lbr"
        );

        if (!System.IO.File.Exists(lbrPath))
        {
            return;
        }

        var role = LbrLoader.LoadFromFile(lbrPath);

        var defeatQuotes = role.Quotes.OnDefeat;

        // Defeat should mention rest, liberation, or relief from the nightmare
        Assert.True(defeatQuotes.Any(q =>
            q.Contains("解脱") ||
            q.Contains("休息") ||
            q.Contains("提灯") ||
            q.Contains("失败")
        ));
    }
}
