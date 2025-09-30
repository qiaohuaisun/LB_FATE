namespace ETBBS;

/// <summary>
/// Provides conventional keys used for unit and tile variables throughout the game engine.
/// These keys define the standard contract for accessing game state properties.
/// All keys are strings to support flexible, data-driven gameplay systems.
/// </summary>
/// <remarks>
/// Variable types are documented in comments. Common types include:
/// - int: Integer values (HP, attack, etc.)
/// - double: Floating-point values (rates, multipliers)
/// - Coord: 2D coordinates
/// - bool: Boolean flags
/// - string: Text values
/// </remarks>
public static class Keys
{
    // ===== Core Stats =====

    /// <summary>Current hit points. Type: int</summary>
    public const string Hp = "hp";

    /// <summary>Current mana/magic points. Type: int or double</summary>
    public const string Mp = "mp";

    /// <summary>Unit position on the board. Type: Coord</summary>
    public const string Pos = "pos";

    /// <summary>Physical attack power. Type: int</summary>
    public const string Atk = "atk";

    /// <summary>Physical defense. Type: int</summary>
    public const string Def = "def";

    /// <summary>Magic attack power. Type: int</summary>
    public const string MAtk = "matk";

    /// <summary>Magic defense. Type: int</summary>
    public const string MDef = "mdef";

    // ===== Status Effect Durations =====

    /// <summary>Number of turns the unit cannot die (HP cannot drop below 1). Type: int</summary>
    public const string UndyingTurns = "undying_turns";

    /// <summary>Number of turns the unit is stunned (cannot act). Type: int</summary>
    public const string StunnedTurns = "stunned_turns";

    /// <summary>Number of turns the unit is silenced (cannot use skills). Type: int</summary>
    public const string SilencedTurns = "silenced_turns";

    /// <summary>Number of turns the unit is rooted (cannot move). Type: int</summary>
    public const string RootedTurns = "rooted_turns";

    /// <summary>Number of turns the unit is immune to new status effects. Type: int</summary>
    public const string StatusImmuneTurns = "status_immune_turns";

    /// <summary>Heal amount triggered when HP first drops below 50%. Type: int</summary>
    public const string AutoHealBelowHalf = "auto_heal_below_half";

    /// <summary>Flag indicating whether the auto-heal below half has been used. Type: bool</summary>
    public const string AutoHealBelowHalfUsed = "auto_heal_below_half_used";

    // ===== Resource Limits =====

    /// <summary>Maximum hit points. Type: int</summary>
    public const string MaxHp = "max_hp";

    /// <summary>Maximum mana/magic points. Type: int or double</summary>
    public const string MaxMp = "max_mp";

    /// <summary>Mana regeneration per turn. Type: int or double</summary>
    public const string MpRegenPerTurn = "mp_regen_per_turn";

    /// <summary>Health regeneration per turn. Type: int or double</summary>
    public const string HpRegenPerTurn = "hp_regen_per_turn";

    // ===== Defensive Mechanics =====

    /// <summary>Shield value that absorbs damage before HP is reduced. Type: int or double</summary>
    public const string ShieldValue = "shield_value";

    // ===== Damage Over Time Effects =====

    /// <summary>Number of turns the bleed effect lasts. Type: int</summary>
    public const string BleedTurns = "bleed_turns";

    /// <summary>Number of turns the burn effect lasts. Type: int</summary>
    public const string BurnTurns = "burn_turns";

    /// <summary>Damage dealt per turn by bleed effect. Type: int</summary>
    public const string BleedPerTurn = "bleed_per_turn";

    /// <summary>Damage dealt per turn by burn effect. Type: int</summary>
    public const string BurnPerTurn = "burn_per_turn";

    // ===== World/Tile Properties =====

    /// <summary>Weather condition affecting the tile or world. Type: string</summary>
    public const string Weather = "weather";

    /// <summary>Hazard flag or level on a tile. Type: bool or int</summary>
    public const string Hazard = "hazard";

    /// <summary>Hazard intensity level. Type: int</summary>
    public const string HazardLevel = "hazardLevel";

    // ===== Combat Attributes =====

    /// <summary>Movement speed or initiative. Type: int or double (tiles per turn)</summary>
    public const string Speed = "speed";

    /// <summary>Critical hit chance. Type: double in range [0,1]</summary>
    public const string CritRate = "crit_rate";

    /// <summary>Critical hit damage multiplier. Type: double (e.g., 1.5 = 150% damage)</summary>
    public const string CritDamage = "crit_damage";

    /// <summary>Hit accuracy. Type: double in range [0,1]</summary>
    public const string Accuracy = "accuracy";

    /// <summary>Evasion chance. Type: double in range [0,1]</summary>
    public const string Evasion = "evasion";

    /// <summary>Armor penetration (flat or percentage). Type: int or double</summary>
    public const string Penetration = "penetration";

