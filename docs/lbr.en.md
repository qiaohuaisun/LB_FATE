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

- Selectors:
  - `enemies|allies [of <unit>] [in range <R> of <unit|point>] [with tag "<tag>"] [with var "<key>" OP <int>] [limit <N>]`
  - `units with tag "<tag>"` / `units with var "<key>" OP <int>`
  - `nearest|farthest [N] enemies|allies of <unit|point>`
- Conditions:
  - `<unit> has tag "<tag>"`
  - `<unit> mp OP <int>` (OP: `>=, >, <=, <, ==, !=`)

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
