namespace LB_FATE;

/// <summary>
/// Adapter to make ClientAutoComplete compatible with InputReader's IAutoComplete interface
/// </summary>
public class ClientAutoCompleteAdapter : IAutoComplete
{
    public List<string> GetCompletions(string input)
    {
        return ClientAutoComplete.GetCompletions(input);
    }
}