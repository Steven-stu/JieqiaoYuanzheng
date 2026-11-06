#if UNITY_EDITOR
using Jieqiao.UI;
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
        // ── 批 1 规则缺陷回归(2026-11-05) ──
        CoopAttackCountsOnSubmit();
        TacticPassesTurn();
        SupportHealTargets();
        TokenCannotAttackFlag();
        GuardDoesNotStack();
        AssistBonusNotConsumedByPreview();
        // ── 批 2 来源撤词回归(2026-11-05) ──
        EquipSwitchRevokesBonus();
        SealSwitchRevokesBonus();
        SealPierceStaysPermanent();
        PierceDamageSeesAuraGuard();
        // ── 自由组卡(§4)回归 ──
        FreeDeckLegality();
        // ── §2 效果补全回归 ──
        SpiritHealsOnDeath();
        RepairUnitAlsoHealsAndSpawns();
        // ── 术语表与 T08 维修补触发（第四十八轮口径） ──
        TokenRepairTriggers();
        TermTableCoversKeywords();
        TermTextsAreClean();
        // ── A-01：撤装/换印不得删掉单位本体自带的关键词 ──
        BaseKeywordSurvivesSwitch();

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

    // ───────────────────────── 批 1 规则缺陷回归 ─────────────────────────

    /// <summary>协攻:提交攻击时也要 +1(此前先标已攻击,导致协攻永远不生效)。</summary>
    private static void CoopAttackCountsOnSubmit()
    {
        V06Engine e = Start(8801, 8, 9);
        if (e == null) { Check("协攻提交 +1", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Player foe = e.Players[1];
        V06Unit a = Put(me, "U-G01", 0);   // 哥布林巫祝 2攻 协攻
        Put(me, "U-G03", 1);               // 前排第二人,满足协攻条件
        V06Unit t = Put(foe, "U-H01", 0);
        t.DeployedThisRound = false;
        a.DeployedThisRound = false;
        e.Active = 0;
        me.ActionPoints = 9; me.MainActionsUsed = 0;
        int preview = e.FinalAttack(a, false);
        V06Action act = new V06Action { Kind = V06ActionKind.Attack, Uid = a.Uid, TargetUid = t.Uid };
        string err = "";
        bool ok = e.Apply(act, out err);
        int declared = e.PendingDamage;
        Check("协攻:攻击值含 +1(预览 " + preview + " / 宣布 " + declared + ")",
            ok && preview == 3 && declared == 3, "ok=" + ok + " err=" + err);
    }

    /// <summary>非单位牌(战术)打出后要交接行动。</summary>
    private static void TacticPassesTurn()
    {
        V06Engine e = Start(8802, 8, 9);
        if (e == null) { Check("战术交接", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        Put(me, "U-H01", 0);
        e.Active = 0;
        me.Hand.Add("S-H03");   // 远征号令:抽 2
        me.ActionPoints = 9; me.MainActionsUsed = 0;
        V06Action act = Find(e, V06ActionKind.PlayCard, "S-H03", -1);
        string err = "";
        bool ok = act != null && e.Apply(act, out err);
        Check("战术牌打完后交接行动", ok && e.Active == 1, "ok=" + ok + " Active=" + e.Active + " err=" + err);
    }

    /// <summary>治疗类战术要真的治疗到目标(S-O04 部族医药 / S-G04 巨魔疗伤)。</summary>
    private static void SupportHealTargets()
    {
        V06Engine e = Start(8803, 8, 9);
        if (e == null) { Check("治疗战术", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit u = Put(me, "U-G05", 0);   // 7 血巨魔
        u.Damage = 4;                     // 受伤 4 → 当前血 3,治疗 3 后为 6(不撞上限)
        int before = u.CurHp;
        bool ok1 = PlayCard(e, 0, "S-O04", u.Uid);
        int after1 = u.CurHp;
        Check("S-O04 部族医药治疗 3", ok1 && after1 == before + 3,
            "ok=" + ok1 + " 血 " + before + " → " + after1);

        // S-G04 巨魔疗伤:普通 3,巨魔 5
        V06Engine e2 = Start(8804, 8, 9);
        V06Player me2 = e2.Players[0];
        V06Unit h = Put(me2, "U-G03", 0);   // 3 血,受 2 伤后剩 1
        h.Damage = 2;
        bool ok2 = PlayCard(e2, 0, "S-G04", h.Uid);
        int hpHuman = h.CurHp;
        V06Unit g = Put(me2, "U-G05", 1);   // 巨魔
        g.Damage = g.MaxHp - 1;
        bool ok3 = PlayCard(e2, 0, "S-G04", g.Uid);
        int hpTroll = g.CurHp;
        Check("S-G04 巨魔疗伤:普通 +3、巨魔 +5", ok2 && ok3 && hpHuman == 3 && hpTroll == 6,
            "普通 1→" + hpHuman + "(应为3,上限3)，巨魔 1→" + hpTroll + "(应为6)");
    }

    /// <summary>卡面写明"不能攻击远征旗"的衍生物不能打旗。</summary>
    private static void TokenCannotAttackFlag()
    {
        V06Engine e = Start(8805, 8, 9);
        if (e == null) { Check("衍生物禁旗", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit tk = Put(me, "T05", 0);    // 荒原奇美拉:突袭;不能攻击远征旗
        tk.DeployedThisRound = false;
        e.Active = 0;
        me.ActionPoints = 9; me.MainActionsUsed = 0;
        bool hasFlagAction = false;
        List<V06Action> acts = e.LegalActions();
        for (int i = 0; i < acts.Count; i++)
        {
            if (acts[i].Kind == V06ActionKind.Attack && acts[i].ToFlag && acts[i].Uid == tk.Uid) { hasFlagAction = true; }
        }
        Check("T05 不能攻击远征旗", !hasFlagAction && !e.CanAttackFlag(tk),
            "枚举到旗攻击=" + hasFlagAction + " CanAttackFlag=" + e.CanAttackFlag(tk));
    }

    /// <summary>坚壁类减伤不叠加:坚壁 + P12 共同战线只减 1。</summary>
    private static void GuardDoesNotStack()
    {
        V06Engine e = Start(8806, 8, 9);
        if (e == null) { Check("坚壁不叠加", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit u = Put(me, "U-H02", 0);   // 护卫(非坚壁)
        me.FrontReduceThisRound = 1;       // P12 效果
        u.AddKw("坚壁");                    // 同时有坚壁
        int dmg = e.ModifyAttackDamage(u, 3, true, 0);
        Check("坚壁 + 共同战线只减 1(3 伤 → 2)", dmg == 2, "实际 " + dmg);
    }

    /// <summary>预览不应消费"每轮首次协攻"标记,否则真打时拿不到 F06 加成。</summary>
    private static void AssistBonusNotConsumedByPreview()
    {
        V06Engine e = Start(8807, 8, 9);
        if (e == null) { Check("协攻预览不消费", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        me.BattlefieldId = "F06";          // 荒原战鼓
        V06Unit a = Put(me, "U-G01", 0);   // 2 攻 协攻
        Put(me, "U-G03", 1);
        a.DeployedThisRound = false;
        int p1 = e.FinalAttack(a, false);  // 预览
        bool usedAfterPreview = me.FirstAssistUsed;
        int p2 = e.FinalAttack(a, false);  // 再预览一次
        Check("协攻预览两次都含 F06 加成且不消费标记",
            p1 == 4 && p2 == 4 && !usedAfterPreview,
            "预览 " + p1 + " / " + p2 + "，预览后 FirstAssistUsed=" + usedAfterPreview);
    }

    // ───────────────────────── 批 2:来源与收口 ─────────────────────────

    /// <summary>Q01(+1攻)换成 Q04(+2攻):应只剩 +2,不能累加成 +3。</summary>
    private static void EquipSwitchRevokesBonus()
    {
        V06Engine e = Start(8811, 8, 9);
        if (e == null) { Check("换装不累加", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        int baseAtk = u.AtkBaseNow;
        bool ok1 = PlayCard(e, 0, "Q01", u.Uid);
        int afterQ01 = u.AtkBaseNow;
        bool ok2 = PlayCard(e, 0, "Q04", u.Uid);
        int afterQ04 = u.AtkBaseNow;
        Check("Q01 → Q04 换装只保留 +2(不累加 +3)",
            ok1 && ok2 && afterQ01 == baseAtk + 1 && afterQ04 == baseAtk + 2,
            "基础 " + baseAtk + " → Q01 " + afterQ01 + " → Q04 " + afterQ04);
    }

    /// <summary>A06(+1攻)换成 A01(护卫):攻击加成要撤掉,同时拿到护卫。</summary>
    private static void SealSwitchRevokesBonus()
    {
        V06Engine e = Start(8812, 8, 9);
        if (e == null) { Check("换印撤词", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        int baseAtk = u.AtkBaseNow;
        bool ok1 = PlayCard(e, 0, "A06", u.Uid);
        int afterA06 = u.AtkBaseNow;
        bool ok2 = PlayCard(e, 0, "A01", u.Uid);
        int afterA01 = u.AtkBaseNow;
        Check("A06 → A01 换印:攻击加成撤销、护卫生效",
            ok1 && ok2 && afterA06 == baseAtk + 1 && afterA01 == baseAtk && u.Has("护卫"),
            "基础 " + baseAtk + " → A06 " + afterA06 + " → A01 " + afterA01 + "，护卫=" + u.Has("护卫"));
    }

    /// <summary>A03 穿透是本局永久,换掉附印仍保留(文档明确例外)。</summary>
    private static void SealPierceStaysPermanent()
    {
        V06Engine e = Start(8813, 8, 9);
        if (e == null) { Check("A03 穿透永久", false, "开局失败"); return; }
        V06Unit u = Put(e.Players[0], "U-D01", 0);
        bool ok1 = PlayCard(e, 0, "A03", u.Uid);
        bool afterPierce = u.Has("穿透");
        bool ok2 = PlayCard(e, 0, "A01", u.Uid);
        Check("A03 穿透换印后仍保留",
            ok1 && ok2 && afterPierce && u.Has("穿透") && u.Has("护卫"),
            "A03 后穿透=" + afterPierce + "，换 A01 后穿透=" + u.Has("穿透") + " 护卫=" + u.Has("护卫"));
    }

    /// <summary>S-D01 破甲重锤:目标若有"光环给的坚壁"(F03)也要按 4 点结算。</summary>
    private static void PierceDamageSeesAuraGuard()
    {
        V06Engine e = Start(8814, 8, 9);
        if (e == null) { Check("S-D01 识别光环坚壁", false, "开局失败"); return; }
        V06Player foe = e.Players[1];
        foe.BattlefieldId = "F03";             // 己方构装获得坚壁
        V06Unit c = Put(foe, "U-M01", 0);      // 构装单位
        int before = c.CurHp;
        bool ok = PlayCard(e, 0, "S-D01", c.Uid);
        int after = c.CurHp;
        Check("S-D01 对有光环坚壁的构装打 4 点",
            ok && before - after == 4, "ok=" + ok + " 血 " + before + " → " + after);
    }

    // ───────────────────────── 自由组卡(§4) ─────────────────────────

    /// <summary>自由 24 张:多种组合都合法;23 张/同名 3 张不合法。</summary>
    private static void FreeDeckLegality()
    {
        var checks = new List<string>();
        bool ok = true;

        // ① 旧模板:8 单位 + 2 战术 + 4 额外 = 24
        ok &= ProbeDeck(
            new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08" },
            new string[] { "S-H01", "S-H02" }, new string[] { "Q01", "Q02", "Q03", "A01" }, true, "8+2+4", checks);
        // ② 12 单位 + 0 战术 = 24
        ok &= ProbeDeck(
            new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08", "U-E01", "U-E02", "U-D01", "U-D02" },
            new string[0], new string[0], true, "12单位", checks);
        // ③ 10 单位 + 2 战术 = 24
        ok &= ProbeDeck(
            new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08", "U-E01", "U-E02" },
            new string[] { "S-H01", "S-H02" }, new string[0], true, "10+2", checks);
        // ④ 23 张不合法
        ok &= ProbeDeck(
            new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08" },
            new string[] { "S-H01" }, new string[] { "Q01", "Q02", "Q03" }, false, "23张", checks);
        // ⑤ 同名 3 张不合法(单位算 2 张 + 额外同卡 1 张)
        ok &= ProbeDeck(
            new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08" },
            new string[] { "S-H01", "S-H02" }, new string[] { "U-H01", "Q02", "Q03" }, false, "同名3张", checks);

        Check("自由组卡 24 张 + 同名≤2", ok, string.Join(" ", checks.ToArray()));
    }

    private static bool ProbeDeck(string[] units, string[] tactics, string[] extras, bool expectLegal, string tag, List<string> log)
    {
        V06DeckSave d = new V06DeckSave();
        d.name = "测试";
        d.units = units;
        d.tactics = tactics;
        d.extras = extras;
        d.upgrades = new string[] { "J01", "J02", "J05" };
        d.supports = new string[] { "P01", "P02", "P03" };
        d.vanguard = units.Length > 0 ? units[0] : null;
        bool legal = d.IsLegal();
        log.Add(tag + "(主牌" + d.MainCount + "→" + (legal ? "合法" : "不合法") + ")");
        return legal == expectLegal;
    }

    // ───────────────────────── §2 效果补全 ─────────────────────────

    /// <summary>T02 微光侍灵被击败时,治疗一个己方受伤单位 1。</summary>
    private static void SpiritHealsOnDeath()
    {
        V06Engine e = Start(8821, 8, 9);
        if (e == null) { Check("T02 死亡治疗", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit wounded = Put(me, "U-G05", 0);
        wounded.Damage = 3;                    // 受伤 3
        V06Unit spirit = Put(me, "T02", 1);     // 微光侍灵 1/1
        spirit.Damage = spirit.MaxHp;           // 直接置死
        int before = wounded.CurHp;
        e.CleanupDeaths();
        int after = wounded.CurHp;
        Check("T02 被击败时治疗己方受伤单位 1", after == before + 1,
            "受伤单位血 " + before + " → " + after);
    }

    /// <summary>M04 维修工蜂:入场既要维修构装 2,也要生成 T08。</summary>
    private static void RepairUnitAlsoHealsAndSpawns()
    {
        V06Engine e = Start(8822, 8, 9);
        if (e == null) { Check("维修+生成", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit c = Put(me, "U-M01", 0);        // 构装(留 1 点伤,避免被治死前先阵亡)
        c.Damage = 1;
        int beforeHp = c.CurHp;
        // U-M04 是单位:用 Deploy 部署,而不是 PlayCard
        me.Hand.Add("U-M04");
        me.ActionPoints = 9; me.MainActionsUsed = 0;
        e.Active = 0;
        V06Action act = Find(e, V06ActionKind.Deploy, "U-M04", -1);
        string err2 = "";
        bool ok = act != null && e.Apply(act, out err2);
        int afterHp = c.CurHp;
        bool spawned = false;
        for (int s = 0; s < V06Slot.Total; s++) { V06Unit u = me.At(s); if (u != null && u.CardId == "T08") { spawned = true; } }
        Check("M04 维修工蜂:维修构装 2 且生成 T08", ok && afterHp > beforeHp && spawned,
            "ok=" + ok + " err=" + err2 + " 构装血 " + beforeHp + " → " + afterHp + " 生成T08=" + spawned);
    }

    /// <summary>T08 被生成进场后，要结算自己的维修（口径：生成 T08 不漏 T08 那一份维修）。</summary>
    private static void TokenRepairTriggers()
    {
        V06Engine e = Start(8833, 8, 9);
        if (e == null) { Check("T08 生成维修", false, "开局失败"); return; }
        V06Player me = e.Players[0];
        V06Unit c = Put(me, "U-M01", 0);        // 构装(勘测步行机 3 血)
        c.Damage = 2;
        int before = c.CurHp;
        V06Unit t = e.SpawnToken(0, "T08");
        int after = c.CurHp;
        Check("T08 维修微机：生成进场治疗另一个己方受伤构装 2",
            t != null && after == before + 2,
            "生成=" + (t != null) + " 构装血 " + before + " → " + after);
    }

    /// <summary>目录里出现过的每个关键词，都必须能在术语表里查到解释。</summary>
    private static void TermTableCoversKeywords()
    {
        System.Collections.Generic.HashSet<string> used = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (c.Keywords == null) { continue; }
            for (int k = 0; k < c.Keywords.Length; k++)
            {
                if (!string.IsNullOrEmpty(c.Keywords[k])) { used.Add(c.Keywords[k]); }
            }
        }
        System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();
        foreach (string k in used) { if (!V06Terms.Has(k)) { missing.Add(k); } }
        Check("术语表覆盖全部关键词（" + used.Count + " 个）", missing.Count == 0,
            "缺：" + string.Join("、", missing.ToArray()) + "　术语表共 " + V06Terms.Count + " 条");
    }

    /// <summary>术语短释：≤25 字、彼此不串词（前后排允许点名远射／飞行，这是必要的）。</summary>
    private static void TermTextsAreClean()
    {
        string[] keys = V06Terms.AllKeys;
        System.Collections.Generic.List<string> bad = new System.Collections.Generic.List<string>();
        System.Collections.Generic.List<string> cross = new System.Collections.Generic.List<string>();
        for (int i = 0; i < keys.Length; i++)
        {
            string s = V06Terms.ShortOf(keys[i]);
            if (string.IsNullOrEmpty(s)) { bad.Add(keys[i] + "(空)"); continue; }
            if (s.Length > 25) { bad.Add(keys[i] + "=" + s.Length + "字"); }
            for (int j = 0; j < keys.Length; j++)
            {
                if (i == j) { continue; }
                if (keys[i] == "前排/后排" && (keys[j] == "远射" || keys[j] == "飞行")) { continue; }
                if (s.IndexOf(keys[j], System.StringComparison.Ordinal) >= 0) { cross.Add(keys[i] + "含" + keys[j]); }
            }
        }
        Check("术语短释 ≤25 字（" + keys.Length + " 条）", bad.Count == 0, string.Join("、", bad.ToArray()));
        Check("术语短释互不串词", cross.Count == 0, string.Join("、", cross.ToArray()));

        string block = V06Terms.Block(V06Catalog.ById("T08"));
        Check("放大面板能解释 T08 的维修", block.Contains("维修") && block.Contains("构装"),
            "block=" + block.Replace("\n", " / "));
    }

    /// <summary>A-01：撤装/换印只撤"这件来源加过的"关键词，本体自带的不动。</summary>
    private static void BaseKeywordSurvivesSwitch()
    {
        // ① U-H07 本体坚壁，穿 Q06 再换 Q01 —— 坚壁必须还在
        V06Engine e1 = Start(8841, 8, 9);
        if (e1 == null) { Check("本体关键词保留", false, "开局失败"); return; }
        V06Unit a = Put(e1.Players[0], "U-H07", 0);
        bool hadGuard = a.Has("坚壁");
        bool okA1 = PlayCard(e1, 0, "Q06", a.Uid);
        bool okA2 = PlayCard(e1, 0, "Q01", a.Uid);
        Check("本体坚壁（U-H07）换装后仍保留",
            hadGuard && okA1 && okA2 && a.Has("坚壁"),
            "本体=" + hadGuard + " Q06=" + okA1 + " Q01=" + okA2 + " 换后坚壁=" + a.Has("坚壁"));

        // ② U-H02 本体护卫（人类预组领航），附 A01 再换 A02 —— 护卫必须还在
        V06Engine e2 = Start(8842, 8, 9);
        if (e2 == null) { Check("本体护卫保留", false, "开局失败"); return; }
        V06Unit b = Put(e2.Players[0], "U-H02", 0);
        bool hadProt = b.Has("护卫");
        bool okB1 = PlayCard(e2, 0, "A01", b.Uid);
        bool okB2 = PlayCard(e2, 0, "A02", b.Uid);
        Check("本体护卫（U-H02 领航盾卫）换印后仍保留",
            hadProt && okB1 && okB2 && b.Has("护卫"),
            "本体=" + hadProt + " A01=" + okB1 + " A02=" + okB2 + " 换后护卫=" + b.Has("护卫"));

        // ③ 对照：本体没有远射的单位，Q03 给的远射换装后必须撤掉
        V06Engine e3 = Start(8843, 8, 9);
        if (e3 == null) { Check("装备给的关键词仍会撤", false, "开局失败"); return; }
        V06Unit c = Put(e3.Players[0], "U-H02", 0);     // 无远射
        bool okC1 = PlayCard(e3, 0, "Q03", c.Uid);
        bool granted = c.Has("远射");
        bool okC2 = PlayCard(e3, 0, "Q01", c.Uid);
        Check("装备给的远射换装后撤销",
            okC1 && okC2 && granted && !c.Has("远射"),
            "Q03 后=" + granted + " 换 Q01 后=" + c.Has("远射"));
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
