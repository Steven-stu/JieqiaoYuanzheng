using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// V0.6 第二层效果：装备 / 附印 / 战况 / 晋阶 / 支援 / 响应。
    /// 与 V06Effects（单位 + 战术）并列，由 V06Effects 的三个入口分派进来。
    ///
    /// 诚实口径：
    ///   · 装备 12、附印 8、战况 6、晋阶 12、支援 12：全部实现；
    ///   · 响应 6：X01/X03/X05 走攻击响应窗口；X02/X04/X06 需要"战术响应窗口"，
    ///     本层尚未接入，登记为未实现（界面不会让它们被使用）。
    /// </summary>
    public static class V06Effects2
    {
        public static readonly HashSet<string> Pending = new HashSet<string>();

        public static void Register()
        {
            if (Pending.Count > 0) { return; }
            Pending.Add("X02");   // 法术偏转：需要战术响应窗口
            Pending.Add("X04");   // 封住引线：需要战术响应窗口
            Pending.Add("X06");   // 切断过载：需要战术响应窗口
        }

        public static bool Handles(string id)
        {
            Register();   // 必须先登记"待做"清单，否则 X02/X04/X06 会被误判为已实现
            if (string.IsNullOrEmpty(id) || id.Length < 2) { return false; }
            char k = id[0];
            if (k == 'Q' || k == 'A' || k == 'F' || k == 'J' || k == 'P') { return true; }
            if (k == 'X') { return !Pending.Contains(id); }
            return false;
        }

        // ───────────────────────── 通用 ─────────────────────────

        private static bool IsUnit(V06Card c) { return c != null && c.Category == V06Category.Unit; }

        private static List<V06Unit> MyUnits(V06Engine e, int camp)
        {
            return e.Players[camp].Units();
        }

        private static List<V06Unit> EnemyUnits(V06Engine e, int camp)
        {
            List<V06Unit> r = new List<V06Unit>();
            V06Player foe = e.Players[1 - camp];
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = foe.At(s);
                if (u != null && !u.SneakActive) { r.Add(u); }
            }
            return r;
        }

        private static int OtherRowEmpty(V06Player p, V06Unit u)
        {
            bool fromFront = V06Slot.IsFront(u.Slot);
            int lo = fromFront ? V06Slot.FrontCount : 0;
            int hi = fromFront ? V06Slot.Total : V06Slot.FrontCount;
            for (int i = lo; i < hi; i++) { if (p.At(i) == null) { return i; } }
            return V06Slot.None;
        }

        public static void MoveUnit(V06Player p, V06Unit u, int slot)
        {
            if (p == null || u == null || slot < 0) { return; }
            if (p.At(slot) != null) { return; }
            p.Slots[u.Slot] = null;
            p.Slots[slot] = u;
            u.Slot = slot;
        }

        // ───────────────────────── 可用性 ─────────────────────────

        public static bool CanPlay(V06Engine e, int camp, V06Card c)
        {
            Register();
            if (c == null) { return false; }
            if (!Handles(c.Id)) { return false; }
            V06Player p = e.Players[camp];
            if (c.Cost < 0 || c.Cost > p.ActionPoints) { return false; }
            char k = c.Id[0];
            switch (k)
            {
                case 'Q':   // 装备：需一个己方单位（Q11 仅构装）
                    {
                        List<V06Unit> mine = MyUnits(e, camp);
                        for (int i = 0; i < mine.Count; i++)
                        {
                            if (c.Id == "Q11" && !V06Effects.IsConstruct(mine[i])) { continue; }
                            return true;
                        }
                        return false;
                    }
                case 'A':   // 附印：同上，A08 仅构装
                    {
                        List<V06Unit> mine = MyUnits(e, camp);
                        for (int i = 0; i < mine.Count; i++)
                        {
                            if (c.Id == "A08" && !V06Effects.IsConstruct(mine[i])) { continue; }
                            return true;
                        }
                        return false;
                    }
                case 'F':   // 战况：己方场上至多 1 张
                    return true;
                case 'J':   // 晋阶：需要一个符合对象的未晋阶单位
                    return TargetUnitsForUpgrade(e, camp, c.Id).Count > 0;
                case 'P':   // 支援
                    switch (c.Id)
                    {
                        case "P02": case "P04": case "P10":
                            return InjuredTargets(e, camp, c.Id == "P10").Count > 0;
                        case "P07":
                            {
                                List<V06Unit> mine = MyUnits(e, camp);
                                for (int i = 0; i < mine.Count; i++) { if (mine[i].Damage > 0) { return true; } }
                                return false;
                            }
                        case "P03": return EnemyUnits(e, camp).Count > 0;
                        case "P06": case "P09": return p.AnyEmptySlot();
                        case "P08": return p.AnyEmptySlot();
                        case "P01": return DeckCheapUnitCount(p) > 0;
                        case "P05": return DiscardEquipmentCount(p) > 0;
                        case "P11": return DiscardUnitKinds(p) >= 2;
                        case "P12": return true;
                    }
                    return false;
                case 'X':
                    return false;   // 响应只在响应窗口内由引擎提供
            }
            return false;
        }

        private static List<V06Unit> InjuredTargets(V06Engine e, int camp, bool constructOnly)
        {
            List<V06Unit> r = new List<V06Unit>();
            List<V06Unit> mine = MyUnits(e, camp);
            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i].Damage <= 0) { continue; }
                if (constructOnly && !V06Effects.IsConstruct(mine[i])) { continue; }
                r.Add(mine[i]);
            }
            return r;
        }

        private static int DeckCheapUnitCount(V06Player p)
        {
            int n = 0;
            for (int i = 0; i < p.Deck.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Deck[i]);
                if (c != null && c.Category == V06Category.Unit && c.Cost <= 3) { n++; }
            }
            return n;
        }

        private static int DiscardEquipmentCount(V06Player p)
        {
            int n = 0;
            for (int i = 0; i < p.Discard.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Discard[i]);
                if (c != null && c.Category == V06Category.Equipment) { n++; }
            }
            return n;
        }

        private static int DiscardUnitKinds(V06Player p)
        {
            HashSet<string> set = new HashSet<string>();
            for (int i = 0; i < p.Discard.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Discard[i]);
                if (c != null && c.Category == V06Category.Unit) { set.Add(c.Name); }
            }
            return set.Count;
        }

        /// <summary>某张晋阶牌现在能指定的己方单位。</summary>
        public static List<V06Unit> TargetUnitsForUpgrade(V06Engine e, int camp, string upgradeId)
        {
            List<V06Unit> r = new List<V06Unit>();
            V06Player p = e.Players[camp];
            List<V06Unit> mine = MyUnits(e, camp);
            for (int i = 0; i < mine.Count; i++)
            {
                V06Unit u = mine[i];
                if (u.Upgraded || u.IsToken) { continue; }
                if (p.UpgradedCards.Contains(u.CardId)) { continue; }
                if (!UpgradeAllows(upgradeId, u)) { continue; }
                r.Add(u);
            }
            return r;
        }

        private static bool UpgradeAllows(string upgradeId, V06Unit u)
        {
            V06Card baseCard = V06Catalog.ById(u.CardId);
            int cost = baseCard == null ? 0 : baseCard.Cost;
            switch (upgradeId)
            {
                case "J01": case "J02": return cost <= 3;
                case "J03": return u.Has("远射");
                case "J04": return u.Has("突袭");
                case "J05": return u.CardId == "U-H02";
                case "J06": return u.CardId == "U-E01";
                case "J07": return u.CardId == "U-D02";
                case "J08": return u.CardId == "U-G01";
                case "J09": return u.CardId == "U-V02";
                case "J10": return u.CardId == "U-O02";
                case "J11": return u.CardId == "U-R02";
                case "J12": return u.CardId == "U-M02";
            }
            return false;
        }

        // ───────────────────────── 行动枚举 ─────────────────────────

        public static void EnumeratePlays(V06Engine e, int camp, V06Card c, List<V06Action> list)
        {
            Register();
            if (c == null || !Handles(c.Id)) { return; }
            char k = c.Id[0];
            switch (k)
            {
                case 'Q':
                case 'A':
                    {
                        List<V06Unit> mine = MyUnits(e, camp);
                        for (int i = 0; i < mine.Count; i++)
                        {
                            if (c.Id == "Q11" && !V06Effects.IsConstruct(mine[i])) { continue; }
                            if (c.Id == "A08" && !V06Effects.IsConstruct(mine[i])) { continue; }
                            list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = mine[i].Uid });
                        }
                        return;
                    }
                case 'F':
                    list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id });
                    return;
                case 'J':
                    {
                        List<V06Unit> tg = TargetUnitsForUpgrade(e, camp, c.Id);
                        for (int i = 0; i < tg.Count; i++)
                        {
                            list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = tg[i].Uid });
                        }
                        return;
                    }
                case 'P':
                    {
                        if (c.Id == "P02" || c.Id == "P04")
                        {
                            List<V06Unit> tg = InjuredTargets(e, camp, false);
                            for (int i = 0; i < tg.Count; i++) { list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = tg[i].Uid }); }
                            return;
                        }
                        if (c.Id == "P10")
                        {
                            List<V06Unit> tg = InjuredTargets(e, camp, true);
                            for (int i = 0; i < tg.Count; i++) { list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = tg[i].Uid }); }
                            return;
                        }
                        if (c.Id == "P03")
                        {
                            List<V06Unit> tg = EnemyUnits(e, camp);
                            for (int i = 0; i < tg.Count; i++) { list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id, Uid = tg[i].Uid }); }
                            return;
                        }
                        list.Add(new V06Action { Kind = V06ActionKind.PlayCard, CardId = c.Id });
                        return;
                    }
            }
        }

        // ───────────────────────── 结算 ─────────────────────────

        public static bool Play(V06Engine e, int camp, V06Action a, out string error)
        {
            error = string.Empty;
            Register();
            V06Card c = V06Catalog.ById(a.CardId);
            if (c == null || !Handles(c.Id)) { error = "不归本层处理"; return false; }
            V06Player p = e.Players[camp];
            if (c.Cost > p.ActionPoints) { error = "行动点不足"; return false; }
            V06Unit mine = a.Uid >= 0 ? p.ByUid(a.Uid) : null;
            V06Unit enemy = a.Uid >= 0 ? e.Players[1 - camp].ByUid(a.Uid) : null;

            // 先从手牌或预备区移除并付费用
            if (a.FromReserve)
            {
                if (!p.Reserve.Contains(c.Id) || p.ReserveUsed.Contains(c.Id)) { error = "预备牌不可用"; return false; }
                p.Reserve.Remove(c.Id);
                p.ReserveUsed.Add(c.Id);
            }
            else
            {
                if (p.Hand.IndexOf(c.Id) < 0) { error = "不在手牌"; return false; }
                p.Hand.Remove(c.Id);
                // 归属：装备/附印挂在单位上、战况挂在玩家上、晋阶牌移出本局、支援结算后进弃牌区。
                // 挂上去的牌不能同时留在弃牌区，否则卡牌守恒会翻倍（由不变量自检抓）。
                char kind = c.Id[0];
                if (kind == 'P') { p.Discard.Add(c.Id); }
                else if (kind == 'J') { p.Exile.Add(c.Id); }
            }
            p.ActionPoints -= c.Cost;
            p.MainActionsUsed++;
            e.Log.Add("方 " + camp + " 使用 " + CategoryName(c.Category) + " " + c.Name);

            char k = c.Id[0];
            switch (k)
            {
                case 'Q': ApplyEquipment(e, p, mine, c); break;
                case 'A': ApplySeal(e, p, mine, c); break;
                case 'F': ApplyBattlefield(e, p, c); break;
                case 'J': ApplyUpgrade(e, p, mine, c); break;
                case 'P': ApplySupport(e, p, c, mine, enemy); break;
                default: error = "未实现：" + c.Id; return false;
            }
            e.CleanupDeaths();
            return true;
        }

        private static string CategoryName(V06Category cat)
        {
            switch (cat)
            {
                case V06Category.Equipment: return "装备";
                case V06Category.Seal: return "附印";
                case V06Category.Status: return "战况";
                case V06Category.Upgrade: return "晋阶";
                case V06Category.Support: return "支援";
                case V06Category.Response: return "响应";
            }
            return cat.ToString();
        }

        private static void ApplyEquipment(V06Engine e, V06Player p, V06Unit u, V06Card c)
        {
            if (u == null) { return; }
            if (!string.IsNullOrEmpty(u.EquipId)) { p.Discard.Add(u.EquipId); e.Log.Add("替换装备：" + u.EquipId + " 弃置"); }
            u.EquipId = c.Id;
            switch (c.Id)
            {
                case "Q01": u.AtkPerm += 1; break;
                case "Q02": u.HpPerm += 2; break;
                case "Q03": u.AddKw("远射"); break;
                case "Q04": u.AtkPerm += 2; break;
                case "Q05": u.ReduceThisRound += 0; u.HpPerm += 0; u.AddKw("林纹披风"); break;
                case "Q06": u.AddKw("坚壁"); break;
                case "Q07": u.AddKw("银纹护符"); break;
                case "Q08": u.AtkPerm += 1; u.AddKw("汲取"); break;
                case "Q09": u.AddKw("协攻"); break;
                case "Q10": u.HpPerm += 3; break;
                case "Q11": u.AddKw("飞行"); break;
                case "Q12": u.AddKw("驯兽铃"); break;
            }
            e.Log.Add(u.Name + " 装备 " + c.Name);
        }

        private static void ApplySeal(V06Engine e, V06Player p, V06Unit u, V06Card c)
        {
            if (u == null) { return; }
            if (!string.IsNullOrEmpty(u.SealId)) { p.Discard.Add(u.SealId); e.Log.Add("替换附印：" + u.SealId + " 弃置"); }
            u.SealId = c.Id;
            switch (c.Id)
            {
                case "A01": u.AddKw("护卫"); break;
                case "A02": u.AddKw("再生"); break;
                case "A03": u.AddKw("穿透"); break;
                case "A04": u.AddKw("余烬"); break;
                case "A05": u.AddKw("夜行"); break;
                case "A06": u.AtkPerm += 1; break;
                case "A07": u.HpPerm += 2; if (u.Damage > 0) { u.Damage -= 1; } break;
                case "A08": u.AddKw("机核"); break;
            }
            e.Log.Add(u.Name + " 附印 " + c.Name);
        }

        private static void ApplyBattlefield(V06Engine e, V06Player p, V06Card c)
        {
            if (!string.IsNullOrEmpty(p.BattlefieldId) && p.BattlefieldId != c.Id)
            {
                p.Discard.Add(p.BattlefieldId);
                e.Log.Add("替换战况：" + p.BattlefieldId + " 弃置");
            }
            p.BattlefieldId = c.Id;
            p.FirstHealUsed = false;
            p.FirstDrainUsed = false;
            p.FirstAssistUsed = false;
            e.Log.Add("方 " + p.Camp + " 战况：" + c.Name);
        }

        private static void ApplyUpgrade(V06Engine e, V06Player p, V06Unit u, V06Card c)
        {
            if (u == null) { e.Log.Add("晋阶目标不存在，效果跳过"); return; }
            V06Card baseCard = V06Catalog.ById(u.CardId);
            int keepDamage = u.Damage;
            u.Upgraded = true;
            u.UpgradeCardId = c.Id;
            // 替换基础攻生，保留能力/装备/伤害/攻击次数
            u.AtkBase = c.Atk;
            u.HpBase = c.Hp;
            u.Damage = keepDamage;
            if (!p.UpgradedCards.Contains(u.CardId)) { p.UpgradedCards.Add(u.CardId); }
            switch (c.Id)
            {
                case "J02": u.AddKw("护卫"); break;
                case "J09": u.AddKw("夜行"); break;
            }
            e.Log.Add(u.Name + " 晋阶为 " + c.Name + "（" + c.Atk + "/" + c.Hp + "）");
            if (baseCard != null) { e.Log.Add("  基础卡 " + baseCard.Name + " 保留能力与装备"); }
        }

        private static void ApplySupport(V06Engine e, V06Player p, V06Card c, V06Unit mine, V06Unit enemy)
        {
            switch (c.Id)
            {
                case "P01":
                    {
                        int best = -1, bestScore = -1;
                        for (int i = 0; i < p.Deck.Count; i++)
                        {
                            V06Card dc = V06Catalog.ById(p.Deck[i]);
                            if (dc == null || dc.Category != V06Category.Unit || dc.Cost > 3) { continue; }
                            int sc = dc.Atk * 2 + dc.Hp;
                            if (sc > bestScore) { bestScore = sc; best = i; }
                        }
                        if (best >= 0) { p.Hand.Add(p.Deck[best]); p.Deck.RemoveAt(best); e.Log.Add("公会增援：检索入手"); }
                    }
                    break;
                case "P02":
                    if (mine != null) { e.Heal(mine, 4); }
                    break;
                case "P03":
                    if (enemy != null) { e.EffectDamage(enemy, 3, c.Name); }
                    break;
                case "P04":
                    if (mine != null)
                    {
                        int slot = OtherRowEmpty(p, mine);
                        if (slot != V06Slot.None) { MoveUnit(p, mine, slot); }
                        e.Heal(mine, 2);
                    }
                    break;
                case "P05":
                    {
                        int idx = -1;
                        for (int i = 0; i < p.Discard.Count; i++)
                        {
                            V06Card dc = V06Catalog.ById(p.Discard[i]);
                            if (dc != null && dc.Category == V06Category.Equipment) { idx = i; break; }
                        }
                        if (idx >= 0) { p.Hand.Add(p.Discard[idx]); p.Discard.RemoveAt(idx); e.Log.Add("工坊交货：装备入手"); }
                    }
                    break;
                case "P06":
                    e.SpawnToken(p.Camp, "T03");
                    e.SpawnToken(p.Camp, "T03");
                    break;
                case "P07":
                    {
                        List<V06Unit> mineAll = p.Units();
                        for (int i = 0; i < mineAll.Count; i++) { if (mineAll[i].Damage > 0) { e.Heal(mineAll[i], 2); } }
                    }
                    break;
                case "P08":
                    {
                        e.SpawnToken(p.Camp, "T05");
                        V06Unit t = V06Effects.BestDamageTarget(e, 1 - p.Camp, true);
                        if (t != null) { e.EffectDamage(t, 1, c.Name); }
                    }
                    break;
                case "P09":
                    e.SpawnToken(p.Camp, "T07");
                    break;
                case "P10":
                    if (mine != null)
                    {
                        e.Heal(mine, 4);
                        int slot = OtherRowEmpty(p, mine);
                        if (slot != V06Slot.None) { MoveUnit(p, mine, slot); }
                    }
                    break;
                case "P11":
                    {
                        HashSet<string> names = new HashSet<string>();
                        for (int i = p.Discard.Count - 1; i >= 0; i--)
                        {
                            V06Card dc = V06Catalog.ById(p.Discard[i]);
                            if (dc == null || dc.Category != V06Category.Unit) { continue; }
                            if (names.Contains(dc.Name)) { continue; }
                            names.Add(dc.Name);
                            p.Deck.Add(p.Discard[i]);
                            p.Discard.RemoveAt(i);
                            if (names.Count >= 2) { break; }
                        }
                        e.Draw(p.Camp, 1);
                    }
                    break;
                case "P12":
                    p.FrontReduceThisRound += 1;
                    e.Log.Add("共同战线：本轮前排减伤 1");
                    break;
            }
        }

        // ───────────────────────── 响应 ─────────────────────────

        public static bool ResponseLegal(V06Engine e, int camp, V06Card c, V06Action pending)
        {
            Register();
            if (c == null || pending == null) { return false; }
            if (Pending.Contains(c.Id)) { return false; }
            V06Unit target = e.Players[camp].ByUid(pending.TargetUid);
            if (pending.ToFlag || target == null) { return false; }   // 掩护/响应都不能保护远征旗
            switch (c.Id)
            {
                case "X01": return true;
                case "X05": return true;
                case "X03":
                    {
                        // 需要己方后排有空格
                        V06Player p = e.Players[camp];
                        for (int s = V06Slot.FrontCount; s < V06Slot.Total; s++) { if (p.At(s) == null) { return true; } }
                        return false;
                    }
            }
            return false;
        }

        public static int ResponseDamageReduce(V06Engine e, int camp, V06Card c, V06Action pending)
        {
            if (c == null) { return 0; }
            switch (c.Id)
            {
                case "X01": return 2;
                case "X05": return 1;
            }
            return 0;
        }

        /// <summary>响应执行后的额外效果（X03 位移/取消攻击、X05 反射）。</summary>
        public static void AfterResponse(V06Engine e, int camp, V06Card c, V06Action pending, int attackerCamp)
        {
            if (c == null || pending == null) { return; }
            V06Player p = e.Players[camp];
            V06Unit target = p.ByUid(pending.TargetUid);
            switch (c.Id)
            {
                case "X03":
                    {
                        if (target == null) { return; }
                        int slot = V06Slot.None;
                        for (int s = V06Slot.FrontCount; s < V06Slot.Total; s++) { if (p.At(s) == null) { slot = s; break; } }
                        if (slot == V06Slot.None) { return; }
                        MoveUnit(p, target, slot);
                        V06Unit atk = e.Players[attackerCamp].ByUid(pending.Uid);
                        bool atkRanged = atk != null && atk.Has("远射");
                        bool atkFlyingCanReachFront = atk != null && atk.Has("飞行");   // 飞行只能打前排
                        if (!atkRanged)
                        {
                            // 目标已到后排，普通/飞行攻击者都打不到 → 本次攻击取消
                            e.PendingCancelled = true;
                            e.Log.Add("抢先脱身：目标移入后排，本次攻击取消");
                        }
                        else
                        {
                            e.Log.Add("抢先脱身：目标移入后排，但攻击者是远射，攻击照常结算");
                        }
                    }
                    break;
                case "X05":
                    {
                        V06Unit atk = e.Players[attackerCamp].ByUid(pending.Uid);
                        if (atk != null) { e.PendingReflect += 1; }
                    }
                    break;
            }
        }
    }
}
