using System;
using System.Collections.Generic;

namespace Jieqiao
{
    public enum CardType
    {
        Unit,
        Tactic,
        Equipment,
        Upgrade,
        Support,
    }

    public enum Effect
    {
        None,
        // unit entry effects
        EntryScoutTop2,      // 侦察员：查看牌库顶 2 张并调整顺序
        EntryHealWounded2,   // 维修师：为一个己方受伤单位回复 2
        // tactics
        Damage3AnyEnemyUnit, // 精准射击 / 定点清除：对一个敌方单位造成 3 点伤害，可选后排
        HealWounded2,        // 战地修复：为一个己方受伤单位回复 2
        Draw2,               // 补给整备：抽 2 张
        SuppressAttack2,     // 牵制：一个敌方单位本轮攻击 -2（可叠加，影响反击）
        // equipment
        EquipPlus1Attack,    // 制式武器：攻击 +1
        EquipRanged,         // 瞄准镜：获得远射
        // support
        SearchBasicUnitLe3,  // 应急调度：从主牌库检索一张费用不高于 3 的基础单位
        HealWounded4,        // 应急修复：为一个己方受伤单位回复 4
    }

    /// <summary>
    /// 一张卡的静态定义。晋阶牌用 StatsFrom 覆盖被晋阶单位的属性与关键词。
    /// </summary>
    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public CardType Type;
        public int Cost;
        public int Atk;
        public int Hp;
        public bool Guard;
        public bool Ranged;
        public Effect Effect;
        /// <summary>晋阶牌专用：要求场上存在该 id 的基础单位。</summary>
        public string UpgradesFrom;
        public string Text;

