#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Jieqiao;

/// <summary>
/// 规则定点测试：每个用例直接构造一个极小局面，用引擎的公开接口执行一步，
/// 然后断言结果。覆盖文档第 6、7、8、9 章里最容易写错的边界。
///
/// 与 PlaytestRunner 的区别：那一边跑完整对局找崩溃，这一边钉死单条规则。
/// </summary>
public static class RuleChecks
{
    private static int _uid = 1000;
    private static int _pass;
    private static int _fail;
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run()
    {
        _pass = 0;
        _fail = 0;
        Out.Length = 0;

        GuardBlocking();
        EmptyFrontAllowsBackAndCamp();
        RangedInBackTakesNoCounter();
        RangedInFrontTakesCounter();
        CoverReducesActiveNotCounter();
        BothDieSimultaneously();
        TollBasicAndUpgraded();
        ClearFrontBonusOncePerRound();
        SpellKillGivesNoClearBonus();
        ClearBonusLogAttribution();
        UpgradeKeepsDamageAndAttackFlag();
        UpgradeOverridesKeywords();
        EquipmentReplacesToDiscard();
        ScopeGrantsRanged();
        SuppressAffectsCounter();
        SuppressExpiresNextRound();
        HandLimitOverflowDiscards();
        DeckReshuffle();
        TideAmounts();
        SimultaneousZeroTieBreak();
        NewUnitCannotActImmediately();
        ReserveOncePerRoundAndOncePerGame();
        RepositionOnlyCrossRow();
        SupplyCurveAndCarryover();
        CoverOncePerRoundOnly();
        HealCannotExceedMaxOrTouchCamp();
        EntryScoutDoesNotConsumeQuota();
        ZeroAttackStillLegal();
        UpgradedUnitDeathKeepsReserveUsed();
        AiEvaluationSign();
        CoverProtectsCamp();
        DrawSequenceRespectsHandLimit();
        ScoutWithShortDeck();
        RepositionKeepsAttackUsed();
        UpgradeKeepsAttackUsed();

        // ── V0.3 试行规则 ──────────────────────────────────────────────
        V03PowerStrikeStatsAndRoundGate();
        V03PowerStrikeKillsThroughHpThreshold();
        V03PowerStrikeDoesNotBoostCounter();
        V03PowerStrikeZeroAtk();
        V03PowerStrikeInvalidCostsNothing();
        V03PowerStrikeQuotaPersistsThroughRepositionAndUpgrade();
        V03ZeroDamageSkipsCoverWindow();
        V03AutoDeclineCover();
        V03ScoutEntryReorderWindow();
        V03DispatchSearchWindow();
        V03DeckValidation();
        V03NewUnitsStats();
        V03UnitACannotUpgrade();
        V03RepositionAnyTimeBeforeSubmit();
        V03LossBreakdownBookkeeping();

        Out.AppendLine("定点规则测试：" + _pass + " 通过 / " + _fail + " 失败");
        return Out.ToString();
    }

    // ── 基础设施 ───────────────────────────────────────────────────────

    private static Slots2 NewSetup(int round = 3)
    {
        var eng = new GameEngine();
        eng.Start(1);
        eng.ConfirmMulligan(0, new List<int>());
        eng.ConfirmMulligan(1, new List<int>());
        var s = eng.S;
        s.Phase = GamePhase.Playing;
        s.Round = round;
        s.CurrentPlayer = 0;
        s.Players[0].ActionsLeft = 3;
        s.Players[1].ActionsLeft = 3;
        s.Players[0].ActionIndexThisRound = 2;
        s.Players[1].ActionIndexThisRound = 2;
        s.Players[0].Hand.Clear();
        s.Players[1].Hand.Clear();
        s.Players[0].Board.Clear();
        s.Players[1].Board.Clear();
        return new Slots2(eng);
    }

    private sealed class Slots2
    {
        public readonly GameEngine Eng;
        public Slots2(GameEngine e) { Eng = e; }
    }

