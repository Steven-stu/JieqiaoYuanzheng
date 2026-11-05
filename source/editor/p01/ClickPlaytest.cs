#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Jieqiao;
using Jieqiao.UI;

/// <summary>
/// 界面点击对局测试：不调用引擎推进，而是用 EventSystem 射线 + ExecuteEvents
/// 模拟真人点击（点手牌 → 点目标格/敌方单位/营地 → 点按钮），再检查引擎日志是否推进。
///
/// 为什么要有这个入口：
///  - 之前每轮都在临时脚本里重写一遍点击器，几次漏掉了两条必须的坑，误判成游戏缺陷；
///  - 这里把坑固化下来：① 本帧刚显示的元素 depth == -1 要走 onClick 回退；
///    ② 重编译后要先 stop/play，否则 FindObjectOfType 可能拿到旧实例（_reserve == null）。
///
/// 用法：进入 Play 模式 → 菜单 Jieqiao/界面点击对局测试。结果写 raw-click-games.csv。
/// </summary>
public static class ClickPlaytest
{
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly BindingFlags Public = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>人类侧按"先打单位、再战术、再部署…"的固定优先级挑动作，用来模拟一个不含策略的玩家。</summary>
    private static readonly ActionKind[] Priority =
    {
        ActionKind.Attack, ActionKind.Tactic, ActionKind.Upgrade, ActionKind.Equip,
        ActionKind.Reserve, ActionKind.Deploy, ActionKind.Reposition, ActionKind.Scout, ActionKind.EndRound,
    };

    private static Battle2DUI _ui;
    private static EventSystem _es;
    private static int _depthFallbacks;
    private static int _clickCount;

    [MenuItem("Jieqiao/界面点击对局测试（需 Play 模式）")]
    public static void RunFromMenu()
    {
        int[] seeds = { 20261, 20262, 20263, 20264, 20265 };
        string csv = RunGames(seeds, true);
        string path = System.IO.Path.Combine(Application.dataPath, "..", "raw-click-games.csv");
        System.IO.File.WriteAllText(path, csv, new UTF8Encoding(false));
        Debug.Log("[ClickPlaytest] 已写出 " + path);
    }