    /// <summary>Life steal percentage. Type: double in range [0,1]</summary>
    public const string Lifesteal = "lifesteal";

    /// <summary>Physical damage resistance. Type: double in range [0,1]</summary>
    public const string ResistPhysical = "resist_physical";

    /// <summary>Magic damage resistance. Type: double in range [0,1]</summary>
    public const string ResistMagic = "resist_magic";

    /// <summary>Attack range in tiles. Type: int</summary>
    public const string Range = "range";

    // ===== Special Combat Mechanics =====

    /// <summary>Maximum distance for multi-strike attacks. Type: int (Manhattan distance)</summary>
    public const string ExtraStrikesRange = "extra_strikes_range";

    /// <summary>Number of strikes in multi-strike attacks. Type: int (>=2)</summary>
    public const string ExtraStrikesCount = "extra_strikes_count";
    // ===== Conditional Bonuses =====

    /// <summary>Evasion bonus during night or dawn phases. Type: double in [0,1]</summary>
    public const string NightOrDawnEvasionBonus = "night_or_dawn_evasion_bonus";

    /// <summary>Number of guaranteed evasion charges. Type: int</summary>
    public const string EvadeCharges = "evade_charges";

    /// <summary>Damage multiplier for the next attack (consumed after use). Type: double</summary>
    public const string NextAttackMultiplier = "next_attack_multiplier";

    /// <summary>HP threshold ratio for ignoring defense. Type: double in (0,1]</summary>
    public const string LowHpIgnoreDefRatio = "low_hp_ignore_def_ratio";

    /// <summary>Temporary evasion bonus. Type: double in [0,1]</summary>
    public const string TempEvasionBonus = "temp_evasion_bonus";

    /// <summary>Duration of temporary evasion bonus. Type: int</summary>
    public const string TempEvasionBonusTurns = "temp_evasion_bonus_turns";

    /// <summary>Number of turns defense is completely ignored. Type: int</summary>
    public const string ForceIgnoreDefTurns = "force_ignore_def_turns";

    // ===== Character Progression =====

    /// <summary>Character level. Type: int</summary>
    public const string Level = "level";

    /// <summary>Experience points. Type: int</summary>
    public const string Exp = "exp";

    // ===== Global Mechanics =====

    /// <summary>Number of turns healing is disabled for the unit. Type: int</summary>
    public const string NoHealTurns = "no_heal_turns";

    /// <summary>Global: Number of turns healing effects are reversed to damage. Type: int</summary>
    public const string ReverseHealTurnsGlobal = "reverse_heal_turns";

    /// <summary>Global: Number of turns damage effects are reversed to healing. Type: int</summary>
    public const string ReverseDamageTurnsGlobal = "reverse_damage_turns";

    // ===== Targeting and Action Control =====

    /// <summary>Number of turns the unit cannot be targeted. Type: int</summary>
    public const string UntargetableTurns = "untargetable_turns";

    /// <summary>Number of turns the unit cannot perform actions. Type: int</summary>
    public const string CannotActTurns = "cannot_act_turns";

    // ===== Triggered Effects =====

    /// <summary>Number of turns the unit heals when taking damage. Type: int</summary>
    public const string OnDamageHealTurns = "on_damage_heal_turns";

    /// <summary>Amount of healing triggered per damage instance. Type: int</summary>
    public const string OnDamageHealValue = "on_damage_heal_value";
}

/// <summary>
/// Provides conventional tag names used throughout the game engine.
/// Tags are boolean flags that indicate unit or tile states, class types, and special conditions.
/// </summary>
public static class Tags
{
    /// <summary>Indicates smoke or obscured vision effect.</summary>
    public const string Smoke = "smoke";

    /// <summary>Unit is stunned and cannot act.</summary>
    public const string Stunned = "stunned";

    /// <summary>Unit is rooted and cannot move.</summary>
    public const string Rooted = "rooted";

    /// <summary>Unit has the Paladin class.</summary>
    public const string Paladin = "paladin";

    /// <summary>Unit has the Pyromancer class.</summary>
    public const string Pyromancer = "pyromancer";

    /// <summary>Night time condition is active.</summary>
    public const string Night = "night";

    /// <summary>Unit cannot die (HP cannot drop below 1).</summary>
    public const string Undying = "undying";

    /// <summary>Unit is in a duel state.</summary>
    public const string Duel = "duel";

    /// <summary>Unit is poisoned.</summary>
    public const string Poisoned = "poisoned";

    /// <summary>Unit is burning.</summary>
    public const string Burning = "burning";

    /// <summary>Unit is frozen.</summary>
    public const string Frozen = "frozen";

    /// <summary>Unit is silenced and cannot use skills.</summary>
    public const string Silenced = "silenced";

    /// <summary>Unit is bleeding.</summary>
    public const string Bleeding = "bleeding";

    /// <summary>Unit has an active shield.</summary>
    public const string Shielded = "shielded";
}

