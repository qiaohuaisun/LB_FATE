# LBR Role/Skill DSL (English Summary)

Language: English | 中文见 docs/lbr.zh-CN.md

This document summarizes the LBR text DSL used by ETBBS to define roles and
skills. For a full Chinese guide, see `docs/lbr.zh-CN.md`.

## File Structure

- A role file defines one role:

```
role "<Name>" id "<id>" {
  description "Shown via info";
  vars { "<key>" = <value>; ... }
  tags { "tag1", "tag2", ... }
  skills {
    skill "<SkillName>" { <script> }
    ...
  }
  quotes {
    on_turn_start ["Quote 1", "Quote 2", ...]
    on_skill "<SkillName>" ["Quote 1", "Quote 2", ...]
    ...
  }
}
```

- Keys like `hp/max_hp/mp/max_mp/atk/def/matk/mdef/range/speed` are conventional.
- Status counters: `*_turns` (e.g., `stunned_turns`, `rooted_turns`), per‑turn regen
  (`hp_regen_per_turn`, `mp_regen_per_turn`), shield (`shield_value`), DoT
  (`bleed_*`, `burn_*`), and generic `per_turn_add:<key>` (+ optional caps via
  `per_turn_max:<key>` or `max_<key>`). Resistances: `resist_physical`/`resist_magic` in [0..1].

## Skill Meta

- `cost mp <N>;`
- `range <N>;`
- `cooldown <N>;`
- `targeting any|enemies|allies|self|tile;`
- `min_range <N>;` (optional minimal range)
- `sealed_until <T>;` (legacy: unusable before global turn T)
- `sealed_until day <D> [phase <P>];` (new, intuitive): unusable before Day D Phase P
  - Day is 1‑based; Phase is 1..5; if phase omitted, unlocks at start of Day D.
- `ends_turn;` (optional): using this skill immediately ends the user's turn.

## Statements

- Damage/control:
  - `deal <N> damage to <unit>` (true)
  - `deal physical <N> damage to <unit> [from <unit>] [ignore defense <P>%]`
  - `deal magic <N> damage to <unit> [from <unit>] [ignore resist <P>%]`
  - `heal <N> to <unit>`
- Position/AoE:
  - `move <unit> to (<x>,<y>)`
  - `dash towards <unit> up to <N>`
  - `knockback <unit> <N>` - Push target N tiles away from caster (stops early if blocked by other units)
  - `pull <unit> <N>` - Pull target N tiles towards caster (stops early if blocked by other units)
  - `line [physical|magic] <P> to <unit> length <L> [radius <R>] [ignore ... <X>%]`
  - `line <P> to <unit> length <L> [radius <R>]` (true damage)
- Tags/vars/resources:
  - `add tag "<tag>" to <unit>` / `remove tag "<tag>" from <unit>`
  - `set unit(<unit>) var "<key>" = <value>`
  - `set tile(<x>,<y>) var "<key>" = <value>`
  - `set global var "<key>" = <value>`
  - `consume mp = <number>` (defaults to caster)
- Control flow:
  - `if <cond> then <stmt> [else <stmt>]`
  - `repeat <N> times <stmt>`
  - `parallel { <stmt>; <stmt>; }`
  - `for each <selector> [in parallel] do { <stmt> }`
  - `chance <P>% then <stmt> [else <stmt>]`

## Selectors and Conditions

### Selectors

**Basic Syntax:**
- `enemies [clauses...]` - Select enemy units
- `allies [clauses...]` - Select ally units
- `nearest [N] enemies|allies of <unit|point>` - Select N nearest units
- `farthest [N] enemies|allies of <unit|point>` - Select N farthest units
- `units with tag "<tag>"` / `units with var "<key>" OP <int>` - Select all units matching criteria

**Available Clauses** (can appear in **any order**):
- `of <unit>` - Specify the reference unit (typically `caster` or `target`). **Optional** for range-based selectors.
- `in range <R> of <unit|point>` - Filter units within range R
- `with tag "<tag>"` - Filter units with specific tag
- `with var "<key>" OP <value>` - Filter units by variable condition
- `order by var "<key>" [asc|desc]` - Sort by variable (default: asc)
- `limit <N>` - Limit result count to N units

