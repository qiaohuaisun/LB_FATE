using System.Linq;
using Xunit;

public class ClientAutoCompleteTests
{
    [Fact]
    public void Adapter_For_IAutoComplete_Wraps_Static_Version()
    {
        var adapter = new LB_FATE.ClientAutoCompleteAdapter();
        var a = adapter.GetCompletions("m");
        var b = LB_FATE.ClientAutoComplete.GetCompletions("m");

        // They should be equal sequence (order/contents)
        Assert.Equal(b, a);
    }
}
