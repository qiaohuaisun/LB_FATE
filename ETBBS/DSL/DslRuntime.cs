namespace ETBBS;

public static class DslRuntime
{
    public const string CasterKey = "$caster";
    public const string TargetKey = "$target";
    public const string TeamsKey = "$teams"; // IReadOnlyDictionary<string,string>
    public const string TargetPointKey = "$point"; // Coord (for tile/point targeting)
    public const string RngKey = "$rng";    // System.Random (for chance)
}
