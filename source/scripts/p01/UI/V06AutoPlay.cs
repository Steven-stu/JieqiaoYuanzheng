using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jieqiao;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 实机对局自动驱动（挂在 V06Battle 场景里，只在测试场景启用）。
    ///
    /// 它扮演玩家：每一步都用真实的 uGUI 射线点击（GraphicRaycaster → PointerClick），
    /// 不是直接调引擎。用于验证「玩家真的能点着打完一局」以及记录整局流水。
    /// 结果写到工程目录外的 v06-playtest-report.txt。
    /// </summary>
    public sealed class V06AutoPlay : MonoBehaviour
    {
        public bool autoStart = true;
        public float actionDelay = 0.12f;
        public int maxActions = 600;
        public bool stopPlayModeOnFinish = true;
        /// <summary>整场会话最长秒数（防呆：无论发生什么都必须收尾并写报告）。</summary>
        public float maxSessionSeconds = 600f;
        /// <summary>同一状态连续停留多少秒就强推一次（看门狗）。</summary>
        public float stallSeconds = 6f;
        /// <summary>在一次播放会话里连打几局（每局换种子与预组）。</summary>
        public int gamesToPlay = 1;
        /// <summary>只用测试牌库（索引 8–10）连打，用于验证装备/附印/战况/响应。</summary>
        public bool testDeckMode = false;
        /// <summary>V0.7：改用扩展测试预组（索引 11 兽群 / 12 少女）连打。</summary>
        public bool expansionDeckMode = false;
        /// <summary>V0.7：第一局开头做一次"融合演练"（注入两只素材 → 真实点击部署 → 点融合按钮）。</summary>
        public bool fusionDrill = false;

        private int _drillStage;                 // 0 未开始 / 1 部署B01 / 2 部署B11 / 3 点融合 / 4 完成
        private float? _deadSince;               // 引擎为空的起始时刻（用于识别脚本重载导致的中断）
        private int _drillFusions;
        private int _evolutionsSeen;
        private int _fusionsSeen;

        private V06BattleUI _ui;
        private readonly StringBuilder _report = new StringBuilder();
        private readonly List<string> _transcript = new List<string>();
        private int _fail;
        private int _done;
        private int _reservePlays;
        private int _equipPlays;
        private bool _finished;

        private void Start()
        {
            // 只有带 -autoplay 才驱动（正式包不自己打）
            bool want = false;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-autoplay", System.StringComparison.OrdinalIgnoreCase)) { want = true; }
            }
            if (!autoStart || !want) { enabled = false; return; }
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _report.AppendLine("V0.6 实机对局（真实射线点击）");
            yield return null;
            _ui = FindObjectOfType<V06BattleUI>();
            if (_ui == null) { Finish("找不到 V06BattleUI"); yield break; }
            // 等界面把第一帧画完（新建控件的 absoluteDepth 需要一帧）
            yield return null;
            yield return null;

            float sessionStart = Time.realtimeSinceStartup;
            float lastProgress = Time.realtimeSinceStartup;
            string lastFingerprint = "";
            int stallKicks = 0;

            int shots = 0;
            float nextShot = Time.realtimeSinceStartup + 1.0f;
            int played = 0;
            var gameResults = new System.Text.StringBuilder();
            int totalClicks = 0;
            int totalFails = 0;

            while (!_finished && _done < maxActions)
            {
                // UI 引用失效（重载/换场景后旧实例的 Engine 为空）时重新查找，
                // 否则协程会每帧 continue 空转、既不推进也不报错——本文件曾因此"静默停住"。
                if (_ui == null || _ui.Engine == null)
                {
                    var refind = FindObjectOfType<V06BattleUI>();
                    if (refind != null && refind.Engine != null)
                    {
                        _ui = refind;
                        _report.AppendLine("（重新获取到 V06BattleUI，继续测试）");
                    }
                    else
                    {
                        // 脚本重载后 Unity 不会重新调用 Start：界面对象还在、引擎字段却被清空，
                        // 这时重找也救不回来，早点收尾比静默空转好（本文件曾因此"没反应"）。
                        if (!_deadSince.HasValue) { _deadSince = Time.realtimeSinceStartup; }
                        else if (Time.realtimeSinceStartup - _deadSince.Value > 3f)
                        {
                            Finish("会话中断：V06BattleUI 的引擎为空（多半是编辑期触发了脚本重载），请重新进入 Play 再跑");
                            yield break;
                        }
                        if (Time.realtimeSinceStartup - sessionStart > maxSessionSeconds) { Finish("会话超时：找不到可用的 V06BattleUI"); yield break; }
                        yield return null;
                        continue;
                    }
                }
                _deadSince = null;

                // 整场会话兜底超时
                if (Time.realtimeSinceStartup - sessionStart > maxSessionSeconds)
                {
                    Finish("会话超时（" + maxSessionSeconds + " 秒）");
                    yield break;
                }

                V06Engine e = _ui.Engine;
                if (e == null) { yield return null; continue; }
                if (e.Finished)
                {
                    played++;
                    string who = e.Winner == _ui.PlayerCamp ? "玩家胜" : (e.Winner == -2 ? "平局" : "对手胜");
                    gameResults.AppendLine("  第 " + played + " 局：" + who + "　" + e.Round + " 轮　旗 "
                        + e.Players[0].FlagHp + ":" + e.Players[1].FlagHp + "　" + e.FinishReason);
                    totalClicks += _done; totalFails += _fail;
                    CountEvolution(e);
                    _report.AppendLine("第 " + played + " 局结束：" + who + "，" + e.Round + " 轮，旗 "
                        + e.Players[0].FlagHp + ":" + e.Players[1].FlagHp + "，点击 " + _done + " 次失败 " + _fail + " 次");
                    if (played >= gamesToPlay) { break; }
                    yield return new WaitForSeconds(0.5f);
                    if (testDeckMode)
                    {
                        int da = 8 + (played % 3);
                        int db = 8 + ((played + 1) % 3);
                        _ui.NewGame(unchecked(90000 + played * 7717), da, db);
                    }
                    else if (expansionDeckMode)
                    {
                        int baseIdx = Math.Max(0, V06DeckLibrary.Count - 2);   // 兽群 / 少女 两套扩展预组
                        int da = baseIdx + (played % 2);
                        int db = baseIdx + ((played + 1) % 2);
                        _ui.NewGame(unchecked(71000 + played * 4231), da, db);
                    }
                    else
                    {
                        int da = played % 8;
                        int db = (played * 3 + 1) % 8;
                        _ui.NewGame(unchecked(90000 + played * 7717), da, db);
                    }
                    _done = 0; _fail = 0;
                    _transcript.Clear();
                    lastFingerprint = "";
                    lastProgress = Time.realtimeSinceStartup;
                    stallKicks = 0;
                    yield return null;
                    continue;
                }

                if (Time.realtimeSinceStartup >= nextShot && shots < 3)
                {
                    nextShot = Time.realtimeSinceStartup + 8f;
                    shots++;
                    Shot("v06-battle-" + shots);
                    _report.AppendLine("截图 v06-battle-" + shots + ".png（第 " + e.Round + " 轮）");
                }

                if (e.Active != _ui.PlayerCamp) { yield return new WaitForSeconds(0.05f); continue; }

                // 看门狗：如果轮到我方却长时间没有任何状态变化，强推一次（让过）
                string fpNow = Fingerprint(e);
                if (fpNow != lastFingerprint)
                {
                    lastFingerprint = fpNow;
                    lastProgress = Time.realtimeSinceStartup;
                }
                else if (Time.realtimeSinceStartup - lastProgress > stallSeconds)
                {
                    stallKicks++;
                    lastProgress = Time.realtimeSinceStartup;
                    _report.AppendLine("⚠ 看门狗：" + stallSeconds + " 秒无进展（第 " + e.Round + " 轮），强制让过并继续");
                    bool kicked = ClickByPath("BtnPass");
                    if (!kicked) { Finish("看门狗兜底也点不到『让过』"); yield break; }
                    if (stallKicks > 30) { Finish("看门狗连续触发过多，放弃本局"); yield break; }
                    yield return null;
                    continue;
                }

                List<V06Action> acts = _ui.CurrentActions;
                if (acts == null || acts.Count == 0) { yield return null; continue; }

                // 右键详览浮层是"点任意处关闭"的模态层：留着它会把整个棋盘挡住（实测报过一串"被 CardZoom 遮挡"）
                if (_ui.IsZoomOpen) { _ui.HideZoomForTest(); yield return null; continue; }

                // V0.7 融合演练：用真实点击走一遍「部署素材 → 点融合按钮」
                if (fusionDrill && played == 0 && _drillStage < 4)
                {
                    yield return null;
                    DrillFusion(e);
                    yield return new WaitForSeconds(actionDelay);
                    continue;
                }

                V06Action pick = Choose(e, acts);
                if (pick == null) { Finish("没有可执行行动"); break; }

                // 让界面先过一帧：Refresh 刚显示出来的控件当帧射线还不可用
                yield return null;
                string before = Fingerprint(e);
                bool ok = ClickThrough(pick);
                string after = Fingerprint(e);
                _done++;

                if (!ok || before == after)
                {
                    _fail++;
                    _report.AppendLine("❌ 点击未生效 #" + _done + "：" + pick);
                    bool passed = ClickByPath("BtnPass");
                    if (!passed || Fingerprint(e) == before) { Finish("点击无法推进对局"); break; }
                }
                else if (_transcript.Count < 120)
                {
                    if (pick.FromReserve) { _reservePlays++; }
                    else if (pick.Kind == V06ActionKind.PlayCard) { _equipPlays++; }
                    _transcript.Add("R" + e.Round + " 玩家：" + pick
                        + "　→ 旗 " + e.Players[0].FlagHp + ":" + e.Players[1].FlagHp
                        + "　行动点 " + e.Players[0].ActionPoints);
                    if (_done % 25 == 0)
                    {
                        Debug.Log("[V06Playtest] 进度：第 " + played + " 局　第 " + e.Round + " 轮　点击 " + _done
                            + " 次　失败 " + _fail + " 次　帧 " + Time.frameCount);
                    }
                }
                yield return new WaitForSeconds(actionDelay);
            }

            V06Engine eng = _ui.Engine;
            _report.AppendLine();
            if (eng != null)
            {
                _report.AppendLine("结束：" + eng.FinishReason);
                _report.AppendLine("  胜方 " + (eng.Winner == _ui.PlayerCamp ? "玩家" : (eng.Winner == -2 ? "平局" : "对手"))
                    + "　轮数 " + eng.Round + "　旗 " + eng.Players[0].FlagHp + " : " + eng.Players[1].FlagHp);
            }
            _report.AppendLine("  ── 全部对局 ──");
            _report.Append(gameResults.ToString());
            _report.AppendLine("  合计点击 " + totalClicks + " 次，失败 " + totalFails + " 次（含未计入最后一局）");
            _report.AppendLine("  经界面调用的预备牌（晋阶/支援）" + _reservePlays + " 次，其他出牌（战术/装备/附印/战况）" + _equipPlays + " 次");
            _report.AppendLine("  V0.7 计数：进化 " + _evolutionsSeen + " 次，融合 " + _fusionsSeen + " 次，"
                + "融合演练成功 " + _drillFusions + " 次（0/1）");
            if (eng != null)
            {
                _report.AppendLine("  战斗日志共 " + eng.Log.Count + " 条，末尾 15 条：");
                int from = Math.Max(0, eng.Log.Count - 15);
                for (int i = from; i < eng.Log.Count; i++) { _report.AppendLine("    " + eng.Log[i]); }
                _report.AppendLine("  玩家操作流水（最多 40 条）：");
                for (int i = 0; i < Math.Min(40, _transcript.Count); i++) { _report.AppendLine("    " + _transcript[i]); }
            }
            Shot("v06-battle-final");
            Finish(_fail == 0 ? "完成" : "完成（有失败点击）");
        }

        /// <summary>
        /// V0.7 融合演练：注入两只融合素材 → 真实点击手牌与格位部署 → 点「融合」按钮。
        /// 目的是确认新按钮在真实射线路径下可点、且引擎确实合成了融合体。
        /// </summary>
        private void DrillFusion(V06Engine e)
        {
            V06Player me = e.Players[_ui.PlayerCamp];
            if (_drillStage == 0)
            {
                me.Hand.Insert(0, "B01");
                me.Hand.Insert(1, "B11");
                me.ActionPoints = 9;
                me.MainActionsUsed = 0;
                _ui.ForceRefresh();
                _drillStage = 1;
                _report.AppendLine("融合演练：注入 B01 + B11 到起手（会临时打破 30 张守恒，仅用于 UI 路径验证）");
                return;
            }
            if (_drillStage == 1)
            {
                int hi = me.Hand.IndexOf("B01");
                int slot = FirstEmptySlot(me);
                if (hi < 0 || hi >= 9 || slot < 0) { _report.AppendLine("融合演练：无法部署 B01（手牌 " + hi + " 槽 " + slot + "）"); _drillStage = 4; return; }
                bool c1 = ClickByPath("Hand" + hi);
                bool c2 = ClickByPath(SlotName(_ui.PlayerCamp, slot));
                _report.AppendLine("融合演练：点击部署 B01 → " + (c1 && c2 ? "成功" : "失败"));
                _drillStage = 2;
                return;
            }
            if (_drillStage == 2)
            {
                int hi = me.Hand.IndexOf("B11");
                int slot = FirstEmptySlot(me);
                if (hi < 0 || hi >= 9 || slot < 0) { _report.AppendLine("融合演练：无法部署 B11（手牌 " + hi + " 槽 " + slot + "）"); _drillStage = 4; return; }
                bool c1 = ClickByPath("Hand" + hi);
                bool c2 = ClickByPath(SlotName(_ui.PlayerCamp, slot));
                _report.AppendLine("融合演练：点击部署 B11 → " + (c1 && c2 ? "成功" : "失败"));
                _drillStage = 3;
                return;
            }
            if (_drillStage == 3)
            {
                _ui.ForceRefresh();
                V06Action f = _ui.BestFuseAction();
                if (f == null)
                {
                    _report.AppendLine("融合演练：刷新后仍没有融合行动（两只素材应已上场）");
                    _drillStage = 4;
                    return;
                }
                string before = Fingerprint(e);
                bool clicked = ClickByPath("BtnFuse");
                bool changed = Fingerprint(e) != before;
                bool onBoard = false;
                for (int s = 0; s < 6; s++)
                {
                    V06Unit u = me.At(s);
                    if (u != null && u.CardId == "B41") { onBoard = true; }
                }
                if (clicked && changed && onBoard) { _drillFusions++; }
                _report.AppendLine("融合演练：点『融合』按钮 → " + (clicked ? "命中" : "未命中")
                    + "，状态变化 " + (changed ? "是" : "否")
                    + "，场上出现 B41 双生狼蛛 " + (onBoard ? "是" : "否"));
                _drillStage = 4;
                return;
            }
        }

        private static int FirstEmptySlot(V06Player p)
        {
            for (int s = 0; s < V06Slot.Total; s++) { if (p.At(s) == null) { return s; } }
            return -1;
        }

        /// <summary>统计整局日志里的进化 / 融合次数。</summary>
        private void CountEvolution(V06Engine e)
        {
            for (int i = 0; i < e.Log.Count; i++)
            {
                if (e.Log[i].StartsWith("进化：")) { _evolutionsSeen++; }
                else if (e.Log[i].IndexOf(" 融合：") >= 0) { _fusionsSeen++; }
            }
        }

        private static V06Action Choose(V06Engine e, List<V06Action> acts)
        {
            V06Action best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < acts.Count; i++)
            {
                V06Engine f = e.Clone();
                string err;
                if (!f.Apply(acts[i], out err)) { continue; }
                int sc = V06Ai.Eval(f, e.Active);
                if (f.PendingAttack != null)
                {
                    List<V06Action> rs = f.LegalActions();
                    int worst = int.MaxValue;
                    for (int r = 0; r < rs.Count; r++)
                    {
                        V06Engine g = f.Clone();
                        string err2;
                        if (!g.Apply(rs[r], out err2)) { continue; }
                        int v = V06Ai.Eval(g, e.Active);
                        if (v < worst) { worst = v; }
                    }
                    if (worst != int.MaxValue) { sc = worst; }
                }
                if (sc > bestScore) { bestScore = sc; best = acts[i]; }
            }
            return best;
        }

        private string SlotName(int camp, int slot)
        {
            return "Slot" + ((camp == _ui.PlayerCamp ? 0 : 1) * 6 + slot);
        }

        private string Fingerprint(V06Engine e)
        {
            var sb = new StringBuilder();
            sb.Append(e.Round).Append('/').Append(e.Active).Append('/').Append(e.Finished).Append('/');
            for (int c = 0; c < 2; c++)
            {
                V06Player p = e.Players[c];
                sb.Append(p.FlagHp).Append(':').Append(p.ActionPoints).Append(':').Append(p.MainActionsUsed)
                  .Append(':').Append(p.Hand.Count).Append(':').Append(p.Deck.Count).Append(':').Append(p.Discard.Count).Append('|');
                for (int s = 0; s < 6; s++)
                {
                    V06Unit u = p.At(s);
                    sb.Append(u == null ? "." : (u.Uid + "," + u.CurHp + "," + u.CardId + "," + u.Growth));
                    sb.Append(' ');
                }
                sb.Append('|');
            }
            return sb.ToString();
        }

        private bool ClickThrough(V06Action a)
        {
            V06Player me = _ui.Engine.Players[_ui.PlayerCamp];
            V06Player foe = _ui.Engine.Players[1 - _ui.PlayerCamp];
            switch (a.Kind)
            {
                case V06ActionKind.Pass: return ClickByPath("BtnPass");
                case V06ActionKind.Cover: return ClickByPath("BtnCover");
                case V06ActionKind.Respond:
                    {
                        // 找到这是第几个响应按钮，点它
                        var list = _ui.RespondActions;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i].CardId == a.CardId) { return ClickByPath("Respond" + i); }
                        }
                        return false;
                    }
                case V06ActionKind.Decline: return ClickByPath("BtnDecline");
                case V06ActionKind.Fuse:
                    // 界面只有一个融合按钮，点它按"最优组合"执行
                    return ClickByPath("BtnFuse");
                case V06ActionKind.Deploy:
                    {
                        int hi = me.Hand.IndexOf(a.CardId);
                        if (hi < 0 || hi >= 9) { return false; }
                        bool c1 = ClickByPath("Hand" + hi);
                        bool c2 = ClickByPath(SlotName(_ui.PlayerCamp, a.Slot));
                        return c1 && c2;
                    }
                case V06ActionKind.PlayCard:
                    {
                        // 预备区（晋阶/支援）与手牌走不同的控件
                        string widget;
                        int idx;
                        if (a.FromReserve)
                        {
                            idx = me.Reserve.IndexOf(a.CardId);
                            widget = "Reserve" + idx;
                        }
                        else
                        {
                            idx = me.Hand.IndexOf(a.CardId);
                            widget = "Hand" + idx;
                        }
                        if (idx < 0) { return false; }
                        bool c1 = ClickByPath(widget);
                        if (a.Uid < 0) { return c1 && ClickByPath(widget); }
                        V06Unit mine = me.ByUid(a.Uid);
                        if (mine != null) { return c1 && ClickTargetFallback(a, mine.Slot, _ui.PlayerCamp); }
                        V06Unit enemyT = foe.ByUid(a.Uid);
                        if (enemyT != null) { return c1 && ClickTargetFallback(a, enemyT.Slot, 1 - _ui.PlayerCamp); }
                        return false;
                    }
                case V06ActionKind.Attack:
                    {
                        V06Unit u = me.ByUid(a.Uid);
                        if (u == null) { return false; }
                        bool c1 = ClickByPath(SlotName(_ui.PlayerCamp, u.Slot));
                        if (a.PowerStrike) { ClickByPath("BtnPower"); }
                        if (a.ToFlag) { return c1 && ClickByPath("EnemyFlagText"); }
                        V06Unit t = foe.ByUid(a.TargetUid);
                        if (t == null) { return false; }
                        return c1 && ClickByPath(SlotName(1 - _ui.PlayerCamp, t.Slot));
                    }
                case V06ActionKind.Swap:
                    {
                        V06Unit u = me.ByUid(a.Uid);
                        if (u == null) { return false; }
                        bool c0 = ClickByPath("BtnSwap");
                        bool c1 = ClickByPath(SlotName(_ui.PlayerCamp, u.Slot));
                        if (a.TargetUid >= 0)
                        {
                            V06Unit o = me.ByUid(a.TargetUid);
                            if (o == null) { return false; }
                            return c0 && c1 && ClickByPath(SlotName(_ui.PlayerCamp, o.Slot));
                        }
                        return c0 && c1 && ClickByPath(SlotName(_ui.PlayerCamp, a.TargetSlot));
                    }
            }
            return false;
        }

        /// <summary>战术目标点击：界面当前对战术目标只走"选牌→再点牌"，这里做一次兜底并记录。</summary>
        private bool ClickTargetFallback(V06Action a, int slot, int camp)
        {
            string before = Fingerprint(_ui.Engine);
            ClickByPath(SlotName(camp, slot));
            if (Fingerprint(_ui.Engine) != before) { return true; }
            string err;
            bool ok = _ui.Engine.Apply(a, out err);
            _report.AppendLine("  注：战术目标点击未走界面通道，已兜底执行 " + a + "（界面待补目标选择）");
            return ok;
        }

        private bool ClickByPath(string goName)
        {
            GameObject go = FindWidget(goName);
            if (go == null) { _report.AppendLine("  ⚠ 找不到控件 " + goName); return false; }
            var rt = go.GetComponent<RectTransform>();
            var canvas = go.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) { _report.AppendLine("  ⚠ 控件缺 RectTransform/Canvas：" + goName); return false; }
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) { _report.AppendLine("  ⚠ 缺 GraphicRaycaster"); return false; }

            Canvas.ForceUpdateCanvases();
            Vector3 world = rt.TransformPoint(rt.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, world);
            var ped = new PointerEventData(EventSystem.current);
            ped.position = screen;
            var hits = new List<RaycastResult>();
            raycaster.Raycast(ped, hits);
            if (hits.Count == 0) { _report.AppendLine("  ⚠ 射线未命中 " + goName + " @" + screen); return false; }
            GameObject hitGo = hits[0].gameObject;
            Transform t = hitGo.transform;
            bool same = false;
            while (t != null) { if (t.gameObject == go) { same = true; break; } t = t.parent; }
            if (!same) { _report.AppendLine("  ⚠ " + goName + " 被 " + hitGo.name + " 遮挡"); return false; }
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitGo);
            if (handler == null) { _report.AppendLine("  ⚠ " + goName + " 无点击处理器"); return false; }
            ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
            return true;
        }

        private GameObject FindWidget(string name)
        {
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].activeInHierarchy) { continue; }
                if (all[i].name == name) { return all[i]; }
            }
            return null;
        }

        private void Shot(string name)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports/" + name + ".png"));
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (Exception ex) { _report.AppendLine("截图失败：" + ex.Message); }
        }

        private void Finish(string why)
        {
            if (_finished) { return; }
            _finished = true;
            _report.AppendLine("收尾：" + why);
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports/v06-playtest-report.txt"));
                File.WriteAllText(path, _report.ToString(), new UTF8Encoding(false));
                Debug.Log("[V06Playtest] 报告已写入 " + path);
            }
            catch (Exception ex) { Debug.LogError("[V06Playtest] 写报告失败：" + ex.Message); }
            Debug.Log("[V06Playtest] DONE");
#if UNITY_EDITOR
            // 测试跑完自动退出播放模式，避免"边播边改代码"导致下一次进来还是旧程序集
            if (stopPlayModeOnFinish) { UnityEditor.EditorApplication.isPlaying = false; }
#endif
        }
    }
}
