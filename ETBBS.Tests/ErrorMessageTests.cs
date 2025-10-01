using ETBBS;
using System;
using Xunit;

public class ErrorMessageTests
{
    [Fact]
    public void Lbr_Error_Message_Includes_Line_And_Caret()
    {
        var text = "role \"X\" id \"x\" {\n" +
                   "  vars { \"hp\" = 10 }\n" +
                   "  tagz { \"x\" }\n" + // 'tagz' is invalid section
                   "}\n";
        try
        {
            _ = LbrLoader.Load(text);
            Assert.Fail("Expected a FormatException");
        }
        catch (FormatException ex)
        {
            var msg = ex.Message;
            Assert.Contains("LBR parse error at line", msg);
            Assert.Contains("^", msg);
        }
    }

    [Fact]
    public void Dsl_Error_Message_Includes_Line_And_Caret()
    {
        var script = "deal 5 damage to"; // missing unit after 'to'
        var opts = new TextDslOptions { ResolveCasterId = _ => string.Empty };
        try
        {
            _ = TextDsl.FromText("S", script, opts);
            Assert.Fail("Expected a FormatException");
        }
        catch (FormatException ex)
        {
            var msg = ex.Message;
            Assert.Contains("DSL parse error at line", msg);
            Assert.Contains("^", msg);
        }
    }
}
