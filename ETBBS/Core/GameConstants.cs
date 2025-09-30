namespace ETBBS;

/// <summary>
/// Central repository for game balance constants and magic numbers.
/// Consolidates hardcoded values to make them easy to tune and maintain.
/// </summary>
public static class GameConstants
{
    // === Action Costs ===

    /// <summary>
    /// MP cost for a basic movement action.
    /// </summary>
    public const double MovementCost = 0.5;

    /// <summary>
    /// MP cost for a basic attack action.
    /// </summary>
    public const double BasicAttackCost = 0.5;

    // === Combat Mechanics ===

    /// <summary>
    /// Maximum evasion rate cap (95%). Prevents guaranteed evasion.
    /// </summary>
    public const double MaxEvasionCap = 0.95;

    /// <summary>
    /// Damage multiplier when in duel mode (1v1 combat).
    /// </summary>
    public const double DuelDamageMultiplier = 3.0;

    /// <summary>
    /// Damage multiplier for counter-attack after successful evasion.
    /// </summary>
    public const double EvadeCounterMultiplier = 2.0;

    /// <summary>
    /// Maximum resistance value cap (100%). Applied to all resist_* variables.
    /// </summary>
    public const double MaxResistanceCap = 1.0;

    /// <summary>
    /// Minimum resistance value floor (0%). Applied to all resist_* variables.
    /// </summary>
    public const double MinResistanceCap = 0.0;

    // === AI and Difficulty ===

    /// <summary>
    /// Default delay between AI actions in milliseconds.
    /// </summary>
    public const int DefaultAiDelayMs = 800;

    // === Network and Communication ===

    /// <summary>
    /// Default port for network multiplayer.
    /// </summary>
    public const int DefaultNetworkPort = 35500;

    /// <summary>
    /// Default address for localhost connections.
    /// </summary>
    public const string DefaultHostAddress = "127.0.0.1";

    // === Map Boundaries ===

    /// <summary>
    /// Minimum allowed map width.
    /// </summary>
    public const int MinMapWidth = 5;

    /// <summary>
    /// Minimum allowed map height.
    /// </summary>
    public const int MinMapHeight = 5;

    /// <summary>
    /// Default map width.
    /// </summary>
    public const int DefaultMapWidth = 25;

    /// <summary>
    /// Default map height.
    /// </summary>
    public const int DefaultMapHeight = 15;

    // === Player Limits ===

    /// <summary>
    /// Minimum number of players in a game.
    /// </summary>
    public const int MinPlayers = 1;

    /// <summary>
    /// Maximum number of players in a game.
    /// </summary>
    public const int MaxPlayers = 7;

    // === Status Effect Defaults ===

    /// <summary>
    /// Default damage per turn for bleed effect when not specified.
    /// </summary>
    public const int DefaultBleedDamagePerTurn = 1;

    /// <summary>
    /// Default damage per turn for burn effect when not specified.
    /// </summary>
    public const int DefaultBurnDamagePerTurn = 1;

    // === Rounding and Precision ===

    /// <summary>
    /// Epsilon value for double comparisons.
    /// </summary>
    public const double Epsilon = 0.0001;
}