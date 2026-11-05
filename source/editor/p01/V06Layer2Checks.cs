#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Jieqiao;

/// <summary>
/// 第三层卡片（装备/附印/战况/响应）定点断言。
/// 与 V06Checks（卡池结构）和 RuleChecks（V0.3 规则）分开：这里验的是
/// V0.6 引擎里这 32 张牌的数值与窗口行为，全部用真实 API（LegalActions/Apply）驱动。
/// </summary>
public static class V06Layer2Checks
{
    private static int _pass;
    private static int _fail;
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run()
    {
        _pass = 0; _fail = 0;
        Out.Length = 0;
        V06Effects.RegisterAll();

        EquipAttackBonus();
        EquipHpKeepsDamage();
        EquipGrantsRanged();
        EquipConstructOnly();
        EquipReplacesOld();
        SealGuardForcesTargeting();
        SealHpAndHealOne();
        BattlefieldConstructGuard();
        BattlefieldFlyingAttack();
        BattlefieldReplace();
        UpgradeReplacesStats();
        UpgradeOncePerCard();
        SupportHeal();
        SupportFrontReduce();
        ResponseShield();
        ResponseMirrorReflect();
        ResponseEscape();
        CardConservation();

        Out.AppendLine("第三层定点测试：" + _pass + " 通过 / " + _fail + " 失败");
        return Out.ToString();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok) { _pass++; Out.AppendLine("  ✅ " + name); }
        else { _fail++; Out.AppendLine("  ❌ " + name + "　—　" + detail); }
    }

    // ───────────────────────── 场景搭建 ─────────────────────────

    private static V06Engine Start(int seed, int deckA, int deckB)
    {
        V06Engine e = new V06Engine();
        string err;
        if (!e.TryStart(seed, deckA, deckB, out err))
        {
            Out.AppendLine("  开局失败：" + err);
            return null;
        }
        e.Log.Clear();
        return e;
    }

    private static V06Unit Put(V06Player p, string cardId, int slot)
    {
        V06Card c = V06Catalog.ById(cardId);
        V06Unit u = new V06Unit();
        u.Uid = p.NextUid++;
        u.Camp = p.Camp;
        u.Slot = slot;
        u.CardId = cardId;
        u.Name = c == null ? cardId : c.Name;
        u.AtkBase = c == null ? 2 : c.Atk;
        u.HpBase = c == null ? 3 : c.Hp;
        u.TagText = c == null ? "" : c.Tags;
        if (c != null) { for (int i = 0; i < c.Keywords.Length; i++) { u.AddKw(c.Keywords[i]); } }
        p.Slots[slot] = u;
        return u;
    }

    private static V06Action Find(V06Engine e, V06ActionKind kind, string cardId, int uid)
    {
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            V06Action a = acts[i];
            if (a.Kind != kind) { continue; }
            if (cardId != null && a.CardId != cardId) { continue; }
            if (uid >= 0 && a.Uid != uid) { continue; }
            return a;
        }
        return null;
    }

    private static bool PlayCard(V06Engine e, int camp, string cardId, int uid)
    {
        V06Player p = e.Players[camp];
        p.Hand.Add(cardId);
        p.ActionPoints = 9;
        p.MainActionsUsed = 0;
        e.Active = camp;
        V06Action a = Find(e, V06ActionKind.PlayCard, cardId, uid);
        if (a == null) { return false; }
        string err;
        return e.Apply(a, out err);
    }

    private static int CountAll(V06Player p)
    {
        int n = p.Hand.Count + p.Deck.Count + p.Discard.Count + p.Exile.Count + p.Reserve.Count + p.ReserveUsed.Count;
        if (!string.IsNullOrEmpty(p.BattlefieldId)) { n++; }
        for (int s = 0; s < V06Slot.Total; s++)
        {
            V06Unit u = p.At(s);
            if (u == null) { continue; }
            if (!u.IsToken) { n++; }
            if (!string.IsNullOrEmpty(u.EquipId)) { n++; }
            if (!string.IsNullOrEmpty(u.SealId)) { n++; }
        }
        return n;
    }

    // ───────────────────────── 装备 ─────────────────────────

    private static void EquipAttackBonus()
    {
        V06Engine e = Start(9001, 8, 9);
        if (e == null) { Check("装备 Q01 攻击 +1", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        int before = u.AtkBaseNow;
        bool ok = PlayCard(e, 0, "Q01", u.Uid);
        Check("装备 Q01 符钢短剑：装备单位攻击 +1", ok && u.AtkBaseNow == before + 1,
            "结果 " + ok + "，攻击 " + before + " → " + u.AtkBaseNow);
    }

    private static void EquipHpKeepsDamage()
    {
        V06Engine e = Start(9002, 8, 9);
        if (e == null) { Check("装备 Q02 生命 +2 保留伤害", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        u.Damage = 1;
        int hpBefore = u.MaxHp;
        bool ok = PlayCard(e, 0, "Q02", u.Uid);
        Check("装备 Q02 加固皮甲：最大生命 +2 且保留已有伤害",
            ok && u.MaxHp == hpBefore + 2 && u.Damage == 1,
            "结果 " + ok + "，最大生命 " + hpBefore + " → " + u.MaxHp + "，伤害 " + u.Damage);
    }

    private static void EquipGrantsRanged()
    {
        V06Engine e = Start(9003, 8, 9);
        if (e == null) { Check("装备 Q03 赋予远射", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 4);       // 后排
        bool before = e.CanAttackFrom(u);
        bool ok = PlayCard(e, 0, "Q03", u.Uid);
        Check("装备 Q03 游侠符镜：后排单位获得远射后可攻击",
            ok && !before && e.CanAttackFrom(u),
            "结果 " + ok + "，装备前可攻击 " + before + "，装备后 " + e.CanAttackFrom(u));
    }

    private static void EquipConstructOnly()
    {
        V06Engine e = Start(9004, 8, 9);
        if (e == null) { Check("Q11 仅构装可装备", false, "开局失败"); return; }
        V06Unit soldier = Put(e.Players[0], "U-D01", 0);   // 矮人
        V06Unit machine = Put(e.Players[0], "U-D07", 1);   // 构装
        e.Players[0].Hand.Add("Q11");
        e.Players[0].ActionPoints = 9;
        e.Active = 0;
        V06Action a = Find(e, V06ActionKind.PlayCard, "Q11", soldier.Uid);
        bool blocked = a == null;
        V06Action b = Find(e, V06ActionKind.PlayCard, "Q11", machine.Uid);
        Check("装备 Q11 机动喷口：只能给构装（对矮人被拒、对傀儡可选）", blocked && b != null,
            "矮人可选=" + (a != null) + "，傀儡可选=" + (b != null));
    }

    private static void EquipReplacesOld()
    {
        V06Engine e = Start(9005, 8, 9);
        if (e == null) { Check("换装备弃旧", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        int all0 = CountAll(e.Players[0]);
        PlayCard(e, 0, "Q01", u.Uid);
        int afterFirst = CountAll(e.Players[0]);
        PlayCard(e, 0, "Q02", u.Uid);
        int afterSecond = CountAll(e.Players[0]);
        // 每次 PlayCard 助手都会往手牌注入 1 张，所以基数逐次 +1
        Check("装备换新时旧牌弃置、总数只随注入增加",
            u.EquipId == "Q02" && e.Players[0].Discard.Contains("Q01")
            && afterFirst == all0 + 1 && afterSecond == all0 + 2,
            "最终装备 " + u.EquipId + "，弃牌区含 Q01=" + e.Players[0].Discard.Contains("Q01")
            + "，卡数 " + all0 + "/" + afterFirst + "/" + afterSecond);
    }

    // ───────────────────────── 附印 ─────────────────────────

    private static void SealGuardForcesTargeting()
    {
        V06Engine e = Start(9006, 8, 9);
        if (e == null) { Check("附印 A01 护卫强制目标", false, "开局失败"); return; }
        V06Unit atk = Put(e.Players[0], "U-D01", 0);
        V06Unit plain = Put(e.Players[1], "U-D03", 0);
        V06Unit guarded = Put(e.Players[1], "U-D05", 1);
        PlayCard(e, 1, "A01", guarded.Uid);
        List<V06Unit> targets = e.AttackTargets(atk);
        Check("附印 A01 守誓符文：获得护卫后成为强制目标",
            guarded.Has("护卫") && targets.Count == 1 && targets[0].Uid == guarded.Uid,
            "护卫=" + guarded.Has("护卫") + "，可选目标数 " + targets.Count);
    }

    private static void SealHpAndHealOne()
    {
        V06Engine e = Start(9007, 8, 9);
        if (e == null) { Check("附印 A07", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        u.Damage = 2;
        int hpBefore = u.MaxHp;
        bool ok = PlayCard(e, 0, "A07", u.Uid);
        Check("附印 A07 鳞息符文：最大生命 +2 且附上时移除 1 点伤害",
            ok && u.MaxHp == hpBefore + 2 && u.Damage == 1,
            "结果 " + ok + "，最大生命 " + hpBefore + " → " + u.MaxHp + "，伤害 " + u.Damage);
    }

    // ───────────────────────── 战况 ─────────────────────────

    private static void BattlefieldConstructGuard()
    {
        V06Engine e = Start(9008, 8, 9);
        if (e == null) { Check("战况 F03", false, "开局失败"); return; }
        V06Unit attacker = Put(e.Players[0], "U-D01", 0);      // 3 攻
        V06Unit machine = Put(e.Players[1], "U-M02", 0);       // 3/5 构装
        bool ok = PlayCard(e, 1, "F03", -1);
        // 让构装方成为防守方并结算一次攻击：直接调用伤害修正
        int dmg = e.ModifyAttackDamage(machine, 3, true, 0);
        Check("战况 F03 工坊防线：构装获得坚壁，受到的攻击伤害 −1",
            ok && dmg == 2 && e.Players[1].BattlefieldId == "F03",
            "结果 " + ok + "，伤害 " + dmg + "（应为 2）");
    }

    private static void BattlefieldFlyingAttack()
    {
        V06Engine e = Start(9009, 8, 9);
        if (e == null) { Check("战况 F05", false, "开局失败"); return; }
        V06Unit flyer = Put(e.Players[0], "U-R02", 0);   // 3 攻 飞行
        int before = e.FinalAttack(flyer, false);
        bool ok = PlayCard(e, 0, "F05", -1);
        int after = e.FinalAttack(flyer, false);
        Check("战况 F05 群龙回翔：飞行单位攻击 +1", ok && after == before + 1,
            "结果 " + ok + "，攻击 " + before + " → " + after);
    }

    private static void BattlefieldReplace()
    {
        V06Engine e = Start(9010, 8, 9);
        if (e == null) { Check("战况替换", false, "开局失败"); return; }
        PlayCard(e, 0, "F03", -1);
        PlayCard(e, 0, "F05", -1);
        Check("战况替换：新战况替换旧战况且旧牌进弃牌区",
            e.Players[0].BattlefieldId == "F05" && e.Players[0].Discard.Contains("F03"),
            "当前战况 " + e.Players[0].BattlefieldId + "，弃牌区含 F03=" + e.Players[0].Discard.Contains("F03"));
    }

    // ───────────────────────── 晋阶 ─────────────────────────

    private static void UpgradeReplacesStats()
    {
        V06Engine e = Start(9011, 8, 9);
        if (e == null) { Check("晋阶 J01", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        u.Damage = 1;
        u.EquipId = "Q01";
        u.AtkPerm = 1;
        bool ok = false;
        V06Player p = e.Players[0];
        p.Reserve.Clear();
        p.Reserve.Add("J01");
        p.ActionPoints = 9;
        p.MainActionsUsed = 0;
        e.Active = 0;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.PlayCard && acts[i].CardId == "J01" && acts[i].Uid == u.Uid)
            {
                string err;
                ok = e.Apply(acts[i], out err);
                break;
            }
        }
        Check("晋阶 J01 远征老兵：攻生替换为 4/5、保留伤害与装备",
            ok && u.AtkBase == 4 && u.HpBase == 5 && u.Damage == 1 && u.EquipId == "Q01" && u.Upgraded,
            "结果 " + ok + "，攻生 " + u.AtkBase + "/" + u.HpBase + "，伤害 " + u.Damage + "，装备 " + u.EquipId);
    }

    private static void UpgradeOncePerCard()
    {
        V06Engine e = Start(9012, 8, 9);
        if (e == null) { Check("晋阶限一次", false, "开局失败"); return; }
        V06Unit first = Put(e.Players[0], "U-D01", 0);
        V06Player p = e.Players[0];
        p.Reserve.Clear();
        p.Reserve.Add("J01");
        p.ActionPoints = 9;
        p.MainActionsUsed = 0;
        e.Active = 0;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.PlayCard && acts[i].CardId == "J01")
            {
                string err;
                e.Apply(acts[i], out err);
                break;
            }
        }
        // 晋阶后该单位不能再晋阶
        p.ReserveUsed.Clear();
        p.Reserve.Clear();
        p.Reserve.Add("J02");
        p.ActionPoints = 9;
        e.Active = 0;
        bool stillOffered = false;
        List<V06Action> acts2 = e.LegalActions();
        for (int i = 0; i < acts2.Count; i++)
        {
            if (acts2[i].Kind == V06ActionKind.PlayCard && acts2[i].CardId == "J02" && acts2[i].Uid == first.Uid) { stillOffered = true; }
        }
        Check("晋阶限制：同一单位不能再次晋阶", first.Upgraded && !stillOffered,
            "已晋阶=" + first.Upgraded + "，仍可选择=" + stillOffered);
    }

    // ───────────────────────── 支援 ─────────────────────────

    private static void SupportHeal()
    {
        V06Engine e = Start(9013, 8, 9);
        if (e == null) { Check("支援 P02", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);   // 3 生命
        u.Damage = 2;                                 // 受伤但活着
        bool ok = false;
        V06Player p = e.Players[0];
        p.Reserve.Clear();
        p.Reserve.Add("P02");
        p.ActionPoints = 9;
        p.MainActionsUsed = 0;
        e.Active = 0;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.PlayCard && acts[i].CardId == "P02" && acts[i].Uid == u.Uid)
            {
                string err;
                ok = e.Apply(acts[i], out err);
                break;
            }
        }
        Check("支援 P02 紧急医疗：治疗受伤单位 4（2 → 0）", ok && u.Damage == 0,
            "结果 " + ok + "，剩余伤害 " + u.Damage);
    }

    private static void SupportFrontReduce()
    {
        V06Engine e = Start(9014, 8, 9);
        if (e == null) { Check("支援 P12", false, "开局失败"); return; }
        V06Unit front = Put(e.Players[0], "U-D01", 0);
        bool ok = false;
        V06Player p = e.Players[0];
        p.Reserve.Clear();
        p.Reserve.Add("P12");
        p.ActionPoints = 9;
        p.MainActionsUsed = 0;
        e.Active = 0;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.PlayCard && acts[i].CardId == "P12")
            {
                string err;
                ok = e.Apply(acts[i], out err);
                break;
            }
        }
        int dmg = e.ModifyAttackDamage(front, 3, true, 0);
        Check("支援 P12 共同战线：本轮前排受到的主动攻击伤害 −1", ok && dmg == 2,
            "结果 " + ok + "，伤害 " + dmg + "（应为 2）");
    }

    // ───────────────────────── 响应 ─────────────────────────

    private static V06Engine SetupAttack(out V06Unit atk, out V06Unit def)
    {
        V06Engine e = Start(9015, 8, 9);
        atk = null; def = null;
        if (e == null) { return null; }
        atk = Put(e.Players[0], "U-D01", 0);     // 矮人 3/3
        def = Put(e.Players[1], "U-D05", 0);     // 哥布林 2/2
        e.Active = 0;
        e.Players[0].ActionPoints = 9;
        e.Players[0].MainActionsUsed = 0;
        V06Action a = null;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.Attack && acts[i].Uid == atk.Uid && acts[i].TargetUid == def.Uid && !acts[i].PowerStrike)
            {
                a = acts[i];
                break;
            }
        }
        if (a == null) { return null; }
        string err;
        if (!e.Apply(a, out err)) { return null; }
        return e;   // 现在处于响应窗口
    }

    private static void ResponseShield()
    {
        V06Unit atk, def;
        V06Engine e = SetupAttack(out atk, out def);
        if (e == null) { Check("响应 X01", false, "无法建立攻击窗口"); return; }
        V06Player d = e.Players[1];
        d.Hand.Add("X01");
        d.ActionPoints = 9;
        V06Action r = Find(e, V06ActionKind.Respond, "X01", -1);
        bool ok = r != null;
        if (ok) { string err; ok = e.Apply(r, out err); }
        Check("响应 X01 举盾：本次攻击伤害 −2", ok && def.Damage == 1,
            "结果 " + ok + "，实际伤害 " + def.Damage + "（原始 3，举盾后应为 1）");
    }

    private static void ResponseMirrorReflect()
    {
        V06Unit atk, def;
        V06Engine e = SetupAttack(out atk, out def);
        if (e == null) { Check("响应 X05", false, "无法建立攻击窗口"); return; }
        V06Player d = e.Players[1];
        d.Hand.Add("X05");
        d.ActionPoints = 9;
        int atkDamageBefore = atk.Damage;
        V06Action r = Find(e, V06ActionKind.Respond, "X05", -1);
        bool ok = r != null;
        if (ok) { string err; ok = e.Apply(r, out err); }
        // 攻击者同时吃反击（目标的攻击值 2）与镜盾反射（1），所以是 2 + 1
        int counter = def.AtkBaseNow;
        int expectedAtk = atkDamageBefore + counter + 1;
        Check("响应 X05 镜盾反射：伤害 −1，攻击者另受 1 点效果伤害（叠加反击）",
            ok && def.Damage == 2 && atk.Damage == expectedAtk,
            "结果 " + ok + "，目标伤害 " + def.Damage + "（应为 2），攻击者伤害 " + atk.Damage
            + "（反击 " + counter + " + 反射 1 = " + expectedAtk + "）");
    }

    private static void ResponseEscape()
    {
        V06Unit atk, def;
        V06Engine e = SetupAttack(out atk, out def);
        if (e == null) { Check("响应 X03", false, "无法建立攻击窗口"); return; }
        V06Player d = e.Players[1];
        d.Hand.Add("X03");
        d.ActionPoints = 9;
        V06Action r = Find(e, V06ActionKind.Respond, "X03", -1);
        bool ok = r != null;
        if (ok) { string err; ok = e.Apply(r, out err); }
        Check("响应 X03 抢先脱身：目标移入后排、非远射攻击取消",
            ok && def.Slot >= V06Slot.FrontCount && def.Damage == 0,
            "结果 " + ok + "，目标槽位 " + def.Slot + "（后排应为 3–5），伤害 " + def.Damage);
    }

    private static void CardConservation()
    {
        V06Engine e = Start(9016, 8, 9);
        if (e == null) { Check("卡牌守恒", false, "开局失败"); return; }
        int a0 = CountAll(e.Players[0]);
        int b0 = CountAll(e.Players[1]);
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        PlayCard(e, 0, "Q01", u.Uid);
        PlayCard(e, 0, "A01", u.Uid);
        PlayCard(e, 0, "F03", -1);
        // 注意：Put 会额外造一个单位，所以这里以"操作前后差值只来自 Put"为判据
        int a1 = CountAll(e.Players[0]);
        // 测试里手造了 1 个单位、往手牌里注入了 3 张牌（Q01/A01/F03），所以应 +4
        Check("装/印/战况挂载后卡牌守恒（30 + 手造单位 1 + 注入 3）",
            a0 == 30 && b0 == 30 && a1 == a0 + 4,
            "方0 " + a0 + " → " + a1 + "（应 +4），方1 " + b0);
    }
}
#endif
