# ETBBS LBR Tools (VS Code Extension)

Adds support for `.lbr` role/skill files used by ETBBS.

Language: English | 中文见 README.md

## Features
- Language identification for `lbr`
- Syntax highlighting for role/skill DSL
- Snippets: `role`, `skill`, control and action templates
- Completions: keywords, meta, known `vars`/`tags`
- Hovers: quick docs for keys and DSL terms
- Folding and document symbols (role/skill with meta summary)
- Diagnostics: brace balance, role header, `vars` entry format, workspace duplicate role ids
- Formatter: simple block/indent formatting

## Get Started
- Create a `.lbr` file
- Insert a `role` block and add `skills {}` with `skill` blocks
- Use snippets like `if`, `foreach`, `deal`, `line`, `move`, `set`, `mp`
- Commands:
  - ETBBS LBR: Validate File
  - ETBBS LBR: Create New Role
  - ETBBS LBR: Format Document

## Settings
- `etbbs-lbr.format.indentSize` — indent size (default 2, 1–8)
- `etbbs-lbr.validate.workspaceDuplicates` — report duplicate `role id` across workspace (default true)

## Syntax Notes
- Comments: start with `#` (till end of line)
- Strings use `"..."`
- Separators `;` and `,` are optional (trailing)
- Meta:
  - `range N` / `cooldown N` / `targeting any|enemies|allies|self|tile`
  - `cost mp N`
  - `min_range N` / `sealed_until T`

## Common Vars
- hp, max_hp, mp, max_mp, atk, def, matk, mdef
- resist_physical, resist_magic, range, speed
- shield_value, undying_turns, mp_regen_per_turn, hp_regen_per_turn
- stunned_turns, silenced_turns, rooted_turns, status_immune_turns
- bleed_turns, bleed_per_turn, burn_turns, burn_per_turn
- auto_heal_below_half, auto_heal_below_half_used

## Common Tags
- stunned, silenced, rooted, frozen, undying, duel, windrealm, charged

## Project
- Implementation: `extension.js`
- Syntax: `syntaxes/lbr.tmLanguage.json`
- Language config: `language-configuration.json`
- Snippets: `snippets/lbr.code-snippets.json`
- Commands: validate, createRole, formatDocument

Tips
- See `docs/lbr.en.md` or `docs/lbr.zh-CN.md` for DSL reference.
- If your terminal font/encoding shows garbled output, switch to UTF‑8.

