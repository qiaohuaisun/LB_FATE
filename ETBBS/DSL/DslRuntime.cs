namespace ETBBS;

public static class DslRuntime
{
    public const string CasterKey = "$caster";
    public const string TargetKey = "$target";
    public const string TeamsKey = "$teams"; // IReadOnlyDictionary<string,string>
    public const string TargetPointKey = "$point"; // Coord (for tile/point targeting)
    public const string RngKey = "$rng";    // System.Random (for chance)
    public const string PhaseKey = "$phase"; // int (1..5) current phase for sample games
    public const string DirKey = "$dir";    // string direction hint: up|down|left|right
    public const string DistanceKey = "$distance"; // string: manhattan|chebyshev|euclidean (selector distance)
}