    /// <summary>跑若干局，返回逐局 CSV。reportToConsole 为 true 时同时打摘要。</summary>
    public static string RunGames(int[] seeds, bool reportToConsole)
    {
        _ui = UnityEngine.Object.FindObjectOfType<Battle2DUI>();
        if (_ui == null)
        {
            Debug.LogError("[ClickPlaytest] 找不到 Battle2DUI：请在 Play 模式下运行");
            return string.Empty;
        }
        _es = UnityEngine.Object.FindObjectOfType<EventSystem>();
        if (_es == null)
        {
            Debug.LogError("[ClickPlaytest] 找不到 EventSystem：点击无法反射/派发");
            return string.Empty;
        }
        // 旧实例检查：重编译后未重新进入 Play 会让 BuildScene 的产物为 null。
        var reserve = typeof(Battle2DUI).GetField("_reserve", Private).GetValue(_ui) as Array;
        if (reserve == null)
        {
            Debug.LogError("[ClickPlaytest] UI 尚未构建（_reserve 为 null）：先退出 Play 再重新进入");
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("seed,rounds,humanClicks,clickFailures,covers,clearBonus,directCamp,winner,finishReason,campA,campB,depthFallbacks,reorders,searches");
        int totGames = 0, totRounds = 0, totClicks = 0, totFails = 0, totCovers = 0, totClear = 0, totCamp = 0, totDepth = 0;

        foreach (int seed in seeds)
        {
            _depthFallbacks = 0;
            _clickCount = 0;
            var newGame = typeof(Battle2DUI).GetMethod("NewGame", Public);
            var vsAiField = typeof(Battle2DUI).GetField("_vsAi", Private);
            vsAiField.SetValue(_ui, true);
            newGame.Invoke(_ui, new object[] { seed });

            var eng = (GameEngine)typeof(Battle2DUI).GetField("_engine", Private).GetValue(_ui);
            var ai = (HeuristicAi)typeof(Battle2DUI).GetField("_ai", Private).GetValue(_ui);
            eng.ConfirmMulligan(0, ai.PickMulligan(eng, 0));
            eng.ConfirmMulligan(1, ai.PickMulligan(eng, 1));
            Refresh();
            Canvas.ForceUpdateCanvases();

            int steps = 0, human = 0, fails = 0, covers = 0, reorders = 0, searches = 0;
            while (!eng.S.IsOver && steps < 900)
            {
                steps++;
                Refresh();
                Canvas.ForceUpdateCanvases();
                if (eng.Pending == PendingKind.Cover) { covers++; eng.ResolveCover(false); continue; }
                if (eng.Pending == PendingKind.ScoutPick) { eng.ResolveScoutPick(0); continue; }
                // V0.3 新窗口：调序与应急调度都要通过界面按钮完成（不直接调引擎）
                if (eng.Pending == PendingKind.Reorder)
                {
                    reorders++;
                    bool swap = ((seed + reorders) % 2) == 0;   // 交替覆盖「保持」与「交换」两条路径
                    Click(swap ? "Swap" : "Keep");
                    if (eng.Pending == PendingKind.Reorder) { eng.ResolveReorder(swap); }
                    continue;
                }
                if (eng.Pending == PendingKind.SearchPick)
                {
                    searches++;
                    Click("Pick0");
                    if (eng.Pending == PendingKind.SearchPick) { eng.ResolveSearchPick(0); }
                    continue;
                }
                if (eng.S.CurrentPlayer == 1) { ai.Step(eng); continue; }

                var legal = eng.LegalActions();
                if (legal.Count == 0) { break; }
                GameAction pick = null;
                for (int k = 0; k < Priority.Length && pick == null; k++)
                {
                    for (int i = 0; i < legal.Count; i++)
                    {
                        if (legal[i].Kind == Priority[k]) { pick = legal[i]; break; }
                    }
                }
                if (pick == null) { pick = legal[0]; }

                var me = eng.S.Players[0];
                int logBefore = eng.Log.Count;
                DriveOneAction(eng, pick, me);

                bool applied = eng.Log.Count > logBefore || eng.Pending != PendingKind.None;
                if (applied) { human++; }
                else { fails++; eng.Apply(pick); }   // 点击路径失效时用引擎兜底，保证对局继续
            }

            int clearBonus = 0, directCamp = 0;
            for (int i = 0; i < eng.Log.Count; i++)
            {
                string t = eng.Log[i].Text;
                if (t.Contains("首次清空敌方前排")) { clearBonus++; }
                if (t.Contains("攻击 敌方营地")) { directCamp++; }
            }
            string reason = eng.S.FinishReason == null ? "未结束" : Compact(eng.S.FinishReason);
            sb.AppendLine(string.Join(",", new[]
            {
                seed.ToString(), eng.S.Round.ToString(), human.ToString(), fails.ToString(),
                covers.ToString(), clearBonus.ToString(), directCamp.ToString(),
                eng.S.Winner.ToString(), reason,
                eng.S.Players[0].Camp.ToString(), eng.S.Players[1].Camp.ToString(),
                _depthFallbacks.ToString(), reorders.ToString(), searches.ToString(),
            }));

            totGames++; totRounds += eng.S.Round; totClicks += human; totFails += fails;
            totCovers += covers; totClear += clearBonus; totCamp += directCamp; totDepth += _depthFallbacks;
            if (reportToConsole)
            {
                Debug.Log(string.Format(
                    "[ClickPlaytest] 种子 {0}：轮 {1}，点击 {2}，失败 {3}，掩护 {4}，清线奖励 {5}，直击营地 {6}，调序 {9}，检索 {10}，结果 {7}（{8}）",
                    seed, eng.S.Round, human, fails, covers, clearBonus, directCamp, WinnerText(eng.S.Winner), reason, reorders, searches));
            }
        }

        if (reportToConsole)
        {
            Debug.Log(string.Format(
                "[ClickPlaytest] 合计：{0} 局 / {1} 轮 / 点击 {2} / 失败 {3} / 掩护 {4} / 清线奖励 {5} / 直击营地 {6} / depth 回退 {7}",
                totGames, totRounds, totClicks, totFails, totCovers, totClear, totCamp, totDepth));
        }
        return sb.ToString();
    }

    private static void DriveOneAction(GameEngine eng, GameAction pick, PlayerState me)
    {
        switch (pick.Kind)
        {
            case ActionKind.Attack:
            {
                // V0.3 §6.6：强攻要先点开关，再点目标；普通攻击要把开关关掉。
                if (pick.PowerStrike) { Click("BtnPower"); }
                string actor = SlotName(eng, 0, pick.ActorUid);
                if (actor != null) { Click(actor); }
                if (pick.TargetCamp) { Click("EnemyCampText"); }
                else
                {
                    string tgt = SlotName(eng, 1, pick.TargetUid);
                    if (tgt != null) { Click(tgt); }
                }
                break;
            }
            case ActionKind.Deploy:
            {
                int hi = me.Hand.FindIndex(c => c.Uid == pick.CardUid);
                Click("Hand" + hi);
                Click("Hand" + hi);   // 新版流程：首点只查看，再点同一张＝确认使用
                Click("MySlot" + ((pick.ToSlot.Front ? 0 : 2) + pick.ToSlot.Index));
                break;
            }
            case ActionKind.Tactic:
            case ActionKind.Equip:
            {
                int hi = me.Hand.FindIndex(c => c.Uid == pick.CardUid);
                Click("Hand" + hi);
                Click("Hand" + hi);   // 同上
                int tgt = pick.Kind == ActionKind.Equip ? pick.ActorUid : pick.TargetUid;
                if (tgt >= 0)
                {
                    string sn = SlotName(eng, 0, tgt) ?? SlotName(eng, 1, tgt);
                    if (sn != null) { Click(sn); }
                }
                break;
            }
            case ActionKind.Reserve:
            {
                int ri = me.Reserve.FindIndex(r => r.Def.Id == pick.ReserveId);
                Click("Reserve" + ri);
                if (pick.TargetUid >= 0)
                {
                    string sn = SlotName(eng, 0, pick.TargetUid) ?? SlotName(eng, 1, pick.TargetUid);
                    if (sn != null) { Click(sn); }
                }
                break;
            }
            case ActionKind.Reposition:
            {
                Click("BtnRepos");
                string mover = SlotName(eng, 0, pick.ActorUid);
                if (mover != null) { Click(mover); }
                if (pick.TargetUid >= 0)
                {
                    string other = SlotName(eng, 0, pick.TargetUid);
                    if (other != null) { Click(other); }
                }
                else
                {
                    Click("MySlot" + ((pick.ToSlot.Front ? 0 : 2) + pick.ToSlot.Index));
                }
                break;
            }
            case ActionKind.Scout:
                Click("BtnScout");
                break;
            case ActionKind.EndRound:
                Click("BtnEnd");
                break;
        }
    }

    /// <summary>
    /// 真实射线点击。三条必备处理：
    ///  1) 不可交互的按钮直接返回 false（UI 没提供这条路）；
    ///  2) 射线命中空 / 命中背后元素时，若目标自身 depth == -1（本帧刚显示），退回 onClick.Invoke()；
    ///  3) 计数以便在报告里区分"真失败"和"同帧时序"。
    /// </summary>
    private static bool Click(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) { return false; }
        var btn = go.GetComponent<Button>();
        if (btn != null && !btn.interactable) { return false; }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { return false; }
        Vector3 center = rt.TransformPoint(rt.rect.center);
        var ped = new PointerEventData(_es) { position = new Vector2(center.x, center.y) };
        var hits = new List<RaycastResult>();
        _es.RaycastAll(ped, hits);
        _clickCount++;
        if (hits.Count > 0)
        {
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            if (handler != null)
            {
                ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
                return true;
            }
        }
        var cr = go.GetComponent<CanvasRenderer>();
        if (cr != null && cr.absoluteDepth == -1 && btn != null && btn.enabled && btn.interactable)
        {
            _depthFallbacks++;
            btn.onClick.Invoke();
            return true;
        }
        return false;
    }

    private static void Refresh()
    {
        typeof(Battle2DUI).GetMethod("Refresh", Private).Invoke(_ui, null);
    }

    private static string SlotName(GameEngine eng, int player, int uid)
    {
        var p = eng.S.Players[player];
        for (int i = 0; i < p.Board.Count; i++)
        {
            if (p.Board[i].Card.Uid == uid)
            {
                int idx = (p.Board[i].Slot.Front ? 0 : 2) + p.Board[i].Slot.Index;
                return (player == 0 ? "MySlot" : "EnemySlot") + idx;
            }
        }
        return null;
    }

    private static string WinnerText(int winner)
    {
        if (winner == 0) { return "甲胜"; }
        if (winner == 1) { return "乙胜"; }
        if (winner == 2) { return "平局"; }
        return "未定";
    }

    /// <summary>把结束原因压成不含逗号的短标签，避免破坏 CSV。</summary>
    private static string Compact(string s)
    {
        s = s.Replace(',', '，');
        if (s.Contains("同时归零")) { return "界潮同时归零"; }
        if (s.Contains("界潮")) { return "界潮单方归零"; }
        if (s.Contains("归零")) { return "行动结算归零"; }
        return s.Length > 20 ? s.Substring(0, 20) : s;
    }
}
#endif