    private static UnitState Unit(CardDef def, bool front, int idx, int damage = 0, int deployedRound = 1)
    {
        var u = new UnitState
        {
            Card = new CardInstance(_uid++, def),
            Slot = new Slot(front, idx),
            Damage = damage,
            DeployedRound = deployedRound,
            DeployedAtActionIndex = 0,
        };
        return u;
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            _pass++;
            Out.AppendLine("  ✅ " + name);
        }
        else
        {
            _fail++;
            Out.AppendLine("  ❌ " + name + "　—　" + detail);
        }
    }

    // ── 攻击目标与反击 ─────────────────────────────────────────────────

    private static void GuardBlocking()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(atk);
        var guard = Unit(CardCatalog.ShieldGuard, true, 0);   // 护卫
        var plain = Unit(CardCatalog.Assault, true, 1);       // 非护卫
        eng.S.Players[1].Board.Add(guard);
        eng.S.Players[1].Board.Add(plain);

        var targets = eng.LegalAttackTargets(atk, eng.S.Players[1]);
        bool onlyGuard = targets.Count == 1 && targets[0].Card.Uid == guard.Card.Uid;
        Check("6.1 敌方前排有护卫时只能打护卫", onlyGuard,
            "可选目标数=" + targets.Count);
    }

    private static void EmptyFrontAllowsBackAndCamp()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(atk);
        var back = Unit(CardCatalog.Crossbow, false, 0);
        eng.S.Players[1].Board.Add(back);

        var targets = eng.LegalAttackTargets(atk, eng.S.Players[1]);
        var actions = eng.LegalActions();
        bool canHitBack = targets.Count == 1 && targets[0].Card.Uid == back.Card.Uid;
        bool canHitCamp = actions.Exists(a => a.Kind == ActionKind.Attack && a.TargetCamp);
        Check("6.1 敌方前排为空时可打后排且可直击营地", canHitBack && canHitCamp,
            "可打后排=" + canHitBack + " 可打营地=" + canHitCamp);
    }

    private static void RangedInBackTakesNoCounter()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var shooter = Unit(CardCatalog.Crossbow, false, 0);   // 远射 + 后排
        eng.S.Players[0].Board.Add(shooter);
        var target = Unit(CardCatalog.ShieldGuard, true, 0);
        eng.S.Players[1].Board.Add(target);
        eng.S.Players[1].Supply = 0;   // 不让掩护介入

        var action = eng.LegalActions().Find(a => a.Kind == ActionKind.Attack && !a.TargetCamp);
        int hpBefore = shooter.CurrentHp;
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        Check("6.3 后排远射攻击不受反击", shooter.CurrentHp == hpBefore,
            "攻击者生命 " + hpBefore + "→" + shooter.CurrentHp + "（应为不变）");
    }

    private static void RangedInFrontTakesCounter()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var shooter = Unit(CardCatalog.Crossbow, true, 0);    // 远射但在前排
        eng.S.Players[0].Board.Add(shooter);
        var target = Unit(CardCatalog.ShieldGuard, true, 0);  // 2 攻
        eng.S.Players[1].Board.Add(target);
        eng.S.Players[1].Supply = 0;

        var action = eng.LegalActions().Find(a => a.Kind == ActionKind.Attack && !a.TargetCamp);
        int hpBefore = shooter.CurrentHp;
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        Check("6.3 前排远射仍受反击", shooter.CurrentHp == hpBefore - 2,
            "攻击者生命 " + hpBefore + "→" + shooter.CurrentHp + "（应 -2）");
    }

    private static void CoverReducesActiveNotCounter()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var attacker = Unit(CardCatalog.Assault, true, 0);    // 3 攻 3 血
        eng.S.Players[0].Board.Add(attacker);
        var defender = Unit(CardCatalog.ShieldGuard, true, 0); // 2 攻 4 血
        eng.S.Players[1].Board.Add(defender);
        eng.S.Players[1].Supply = 3;

        var action = eng.LegalActions().Find(a => a.Kind == ActionKind.Attack && !a.TargetCamp);
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(true); }

        bool defTookOne = defender.Damage == 2;   // 3-1
        bool atkTookTwo = attacker.Damage == 2;   // 反击仍 2
        Check("7.1 掩护只减主动伤害，不减反击", defTookOne && atkTookTwo,
            "防守方受伤=" + defender.Damage + "（应 2），攻击方受伤=" + attacker.Damage + "（应 2）");
    }

    private static void BothDieSimultaneously()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var a = Unit(CardCatalog.Assault, true, 0);        // 3/3
        var b = Unit(CardCatalog.Assault, true, 0);        // 3/3
        eng.S.Players[0].Board.Add(a);
        eng.S.Players[1].Board.Add(b);
        eng.S.Players[1].Supply = 0;
        int camp0 = eng.S.Players[0].Camp;
        int camp1 = eng.S.Players[1].Camp;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.TargetCamp);
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        bool bothBoardEmpty = eng.S.Players[0].Board.Count == 0 && eng.S.Players[1].Board.Count == 0;
        // 各承担 1 点基础战损；进攻方还拿到清线奖励 1
        bool tollOk = eng.S.Players[0].Camp == camp0 - 1 && eng.S.Players[1].Camp == camp1 - 2;
        Check("6.5 同时阵亡：双方各扣战损，进攻方额外拿清线奖励", bothBoardEmpty && tollOk,
            "甲营地 " + camp0 + "→" + eng.S.Players[0].Camp + "，乙营地 " + camp1 + "→" + eng.S.Players[1].Camp);
    }

    private static void TollBasicAndUpgraded()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(atk);
        var upgraded = Unit(CardCatalog.ShieldGuard, true, 0, 5);   // 4 血 + 5 伤 → 濒死
        upgraded.Upgrade = CardCatalog.UpShieldCaptain;             // 晋阶单位
        eng.S.Players[1].Board.Add(upgraded);
        // 再放一个非护卫前排，避免「清空前排」奖励干扰战损判断
        var extra = Unit(CardCatalog.Assault, true, 1);
        eng.S.Players[1].Board.Add(extra);
        eng.S.Players[1].Supply = 0;
        int camp1 = eng.S.Players[1].Camp;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.TargetCamp
                                                 && x.TargetUid == upgraded.Card.Uid);
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        bool ok = eng.S.Players[1].Camp == camp1 - 2;
        Check("6.5 晋阶单位阵亡的战损为 2", ok,
            "乙营地 " + camp1 + "→" + eng.S.Players[1].Camp + "（应 -2；前排未清空，无清线奖励）");
    }

    private static void ClearFrontBonusOncePerRound()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var a1 = Unit(CardCatalog.Assault, true, 0);
        var a2 = Unit(CardCatalog.Assault, true, 1);
        eng.S.Players[0].Board.Add(a1);
        eng.S.Players[0].Board.Add(a2);
        var d1 = Unit(CardCatalog.Scout, true, 0);
        eng.S.Players[1].Board.Add(d1);
        var d2 = Unit(CardCatalog.Scout, true, 1);
        eng.S.Players[1].Board.Add(d2);
        eng.S.Players[1].Supply = 0;
        int camp1 = eng.S.Players[1].Camp;

        // 第一次攻击只清掉一个前排 → 不清线
        var atk1 = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == a1.Card.Uid && !x.TargetCamp);
        eng.Apply(atk1);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        int afterFirst = eng.S.Players[1].Camp;

        // 第二次攻击清空前排 → 拿一次奖励
        eng.S.CurrentPlayer = 0;
        eng.S.Players[0].ActionsLeft = 3;
        var atk2 = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == a2.Card.Uid && !x.TargetCamp);
        eng.Apply(atk2);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        int afterSecond = eng.S.Players[1].Camp;

        bool bonusTaken = eng.S.Players[0].ClearFrontBonusUsed;
        Check("7.2 清空前排只在真正清空的那次给奖励", bonusTaken && afterSecond < afterFirst,
            "乙营地 " + camp1 + "→(第一次)" + afterFirst + "→(第二次)" + afterSecond + "，奖励标记=" + bonusTaken);
    }

    // 记录过两次统计踩坑：清线奖励日志写在 ResolveAttack 内、且带 S.Round 与进攻方名字。
    // 统计脚本必须按「日志轮次 + 名字前缀」归属，不能按当前轮次或当前玩家推断。
    private static void ClearBonusLogAttribution()
    {
        var t = NewSetup(5);
        var eng = t.Eng;
        var a = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(a);
        var d = Unit(CardCatalog.Scout, true, 0, 2);   // 1/3，已受 2 伤
        eng.S.Players[1].Board.Add(d);
        eng.S.Players[1].Supply = 0;
        int logStart = eng.Log.Count;

        var atk = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == a.Card.Uid && !x.TargetCamp);
        eng.Apply(atk);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        LogEntry bonus = null;
        for (int i = logStart; i < eng.Log.Count; i++)
        {
            if (eng.Log[i].Text.Contains("首次清空敌方前排")) { bonus = eng.Log[i]; }
        }

        bool found = bonus != null;
        bool roundOk = found && bonus.Round == 5;
        bool ownerOk = found && bonus.Text.StartsWith(eng.S.Players[0].Name);
        bool flagOk = eng.S.Players[0].ClearFrontBonusUsed && !eng.S.Players[1].ClearFrontBonusUsed;
        Check("7.2 清线奖励日志归属进攻方且轮次正确", found && roundOk && ownerOk && flagOk,
            "找到=" + found + " 轮次=" + (found ? bonus.Round.ToString() : "-") + " 归属甲方=" + ownerOk + " 奖励标记甲/乙=" + flagOk);
    }

    private static void SpellKillGivesNoClearBonus()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var only = Unit(CardCatalog.Scout, true, 0, 2);   // 1/3，已受 2 伤
        eng.S.Players[1].Board.Add(only);
        eng.S.Players[1].Supply = 0;
        var shot = new CardInstance(_uid++, CardCatalog.PreciseShot);
        eng.S.Players[0].Hand.Add(shot);
        int camp1 = eng.S.Players[1].Camp;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Tactic && x.CardUid == shot.Uid);
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        bool frontCleared = eng.S.Players[1].FrontUnits.Count == 0;
        bool noBonus = !eng.S.Players[0].ClearFrontBonusUsed;
        // 只应有 1 点基础战损
        bool tollOnly = eng.S.Players[1].Camp == camp1 - 1;
        Check("7.2 法术清线不触发清线奖励", frontCleared && noBonus && tollOnly,
            "前排已清=" + frontCleared + " 奖励标记=" + !noBonus + " 乙营地 " + camp1 + "→" + eng.S.Players[1].Camp);
    }

    // ── 晋阶与装备 ─────────────────────────────────────────────────────

    private static void UpgradeKeepsDamageAndAttackFlag()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var u = Unit(CardCatalog.ShieldGuard, true, 0, 2);
        u.AttackedThisRound = true;
        eng.S.Players[0].Board.Add(u);
        eng.S.Players[0].Supply = 5;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "up_shield_captain");
        eng.Apply(action);

        bool keepsDamage = u.Damage == 2;
        bool keepsAttacked = u.AttackedThisRound;
        bool maxHpUp = u.MaxHp == 6 && u.CurrentHp == 4;
        Check("8.1 晋阶保留伤害与已攻击状态、最大生命增加但不回血",
            keepsDamage && keepsAttacked && maxHpUp,
            "伤害=" + u.Damage + " 已攻击=" + keepsAttacked + " 血=" + u.CurrentHp + "/" + u.MaxHp);
    }

    private static void UpgradeOverridesKeywords()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var u = Unit(CardCatalog.ShieldGuard, true, 0);
        eng.S.Players[0].Board.Add(u);
        eng.S.Players[0].Supply = 5;
        bool guardBefore = u.HasGuard;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "up_breaker");
        eng.Apply(action);

        Check("8.1 破阵先锋覆盖关键词、失去护卫",
            guardBefore && !u.HasGuard && u.Atk == 4 && u.MaxHp == 5,
            "护卫 " + guardBefore + "→" + u.HasGuard + "，攻=" + u.Atk + "，血=" + u.MaxHp);
    }

    private static void EquipmentReplacesToDiscard()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var u = Unit(CardCatalog.Assault, true, 0);
        u.Equipment = CardCatalog.Scope;
        eng.S.Players[0].Board.Add(u);
        eng.S.Players[0].Supply = 5;
        var weapon = new CardInstance(_uid++, CardCatalog.StdWeapon);
        eng.S.Players[0].Hand.Add(weapon);
        int discardBefore = eng.S.Players[0].Discard.Count;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Equip && x.CardUid == weapon.Uid);
        eng.Apply(action);

        bool swapped = u.Equipment == CardCatalog.StdWeapon;
        bool oldDiscarded = eng.S.Players[0].Discard.Count == discardBefore + 1;
        bool atkUp = u.Atk == 4;
        Check("8.2 装备替换：旧装备进弃牌区、攻击 +1", swapped && oldDiscarded && atkUp,
            "当前装备=" + (u.Equipment != null ? u.Equipment.Name : "无") + " 弃牌+" + (eng.S.Players[0].Discard.Count - discardBefore) + " 攻击=" + u.Atk);
    }

    private static void ScopeGrantsRanged()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var u = Unit(CardCatalog.Assault, false, 0);   // 后排、本身无远射
        eng.S.Players[0].Board.Add(u);
        bool rangedBefore = u.HasRanged;
        bool canAttackBefore = eng.CanAttackNow(u, eng.S.Players[0]);

        u.Equipment = CardCatalog.Scope;
        bool rangedAfter = u.HasRanged;
        bool canAttackAfter = eng.CanAttackNow(u, eng.S.Players[0]);

        Check("8.2 瞄准镜让后排单位可以攻击",
            !rangedBefore && !canAttackBefore && rangedAfter && canAttackAfter,
            "远射 " + rangedBefore + "→" + rangedAfter + "，可攻击 " + canAttackBefore + "→" + canAttackAfter);
    }

    // ── 牵制 ───────────────────────────────────────────────────────────

    private static void SuppressAffectsCounter()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);         // 3 攻
        eng.S.Players[0].Board.Add(atk);
        var def = Unit(CardCatalog.ShieldGuard, true, 0);     // 2 攻
        def.TempAtkMod = -2;                                   // 被牵制到 0
        eng.S.Players[1].Board.Add(def);
        eng.S.Players[1].Supply = 0;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.TargetCamp);
        int hpBefore = atk.CurrentHp;
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        Check("6.2 牵制也会降低被牵制单位的反击",
            atk.CurrentHp == hpBefore,   // 反击 0
            "攻击者生命 " + hpBefore + "→" + atk.CurrentHp + "（被牵制单位攻击 0，应无反击）");
    }

    private static void SuppressExpiresNextRound()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var u = Unit(CardCatalog.Assault, true, 0);
        u.TempAtkMod = -2;
        eng.S.Players[0].Board.Add(u);
        int atkNow = u.Atk;

        // 强制进入下一轮（清空剩余行动 → EndRound 路径）
        eng.S.Players[1].ActionsLeft = 0;
        eng.S.Players[0].ActionsLeft = 1;
        var end = eng.LegalActions().Find(x => x.Kind == ActionKind.EndRound);
        eng.Apply(end);

        Check("4.1 牵制的临时减攻在轮末清零",
            atkNow == 1 && u.TempAtkMod == 0 && u.Atk == 3,
            "本轮攻击=" + atkNow + "，新轮修正=" + u.TempAtkMod + "，新轮攻击=" + u.Atk);
    }

    // ── 抽牌堆与手牌 ───────────────────────────────────────────────────

    private static void HandLimitOverflowDiscards()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        for (int i = 0; i < GameEngine.HandLimit; i++)
        {
            p.Hand.Add(new CardInstance(_uid++, CardCatalog.Assault));
        }
        // 用「补给整备」抽 2 张，手牌已满 → 应进弃牌区
        p.Supply = 5;
        var resupply = new CardInstance(_uid++, CardCatalog.Resupply);
        p.Hand[0] = resupply;
        int discardBefore = p.Discard.Count;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Tactic && x.CardUid == resupply.Uid);
        eng.Apply(action);

        bool handStayed8 = p.Hand.Count == GameEngine.HandLimit;
        bool discardedTwo = p.Discard.Count >= discardBefore + 2;   // 战术牌本身 + 超额牌
        Check("9 手牌上限 8：超出的抽牌直接进弃牌区", handStayed8 && discardedTwo,
            "手牌=" + p.Hand.Count + " 弃牌+" + (p.Discard.Count - discardBefore));
    }

    private static void DeckReshuffle()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Deck.Clear();
        p.Discard.Clear();
        for (int i = 0; i < 3; i++)
        {
            p.Discard.Add(new CardInstance(_uid++, CardCatalog.Assault));
        }
        p.Hand.Clear();
        p.Supply = 5;
        var resupply = new CardInstance(_uid++, CardCatalog.Resupply);
        p.Hand.Add(resupply);

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Tactic && x.CardUid == resupply.Uid);
        eng.Apply(action);

        bool drew = p.Hand.Count >= 2;   // 战术牌用掉后，至少抽到 1 张
        Check("9 牌库耗尽时把弃牌区洗成新牌库", drew,
            "手牌=" + p.Hand.Count + " 牌库=" + p.Deck.Count + " 弃牌=" + p.Discard.Count);
    }

    // ── 界潮与终局 ─────────────────────────────────────────────────────

    private static void TideAmounts()
    {
        var results = new List<string>();
        bool ok = true;
        foreach (int round in new[] { 6, 7, 8, 9 })
        {
            var t = NewSetup(round);
            var eng = t.Eng;
            int c0 = eng.S.Players[0].Camp;
            eng.S.Players[0].ActionsLeft = 1;
            eng.S.Players[1].ActionsLeft = 0;
            var end = eng.LegalActions().Find(x => x.Kind == ActionKind.EndRound);
            eng.Apply(end);
            int dealt = c0 - eng.S.Players[0].Camp;
            int expect = round >= 7 ? round - 6 : 0;
            results.Add("R" + round + ":" + dealt);
            if (dealt != expect)
            {
                ok = false;
            }
        }
        Check("4.4 界潮伤害 = 轮次 − 6（第 7 轮起）", ok, string.Join(" ", results.ToArray()));
    }

    private static void SimultaneousZeroTieBreak()
    {
        var t = NewSetup();
        var eng = t.Eng;
        eng.S.Players[0].Camp = 2;
        eng.S.Players[1].Camp = 1;
        eng.S.Round = 7;   // 界潮 1 点，双方都归零 → 比较结算前营地

        eng.S.Players[0].ActionsLeft = 1;
        eng.S.Players[1].ActionsLeft = 0;
        var end = eng.LegalActions().Find(x => x.Kind == ActionKind.EndRound);
        eng.Apply(end);

        Check("1.3 双方同时归零时比较结算前营地（甲2 > 乙1 → 甲胜）",
            eng.S.IsOver && eng.S.Winner == 0,
            "结束=" + eng.S.IsOver + " 胜者=" + eng.S.Winner + " 原因=" + eng.S.FinishReason);
    }

    // ── 行动资格与预备区 ───────────────────────────────────────────────

    private static void NewUnitCannotActImmediately()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        // 直接构造「本轮第 1 次行动时部署」的状态：DeployedAtActionIndex = 1
        p.ActionIndexThisRound = 1;
        var u = Unit(CardCatalog.Assault, true, 0);
        u.DeployedRound = eng.S.Round;
        u.DeployedAtActionIndex = 1;
        p.Board.Add(u);
        eng.S.CurrentPlayer = 0;
        p.ActionsLeft = 3;

        // 部署当次：不可行动、也没有攻击动作可选
        bool cannotActSameAction = !eng.CanAct(u, p);
        bool noAttackAction = !eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.ActorUid == u.Card.Uid);

        // 己方完成这一次行动后（计数 +1），下一次主行动就可以行动
        p.ActionIndexThisRound = 2;
        bool canActNextAction = eng.CanAct(u, p)
                                && eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.ActorUid == u.Card.Uid);

        Check("5.1 新部署单位要等己方下一次主行动才能行动",
            cannotActSameAction && noAttackAction && canActNextAction,
            "部署当次可行动=" + !cannotActSameAction + "，当次有攻击动作=" + !noAttackAction
            + "，下次行动后可行动=" + canActNextAction);
    }

    private static void ReserveOncePerRoundAndOncePerGame()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        p.Camp = 10;
        var wounded1 = Unit(CardCatalog.ShieldGuard, true, 0, 2);
        var wounded2 = Unit(CardCatalog.Assault, true, 1, 1);
        p.Board.Add(wounded1);
        p.Board.Add(wounded2);

        var first = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "sup_repair");
        bool firstOk = first != null;
        eng.Apply(first);
        // 调用后回合已交给对手，检查前必须切回甲，否则查到的是乙的合法动作
        eng.S.CurrentPlayer = 0;
        p.Supply = 7;
        p.ActionsLeft = 3;

        bool blockedSameRound = !eng.LegalActions().Exists(x => x.Kind == ActionKind.Reserve);
        bool cardMarkedUsed = p.FindReserve("sup_repair").Used;

        Check("8.3 预备区每轮只能调用 1 张",
            firstOk && blockedSameRound && cardMarkedUsed,
            "第一次可调用=" + firstOk + "，同轮再次可用=" + !blockedSameRound + "，卡已标记用掉=" + cardMarkedUsed);
    }

    // ── 5.3 换防 / 1.2 补给曲线 / 7.1 掩护额度 / 6.4 治疗 / 9 侦察 / 6.2 零攻 ──

    private static void RepositionOnlyCrossRow()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var u = Unit(CardCatalog.Assault, true, 0);   // 前1
        p.Board.Add(u);

        var acts = eng.LegalActions().FindAll(x => x.Kind == ActionKind.Reposition);
        bool noLateral = !acts.Exists(x => x.ActorUid == u.Card.Uid && x.TargetUid < 0
                                           && x.ToSlot.Front == u.Slot.Front);
        bool canCrossRow = acts.Exists(x => x.ActorUid == u.Card.Uid && x.TargetUid < 0
                                            && x.ToSlot.Front != u.Slot.Front);

        // 即使绕过 LegalActions 直接构造同排横移，引擎也必须拒绝
        var forged = new GameAction
        {
            Kind = ActionKind.Reposition, ActorUid = u.Card.Uid, TargetUid = -1, ToSlot = new Slot(true, 1),
        };
        bool rejected = eng.Apply(forged) == null && u.Slot.Index == 0;

        Check("5.3 换防只能跨排或前后排互换，不做同排横移",
            noLateral && canCrossRow && rejected,
            "同排横移未出现在合法动作=" + noLateral + "，可跨排=" + canCrossRow
            + "，伪造同排动作被拒=" + rejected + "，单位仍在" + u.Slot.Label);
    }

    private static void SupplyCurveAndCarryover()
    {
        var t = NewSetup(1);
        var eng = t.Eng;
        var p0 = eng.S.Players[0];
        var p1 = eng.S.Players[1];
        bool r1 = p0.Supply == 3 && p1.Supply == 3;

        p0.Supply = 5;   // 第 1 轮剩 5 → 结转上限 2
        p1.Supply = 1;   // 剩 1 → 结转 1
        p0.ActionsLeft = 1;
        p1.ActionsLeft = 0;
        eng.S.CurrentPlayer = 0;
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.EndRound));
        bool r2 = eng.S.Round == 2 && p0.Supply == 6 && p1.Supply == 5;

        p0.Supply = 9;   // 第 2 轮剩 9 → 仍只结转 2
        p1.Supply = 0;
        p0.ActionsLeft = 1;
        p1.ActionsLeft = 0;
        eng.S.CurrentPlayer = 0;
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.EndRound));
        bool r3 = eng.S.Round == 3 && p0.Supply == 7 && p1.Supply == 5;

        Check("1.2 基础补给 3/4/5、结转上限 2、单轮最大 7",
            r1 && r2 && r3,
            "R1=" + (r1 ? "3/3" : "错") + "，R2 期望 6/5 实际 " + eng.S.Players[0].Supply + "，R3 期望 7/5");
    }

    private static void CoverOncePerRoundOnly()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var me = eng.S.Players[0];
        var foe = eng.S.Players[1];
        var def = Unit(CardCatalog.ShieldGuard, true, 0);    // 2攻4血
        me.Board.Add(def);
        var atk1 = Unit(CardCatalog.Assault, true, 0);       // 3攻3血
        foe.Board.Add(atk1);

        me.Supply = 5;
        me.CoverUsed = true;          // 本轮掩护已用掉
        foe.Supply = 0;
        eng.S.CurrentPlayer = 1;
        foe.ActionsLeft = 3;
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == atk1.Card.Uid));
        bool noWindow = eng.Pending == PendingKind.None;
        bool fullDamage = def.Damage == 3 && me.Supply == 5;

        // 掩护额度重置后应重新开窗口，且只减主动伤害
        me.CoverUsed = false;
        def.Damage = 0;
        var atk2 = Unit(CardCatalog.Assault, false, 0);      // 后排普通单位不能攻击，这里放前排侧格
        atk2.Slot = new Slot(true, 1);
        foe.Board.Add(atk2);
        eng.S.CurrentPlayer = 1;
        foe.ActionsLeft = 3;
        var a2 = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == atk2.Card.Uid);
        eng.Apply(a2);
        bool window = eng.Pending == PendingKind.Cover;
        eng.ResolveCover(true);
        bool reduced = def.Damage == 2 && me.Supply == 4 && me.CoverUsed;

        Check("7.1 掩护每轮一次；用掉后不再开窗口；重置后只减 1 点主动伤害",
            noWindow && fullDamage && window && reduced,
            "已用后开窗口=" + !noWindow + "，满伤=" + fullDamage
            + "，重置后开窗口=" + window + "，减伤结果=" + reduced + "（伤害 " + def.Damage + "，补给 " + me.Supply + "）");
    }

    private static void HealCannotExceedMaxOrTouchCamp()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 5;
        p.Camp = 10;
        var fresh = Unit(CardCatalog.Assault, true, 0);          // 满血，不该成为目标
        var wounded = Unit(CardCatalog.Assault, true, 1, 1);     // 只受 1 伤
        p.Board.Add(fresh);
        p.Board.Add(wounded);
        var repair = new CardInstance(_uid++, CardCatalog.FieldRepair);
        p.Hand.Add(repair);

        var acts = eng.LegalActions().FindAll(x => x.Kind == ActionKind.Tactic && x.CardUid == repair.Uid);
        bool onlyWounded = acts.Count == 1 && acts[0].TargetUid == wounded.Card.Uid;
        eng.Apply(acts[0]);
        bool cappedAtMax = wounded.Damage == 0 && wounded.CurrentHp == wounded.MaxHp;
        bool campUntouched = p.Camp == 10;

        Check("6.4 治疗只作用于受伤单位、不超过最大生命、不能治疗营地",
            onlyWounded && cappedAtMax && campUntouched,
            "目标只有受伤单位=" + onlyWounded + "，回复后满血=" + cappedAtMax
            + "，营地未变=" + campUntouched + "（" + p.Camp + "）");
    }

    private static void EntryScoutDoesNotConsumeQuota()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 5;
        var scout = new CardInstance(_uid++, CardCatalog.Scout);
        p.Hand.Add(scout);
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Deploy && x.CardUid == scout.Uid));

        bool entryFired = p.Board.Count == 1 && !p.ScoutUsed;
        eng.S.CurrentPlayer = 0;
        p.Supply = 5;
        p.ActionsLeft = 3;
        bool scoutStillLegal = eng.LegalActions().Exists(x => x.Kind == ActionKind.Scout);

        Check("9 侦察员入场不占用每轮一次的通用侦察额度",
            entryFired && scoutStillLegal,
            "入场后通用侦察标记=" + p.ScoutUsed + "，仍可通用侦察=" + scoutStillLegal);
    }

    private static void ZeroAttackStillLegal()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var me = eng.S.Players[1];
        var foe = eng.S.Players[0];
        var atk = Unit(CardCatalog.Assault, true, 0);
        atk.TempAtkMod = -5;                                  // 压到 0
        foe.Board.Add(atk);
        var def = Unit(CardCatalog.ShieldGuard, true, 0, 1);   // 只剩 3 血
        me.Board.Add(def);
        me.Supply = 0;                                         // 不进入掩护

        int defHpBefore = def.CurrentHp;
        int atkHpBefore = atk.CurrentHp;
        bool legal = atk.Atk == 0
                     && eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.ActorUid == atk.Card.Uid);
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == atk.Card.Uid));
        bool noDamage = def.CurrentHp == defHpBefore;
        bool counterStillHits = atk.CurrentHp == atkHpBefore - 2;

        Check("6.2 攻击力 0 仍可宣布合法攻击，且前排攻击仍受反击",
            legal && noDamage && counterStillHits,
            "0 攻可攻击=" + legal + "，目标未掉血=" + noDamage + "，攻击者吃反击=" + counterStillHits);
    }

    private static void UpgradedUnitDeathKeepsReserveUsed()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var o = eng.S.Players[1];
        p.Supply = 7;
        var u = Unit(CardCatalog.ShieldGuard, true, 0);
        p.Board.Add(u);
        var filler = Unit(CardCatalog.BannerGuard, true, 1);   // 挡住清线奖励，只看战损
        p.Board.Add(filler);

        var up = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "up_shield_captain");
        bool canUpgrade = up != null;
        eng.Apply(up);
        bool upgraded = u.Upgrade != null;
        u.Damage = u.MaxHp;                                    // 直接打到濒死

        var killer = Unit(CardCatalog.Assault, true, 0);
        o.Board.Add(killer);
        o.Supply = 0;
        p.Supply = 0;                                          // 避免掩护窗口
        eng.S.CurrentPlayer = 1;
        o.ActionsLeft = 3;
        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.ActorUid == killer.Card.Uid));

        bool tollTwo = p.Camp == GameEngine.CampStart - 2;
        bool baseToDiscard = p.Discard.Exists(c => c.Def.Id == "guard_shield");
        bool noUpgradeCycle = !p.Discard.Exists(c => c.Def.Id == "up_shield_captain");
        bool reserveStaysUsed = p.FindReserve("up_shield_captain").Used;

        Check("8.3 晋阶单位阵亡：战损 2、只送回基础牌、晋阶牌仍记为已使用",
            canUpgrade && upgraded && tollTwo && baseToDiscard && noUpgradeCycle && reserveStaysUsed,
            "可晋阶=" + canUpgrade + "，已晋阶=" + upgraded + "，战损2=" + tollTwo
            + "，基础牌进弃牌=" + baseToDiscard + "，晋阶牌未回流=" + noUpgradeCycle
            + "，预备仍记已用=" + reserveStaysUsed);
    }

    /// <summary>
    /// AI 评分的符号检查。营地是生命值，越多越好；打掉对面营地必须让评分变高。
    /// 这里曾经写成 (敌方 − 我方)，于是 AI 把「造成营地伤害」当成掉分，
    /// 结果整局都不敢直击营地、也不敢做击杀。
    /// </summary>
    private static void AiEvaluationSign()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var o = eng.S.Players[1];

        p.Camp = 20; o.Camp = 10;
        int winning = HeuristicAi.Evaluate(eng.S, 0);
        p.Camp = 10; o.Camp = 20;
        int losing = HeuristicAi.Evaluate(eng.S, 0);
        bool campSignOk = winning > losing;

        p.Camp = 20; o.Camp = 20;
        int even = HeuristicAi.Evaluate(eng.S, 0);
        o.Camp = 17;
        int afterHit = HeuristicAi.Evaluate(eng.S, 0);
        bool damageIsGood = afterHit > even;

        Check("AI 评分：自己营地多 / 敌方营地少 评分更高，且造成营地伤害必须加分",
            campSignOk && damageIsGood,
            "领先=" + winning + " 落后=" + losing + "（应领先>落后）；敌方营地 20→17 后评分 "
            + even + "→" + afterHit + "（应变高）");
    }

    /// <summary>7.1：掩护同样可以保护营地——直击营地被掩护后少 1 点。</summary>
    private static void CoverProtectsCamp()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var o = eng.S.Players[1];
        var atk = Unit(CardCatalog.Assault, true, 0);   // 3 攻
        p.Board.Add(atk);
        o.Board.Clear();                                 // 敌方前排为空 → 可直击营地
        o.Supply = 5;
        o.Camp = 20;
        eng.S.CurrentPlayer = 0;
        p.ActionsLeft = 3;

        var camps = eng.LegalActions().FindAll(x => x.Kind == ActionKind.Attack && x.TargetCamp);
        bool hasCampAction = camps.Count > 0;
        eng.Apply(camps[0]);
        bool window = eng.Pending == PendingKind.Cover;
        eng.ResolveCover(true);
        // 3 主动伤害 − 1 掩护 = 2
        bool reduced = o.Camp == 18;

        Check("7.1 掩护可以保护营地：直击 3 点经掩护后只受 2 点",
            hasCampAction && window && reduced,
            "有直击动作=" + hasCampAction + "，开掩护窗口=" + window + "，敌方营地 20→" + o.Camp + "（应为 18）");
    }

    /// <summary>
    /// 9：按顺序抽牌。手牌满 8 张时打出「补给整备」→ 手牌先变 7，
    /// 抽第 1 张填到 8，抽第 2 张就超限、直接进弃牌区。
    /// </summary>
    private static void DrawSequenceRespectsHandLimit()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        for (int i = 0; i < GameEngine.HandLimit; i++)
        {
            p.Hand.Add(new CardInstance(_uid++, CardCatalog.Assault));
        }
        p.Supply = 5;
        var resupply = new CardInstance(_uid++, CardCatalog.Resupply);
        p.Hand[0] = resupply;
        p.Deck.Clear();
        var top = new CardInstance(_uid++, CardCatalog.ShieldGuard);
        var second = new CardInstance(_uid++, CardCatalog.BannerGuard);
        p.Deck.Add(top);
        p.Deck.Add(second);
        int discardBefore = p.Discard.Count;

        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Tactic && x.CardUid == resupply.Uid));

        bool handFull = p.Hand.Count == GameEngine.HandLimit;
        bool firstEntered = p.Hand.Exists(c => c.Uid == top.Uid);
        bool secondDiscarded = p.Discard.Exists(c => c.Uid == second.Uid);
        bool discardGrew = p.Discard.Count == discardBefore + 2;   // 战术牌本身 + 超限那张

        Check("9 手牌满时抽 2：第 1 张补到上限、第 2 张直接弃置（不能先看后挑）",
            handFull && firstEntered && secondDiscarded && discardGrew,
            "手牌=" + p.Hand.Count + "，第1张入手=" + firstEntered
            + "，第2张进弃牌=" + secondDiscarded + "，弃牌+" + (p.Discard.Count - discardBefore));
    }

    /// <summary>9：牌库不足 3 张时，侦察只揭示现有的张数。</summary>
    private static void ScoutWithShortDeck()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Deck.Clear();
        p.Discard.Clear();
        p.Hand.Clear();
        p.Supply = 5;
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Assault));
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.ShieldGuard));

        eng.Apply(eng.LegalActions().Find(x => x.Kind == ActionKind.Scout));
        bool revealedTwo = eng.PendingScoutCards.Count == 2;
        eng.ResolveScoutPick(0);
        bool gotOne = p.Hand.Count == 1;
        bool restToBottom = p.Deck.Count == 1;

        Check("9 牌库只有 2 张时侦察只揭示 2 张，选 1 后其余回牌库底",
            revealedTwo && gotOne && restToBottom,
            "揭示=" + eng.PendingScoutCards.Count + "（记录用），入手=" + p.Hand.Count + "，牌库剩=" + p.Deck.Count);
    }

    /// <summary>5.3：换防不重置攻击资格——已经攻击过的单位换到另一排仍不能攻击。</summary>
    private static void RepositionKeepsAttackUsed()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var u = Unit(CardCatalog.Assault, true, 0);
        u.AttackedThisRound = true;
        p.Board.Add(u);

        var rep = eng.LegalActions().Find(x => x.Kind == ActionKind.Reposition && x.ActorUid == u.Card.Uid);
        bool hasRep = rep != null;
        eng.Apply(rep);
        bool movedToBack = u.Slot.Front == false;
        p.ActionIndexThisRound = 5;
        p.ActionsLeft = 3;
        eng.S.CurrentPlayer = 0;
        bool stillCannotAttack = !eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.ActorUid == u.Card.Uid);

        Check("5.3 换防不重置攻击资格：已攻击单位换到后排仍不能攻击",
            hasRep && movedToBack && stillCannotAttack,
            "有换防动作=" + hasRep + "，换到后排=" + movedToBack + "，仍不能攻击=" + stillCannotAttack);
    }

    /// <summary>8.1：晋阶保留「本轮已攻击」状态，晋阶后仍不能再次攻击。</summary>
    private static void UpgradeKeepsAttackUsed()
    {
        var t = NewSetup();
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        var u = Unit(CardCatalog.ShieldGuard, true, 0);
        u.AttackedThisRound = true;
        p.Board.Add(u);

        var up = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "up_shield_captain");
        bool hasUpgrade = up != null;
        eng.Apply(up);
        bool upgraded = u.Upgrade != null;
        bool flagKept = u.AttackedThisRound;
        p.ActionIndexThisRound = 5;
        p.ActionsLeft = 3;
        eng.S.CurrentPlayer = 0;
        bool stillCannotAttack = !eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.ActorUid == u.Card.Uid);

        Check("8.1 晋阶保留已攻击状态：晋阶后不能再次攻击",
            hasUpgrade && upgraded && flagKept && stillCannotAttack,
            "可晋阶=" + hasUpgrade + "，已晋阶=" + upgraded + "，已攻击标记保留=" + flagKept
            + "，仍不能攻击=" + stillCannotAttack);
    }

    // ── V0.3 强攻（§6.6）──────────────────────────────────────────────

    /// <summary>6.6：第 1~3 轮不开放；第 4 轮起每轮 1 次，扣 2 补给，主动伤害 +1。</summary>
    private static void V03PowerStrikeStatsAndRoundGate()
    {
        var t3 = NewSetup(3);
        var eng3 = t3.Eng;
        var a3 = Unit(CardCatalog.Assault, true, 0);
        eng3.S.Players[0].Board.Add(a3);
        eng3.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        eng3.S.Players[0].Supply = 7;
        bool closedRound3 = !eng3.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.PowerStrike)
                            && !eng3.PowerStrikeAvailable(eng3.S.Players[0]);

        var t = NewSetup(4);
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);          // 3 攻
        eng.S.Players[0].Board.Add(atk);
        var guard = Unit(CardCatalog.ShieldGuard, true, 0);    // 2/4
        eng.S.Players[1].Board.Add(guard);
        eng.S.Players[0].Supply = 7;
        int campBefore = eng.S.Players[1].Camp;

        var action = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        bool hasPower = action != null;
        eng.Apply(action);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        bool supplySpent = eng.S.Players[0].Supply == 5;
        bool quotaUsed = eng.S.Players[0].PowerStrikeUsed;
        // 强攻 4 伤打 2/4 盾卫士 → 阵亡，乙承担战损 1 + 清线 1
        bool killed = eng.S.Players[1].Board.Count == 0;
        bool campDrop = eng.S.Players[1].Camp == campBefore - 2;
        eng.S.CurrentPlayer = 0;
        eng.S.Players[0].ActionsLeft = 3;
        bool noSecond = !eng.LegalActions().Exists(x => x.Kind == ActionKind.Attack && x.PowerStrike);

        Check("6.6 强攻：第 4 轮起开放、每轮 1 次、-2 补给、主动伤害 +1",
            closedRound3 && hasPower && supplySpent && quotaUsed && killed && campDrop && noSecond,
            "第3轮关闭=" + closedRound3 + " 有强攻选项=" + hasPower + " 补给7→5=" + supplySpent
            + " 额度已用=" + quotaUsed + " 击杀=" + killed + " 营地-2=" + campDrop + " 本轮不可再用=" + noSecond);
    }

    /// <summary>11.5 官方算例：3/3 强攻打 2/4 盾卫士 → 击杀，乙承担战损 1 并触发清线 1。</summary>
    private static void V03PowerStrikeKillsThroughHpThreshold()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(atk);
        var guard = Unit(CardCatalog.ShieldGuard, true, 0);
        eng.S.Players[1].Board.Add(guard);
        eng.S.Players[0].Supply = 7;

        // 普通攻击：3 伤，盾卫士剩 1 血，不阵亡
        var plain = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.PowerStrike && !x.TargetCamp);
        eng.Apply(plain);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        bool survivedPlain = guard.CurrentHp == 1 && eng.S.Players[1].Board.Count == 1;

        eng.S.CurrentPlayer = 0;
        eng.S.Players[0].ActionsLeft = 3;
        eng.S.Players[0].Supply = 7;
        atk.AttackedThisRound = false;   // 用例需要同一单位再攻一次，手动清掉本轮已攻击标记
        int campBefore = eng.S.Players[1].Camp;
        var power = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        bool hasPower = power != null;
        eng.Apply(power);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        bool killed = eng.S.Players[1].Board.Count == 0;
        bool tollAndClear = eng.S.Players[1].Camp == campBefore - 2;

        Check("11.5 强攻跨过生命阈值：普通 3 伤留 1 血，强攻 4 伤击杀并产生战损+清线",
            survivedPlain && hasPower && killed && tollAndClear,
            "普通后剩1血=" + survivedPlain + " 有强攻=" + hasPower + " 击杀=" + killed
            + " 营地-" + (campBefore - eng.S.Players[1].Camp));
    }

    /// <summary>6.6：强攻不提高本次或之后的反击。</summary>
    private static void V03PowerStrikeDoesNotBoostCounter()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);           // 3/3
        eng.S.Players[0].Board.Add(atk);
        var guard = Unit(CardCatalog.ShieldGuard, true, 0);     // 2 攻
        eng.S.Players[1].Board.Add(guard);
        eng.S.Players[0].Supply = 7;

        var power = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        eng.Apply(power);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        bool counterNormal = atk.Damage == 2;
        Check("6.6 强攻不提高反击：3/3 强攻打 2 攻盾卫士仍只吃 2 点反击", counterNormal,
            "攻击者累计伤害=" + atk.Damage + "（应为 2）");
    }

    /// <summary>6.6：0 攻单位可以强攻造成 1 点主动伤害；若被掩护则为 0。</summary>
    private static void V03PowerStrikeZeroAtk()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        var weak = Unit(CardCatalog.BannerGuard, true, 0);
        weak.TempAtkMod = -5;                       // 当前攻击力截到 0
        eng.S.Players[0].Board.Add(weak);
        var dummy = Unit(CardCatalog.BannerGuard, true, 0);   // 1/6
        eng.S.Players[1].Board.Add(dummy);
        eng.S.Players[0].Supply = 7;
        eng.S.Players[1].CoverUsed = true;          // 先让防守方不能掩护，测「无掩护 = 1 伤」

        var power = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        bool hasPower = power != null;
        int hpBefore = dummy.CurrentHp;
        eng.Apply(power);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }
        bool oneDamage = dummy.CurrentHp == hpBefore - 1;

        // 第二段：0 攻 + 强攻 + 掩护 → 0 伤
        var t2 = NewSetup(4);
        var eng2 = t2.Eng;
        var weak2 = Unit(CardCatalog.BannerGuard, true, 0);
        weak2.TempAtkMod = -5;
        eng2.S.Players[0].Board.Add(weak2);
        var dummy2 = Unit(CardCatalog.BannerGuard, true, 0);
        eng2.S.Players[1].Board.Add(dummy2);
        eng2.S.Players[0].Supply = 7;
        eng2.S.Players[1].Supply = 5;
        int hp2 = dummy2.CurrentHp;
        var power2 = eng2.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        eng2.Apply(power2);
        bool windowOpened = eng2.Pending == PendingKind.Cover;
        while (eng2.Pending == PendingKind.Cover) { eng2.ResolveCover(true); }
        bool zeroDamage = dummy2.CurrentHp == hp2;

        Check("6.6 0 攻强攻造成 1 点主动伤害，被掩护后为 0",
            hasPower && oneDamage && windowOpened && zeroDamage,
            "有强攻=" + hasPower + " 无掩护1伤=" + oneDamage + " 开窗口=" + windowOpened + " 掩护后0伤=" + zeroDamage);
    }

    /// <summary>6.5-1：强攻提交失败不扣资源。第 3 轮或补给不足时 Apply 应返回 null 且补给不变。</summary>
    private static void V03PowerStrikeInvalidCostsNothing()
    {
        var t = NewSetup(3);
        var eng = t.Eng;
        var atk = Unit(CardCatalog.Assault, true, 0);
        eng.S.Players[0].Board.Add(atk);
        eng.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        eng.S.Players[0].Supply = 7;
        var forged = new GameAction
        {
            Kind = ActionKind.Attack, ActorUid = atk.Card.Uid,
            TargetUid = eng.S.Players[1].Board[0].Card.Uid, TargetCamp = false, PowerStrike = true,
        };
        LogEntry r = eng.Apply(forged);
        bool rejected = r == null && eng.S.Players[0].Supply == 7 && !eng.S.Players[0].PowerStrikeUsed
                        && !atk.AttackedThisRound;
        Check("6.5 强攻提交失败不扣资源（第 3 轮伪造强攻被拒，补给与额度不变）", rejected,
            "返回=" + (r == null ? "null" : "日志") + " 补给=" + eng.S.Players[0].Supply
            + " 额度已用=" + eng.S.Players[0].PowerStrikeUsed + " 已攻击=" + atk.AttackedThisRound);
    }

    /// <summary>6.6：额度属于玩家，换防与晋阶都不重置；新的一轮才恢复。</summary>
    private static void V03PowerStrikeQuotaPersistsThroughRepositionAndUpgrade()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        var back = Unit(CardCatalog.Assault, false, 0);      // 后排（无远射不能攻击）
        var front = Unit(CardCatalog.ShieldGuard, true, 1);  // 前排
        p.Board.Add(back);
        p.Board.Add(front);
        eng.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        p.PowerStrikeUsed = true;

        var rep = eng.LegalActions().Find(x => x.Kind == ActionKind.Reposition);
        bool hasRep = rep != null;
        eng.Apply(rep);
        bool keptAfterRep = p.PowerStrikeUsed;

        p.Supply = 7;
        p.ActionsLeft = 3;
        eng.S.CurrentPlayer = 0;
        p.ActionIndexThisRound = 5;
        var up = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "up_shield_captain");
        bool hasUp = up != null;
        eng.Apply(up);
        bool keptAfterUp = p.PowerStrikeUsed;

        Check("6.6 强攻额度属于玩家：换防与晋阶都不重置",
            hasRep && keptAfterRep && hasUp && keptAfterUp,
            "有换防=" + hasRep + " 换防后仍已用=" + keptAfterRep + " 有晋阶=" + hasUp + " 晋阶后仍已用=" + keptAfterUp);
    }

    /// <summary>7.1：主动伤害为 0 时跳过掩护窗口（不弹窗、不扣费、不消耗额度）。</summary>
    private static void V03ZeroDamageSkipsCoverWindow()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        var weak = Unit(CardCatalog.BannerGuard, true, 0);
        weak.TempAtkMod = -5;
        eng.S.Players[0].Board.Add(weak);
        eng.S.Players[1].Board.Add(Unit(CardCatalog.BannerGuard, true, 0));
        eng.S.Players[1].Supply = 5;

        var plain = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.PowerStrike && !x.TargetCamp);
        eng.Apply(plain);
        bool noWindow = eng.Pending == PendingKind.None;
        bool noSpend = eng.S.Players[1].Supply == 5 && !eng.S.Players[1].CoverUsed;
        Check("7.1 主动伤害为 0 时跳过掩护窗口", noWindow && noSpend,
            "窗口=" + eng.Pending + " 防守补给=" + eng.S.Players[1].Supply + " 掩护已用=" + eng.S.Players[1].CoverUsed);
    }

    /// <summary>12.1：开启「本轮自动放弃掩护」后不再弹窗，也不自动花费补给。</summary>
    private static void V03AutoDeclineCover()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        eng.S.Players[0].Board.Add(Unit(CardCatalog.Assault, true, 0));
        eng.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        eng.S.Players[1].Supply = 5;
        eng.S.Players[1].AutoDeclineCover = true;

        var atk = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.TargetCamp);
        eng.Apply(atk);
        bool noWindow = eng.Pending == PendingKind.None;
        bool noSpend = eng.S.Players[1].Supply == 5 && !eng.S.Players[1].CoverUsed;

        // 撤回设置后重新开窗
        var t2 = NewSetup(4);
        var eng2 = t2.Eng;
        eng2.S.Players[0].Board.Add(Unit(CardCatalog.Assault, true, 0));
        eng2.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        eng2.S.Players[1].Supply = 5;
        var atk2 = eng2.LegalActions().Find(x => x.Kind == ActionKind.Attack && !x.TargetCamp);
        eng2.Apply(atk2);
        bool windowBack = eng2.Pending == PendingKind.Cover;

        Check("12.1 自动放弃掩护：不弹窗、不自动花补给；撤回后恢复弹窗",
            noWindow && noSpend && windowBack,
            "自动时无窗口=" + noWindow + " 未花补给=" + noSpend + " 撤回后有窗口=" + windowBack);
    }

    /// <summary>9.1：侦察员入场开调序窗口，由玩家决定保持或交换。</summary>
    private static void V03ScoutEntryReorderWindow()
    {
        var t = NewSetup(3);
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        var scoutCard = new CardInstance(_uid++, CardCatalog.Scout);
        p.Hand.Add(scoutCard);
        // 保证牌库顶两张是不同费用，才能判断是否交换
        p.Deck.Clear();
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Crossbow));   // 费 3
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Scout));      // 费 1
        for (int i = 0; i < 5; i++) { p.Deck.Add(new CardInstance(_uid++, CardCatalog.Assault)); }

        var deploy = eng.LegalActions().Find(x => x.Kind == ActionKind.Deploy && x.CardUid == scoutCard.Uid);
        bool hasDeploy = deploy != null;
        eng.Apply(deploy);
        bool window = eng.Pending == PendingKind.Reorder && eng.PendingReorderCards.Count == 2;
        string top1 = eng.PendingReorderCards[0].Def.Name;
        eng.ResolveReorder(true);
        bool swapped = eng.S.Players[0].Deck[0].Def.Name == "侦察员" && eng.S.Players[0].Deck[1].Def.Name == "机弩手";
        bool actionConsumed = eng.S.Players[0].ActionsLeft == 2;

        Check("9.1 侦察员入场开调序窗口，玩家选择交换后生效且不额外消耗行动",
            hasDeploy && window && top1 == "机弩手" && swapped && actionConsumed,
            "可部署=" + hasDeploy + " 窗口=" + window + " 第1张=" + top1
            + " 交换后=" + eng.S.Players[0].Deck[0].Def.Name + "/" + eng.S.Players[0].Deck[1].Def.Name
            + " 剩余行动=" + eng.S.Players[0].ActionsLeft);
    }

    /// <summary>9.2：应急调度先开候选窗口，取消不付费，确认后才付费入手并洗牌。</summary>
    private static void V03DispatchSearchWindow()
    {
        var t = NewSetup(3);
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        p.Deck.Clear();
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Assault));
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Crossbow));
        p.Deck.Add(new CardInstance(_uid++, CardCatalog.Assault));

        var dispatch = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "sup_dispatch");
        bool hasAction = dispatch != null;
        eng.Apply(dispatch);
        bool window = eng.Pending == PendingKind.SearchPick && eng.PendingSearchCards.Count == 2;
        // 候选按牌名聚合：突击队员 2 张只出现一次
        bool aggregated = window && eng.PendingSearchCards.Count == 2;
        eng.CancelSearch();
        bool cancelFree = eng.S.Players[0].Supply == 7
                          && !eng.S.Players[0].ReserveUsedThisRound
                          && eng.S.Players[0].ActionsLeft == 3
                          && !eng.S.Players[0].FindReserve("sup_dispatch").Used;

        var dispatch2 = eng.LegalActions().Find(x => x.Kind == ActionKind.Reserve && x.ReserveId == "sup_dispatch");
        bool stillAvailable = dispatch2 != null;
        eng.Apply(dispatch2);
        int handBefore = eng.S.Players[0].Hand.Count;
        string picked = eng.PendingSearchCards[1].Def.Name;
        eng.ResolveSearchPick(1);
        bool paid = eng.S.Players[0].Supply == 4 && eng.S.Players[0].ReserveUsedThisRound;
        bool inHand = eng.S.Players[0].Hand.Count == handBefore + 1
                      && eng.S.Players[0].Hand.Exists(c => c.Def.Name == picked);
        bool reserveUsed = eng.S.Players[0].FindReserve("sup_dispatch").Used;

        Check("9.2 应急调度：候选窗口可选可取消，确认后付 3 补给入手并洗牌",
            hasAction && window && aggregated && cancelFree && stillAvailable && paid && inHand && reserveUsed,
            "有动作=" + hasAction + " 窗口=" + window + " 聚合=" + aggregated + " 取消免费=" + cancelFree
            + " 可再次调用=" + stillAvailable + " 付费后补给=" + eng.S.Players[0].Supply
            + " 入手=" + inHand + " 预备已用=" + reserveUsed);
    }

    /// <summary>3.1 / 3.4：三套预组合法；越界构筑给出具体原因。</summary>
    private static void V03DeckValidation()
    {
        string err0, err1, err2;
        bool okBasic = CardCatalog.ValidateDeck(CardCatalog.PreconDeckBasic, "guard_shield", CardCatalog.PreconReserve, out err0);
        bool okAggro = CardCatalog.ValidateDeck(CardCatalog.PreconDeckAggro, "unit_a", CardCatalog.PreconReserve, out err1);
        bool okRanged = CardCatalog.ValidateDeck(CardCatalog.PreconDeckRanged, "unit_c", CardCatalog.PreconReserve, out err2);

        var tooMany = new string[24];
        for (int i = 0; i < 24; i++) { tooMany[i] = "guard_shield"; }
        string err3;
        bool rejectDup = !CardCatalog.ValidateDeck(tooMany, "guard_shield", CardCatalog.PreconReserve, out err3);

        var shortDeck = new string[23];
        for (int i = 0; i < 23; i++) { shortDeck[i] = CardCatalog.PreconDeckBasic[i]; }
        string err4;
        bool rejectShort = !CardCatalog.ValidateDeck(shortDeck, "guard_shield", CardCatalog.PreconReserve, out err4);

        string err5;
        bool rejectForeign = !CardCatalog.ValidateDeck(CardCatalog.PreconDeckRanged, "unit_d", CardCatalog.PreconReserve, out err5);
        bool reasonOk = err3 != null && err3.Contains("同名") && err4 != null && err4.Contains("24")
                        && err5 != null && err5.Contains("不在该牌组");

        Check("3.1 构筑校验：三套预组合法，同名/张数/领航越界均给出具体原因",
            okBasic && okAggro && okRanged && rejectDup && rejectShort && rejectForeign && reasonOk,
            "基准=" + okBasic + " 进攻=" + okAggro + " 远射=" + okRanged + " 抓到重复=" + rejectDup
            + " 抓到张数=" + rejectShort + " 抓到领航越界=" + rejectForeign
            + " 原因文案=" + (err3 ?? "-") + " | " + (err4 ?? "-") + " | " + (err5 ?? "-"));
    }

    /// <summary>10.1：4 张新单位的数值与关键词。</summary>
    private static void V03NewUnitsStats()
    {
        bool a = CardCatalog.UnitA.Cost == 3 && CardCatalog.UnitA.Atk == 4 && CardCatalog.UnitA.Hp == 3;
        bool b = CardCatalog.UnitB.Cost == 3 && CardCatalog.UnitB.Atk == 2 && CardCatalog.UnitB.Hp == 5 && CardCatalog.UnitB.Guard;
        bool c = CardCatalog.UnitC.Cost == 3 && CardCatalog.UnitC.Atk == 3 && CardCatalog.UnitC.Hp == 2 && CardCatalog.UnitC.Ranged;
        bool d = CardCatalog.UnitD.Cost == 1 && CardCatalog.UnitD.Atk == 2 && CardCatalog.UnitD.Hp == 1;
        bool allBasic = CardCatalog.UnitA.IsBasicUnit && CardCatalog.UnitB.IsBasicUnit
                        && CardCatalog.UnitC.IsBasicUnit && CardCatalog.UnitD.IsBasicUnit;
        Check("10.1 新单位数值：A 3费4/3、B 3费2/5护卫、C 3费3/2远射、D 1费2/1",
            a && b && c && d && allBasic,
            "A=" + a + " B=" + b + " C=" + c + " D=" + d + " 都是基础单位=" + allBasic);
    }

    /// <summary>10.1：单位A/D 不能套用现有晋阶牌。</summary>
    private static void V03UnitACannotUpgrade()
    {
        var t = NewSetup(3);
        var eng = t.Eng;
        var p = eng.S.Players[0];
        p.Supply = 7;
        var ua = Unit(CardCatalog.UnitA, true, 0);
        var ud = Unit(CardCatalog.UnitD, true, 1);
        p.Board.Add(ua);
        p.Board.Add(ud);
        var upgrades = eng.LegalActions().FindAll(x => x.Kind == ActionKind.Reserve
            && (x.ReserveId == "up_shield_captain" || x.ReserveId == "up_breaker" || x.ReserveId == "up_precise_crossbow"));
        bool none = upgrades.Count == 0;
        Check("10.1 单位A/单位D 不能套用现有 3 张晋阶牌", none, "可用晋阶动作数=" + upgrades.Count);
    }

    /// <summary>5.3：V0.3 放宽为「自己任意一次主行动提交前」，不再限于第一次。</summary>
    private static void V03RepositionAnyTimeBeforeSubmit()
    {
        var t = NewSetup(3);
        var eng = t.Eng;
        var p = eng.S.Players[0];
        var back = Unit(CardCatalog.Assault, false, 0);
        var front = Unit(CardCatalog.ShieldGuard, true, 1);
        p.Board.Add(back);
        p.Board.Add(front);
        p.ActionsLeft = 1;              // 已经用掉 2 次主行动
        p.ActionIndexThisRound = 2;
        bool stillLegal = eng.LegalActions().Exists(x => x.Kind == ActionKind.Reposition);
        var rep = eng.LegalActions().Find(x => x.Kind == ActionKind.Reposition);
        int actionsBefore = p.ActionsLeft;
        eng.Apply(rep);
        bool noActionCost = p.ActionsLeft == actionsBefore;
        bool usedFlag = p.RepositionUsed;
        bool consumed = p.ActionsLeft == actionsBefore && actionsBefore == 1;

        Check("5.3 换防可在任意一次主行动提交前进行，且不消耗主行动",
            stillLegal && noActionCost && usedFlag && consumed,
            "第3次行动前仍可换防=" + stillLegal + " 不消耗行动=" + noActionCost
            + " 标记已用=" + usedFlag + " 剩余行动=" + p.ActionsLeft);
    }

    /// <summary>1.3：终局说明要能给出各项损失，所以引擎按来源累计且与营地变化一致。</summary>
    private static void V03LossBreakdownBookkeeping()
    {
        var t = NewSetup(4);
        var eng = t.Eng;
        eng.S.Players[0].Board.Add(Unit(CardCatalog.Assault, true, 0));
        eng.S.Players[1].Board.Add(Unit(CardCatalog.ShieldGuard, true, 0));
        eng.S.Players[0].Supply = 7;
        var atk = eng.LegalActions().Find(x => x.Kind == ActionKind.Attack && x.PowerStrike && !x.TargetCamp);
        eng.Apply(atk);
        while (eng.Pending == PendingKind.Cover) { eng.ResolveCover(false); }

        GameState s = eng.S;
        int sumA = s.LossToll[0] + s.LossDirect[0] + s.LossClear[0] + s.LossTide[0];
        int sumB = s.LossToll[1] + s.LossDirect[1] + s.LossClear[1] + s.LossTide[1];
        bool campMatches = (GameEngine.CampStart - sumA) == s.Players[0].Camp
                           && (GameEngine.CampStart - sumB) == s.Players[1].Camp;
        bool tollRecorded = s.LossToll[1] == 1 && s.LossClear[1] == 1;
        Check("1.3 终局说明的各项损失与营地变化一致（战损/直击/清线/界潮）",
            campMatches && tollRecorded,
            "甲损失合计=" + sumA + "→营地" + s.Players[0].Camp + "；乙损失合计=" + sumB + "→营地"
            + s.Players[1].Camp + "；乙战损=" + s.LossToll[1] + " 清线=" + s.LossClear[1]);
    }
}
#endif
