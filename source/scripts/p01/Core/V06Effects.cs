using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// V0.6 卡牌效果解释器。
    /// 覆盖范围（诚实口径，由 V06Effects.CoverageReport 自报）：
    ///   · 64 张单位：入场效果 + 关键词，全部实现；
    ///   · 32 张战术：全部实现；
    ///   · 装备/附印/战况/响应/晋阶/支援：尚未实现，登记在 Unimplemented，界面不会让它们被使用。
    /// 预组牌库（24 张 = 单位 16 + 战术 8）因此可以完整对局。
    /// </summary>
    public static class V06Effects
    {
        public static readonly HashSet<string> Implemented = new HashSet<string>();
        public static readonly HashSet<string> Unimplemented = new HashSet<string>();

        // ───────────────────────── 登记表 ─────────────────────────

        public static void RegisterAll()
        {
            if (Implemented.Count > 0) { return; }
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                V06Card c = V06Catalog.All[i];
                bool ok = false;
                if (c.Category == V06Category.Unit)
                {
                    ok = EnterSpec(c.Id) != EnterKind.None || IsPlainUnit(c.Id);
                }
                else if (c.Category == V06Category.Tactic)
                {
                    ok = GetSpec(c.Id) != null;
                }
                else if (V06Effects2.Handles(c.Id))
                {
                    ok = true;   // 装备/附印/战况/晋阶/支援 + 可用的响应
                }
                if (ok) { Implemented.Add(c.Id); } else { Unimplemented.Add(c.Id); }
            }
        }

        public static string CoverageReport()
        {
            RegisterAll();
            int u = 0, t = 0, other = 0, uTot = 0, tTot = 0, oTot = 0;
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                V06Card c = V06Catalog.All[i];
                bool impl = Implemented.Contains(c.Id);
                if (c.Category == V06Category.Unit) { uTot++; if (impl) { u++; } }
                else if (c.Category == V06Category.Tactic) { tTot++; if (impl) { t++; } }
                else { oTot++; if (impl) { other++; } }
            }
            return "单位 " + u + "/" + uTot + "，战术 " + t + "/" + tTot
                   + "，其余分类 " + other + "/" + oTot + "（本层不含，界面禁用）";
        }

        // ───────────────────────── 单位入场效果 ─────────────────────────

        private enum EnterKind
        {
            None,
            HealOther2,          // 可治疗另一个己方单位 2
            DamageEnemy1,        // 对一个敌单位造成 1 点效果伤害
            DamageEnemyFront1,   // 对一个敌前排单位造成 1 点效果伤害
            SpawnToken,          // 若有空格，生成衍生物
            BuffFriendlyAtk1,    // 一个己方单位本轮攻击 +1
            PeekTwo,             // 看牌库顶 2 张，可交换顺序
            SelfDamage1,         // 对自身造成 1 点效果伤害
            RepairConstruct2,    // 维修：治疗另一个己方构装 2
            DragonFear           // 龙威：使一个敌单位本轮下一次主动攻击 −1
        }

        private static EnterKind EnterSpec(string id)
        {
            switch (id)
            {
                case "U-H04": case "U-E03": case "U-E08": case "U-V04": return EnterKind.HealOther2;
                case "U-H06": return EnterKind.DamageEnemy1;
                case "U-D05": return EnterKind.DamageEnemyFront1;
                case "U-E06": case "U-E07": case "U-G04": case "U-V08":
                case "U-O08": case "U-R08": case "U-M05":
                    return EnterKind.SpawnToken;
                case "U-O06": return EnterKind.BuffFriendlyAtk1;
                case "U-M01": return EnterKind.PeekTwo;
                case "U-M08": return EnterKind.SelfDamage1;
                case "U-D04": case "U-G03": case "U-M04": case "G07": return EnterKind.RepairConstruct2;
                // T08 维修微机：作为衍生物被"生成"进场时，也要结算自己的维修
                // （口径已定：M04/G07 生成 T08 不代表 T08 自己那份维修被漏掉）
                case "T08": return EnterKind.RepairConstruct2;
                case "U-R06": return EnterKind.DragonFear;
                // V0.7 少女向单位
                case "G01": case "G16": return EnterKind.DamageEnemyFront1;
                case "G02": return EnterKind.DamageEnemy1;
                case "G03": case "G11": case "G14": return EnterKind.HealOther2;
                case "G04": case "G10": return EnterKind.BuffFriendlyAtk1;
                case "G08": return EnterKind.PeekTwo;
                // ── V0.7 第三批卡池 N61–N140：只复用上面 9 种入场效果，不新增代码路径 ──
                case "N61": case "N71": return EnterKind.DamageEnemyFront1;
                case "N64": case "N72": case "N78": case "N133": return EnterKind.DamageEnemy1;
                case "N63": case "N70": case "N73": case "N87":
                case "N100": case "N106": case "N124":
                    return EnterKind.HealOther2;
                case "N62": case "N69": case "N77": case "N103":
                case "N109": case "N122":
                    return EnterKind.BuffFriendlyAtk1;
                case "N65": case "N108": case "N130": return EnterKind.PeekTwo;
                case "N74": case "N102": case "N127": case "N135": return EnterKind.SpawnToken;
                // 维修/龙威 是关键词，但引擎按卡号登记才结算（与既有的 M04/G07/U-R06 同一口径）。
                // 注意：EnterSpec 只能返回一种效果，所以「维修」不能和别的入场效果写在同一张卡上。
                case "N79": case "N92": case "N125": return EnterKind.RepairConstruct2;
                case "N76": case "N89": case "N91": case "N115": case "N128": case "N134":
                    return EnterKind.DragonFear;
            }
            return EnterKind.None;
        }

        private static string EnterToken(string id)
        {
            switch (id)
            {
                case "U-E06": return "T02";
                case "U-E07": return "T01";
                case "U-G04": return "T03";
                case "U-V08": return "T04";
                case "U-O08": return "T05";
                case "U-R08": return "T07";
                case "U-M04": return "T08";
                case "U-M05": return "T06";
                case "G07": return "T08";
                // V0.7 第三批卡池的 4 个铺场单位
                case "N74": return "T05";   // 荒原奇美拉
                case "N102": return "T07";  // 霜翼雏龙
                case "N127": return "T01";  // 灵鹿牧者
                case "N135": return "T06";  // 守备无人机
            }
            return null;
        }

        /// <summary>没有入场效果、也没有关键词之外能力的“白板”单位。</summary>
        private static bool IsPlainUnit(string id)
        {
            V06Card c = V06Catalog.ById(id);
            if (c == null) { return false; }
            // 只有关键词（或无效果）的单位由引擎的关键词处理，视为已实现
            return c.Text != null && (c.Text == "无。" || c.Keywords.Length > 0);
        }

        public static void OnEnter(V06Engine e, V06Player owner, V06Unit u)
        {
            EnterKind k = EnterSpec(u.CardId);
            switch (k)
            {
                case EnterKind.None:
                    break;
                case EnterKind.HealOther2:
                    {
                        V06Unit t = BestHealTarget(e, owner.Camp, false, u.Uid);
                        if (t != null) { e.Heal(t, 2); e.Log.Add(u.Name + " 入场治疗 " + t.Name + " 2"); }
                    }
                    break;
                case EnterKind.DamageEnemy1:
                    {
                        V06Unit t = BestDamageTarget(e, 1 - owner.Camp, false);
                        if (t != null) { e.EffectDamage(t, 1, u.Name + " 入场"); }
                    }
                    break;
                case EnterKind.DamageEnemyFront1:
                    {
                        V06Unit t = BestDamageTarget(e, 1 - owner.Camp, true);
                        if (t != null) { e.EffectDamage(t, 1, u.Name + " 入场"); }
                    }
                    break;
                case EnterKind.SpawnToken:
                    {
                        string tok = EnterToken(u.CardId);
                        if (tok != null) { e.SpawnToken(owner.Camp, tok); }
                    }
                    break;
                case EnterKind.BuffFriendlyAtk1:
                    {
                        V06Unit t = BestAttacker(e, owner.Camp);
                        if (t != null) { t.TempAtk += 1; e.Log.Add(u.Name + " 使 " + t.Name + " 本轮攻击 +1"); }
                    }
                    break;
                case EnterKind.PeekTwo:
                    e.Log.Add(u.Name + " 查看牌库顶 2 张（AI 不改变顺序）");
                    break;
                case EnterKind.SelfDamage1:
                    e.EffectDamage(u, 1, u.Name + " 入场");
                    break;
                case EnterKind.RepairConstruct2:
                    {
                        V06Unit t = BestHealTarget(e, owner.Camp, true, u.Uid);
                        if (t != null) { e.Heal(t, 2); e.Log.Add(u.Name + " 维修 " + t.Name + " 2"); }
                        // 同时带"生成"的维修单位(M04/G07):维修之后继续走生成
                        string tok = EnterToken(u.CardId);
                        if (tok != null) { e.SpawnToken(owner.Camp, tok); e.Log.Add(u.Name + " 生成 " + tok); }
                    }
                    break;
                case EnterKind.DragonFear:
                    {
                        V06Unit t = BestAttacker(e, 1 - owner.Camp);
                        if (t != null) { t.DragonFearThisRound = true; e.Log.Add(u.Name + " 龙威：" + t.Name + " 本轮下次主动攻击 −1"); }
                    }
                    break;
            }
        }

        // ───────────────────────── 目标挑选（AI 用） ─────────────────────────

        public static V06Unit BestDamageTarget(V06Engine e, int camp, bool frontOnly)
        {
            V06Player side = e.Players[camp];
            V06Unit best = null;
            for (int s = 0; s < V06Slot.Total; s++)
            {
                if (frontOnly && !V06Slot.IsFront(s)) { continue; }
                V06Unit u = side.At(s);
                if (u == null || u.SneakActive) { continue; }
                if (best == null) { best = u; continue; }
                // 优先能一击打死的最低生命，其次攻击最高
                bool killNew = u.CurHp <= 1 && (best.CurHp > 1);
                if (killNew || (u.CurHp < best.CurHp) || (u.CurHp == best.CurHp && u.AtkBaseNow > best.AtkBaseNow)) { best = u; }
            }
            return best;
        }

        public static V06Unit BestHealTarget(V06Engine e, int camp, bool constructOnly, int excludeUid)
        {
            V06Player side = e.Players[camp];
            V06Unit best = null;
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = side.At(s);
                if (u == null || u.Uid == excludeUid) { continue; }
                if (constructOnly && !IsConstruct(u)) { continue; }
                if (u.Damage <= 0) { continue; }
                if (best == null || u.Damage > best.Damage) { best = u; }
            }
            return best;
        }

        public static V06Unit BestAttacker(V06Engine e, int camp)
        {
            // 敌方单体指向(龙威等)不能选潜行单位
            V06Player side = e.Players[camp];
            V06Unit best = null;
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = side.At(s);
                if (u == null || u.SneakActive) { continue; }
                bool canAtk = V06Slot.IsFront(s) || u.Has("远射") || u.Has("飞行");
                if (!canAtk || u.AttackedThisRound) { continue; }
                if (best == null || u.AtkBaseNow > best.AtkBaseNow) { best = u; }
            }
            if (best == null)
            {
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = side.At(s);
                    if (u == null || u.SneakActive) { continue; }
                    if (best == null || u.AtkBaseNow > best.AtkBaseNow) { best = u; }
                }
            }
            return best;
        }

        public static bool IsConstruct(V06Unit u)
        {
            return u != null && u.TagText != null && (u.TagText.Contains("构装") || u.TagText.Contains("机甲"));
        }

        public static bool IsTroll(V06Unit u)
        {
            return u != null && u.TagText != null && u.TagText.Contains("巨魔");
        }

        // ───────────────────────── 战术规格 ─────────────────────────

        public enum Trg { None, FriendlyUnit, FriendlyInjured, FriendlyConstruct, FriendlyInjuredConstruct, EnemyUnit, EnemyBack, TwoTargets }

        public sealed class TacticSpec
        {
            public string Id;
            public Trg Target;
            public int Cost;
        }

        private static readonly Dictionary<string, TacticSpec> Specs = new Dictionary<string, TacticSpec>();

        private static void Add(string id, Trg trg)
        {
            V06Card c = V06Catalog.ById(id);
            if (c == null) { return; }
            TacticSpec s = new TacticSpec();
            s.Id = id; s.Target = trg; s.Cost = c.Cost;
            Specs[id] = s;
        }

        private static TacticSpec GetSpec(string id)
        {
            if (Specs.Count == 0)
            {
                Add("S-H01", Trg.FriendlyUnit); Add("S-H02", Trg.FriendlyInjured); Add("S-H03", Trg.None); Add("S-H04", Trg.FriendlyUnit);
                Add("S-E01", Trg.EnemyUnit); Add("S-E02", Trg.EnemyUnit); Add("S-E03", Trg.FriendlyInjured); Add("S-E04", Trg.FriendlyUnit);
                Add("S-D01", Trg.EnemyUnit); Add("S-D02", Trg.FriendlyInjured); Add("S-D03", Trg.None); Add("S-D04", Trg.None);
                Add("S-G01", Trg.None); Add("S-G02", Trg.None); Add("S-G03", Trg.None); Add("S-G04", Trg.FriendlyInjured);
                Add("S-V01", Trg.EnemyUnit); Add("S-V02", Trg.TwoTargets); Add("S-V03", Trg.FriendlyUnit); Add("S-V04", Trg.None);
                Add("S-O01", Trg.FriendlyUnit); Add("S-O02", Trg.None); Add("S-O03", Trg.EnemyBack); Add("S-O04", Trg.FriendlyInjured);
                Add("S-R01", Trg.None); Add("S-R02", Trg.FriendlyUnit); Add("S-R03", Trg.None); Add("S-R04", Trg.EnemyUnit);
                Add("S-M01", Trg.FriendlyConstruct); Add("S-M02", Trg.FriendlyInjuredConstruct); Add("S-M03", Trg.None); Add("S-M04", Trg.EnemyUnit);
            }
            TacticSpec r;
            return Specs.TryGetValue(id, out r) ? r : null;
        }

        private static List<V06Unit> TargetsOf(V06Engine e, int camp, Trg trg)
        {
            List<V06Unit> r = new List<V06Unit>();
            V06Player mine = e.Players[camp];
            V06Player foe = e.Players[1 - camp];
            switch (trg)
            {
                case Trg.FriendlyUnit:
                    return mine.Units();
                case Trg.FriendlyInjured:
                    for (int i = 0; i < V06Slot.Total; i++) { V06Unit u = mine.At(i); if (u != null && u.Damage > 0) { r.Add(u); } }
                    return r;
                case Trg.FriendlyConstruct:
                    for (int i = 0; i < V06Slot.Total; i++) { V06Unit u = mine.At(i); if (u != null && IsConstruct(u)) { r.Add(u); } }
                    return r;
                case Trg.FriendlyInjuredConstruct:
                    for (int i = 0; i < V06Slot.Total; i++) { V06Unit u = mine.At(i); if (u != null && IsConstruct(u) && u.Damage > 0) { r.Add(u); } }
                    return r;
                case Trg.EnemyUnit:
                    for (int i = 0; i < V06Slot.Total; i++) { V06Unit u = foe.At(i); if (u != null && !u.SneakActive) { r.Add(u); } }
                    return r;
                case Trg.EnemyBack:
                    for (int i = V06Slot.FrontCount; i < V06Slot.Total; i++) { V06Unit u = foe.At(i); if (u != null && !u.SneakActive) { r.Add(u); } }
                    return r;
            }
            return r;
        }

        // ───────────────────────── 战术：可用性与枚举 ─────────────────────────

        public static bool CanPlay(V06Engine e, int camp, V06Card c)
        {
            if (c == null) { return false; }
            if (c.Category != V06Category.Tactic)
            {
                // 装备/附印/战况/晋阶/支援/响应交给第二层
                if (V06Effects2.Handles(c.Id)) { return V06Effects2.CanPlay(e, camp, c); }
                return false;
            }
            TacticSpec s = GetSpec(c.Id);
            if (s == null) { return false; }
            V06Player p = e.Players[camp];
            if (c.Cost > p.ActionPoints) { return false; }
            switch (c.Id)
            {
                case "S-D03":
                    // 从牌库检索装备；预组牌库不含装备 → 不可使用
                    return DeckHas(p, V06Category.Equipment);
                case "S-G02": case "S-R03":
                    if (!p.AnyEmptySlot()) { return false; }
                    break;
                case "S-O03":
                    if (TargetsOf(e, camp, Trg.EnemyBack).Count == 0) { return false; }
                    if (!EnemyFrontHasSpace(e, camp)) { return false; }
                    break;
                case "S-G03":
                    if (!DiscardHasConstructOrEquip(e.Players[camp])) { return false; }
                    break;
                case "S-V03":
                    // 需要"某个己方单位 + 另一排的空格"同时存在，否则这张牌无从落位
                    if (!HasOtherRowSpace(e.Players[camp])) { return false; }
                    break;
            }
            if (s.Target == Trg.TwoTargets)
            {
                return TargetsOf(e, camp, Trg.FriendlyUnit).Count >= 2;
            }
            if (s.Target != Trg.None && TargetsOf(e, camp, s.Target).Count == 0) { return false; }
            return true;
        }

        private static bool DeckHas(V06Player p, V06Category cat)
        {
            if (p == null) { return false; }
            for (int i = 0; i < p.Deck.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Deck[i]);
                if (c != null && c.Category == cat) { return true; }
            }
            return false;
        }

        private static bool DiscardHasConstructOrEquip(V06Player p)
        {
            for (int i = 0; i < p.Discard.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Discard[i]);
                if (c == null) { continue; }
                if (c.Category == V06Category.Equipment || c.Tags != null && c.Tags.Contains("构装")) { return true; }
            }
            return false;
        }

        private static bool EnemyFrontHasSpace(V06Engine e, int camp)
        {
            V06Player foe = e.Players[1 - camp];
            for (int s = 0; s < V06Slot.FrontCount; s++) { if (foe.At(s) == null) { return true; } }
            return false;
        }

        public static void EnumeratePlays(V06Engine e, int camp, V06Card c, List<V06Action> list)
        {
            if (c.Category != V06Category.Tactic && V06Effects2.Handles(c.Id))
            {
                V06Effects2.EnumeratePlays(e, camp, c, list);
                return;
            }
            TacticSpec s = GetSpec(c.Id);
            if (s == null) { return; }
            if (s.Target == Trg.None)
            {
                list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id });
                return;
            }
            if (s.Target == Trg.TwoTargets)
            {
                List<V06Unit> fs = TargetsOf(e, camp, Trg.FriendlyUnit);
                for (int i = 0; i < fs.Count; i++)
                {
                    for (int j = 0; j < fs.Count; j++)
                    {
                        if (i == j) { continue; }
                        list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = fs[i].Uid, TargetUid = fs[j].Uid });
                    }
                }
                return;
            }
            List<V06Unit> tg = TargetsOf(e, camp, s.Target);
            // S-V03 红雾换位：落点由玩家选，所以每个"单位 × 对面空格"都是一个可选方案，
            // 不能只给单位、再由引擎自己挑空格（那样玩家决定不了换到哪一格）。
            if (c.Id == "S-V03")
            {
                V06Player pv3 = e.Players[camp];
                for (int i = 0; i < tg.Count; i++)
                {
                    V06Unit u = tg[i];
                    if (u == null) { continue; }
                    int lo = V06Slot.IsFront(u.Slot) ? V06Slot.FrontCount : 0;
                    int hi = V06Slot.IsFront(u.Slot) ? V06Slot.Total : V06Slot.FrontCount;
                    for (int sl = lo; sl < hi; sl++)
                    {
                        if (pv3.At(sl) != null) { continue; }
                        list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = u.Uid, TargetSlot = sl });
                    }
                }
                return;
            }
            for (int i = 0; i < tg.Count; i++)
            {
                list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = tg[i].Uid });
            }
        }

        /// <summary>场上是否存在"某己方单位 + 其对面一排的空格"（S-V03 这类需要落点的牌判断可用性）。</summary>
        private static bool HasOtherRowSpace(V06Player p)
        {
            if (p == null) { return false; }
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = p.At(s);
                if (u == null) { continue; }
                int lo = V06Slot.IsFront(u.Slot) ? V06Slot.FrontCount : 0;
                int hi = V06Slot.IsFront(u.Slot) ? V06Slot.Total : V06Slot.FrontCount;
                for (int t = lo; t < hi; t++) { if (p.At(t) == null) { return true; } }
            }
            return false;
        }

        // ───────────────────────── 战术：结算 ─────────────────────────

        public static bool Play(V06Engine e, int camp, V06Action a, out string error)
        {
            error = string.Empty;
            V06Card c = V06Catalog.ById(a.CardId);
            if (c == null) { error = "未知卡牌"; return false; }
            if (c.Category != V06Category.Tactic)
            {
                if (V06Effects2.Handles(c.Id)) { return V06Effects2.Play(e, camp, a, out error); }
                error = "未实现：" + a.CardId;
                return false;
            }
            TacticSpec s = GetSpec(c.Id);
            if (s == null) { error = "未实现：" + a.CardId; return false; }
            V06Player p = e.Players[camp];
            V06Player foe = e.Players[1 - camp];
            if (p.Hand.IndexOf(c.Id) < 0) { error = "不在手牌"; return false; }
            if (c.Cost > p.ActionPoints) { error = "行动点不足"; return false; }
            V06Unit t1 = a.Uid >= 0 ? p.ByUid(a.Uid) : null;
            V06Unit t2 = a.TargetUid >= 0 ? p.ByUid(a.TargetUid) : null;
            V06Unit e1 = a.Uid >= 0 ? foe.ByUid(a.Uid) : null;
            V06Unit e2 = a.TargetUid >= 0 ? foe.ByUid(a.TargetUid) : null;

            p.Hand.Remove(c.Id);
            p.Discard.Add(c.Id);
            p.ActionPoints -= c.Cost;
            p.MainActionsUsed++;
            e.Log.Add("方 " + camp + " 使用战术 " + c.Name);

            switch (c.Id)
            {
                // 人类
                case "S-H01": if (t1 != null) { t1.TempAtk += 2; } break;
                case "S-H02": if (t1 != null) { e.Heal(t1, 3); } break;
                case "S-H03": e.Draw(camp, 2); break;
                case "S-H04":
                    if (t1 != null)
                    {
                        t1.AddKw("护卫");
                        int slot = FrontEmptyOf(p);
                        if (slot != V06Slot.None && V06Slot.IsBack(t1.Slot)) { MoveUnit(p, t1, slot); }
                    }
                    break;
                // 精灵
                case "S-E01": if (e1 != null) { e1.TempAtk -= 2; } break;
                case "S-E02": if (e1 != null) { e.EffectDamage(e1, 4, c.Name); } break;
                case "S-E03": if (t1 != null) { e.Heal(t1, 4); } break;
                case "S-E04":
                    if (t1 != null) { MoveToOtherRow(p, t1); }
                    e.Draw(camp, 1);
                    break;
                // 矮人
                case "S-D01": if (e1 != null) { e.EffectDamage(e1, e.HasEffectiveKeyword(e1, "坚壁") ? 4 : 3, c.Name); } break;
                case "S-D02":
                    if (t1 != null) { e.Heal(t1, IsConstruct(t1) ? 4 : 2); }
                    break;
                case "S-D03":
                    {
                        // 检索装备入手（预组无装备，通常不可用）
                        int idx = -1;
                        for (int i = 0; i < p.Deck.Count; i++)
                        {
                            V06Card dc = V06Catalog.ById(p.Deck[i]);
                            if (dc != null && dc.Category == V06Category.Equipment) { idx = i; break; }
                        }
                        if (idx >= 0) { p.Hand.Add(p.Deck[idx]); p.Deck.RemoveAt(idx); }
                    }
                    break;
                case "S-D04": p.FrontReduceThisRound = 1; break;
                // 哥布林
                case "S-G01":
                    for (int i = 0; i < V06Slot.FrontCount; i++) { V06Unit u = foe.At(i); if (u != null) { e.EffectDamage(u, 2, c.Name); } }
                    break;
                case "S-G02":
                    e.SpawnToken(camp, "T03");
                    e.SpawnToken(camp, "T03");
                    break;
                case "S-G03":
                    {
                        int idx = -1;
                        for (int i = 0; i < p.Discard.Count; i++)
                        {
                            V06Card dc = V06Catalog.ById(p.Discard[i]);
                            if (dc == null) { continue; }
                            if (dc.Category == V06Category.Equipment || (dc.Tags != null && dc.Tags.Contains("构装"))) { idx = i; break; }
                        }
                        if (idx >= 0) { p.Deck.Add(p.Discard[idx]); p.Discard.RemoveAt(idx); }
                        e.Draw(camp, 1);
                    }
                    break;
                case "S-G04": if (t1 != null) { e.Heal(t1, IsTroll(t1) ? 5 : 3); } break;
                // 血族
                case "S-V01": if (e1 != null) { e.EffectDamage(e1, 3, c.Name); } break;
                case "S-V02":
                    if (t1 != null && t2 != null)
                    {
                        e.EffectDamage(t1, 1, c.Name);
                        e.Heal(t2, 4);
                    }
                    break;
                case "S-V03":
                    if (t1 != null)
                    {
                        // 落位由玩家指定（a.TargetSlot，必须是对面一排的空格）；
                        // 没指定时才退回"另一排第一个空格"。以前无条件自动挑空格，玩家选不了落点。
                        if (a.TargetSlot >= 0 && p.At(a.TargetSlot) == null
                            && V06Slot.IsFront(a.TargetSlot) != V06Slot.IsFront(t1.Slot))
                        {
                            MoveUnit(p, t1, a.TargetSlot);
                        }
                        else if (a.TargetSlot < 0)
                        {
                            MoveToOtherRow(p, t1);
                        }
                        if (!t1.AttackedThisRound) { t1.SneakActive = true; }
                    }
                    break;
                case "S-V04": e.Draw(camp, 2); break;
                // 荒原
                case "S-O01": if (t1 != null) { t1.TempAtk += 1; t1.AddKw("穿透"); } break;
                case "S-O02":
                    for (int i = 0; i < V06Slot.FrontCount; i++) { V06Unit u = p.At(i); if (u != null) { u.TempAtk += 1; } }
                    break;
                case "S-O03":
                    {
                        V06Unit back = e1;
                        if (back != null && V06Slot.IsBack(back.Slot))
                        {
                            int slot = FrontEmptyOf(foe);
                            if (slot != V06Slot.None) { MoveUnit(foe, back, slot); }
                        }
                    }
                    break;
                case "S-O04": if (t1 != null) { e.Heal(t1, 3); } e.Draw(camp, 1); break;
                // 龙群
                case "S-R01":
                    for (int i = 0; i < V06Slot.FrontCount; i++) { V06Unit u = foe.At(i); if (u != null) { e.EffectDamage(u, 2, c.Name); } }
                    break;
                case "S-R02": if (t1 != null) { t1.ReduceThisRound += 2; } break;
                case "S-R03": e.SpawnToken(camp, "T07"); e.Draw(camp, 1); break;
                case "S-R04": if (e1 != null) { e1.TempAtk -= 3; } break;
                // 铁庭
                case "S-M01": if (t1 != null && IsConstruct(t1)) { t1.OverloadThisRound = true; } break;
                case "S-M02": if (t1 != null) { e.Heal(t1, 5); } break;
                case "S-M03":
                    {
                        e.Draw(camp, 2);
                        if (p.Hand.Count > 0)
                        {
                            // 放回最贵的一张到牌库底
                            int worst = 0;
                            int cbest = -1;
                            for (int i = 0; i < p.Hand.Count; i++)
                            {
                                V06Card hc = V06Catalog.ById(p.Hand[i]);
                                int cost = hc == null ? 0 : hc.Cost;
                                if (cost > cbest) { cbest = cost; worst = i; }
                            }
                            p.Deck.Add(p.Hand[worst]);
                            p.Hand.RemoveAt(worst);
                        }
                    }
                    break;
                case "S-M04": if (e1 != null) { e.EffectDamage(e1, 4, c.Name); } break;
                default:
                    error = "未实现：" + c.Id;
                    return false;
            }
            e.CleanupDeaths();
            e.AfterNonUnitAction();
            return true;
        }

        private static int FrontEmptyOf(V06Player p)
        {
            for (int i = 0; i < V06Slot.FrontCount; i++) { if (p.At(i) == null) { return i; } }
            return V06Slot.None;
        }

        private static int OtherRowEmpty(V06Player p, V06Unit u)
        {
            bool fromFront = V06Slot.IsFront(u.Slot);
            int lo = fromFront ? V06Slot.FrontCount : 0;
            int hi = fromFront ? V06Slot.Total : V06Slot.FrontCount;
            for (int i = lo; i < hi; i++) { if (p.At(i) == null) { return i; } }
            return V06Slot.None;
        }

        private static void MoveUnit(V06Player p, V06Unit u, int slot)
        {
            if (p.At(slot) != null) { return; }
            p.Slots[u.Slot] = null;
            p.Slots[slot] = u;
            u.Slot = slot;
        }

        private static void MoveToOtherRow(V06Player p, V06Unit u)
        {
            int slot = OtherRowEmpty(p, u);
            if (slot != V06Slot.None) { MoveUnit(p, u, slot); }
        }

        // ───────────────────────── 响应（预组牌库不含响应牌） ─────────────────────────

        public static bool ResponseLegal(V06Engine e, int camp, V06Card c, V06Action pending)
        {
            return V06Effects2.ResponseLegal(e, camp, c, pending);
        }

        public static int ResponseDamageReduce(V06Engine e, int camp, V06Card c, V06Action pending)
        {
            return V06Effects2.ResponseDamageReduce(e, camp, c, pending);
        }
    }
}