**Examples:**
```lbr
# All clauses - any order works!
enemies of caster in range 4 of caster with tag "stunned" limit 2

# Same as above, different order
enemies in range 4 of caster of caster limit 2 with tag "stunned"

# Order by HP, lowest first
enemies of caster in range 10 of caster order by var "hp" asc limit 1

# Simplified: omit "of caster" when using range
enemies in range 3 of target

# Nearest units (of clause implicit in "of" after enemies)
nearest 3 enemies of caster

# All allies within range
allies in range 5 of caster
```

### Conditions

- `<unit> has tag "<tag>"` - Check if unit has a tag
- `<unit> mp OP <int>` - Compare MP value (OP: `>=, >, <=, <, ==, !=`)

## Values and Expressions

- `var "<key>" of <unit>` reads a variable
- Simple arithmetic: `<primary> (('+'|'-') <primary>)*`
- Types: numbers (int/double), strings, booleans

## Runtime Globals

- `$caster`: string id of the caster
- `$target`: string or empty (optional)
- `$teams`: `unitId -> teamId` dictionary
- `$point`: `Coord` (for tile/point targeting)
- `$rng`: `System.Random` (used in `chance`)

## Execution & Validation

- Engine validates skills: MP requirement, range/targeting, cooldown, sealed‑until.
- Turn system (outside of skills) applies per‑turn updates: counters decay, undying/end, regen,
  DoT (`bleed`/`burn`), generic `per_turn_add:<key>` with clamping.

## Quotes System

Roles can define quotes/dialogues that play during specific game events to enhance immersion. Each event type can have multiple quotes, and the system will randomly select one to display.

### Supported Event Types

```
quotes {
  # When turn starts
  on_turn_start ["Quote 1", "Quote 2", ...]

  # When turn ends
  on_turn_end ["Quote 1", "Quote 2", ...]

  # When using specific skills (can be defined per skill)
  on_skill "<SkillName>" ["Quote 1", "Quote 2", ...]

  # When taking damage
  on_damage ["Quote 1", "Quote 2", ...]

  # When HP falls below specified percentage (0-1 decimal)
  on_hp_below 0.5 ["Quote 1", "Quote 2", ...]
  on_hp_below 0.2 ["Quote 1", "Quote 2", ...]

  # When victorious
  on_victory ["Quote 1", "Quote 2", ...]

  # When defeated
  on_defeat ["Quote 1", "Quote 2", ...]
}
```

### Notes

- HP threshold quotes trigger only once per threshold
- Thresholds are checked from high to low; triggers on first match
- Quotes block is optional; omitting it won't affect role functionality
- Current version primarily supports quote display for Boss characters

### Example

```lbr
role "Beast King" id "boss_beast" {
  vars { "hp" = 105; "max_hp" = 105; }

  skills {
    skill "Destructive Roar" {
      range 4; targeting enemies;
      deal magic 6 damage to target from caster;
    }
  }

  quotes {
    on_turn_start [
      "Foolish humans, tremble before me!",
      "The hour of destruction has come..."
    ]

    on_skill "Destructive Roar" [
      "Hear the roar of annihilation!",
      "Perish—Destructive Roar!"
    ]

    on_hp_below 0.5 [
      "Hmph... not bad, but not enough!",
      "You've successfully angered me!"
    ]

    on_hp_below 0.2 [
      "Impossible... how could I...",
      "Curse you... mere humans..."
    ]
  }
}
```

## Common Syntax Errors

### Error 1: Invalid unit reference

**Incorrect:**
```lbr
deal physical 10 damage to enemy
```

**Error message:**
```
DSL parse error: unknown unit reference
Suggestion: Expected one of: caster, target, it, unit id "..."
```

**Correct:**
```lbr
deal physical 10 damage to target from caster
```

### Error 2: Duplicate clauses in selector

**Incorrect:**
```lbr
for each enemies of caster in range 2 of caster limit 3 limit 5 do { ... }
```

**Error message:**
```
DSL parse error: duplicate 'limit' clause in selector
```

**Correct:**
```lbr
for each enemies of caster in range 2 of caster limit 3 do { ... }
```

### Error 3: `in parallel` placement

**Incorrect:**
```lbr
for each enemies in parallel of caster in range 2 of caster do { ... }
```

**Correct:**
```lbr
for each enemies of caster in range 2 of caster in parallel do { ... }
```

**Explanation:** The `in parallel` modifier must come after all selector clauses, immediately before `do`.

### Using the Validator Tool

Before running the game, validate syntax using:

```bash
dotnet run --project ETBBS.LbrValidator -- roles -v
```

See `ETBBS.LbrValidator/README.md` for details.