        public bool IsBasicUnit
        {
            get { return Type == CardType.Unit; }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// 文档第 10 章的 12 种主牌 + 6 张预备牌 + 可直接使用的 24 张预组。
    /// </summary>
    public static class CardCatalog
    {
        public static readonly List<CardDef> All = new List<CardDef>();

        public static readonly CardDef ShieldGuard = Add(new CardDef
        {
            Id = "guard_shield", Name = "盾卫士", Type = CardType.Unit, Cost = 2, Atk = 2, Hp = 4,
            Guard = true, Effect = Effect.None, Text = "护卫。处于前排时优先承受普通攻击。",
        });

        public static readonly CardDef Scout = Add(new CardDef
        {
            Id = "scout_unit", Name = "侦察员", Type = CardType.Unit, Cost = 1, Atk = 1, Hp = 3,
            Effect = Effect.EntryScoutTop2, Text = "入场时查看牌库顶至多 2 张，自行调整顺序。",
        });

        public static readonly CardDef Assault = Add(new CardDef
        {
            Id = "assault", Name = "突击队员", Type = CardType.Unit, Cost = 2, Atk = 3, Hp = 3,
            Effect = Effect.None, Text = "无额外关键词，作为基础进攻单位。",
        });

        public static readonly CardDef Crossbow = Add(new CardDef
        {
            Id = "crossbow", Name = "机弩手", Type = CardType.Unit, Cost = 3, Atk = 2, Hp = 3,
            Ranged = true, Effect = Effect.None, Text = "远射。处于后排时可攻击，且不受反击。",
        });

        public static readonly CardDef Mechanic = Add(new CardDef
        {
            Id = "mechanic", Name = "维修师", Type = CardType.Unit, Cost = 2, Atk = 1, Hp = 3,
            Effect = Effect.EntryHealWounded2, Text = "入场时为一个己方受伤单位回复 2 点生命。",
        });

        public static readonly CardDef BannerGuard = Add(new CardDef
        {
            Id = "banner_guard", Name = "城邦旗卫", Type = CardType.Unit, Cost = 3, Atk = 1, Hp = 6,
            Guard = true, Effect = Effect.None, Text = "护卫。生命较高，攻击较低。",
        });

        // ── V0.3 §10.1 新增 4 种试行单位（不能套用现有晋阶牌）─────────────────
        public static readonly CardDef UnitA = Add(new CardDef
        {
            Id = "unit_a", Name = "单位A", Type = CardType.Unit, Cost = 3, Atk = 4, Hp = 3,
            Effect = Effect.None, Text = "进攻代称。无额外关键词；不能使用现有 3 张晋阶牌。",
        });

        public static readonly CardDef UnitB = Add(new CardDef
        {
            Id = "unit_b", Name = "单位B", Type = CardType.Unit, Cost = 3, Atk = 2, Hp = 5,
            Guard = true, Effect = Effect.None, Text = "护卫。与 1/6 城邦旗卫相比，用 1 点生命换 1 点攻击。",
        });

        public static readonly CardDef UnitC = Add(new CardDef
        {
            Id = "unit_c", Name = "单位C", Type = CardType.Unit, Cost = 3, Atk = 3, Hp = 2,
            Ranged = true, Effect = Effect.None, Text = "远射。与 2/3 机弩手相比，用 1 点生命换 1 点攻击。",
        });

        public static readonly CardDef UnitD = Add(new CardDef
        {
            Id = "unit_d", Name = "单位D", Type = CardType.Unit, Cost = 1, Atk = 2, Hp = 1,
            Effect = Effect.None, Text = "轻型代称。无额外关键词，生命低；不能使用现有晋阶牌。",
        });

        public static readonly CardDef PreciseShot = Add(new CardDef
        {
            Id = "precise_shot", Name = "精准射击", Type = CardType.Tactic, Cost = 2,
            Effect = Effect.Damage3AnyEnemyUnit, Text = "对一个敌方单位造成 3 点伤害，可选择后排，不受护卫目标限制。",
        });

        public static readonly CardDef FieldRepair = Add(new CardDef
        {
            Id = "field_repair", Name = "战地修复", Type = CardType.Tactic, Cost = 1,
            Effect = Effect.HealWounded2, Text = "为一个己方受伤单位回复 2 点生命，不可治疗营地。",
        });

        public static readonly CardDef Resupply = Add(new CardDef
        {
            Id = "resupply", Name = "补给整备", Type = CardType.Tactic, Cost = 2,
            Effect = Effect.Draw2, Text = "抽 2 张牌，遵守手牌上限与正常洗牌循环。",
        });

        public static readonly CardDef Suppress = Add(new CardDef
        {
            Id = "suppress", Name = "牵制", Type = CardType.Tactic, Cost = 1,
            Effect = Effect.SuppressAttack2, Text = "一个敌方单位本轮攻击减 2，最低 0；可以叠加，也影响反击。",
        });

        public static readonly CardDef StdWeapon = Add(new CardDef
        {
            Id = "std_weapon", Name = "制式武器", Type = CardType.Equipment, Cost = 1,
            Effect = Effect.EquipPlus1Attack, Text = "装备单位攻击加 1。",
        });

        public static readonly CardDef Scope = Add(new CardDef
        {
            Id = "scope", Name = "瞄准镜", Type = CardType.Equipment, Cost = 2,
            Effect = Effect.EquipRanged, Text = "装备单位获得远射，不增加攻击。",
        });

        // ── 公开预备区：晋阶 ───────────────────────────────────────────────
        public static readonly CardDef UpShieldCaptain = Add(new CardDef
        {
            Id = "up_shield_captain", Name = "盾卫长", Type = CardType.Upgrade, Cost = 2, Atk = 3, Hp = 6,
            Guard = true, UpgradesFrom = "guard_shield", Text = "由盾卫士晋阶为 3 攻击 6 生命，保留护卫。",
        });

        public static readonly CardDef UpBreaker = Add(new CardDef
        {
            Id = "up_breaker", Name = "破阵先锋", Type = CardType.Upgrade, Cost = 2, Atk = 4, Hp = 5,
            Guard = false, UpgradesFrom = "guard_shield", Text = "由盾卫士晋阶为 4 攻击 5 生命，不再拥有护卫。",
        });

        public static readonly CardDef UpPreciseCrossbow = Add(new CardDef
        {
            Id = "up_precise_crossbow", Name = "精密弩师", Type = CardType.Upgrade, Cost = 2, Atk = 3, Hp = 4,
            Ranged = true, UpgradesFrom = "crossbow", Text = "由机弩手晋阶为 3 攻击 4 生命，保留远射。",
        });

        // ── 公开预备区：支援 ───────────────────────────────────────────────
        public static readonly CardDef SupDispatch = Add(new CardDef
        {
            Id = "sup_dispatch", Name = "应急调度", Type = CardType.Support, Cost = 3,
            Effect = Effect.SearchBasicUnitLe3,
            Text = "从主牌库检索一张费用不高于 3 的基础单位加入手牌，然后洗牌；不能从弃牌区检索。",
        });

        public static readonly CardDef SupSnipe = Add(new CardDef
        {
            Id = "sup_snipe", Name = "定点清除", Type = CardType.Support, Cost = 3,
            Effect = Effect.Damage3AnyEnemyUnit,
            Text = "对一个敌方单位造成 3 点伤害，可选择后排。",
        });

        public static readonly CardDef SupRepair = Add(new CardDef
        {
            Id = "sup_repair", Name = "应急修复", Type = CardType.Support, Cost = 2,
            Effect = Effect.HealWounded4,
            Text = "为一个己方受伤单位回复 4 点生命；本局该预备牌仅可使用一次。",
        });

        private static CardDef Add(CardDef d)
        {
            All.Add(d);
            return d;
        }

        public static CardDef ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Id == id)
                {
                    return All[i];
                }
            }
            return null;
        }

        /// <summary>V0.3 §10.4「基准」预组：保留 V0.2 配置，便于隔离规则改动的影响。</summary>
        public static readonly string[] PreconDeckBasic =
        {
            "guard_shield", "guard_shield",
            "scout_unit", "scout_unit",
            "assault", "assault",
            "crossbow", "crossbow",
            "mechanic", "mechanic",
            "banner_guard", "banner_guard",
            "precise_shot", "precise_shot",
            "field_repair", "field_repair",
            "resupply", "resupply",
            "suppress", "suppress",
            "std_weapon", "std_weapon",
            "scope", "scope",
        };

        /// <summary>V0.3 §10.4「进攻」预组：更高进攻阈值与低费铺场。</summary>
        public static readonly string[] PreconDeckAggro =
        {
            "guard_shield", "guard_shield",
            "scout_unit", "scout_unit",
            "assault", "assault",
            "unit_a", "unit_a",
            "unit_c", "unit_c",
            "unit_d", "unit_d",
            "precise_shot", "precise_shot",
            "field_repair", "field_repair",
            "resupply", "resupply",
            "suppress", "suppress",
            "std_weapon", "std_weapon",
            "scope", "scope",
        };

        /// <summary>V0.3 §10.4「远射」预组：依靠后排输出与前排保护。</summary>
        public static readonly string[] PreconDeckRanged =
        {
            "guard_shield", "guard_shield",
            "scout_unit", "scout_unit",
            "crossbow", "crossbow",
            "mechanic", "mechanic",
            "unit_b", "unit_b",
            "unit_c", "unit_c",
            "precise_shot", "precise_shot",
            "field_repair", "field_repair",
            "resupply", "resupply",
            "suppress", "suppress",
            "std_weapon", "std_weapon",
            "scope", "scope",
        };

        /// <summary>三套预组的 id 与显示名（顺序与 §10.4 表格一致）。</summary>
        public static readonly string[] PreconNames = { "基准", "进攻", "远射" };

        public static string[] PreconDeckById(int which)
        {
            if (which == 1)
            {
                return PreconDeckAggro;
            }
            if (which == 2)
            {
                return PreconDeckRanged;
            }
            return PreconDeckBasic;
        }

        /// <summary>默认预组（基准），供旧调用点与存档兼容。</summary>
        public static readonly string[] PreconDeck = PreconDeckBasic;

        /// <summary>公开预备区 6 张：3 张晋阶 + 3 张支援。</summary>
        public static readonly string[] PreconReserve =
        {
            "up_shield_captain", "up_breaker", "up_precise_crossbow",
            "sup_dispatch", "sup_snipe", "sup_repair",
        };

        /// <summary>领航单位候选：费用不高于 3 的基础单位。</summary>
        public static bool IsVanguardCandidate(CardDef d)
        {
            return d != null && d.Type == CardType.Unit && d.Cost <= 3;
        }

        /// <summary>
        /// V0.3 §3.1 / §3.4 构筑校验：任一项不合法时返回 false 并给出具体原因，
        /// 调用方（UI）应提示原因并拒绝开始对局。
        /// </summary>
        public static bool ValidateDeck(string[] deckIds, string vanguardId, string[] reserveIds, out string error)
        {
            error = null;
            if (deckIds == null || reserveIds == null)
            {
                error = "牌组或预备区为空";
                return false;
            }
            if (deckIds.Length != 24)
            {
                error = string.Format("主牌组必须恰好 24 张，当前 {0} 张", deckIds.Length);
                return false;
            }
            var counts = new Dictionary<string, int>();
            int units = 0, cheapUnits = 0, tactics = 0, equipments = 0;
            for (int i = 0; i < deckIds.Length; i++)
            {
                CardDef d = ById(deckIds[i]);
                if (d == null)
                {
                    error = "未知卡牌 id：" + deckIds[i];
                    return false;
                }
                if (d.Type == CardType.Upgrade || d.Type == CardType.Support)
                {
                    error = d.Name + " 是预备牌，不能放进主牌库";
                    return false;
                }
                int c;
                counts.TryGetValue(d.Id, out c);
                counts[d.Id] = c + 1;
                if (c + 1 > 2)
                {
                    error = "同名主牌最多 2 张：" + d.Name;
                    return false;
                }
                if (d.Type == CardType.Unit)
                {
                    units++;
                    if (d.Cost <= 3)
                    {
                        cheapUnits++;
                    }
                }
                else if (d.Type == CardType.Tactic)
                {
                    tactics++;
                }
                else if (d.Type == CardType.Equipment)
                {
                    equipments++;
                }
            }
            if (units < 10)
            {
                error = string.Format("至少 10 张单位，当前 {0} 张", units);
                return false;
            }
            if (cheapUnits < 6)
            {
                error = string.Format("至少 6 张费用不高于 3 的单位，当前 {0} 张", cheapUnits);
                return false;
            }
            if (tactics > 10)
            {
                error = string.Format("战术牌最多 10 张，当前 {0} 张", tactics);
                return false;
            }
            if (equipments > 4)
            {
                error = string.Format("装备牌最多 4 张，当前 {0} 张", equipments);
                return false;
            }
            CardDef vanguard = ById(vanguardId);
            if (!IsVanguardCandidate(vanguard))
            {
                error = "领航必须是费用不高于 3 的基础单位";
                return false;
            }
            int vc;
            counts.TryGetValue(vanguard.Id, out vc);
            if (vc <= 0)
            {
                error = "领航单位不在该牌组中：" + vanguard.Name;
                return false;
            }
            if (reserveIds.Length != 6)
            {
                error = string.Format("预备区必须是 3 张晋阶 + 3 张支援共 6 张，当前 {0} 张", reserveIds.Length);
                return false;
            }
            int ups = 0, sups = 0;
            var rseen = new Dictionary<string, int>();
            for (int i = 0; i < reserveIds.Length; i++)
            {
                CardDef d = ById(reserveIds[i]);
                if (d == null)
                {
                    error = "未知预备牌 id：" + reserveIds[i];
                    return false;
                }
                if (d.Type != CardType.Upgrade && d.Type != CardType.Support)
                {
                    error = d.Name + " 不是预备牌";
                    return false;
                }
                int rc;
                rseen.TryGetValue(d.Id, out rc);
                if (rc > 0)
                {
                    error = "同一预备牌只能配置 1 张：" + d.Name;
                    return false;
                }
                rseen[d.Id] = rc + 1;
                if (d.Type == CardType.Upgrade)
                {
                    ups++;
                }
                else
                {
                    sups++;
                }
            }
            if (ups != 3 || sups != 3)
            {
                error = string.Format("预备区必须是 3 张晋阶 + 3 张支援，当前 {0} + {1}", ups, sups);
                return false;
            }
            return true;
        }
    }
}
