# 角色数值与机制总览（LB_FATE）

重要说明：本示例对战（LB_FATE）中的“持续时间/每回合数值”，按“阶段（phase）”为单位结算。

- 每个游戏日（day）包含 5 个阶段，持续效果在每个阶段结束时结算一次并将计时减 1。
- 适用范围：`*_turns`（如 `undying_turns / stunned_turns / silenced_turns / rooted_turns / no_heal_turns` 等）、DoT（`bleed_turns / burn_turns`）与每回合资源（`mp_regen_per_turn / hp_regen_per_turn`）。
- 解封条件如 `sealed_until day X [phase Y]` 仍以“天/阶段”判定，未改变。

说明：本文件汇总 `roles/` 下示例角色的基础数值、被动/机制与技能要点，便于查阅与对比。术语与变量名遵循引擎约定；技能元信息如未在脚本中显式声明则标注为“未声明”。

—

## 阿尔托莉雅（Saber） — `saber_artoria`

- 基础数值
  - HP 38 / MaxHP 38 / MP 6.0
  - ATK 9 / DEF 5 / RANGE 1 / SPEED 3
  - 被动：`mp_regen_per_turn = 0.5`；`auto_heal_below_half = 12`（首次跌至半血以下时自动回复）
- 关键机制
  - “风界”标记（windrealm）：解锁若干技能的追加效果
  - “蓄力”（charged）：强化宝具并被使用后移除
- 技能
  - Basic Attack（range 1, enemies）：20% 概率物理 6（忽视 50% 护甲），否则物理 6
  - 疾风突斩（range 3, enemies, cd 1, mp 1.0）：突进至目标（最多 3 格），物理 8（30% 概率忽视 50% 护甲）；若有风界，则对目标周围 1 格内敌人并行造成物理 4
  - 剑风咆哮（range 0, self, cd 2, mp 0.5）：获得标签 `attack_up`，ATK +2；若有风界，则对目标附加 `stunned` 1 回合
  - 誓约胜利之剑（range 3, enemies, cd 5, sealed_until day 3, mp 2.5）：直线群体伤害：物理 9（长 3，半径 0）+ 真实 3；若有风界，额外魔法 5；若“蓄力”，再追加真实 3（长 5）并移除 `charged`
  - 蓄力（self, cd 3, mp 1.0，回合结束）：`undying_turns = 1`、`status_immune_turns = 1`、获得 `charged`
  - 进入风界（self, cd 2, mp 1.5，回合结束）：获得 `windrealm`

—

## 恩奇都（Lancer） — `lancer_enkidu`

- 基础数值
  - HP 34 / MaxHP 34 / MP 5.0
  - ATK 7 / DEF 5 / MDEF 6 / RANGE 1 / SPEED 3
  - 被动：`mp_regen_per_turn = 0.5`，`hp_regen_per_turn = 1`
  - 法抗成长：`resist_magic = 0.1`，每回合 +0.05，上限 0.6
  - 近身连击触发范围：`extra_strikes_range = 2`（曼哈顿 ≤2），`extra_strikes_count = 2`
- 技能
  - Basic Attack（range 1, enemies）：物理 6
  - Nature's Blessing（range 0, self, cd 1）：自疗 1（并维持回蓝）
  - Twin Chains（range 3, enemies, cd 2, mp 1.0）：两段打击，每段 40% 无视护甲、40% 施加 `rooted(1)`；自身 `speed +1`
  - Wide Sweep（range 3, enemies, cd 3, mp 1.5）：并行命中至多 2 名敌人（以施法者为中心 range 3），每名受击敌人令自身 ATK +1 且自疗 1
  - Chains of Heaven（range 5, min_range 4, targeting tile, cd 5, sealed_until 3, mp 2.5）：以点选坐标为中心，范围 2（约 5×5）内敌人并行，重复 6 次真实伤害 1，并附加 `rooted(1)`

—

## 杰克（Assassin） — `assassin_jack`

- 基础数值
  - HP 30 / MaxHP 30 / MP 6.0
  - ATK 7 / DEF 3 / RANGE 1 / SPEED 4
  - 被动参数：`night_or_dawn_evasion_bonus = 0.25`（夜晚/凌晨额外 25% 闪避）、`low_hp_ignore_def_ratio = 0.30`（目标 HP/MaxHP ≤30% 时无视护甲）
- 技能
  - Basic Attack（range 1, enemies）：物理 6（被动可对低血目标无视护甲）
  - 完美回避（range 0, self, cd 3）：`evade_charges = 1`（必闪一次）；触发后下一次攻击伤害翻倍（引擎通过 `next_attack_multiplier` 处理）
  - 三连突袭（range 0, self, cd 3）：`extra_strikes_count = 3`（全图触发），`speed +2`
  - 解体圣母（range 0, self, cd 5, sealed_until 3, mp 2.0）：夜晚/凌晨闪避加成为 50%，低血无视护甲阈值提升至 100%

—

## 凯特神父（Caster） — `caster_kate`

