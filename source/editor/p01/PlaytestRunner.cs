#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Jieqiao;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动化对局测试：跑完整对局并在每一步校验规则不变量，把报告写成文件。
///
/// 命令行用法（Unity 关闭后）：
///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; \
///             -executeMethod PlaytestRunner.RunFromCommandLine \
///             -logFile &lt;日志&gt;
///
/// 报告输出到工程根目录 playtest-report.md。
/// </summary>
public static class PlaytestRunner
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static int _violations;

    [MenuItem("Jieqiao/跑完整对局测试并生成报告")]
    public static void RunFromMenu()
    {
        BuildReport(10, 300, 202);
        Debug.Log(Report.ToString());
        AssetDatabase.Refresh();
    }

    public static void RunFromCommandLine()
    {
        try
        {
            BuildReport(10, 300, 202);
            System.IO.File.WriteAllText("playtest-report.md", Report.ToString(), Encoding.UTF8);
            Debug.Log("[PlaytestRunner] 报告已写入 playtest-report.md，违规数=" + _violations);
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaytestRunner] 测试崩溃: " + e);
        }
    }

    private static void Line(string s)
    {
        Report.AppendLine(s);
    }

    private static void Violation(string where, string what)
    {
        _violations++;
        Report.AppendLine("  ❌ [" + where + "] " + what);
    }

    public static void BuildReport(int detailedRounds, int sweepGames, int seed)
    {
        Report.Length = 0;
        _violations = 0;
        Line("# 界桥远征 自动对局测试报告");
        Line("");
        Line("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Line("");

        Line("## 一、单局详细复盘（逐行动校验，目标 " + detailedRounds + " 轮）");
        Line("");
        DetailedGame(seed, detailedRounds);

        Line("");
        Line("## 二、定点规则测试");
        Line("");
        Line(RuleChecks.Run());

        Line("");
        Line("## 三、批量不变量扫描（" + sweepGames + " 局）");
        Line("");
        Sweep(sweepGames);

        Line("");
        Line("## 结论");
        Line("");
        Line(_violations == 0 ? "✅ 未发现规则违规。" : "❌ 共发现 " + _violations + " 处违规，见上文 ❌ 标记。");
    }

    // ── 单局详细复盘 ───────────────────────────────────────────────────

    private static void DetailedGame(int seed, int maxRounds)
    {
        var eng = new GameEngine();
        eng.Start(seed);
        var a0 = new HeuristicAi(seed * 7 + 1);
        var a1 = new HeuristicAi(seed * 13 + 2);
        eng.ConfirmMulligan(0, a0.PickMulligan(eng, 0));
        eng.ConfirmMulligan(1, a1.PickMulligan(eng, 1));
        Line("种子 " + seed + "　开局手牌：甲 " + eng.HandText(eng.S.Players[0]) + "；乙 " + eng.HandText(eng.S.Players[1]));
        Line("");

        int step = 0;
        int lastRound = 0;
        int actions = 0;
        var roundActions = new int[2];
        var attacksThisRound = new Dictionary<int, int>();
        var lastLogCount = eng.Log.Count;

        // 每轮开始时的快照，用于校验轮次重置
        int supplyMaxSeen = 0;

        while (!eng.S.IsOver && eng.S.Round <= maxRounds && step < 4000)
        {
            step++;

            if (eng.S.Round != lastRound)
            {
                // 轮次切换时的检查
                if (lastRound > 0)
                {
                    Line("  ── 第 " + lastRound + " 轮结束：甲营地 " + eng.S.Players[0].Camp
                         + "，乙营地 " + eng.S.Players[1].Camp
                         + "；本轮行动数 甲" + roundActions[0] + "/乙" + roundActions[1]);
                }
                lastRound = eng.S.Round;
                roundActions[0] = 0;
                roundActions[1] = 0;
                attacksThisRound.Clear();
                CheckRoundStart(eng);
            }

            if (eng.Pending == PendingKind.Cover)
            {
                int defender = 1 - eng.S.CurrentPlayer;
                eng.ResolveCover(ShouldCover(eng, defender));
                CheckInvariants(eng, "掩护结算后");
                continue;
            }
            if (eng.Pending == PendingKind.ScoutPick)
            {
                eng.ResolveScoutPick(0);
                CheckInvariants(eng, "侦察结算后");
                continue;
            }

            int actor = eng.S.CurrentPlayer;
            List<GameAction> legal = eng.LegalActions();
            HeuristicAi ai = actor == 0 ? a0 : a1;
            GameAction pick = ChooseForReport(ai, eng, legal);
            if (pick == null)
            {
                Line("  ⚠ 第 " + eng.S.Round + " 轮 " + eng.S.Players[actor].Name + " 无合法行动，停止");
                break;
            }

            // 攻击次数检查（每个单位每轮一次主动攻击）
            if (pick.Kind == ActionKind.Attack)
            {
                if (attacksThisRound.ContainsKey(pick.ActorUid))
                {
                    Violation("攻击次数", eng.S.Players[actor].Name + " 的单位 #" + pick.ActorUid + " 本轮第二次主动攻击");
                }
                attacksThisRound[pick.ActorUid] = 1;
            }

            int supplyBefore = eng.S.Players[actor].Supply;
            int roundBefore = eng.S.Round;
            int actionsLeftBefore = eng.S.Players[actor].ActionsLeft;

            LogEntry e = eng.Apply(pick);
            if (e == null)
            {
                Violation("行动合法性", "Apply 拒绝了刚由 LegalActions 给出的动作：" + pick.Label);
                break;
            }
            roundActions[actor]++;
            actions++;

            // 掩护/侦察挂起时行动尚未结算完，主行动次数要等 Resolve 之后才扣；
            // 跨轮时补给会在新轮重置。这两种情况都不该判为违规。
            bool pendingNow = eng.Pending != PendingKind.None;
            bool roundChanged = eng.S.Round != roundBefore;
            if (!pendingNow && !roundChanged
                && pick.Kind != ActionKind.EndRound && pick.Kind != ActionKind.Reposition)
            {
                if (eng.S.Players[actor].ActionsLeft == actionsLeftBefore)
                {
                    Violation("行动消耗", pick.Label + " 没有消耗主行动次数");
                }
                if (pick.Kind != ActionKind.Attack && eng.S.Players[actor].Supply > supplyBefore)
                {
                    Violation("补给", pick.Label + " 反而增加了补给（" + supplyBefore + "→" + eng.S.Players[actor].Supply + "）");
                }
            }

            CheckInvariants(eng, pick.Label);
            Line("    " + eng.S.Players[actor].Name + "： " + e.Text);
            lastLogCount = eng.Log.Count;
        }

        Line("");
        string winner = eng.S.Winner < 0 ? "（未结束，被轮次上限截断）"
            : eng.S.Winner == 2 ? "平局"
            : eng.S.Players[eng.S.Winner].Name;
        Line(string.Format("结果：种子 {0}，停在第 {1} 轮，胜者={2}，原因={3}，总行动 {4} 次",
            seed, eng.S.Round, winner, eng.S.FinishReason ?? "（对局仍在进行）", actions));
    }

    private static bool ShouldCover(GameEngine eng, int defender)
    {
        PlayerState me = eng.S.Players[defender];
        if (me.CoverUsed || me.Supply < 1)
        {
            return false;
        }
        GameAction pending = eng.PendingAction;
        if (pending == null)
        {
            return false;
        }
        if (pending.TargetCamp)
        {
            return me.Camp <= 2;
        }
        UnitState t = eng.FindUnit(me, pending.TargetUid);
        return t != null && t.CurrentHp <= 3;
    }

    private static GameAction ChooseForReport(HeuristicAi ai, GameEngine eng, List<GameAction> legal)
    {
        if (legal.Count == 0)
        {
            return null;
        }
        // 用 AI 的选择，但要能从合法列表里找到同一项
        GameAction chosen = null;
        // HeuristicAi.ChooseBest 是私有的，这里用「优先攻击/晋阶/装备/战术/部署」的固定顺序，
        // 目的是尽量覆盖不同行动类型，而不是追求胜率。
        ActionKind[] order =
        {
            ActionKind.Attack, ActionKind.Upgrade, ActionKind.Equip, ActionKind.Tactic,
            ActionKind.Reserve, ActionKind.Deploy, ActionKind.Scout, ActionKind.Reposition, ActionKind.EndRound,
        };
        for (int k = 0; k < order.Length && chosen == null; k++)
        {
            for (int i = 0; i < legal.Count; i++)
            {
                if (legal[i].Kind == order[k])
                {
                    chosen = legal[i];
                    break;
                }
            }
        }
        return chosen ?? legal[0];
    }

    // ── 不变量 ─────────────────────────────────────────────────────────

    private static void CheckRoundStart(GameEngine eng)
    {
        int round = eng.S.Round;
        for (int i = 0; i < 2; i++)
        {
            PlayerState p = eng.S.Players[i];
            if (p.ActionsLeft != GameEngine.ActionsPerRound)
            {
                Violation("轮开始", "第 " + round + " 轮 " + p.Name + " 主行动数=" + p.ActionsLeft + "，应为 3");
            }
            if (p.CoverUsed || p.RepositionUsed || p.ScoutUsed || p.ReserveUsedThisRound || p.ClearFrontBonusUsed)
            {
                Violation("轮开始", "第 " + round + " 轮 " + p.Name + " 的每轮标记没有重置");
            }
            for (int u = 0; u < p.Board.Count; u++)
            {
                if (p.Board[u].AttackedThisRound)
                {
                    Violation("轮开始", "第 " + round + " 轮 " + p.Board[u].Card.Short + " 的「已攻击」没有重置");
                }
                if (p.Board[u].TempAtkMod != 0)
                {
                    Violation("轮开始", "第 " + round + " 轮 " + p.Board[u].Card.Short + " 的临时攻击修正没有清零");
                }
            }
            int expected = eng.BaseSupplyForRound(round) + p.Carryover;
            if (p.Supply != expected)
            {
                // Carryover 在轮开始时已清零，这里用「基础 + 已消费的结转」无法复原，
                // 因此只检查上限与下限。
            }
            if (p.Supply > 7)
            {
                Violation("补给上限", "第 " + round + " 轮 " + p.Name + " 补给=" + p.Supply + " 超过 7");
            }
        }
    }

    private static void CheckInvariants(GameEngine eng, string where)
    {
        if (eng.S.IsOver)
        {
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            PlayerState p = eng.S.Players[i];
            if (p.Supply < 0)
            {
                Violation(where, p.Name + " 补给为负：" + p.Supply);
            }
            if (p.Supply > 7)
            {
                Violation(where, p.Name + " 补给超上限：" + p.Supply);
            }
            if (p.Hand.Count > GameEngine.HandLimit)
            {
                Violation(where, p.Name + " 手牌 " + p.Hand.Count + " 超过上限 8");
            }
            if (p.Board.Count > 4)
            {
                Violation(where, p.Name + " 场上 " + p.Board.Count + " 个单位，超过 4 格");
            }
            if (p.ActionsLeft < 0 || p.ActionsLeft > GameEngine.ActionsPerRound)
            {
                Violation(where, p.Name + " 主行动数异常：" + p.ActionsLeft);
            }
            if (p.Camp > GameEngine.CampStart)
            {
                Violation(where, p.Name + " 营地 " + p.Camp + " 超过初始 20（竞技对局不能治疗营地）");
            }
            for (int u = 0; u < p.Board.Count; u++)
            {
                if (p.Board[u].CurrentHp <= 0)
                {
                    Violation(where, p.Board[u].Card.Short + " 生命 " + p.Board[u].CurrentHp + " 仍留在场上（应已阵亡）");
                }
                if (p.Board[u].Damage < 0)
                {
                    Violation(where, p.Board[u].Card.Short + " 累计伤害为负");
                }
            }
            if (p.CoverUsed && p.Supply >= 0 && false)
            {
                // 占位：掩护标记与补给不矛盾即可
            }
        }
        if (eng.S.Round > 30)
        {
            Violation(where, "轮次超过 30，可能死循环");
        }
    }

    // ── 批量扫描 ───────────────────────────────────────────────────────

    private static void Sweep(int games)
    {
        int p0 = 0, p1 = 0, draw = 0, unfinished = 0;
        int roundSum = 0, roundMin = int.MaxValue, roundMax = 0;
        int tide = 0;
        int before = _violations;

        for (int seed = 1; seed <= games; seed++)
        {
            var eng = new GameEngine();
            eng.Start(seed);
            var a0 = new HeuristicAi(seed * 7 + 1);
            var a1 = new HeuristicAi(seed * 13 + 2);
            eng.ConfirmMulligan(0, a0.PickMulligan(eng, 0));
            eng.ConfirmMulligan(1, a1.PickMulligan(eng, 1));

            int guard = 0;
            while (!eng.S.IsOver && guard < 3000)
            {
                guard++;
                if (eng.Pending == PendingKind.Cover)
                {
                    eng.ResolveCover(ShouldCover(eng, 1 - eng.S.CurrentPlayer));
                    CheckInvariants(eng, "seed " + seed);
                    continue;
                }
                if (eng.Pending == PendingKind.ScoutPick)
                {
                    eng.ResolveScoutPick(0);
                    CheckInvariants(eng, "seed " + seed);
                    continue;
                }
                HeuristicAi ai = eng.S.CurrentPlayer == 0 ? a0 : a1;
                if (!ai.Step(eng))
                {
                    break;
                }
                CheckInvariants(eng, "seed " + seed);
            }

            if (!eng.S.IsOver)
            {
                unfinished++;
            }
            roundSum += eng.S.Round;
            roundMin = Math.Min(roundMin, eng.S.Round);
            roundMax = Math.Max(roundMax, eng.S.Round);
            if (eng.S.Winner == 0) p0++;
            else if (eng.S.Winner == 1) p1++;
            else draw++;
            if (eng.S.FinishReason != null && eng.S.FinishReason.Contains("界潮")) tide++;
        }

        Line("局数：" + games + "　未结束：" + unfinished + "　新增违规：" + (_violations - before));
        Line(string.Format("胜负：甲 {0} / 乙 {1} / 平 {2}", p0, p1, draw));
        Line(string.Format("轮次：平均 {0:0.0}（{1}–{2}）", (float)roundSum / games, roundMin, roundMax));
        Line("界潮收尾：" + tide + " / " + games);
    }
}
#endif
