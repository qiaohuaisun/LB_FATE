namespace ETBBS;

/// <summary>
/// Conventional keys used in Vars and Tags.
/// </summary>
public static class Keys
{
    // Vars keys (types):
    public const string Hp = "hp";          // int
    public const string Mp = "mp";          // int
    public const string Pos = "pos";        // Coord
    public const string Atk = "atk";        // int
    public const string Def = "def";        // int
    public const string MAtk = "matk";      // int (magic attack)
    public const string MDef = "mdef";      // int (magic defense)
    public const string UndyingTurns = "undying_turns"; // int remaining turns cannot drop below 1
    public const string StunnedTurns = "stunned_turns";   // int
    public const string SilencedTurns = "silenced_turns"; // int
    public const string RootedTurns = "rooted_turns";     // int
    public const string StatusImmuneTurns = "status_immune_turns"; // int (during >0, new CC will be ignored)
    public const string AutoHealBelowHalf = "auto_heal_below_half"; // int (heal amount when first dropping below 50%)
    public const string AutoHealBelowHalfUsed = "auto_heal_below_half_used"; // bool

    // Example world/tile keys
    public const string Weather = "weather";      // string
    public const string Hazard = "hazard";        // bool | int
    public const string HazardLevel = "hazardLevel"; // int
    public const string MaxHp = "max_hp"; // int
    public const string MaxMp = "max_mp"; // int | double (treated as full mana for refills)
    public const string MpRegenPerTurn = "mp_regen_per_turn"; // int | double
    public const string HpRegenPerTurn = "hp_regen_per_turn"; // int | double (per-turn heal)
    public const string ShieldValue = "shield_value"; // int | double (consumed before HP)
    public const string BleedTurns = "bleed_turns"; // int
    public const string BurnTurns = "burn_turns";   // int
    public const string BleedPerTurn = "bleed_per_turn"; // int
    public const string BurnPerTurn = "burn_per_turn";   // int

    // Common combat attributes (recommended types)
    public const string Speed = "speed";                // int | double (initiative or tiles/turn)
    public const string CritRate = "crit_rate";         // double in [0,1]
    public const string CritDamage = "crit_damage";     // double multiplier, e.g., 1.5
    public const string Accuracy = "accuracy";          // double in [0,1]
    public const string Evasion = "evasion";            // double in [0,1]
    public const string Penetration = "penetration";    // int | double (flat or ratio per system)
    public const string Lifesteal = "lifesteal";        // double in [0,1]
    public const string ResistPhysical = "resist_physical"; // double in [0,1]
    public const string ResistMagic = "resist_magic";       // double in [0,1]
    public const string Range = "range";                // int (tiles)
    // Generic mechanics helpers
    public const string ExtraStrikesRange = "extra_strikes_range"; // int (Manhattan distance threshold)
    public const string ExtraStrikesCount = "extra_strikes_count"; // int (>=2)
    // Generic combat helper variables (for extensible talents/passives)
    public const string NightOrDawnEvasionBonus = "night_or_dawn_evasion_bonus"; // double in [0,1]
    public const string EvadeCharges = "evade_charges"; // int (guaranteed evasion charges)
    public const string NextAttackMultiplier = "next_attack_multiplier"; // double (applies to next outgoing attack then clears)
    public const string LowHpIgnoreDefRatio = "low_hp_ignore_def_ratio"; // double in (0,1], if target hp/max_hp <= ratio, ignore defense
    public const string TempEvasionBonus = "temp_evasion_bonus"; // double in [0,1]
    public const string TempEvasionBonusTurns = "temp_evasion_bonus_turns"; // int
    public const string ForceIgnoreDefTurns = "force_ignore_def_turns"; // int
    public const string Level = "level";                // int
    public const string Exp = "exp";                    // int
}

/// <summary>
/// Conventional tag names.
/// </summary>
public static class Tags
{
    public const string Smoke = "smoke";
    public const string Stunned = "stunned";
    public const string Rooted = "rooted";
    public const string Paladin = "paladin";
    public const string Pyromancer = "pyromancer";
    public const string Night = "night";
    public const string Undying = "undying";
    public const string Duel = "duel";
    public const string Poisoned = "poisoned";
    public const string Burning = "burning";
    public const string Frozen = "frozen";
    public const string Silenced = "silenced";
    public const string Bleeding = "bleeding";
    public const string Shielded = "shielded";
}