- 基础数值
  - HP 28 / MaxHP 28 / MP 8.0
  - ATK 5 / DEF 3 / MDEF 4 / MATK 7 / RANGE 2 / SPEED 3
- 技能
  - Basic Attack（range 2, enemies）：魔法 5
  - 鉴识眼A+（range 2, targeting any, cost mp 3, cd 0）：记录目标信息至全局：`inspect_hp / inspect_mp / inspect_pos`
  - 号召（range 2, targeting any, cost mp 5, cd 2）：设置全局标记 `force_act_now`（引擎在同一阶段额外令目标行动一次，随后清除标记）
  - 烧掉他们（range 0, self, cost mp 3, cd 5, sealed_until 3）：以 `$point`（前方）为中心 3×3（切比雪夫 1）对敌人并行造成真实 8，并附加 `burn_turns = 2 / burn_per_turn = 2`
  - 转化（range 0, self, cd 0）：交换自身 `HP` 与 `MP`（内部用临时变量完成；无需清理）

—

## 莱地科（Berserker） — `berserker_laidico`

- 基础数值
  - HP 34 / MaxHP 34 / MP 6.0
  - ATK 9 / DEF 3 / RANGE 1 / SPEED 3 / CLASS "Berserker"
- 技能
  - Basic Attack（range 1, enemies）：若目标有 `dragon`，物理 8（忽视 50% 护甲）；否则物理 7
  - 死海侵蚀（range 2, enemies, cd 1, cost mp 1）：魔法 6；附加 `burn_turns = 2 / burn_per_turn = 2`
  - 复仇觉醒（range 0, self, cd 2, cost mp 1，回合结束）：`next_attack_multiplier = 1.5`，`force_ignore_def_turns = 1`
  - 沧溟死域·永夜归墟（range 0, self, cd 5, sealed_until day 3, cost mp 3）：全局 `reverse_heal_turns = 2`；全场敌人并行附加灼烧（龙额外受创真实 2）；自身 `force_ignore_def_turns = 2`，`speed +2`
  - 死海镰刃（range 1, enemies, cd 3, cost mp 2）：物理 9（忽视 50% 护甲）；目标 `no_heal_turns = 2`、`bleed_turns = 2 / bleed_per_turn = 2`；若目标为 `dragon`，额外真实 3；并沿线追加真实 3（长 2）

—

## 琳达梅尔（Rider） — `rider_lindamel`

- 基础数值
  - HP 30 / MaxHP 30 / MP 5.0 / MaxMP 5.0
  - ATK 8 / DEF 4 / MDEF 5
- 技能
  - Basic Attack（range 1, enemies）：20% 概率物理 5（忽视 50% 护甲），否则物理 5
  - Piercing Lance（range 2）：两段物理 6（第二段忽视 50% 护甲）；自损 HP 2；`consume mp = 0.5`
  - Plague Frenzy（range 2）：以自身为中心范围 2 内敌人并行物理 5；每命中一次自疗 1；`consume mp = 2.5`
  - Duel Judgement（sealed_until 3）：目标回满 HP；双方获得 `duel`；施法者 `undying_turns = 3`（以阶段为单位）；双方 `mp_regen_per_turn = 1`（按阶段回蓝）

—

## 俄里翁（Archer） — `archer_orion`

- 基础数值
  - HP 32 / MaxHP 32 / MP 5.0
  - ATK 7 / DEF 4 / RANGE 3 / SPEED 3
  - 姿态基准：`base_atk = 7 / base_speed = 3 / base_range = 3`
  - 每日结束自愈：`per_turn_add:hp = 4`
- 技能
  - Basic Attack（range 3, enemies）：物理 6（固定忽视 30% 护甲）
  - Piercing Volley（range 4, enemies, cd 2, mp 1.0）：直线物理 7（长 3，半径 0，忽视 30% 护甲）
  - Hunter's Stance（range 0, self, cd 2, mp 0.5，回合结束）：姿态切换；若已是近战姿态则恢复至基准；否则进入近战姿态（`range = 1`、ATK +2、SPEED +1），并打上 `stance_melee`
  - Artemis' Blessing（range 0, self, cd 5, sealed_until day 3, mp 2.5）：清除常见负面及其回合计时；`heal 8`、`MP +2`；ATK 提升至 `base_atk + 3`；`status_immune_turns = 2`

—

## 附注

- 目标选择与范围语义
  - `range R of <unit>`：以单位为中心的曼哈顿距离 R；`point` 表示 `$point` 全局坐标
  - `line <type> P to target length L [radius R]`：沿施法方向延伸 L 的线性范围，`radius` 为横向半径
- 资源与状态约定（节选）
  - `mp_regen_per_turn`/`hp_regen_per_turn`：每回合增量（回合推进时生效）
  - `undying_turns`：不灭；生命不会降至 0
  - `status_immune_turns`：短暂免疫新的负面
  - `no_heal_turns`：不可被治疗
  - `reverse_heal_turns`（全局）：治疗转为伤害
  - `evade_charges`：保证闪避的次数（触发后消耗）
