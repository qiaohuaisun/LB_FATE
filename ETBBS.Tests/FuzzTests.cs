using ETBBS;
using System;
using Xunit;

public class FuzzTests
{
    [Fact]
    public void DSL_Fuzz_ParsesOrFailsGracefully()
    {
        var rng = new Random(12345);
        string[] tokens = new[] { "deal", "heal", "damage", "to", "target", "caster", "{", "}", ";", "if", "then", "else", "for", "each", "enemies", "allies", "of", "in", "range", "do", "set", "global", "var", "\"x\"", "=", "1", "2", "3" };
        for (int i = 0; i < 100; i++)
        {
            int n = rng.Next(3, 30);
            var sb = new System.Text.StringBuilder();
            for (int j = 0; j < n; j++) { sb.Append(tokens[rng.Next(tokens.Length)]).Append(' '); }
            var script = sb.ToString();
            try
            {
                _ = TextDsl.FromText("Fuzz", script, new TextDslOptions { ResolveCasterId = _ => "C" });
            }
            catch (FormatException)
            {
                // expected for many cases
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unexpected exception: {ex.GetType().Name}: {ex.Message}\nScript: {script}");
            }
        }
    }

    [Fact]
    public void LBR_Fuzz_ParsesOrFailsGracefully()
    {
        var rng = new Random(54321);
        string[] body = new[] { "description \"x\"", "vars { \"hp\" = 10 }", "tags { \"a\" }", "skills { skill \"S\" { deal 1 damage to target } }" };
        for (int i = 0; i < 50; i++)
        {
            var s = $"role \"R{i}\" id \"r{i}\" {{ {string.Join(' ', body, 0, rng.Next(1, body.Length))} }}";
            try
            {
                _ = LbrLoader.Load(s);
            }
            catch (FormatException)
            {
            }
            catch (Exception ex)
            {
                Assert.Fail($"Unexpected exception: {ex.GetType().Name}: {ex.Message}\nText: {s}");
            }
        }
    }
}

