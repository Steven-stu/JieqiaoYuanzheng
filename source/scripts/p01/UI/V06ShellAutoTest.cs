using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 自动玩家（V0.7 界面巡检）。
    ///
    /// 干的事：挂着它进 Play 模式，它会像一个真人一样用**真实射线点击**把大厅 8 个页面、
    /// 图鉴翻页、组卡选牌、签到领取、单人开局、战斗部署/让过全部点一遍，
    /// 同时检查这几类"玩家看得见但代码不报错"的问题：
    ///   · 皮肤没加载（该有图的地方是纯色）
    ///   · 文字溢出控件（preferredWidth/Height 超框）
    ///   · 控件零尺寸 / 按钮不可点 / 被别的层挡住（射线打不中它）
    ///   · 页面切换失败、状态没变（点了没反应）
    /// 结果写到 reports/v07-shell-autotest.txt。
    /// </summary>
    public sealed class V06ShellAutoTest : MonoBehaviour
    {
        public bool autoStart = true;
        public float stepDelay = 0.12f;
        public int battleActions = 40;

        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<string> _issues = new List<string>();
        private V06ShellUI _shell;
        private V06BattleUI _battle;
        private int _clicks;
        private int _clickFails;
        private bool _done;

        /// <summary>只有命令行带 -autotest 才跑（正式包不会自己点）。编辑器里也可用 EditorPrefs 强制开启。</summary>
        public static bool WantedByCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-autotest", System.StringComparison.OrdinalIgnoreCase)) { return true; }
            }
#if UNITY_EDITOR
            // 编辑器里排查用：MCP 里 SetBool("V06AutoTestForce", true) 后进 Play 就会跑
            if (UnityEditor.EditorPrefs.GetBool("V06AutoTestForce", false)) { return true; }
#endif
            return false;
        }

        private void Start()
        {
            if (!autoStart || !WantedByCommandLine()) { enabled = false; return; }
            Debug.Log("[V06AutoTest] 命令行 -autotest 命中，开始实机巡检（autoStart=" + autoStart + "）");
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            yield return null;
            _shell = FindObjectOfType<V06ShellUI>();
            _battle = FindObjectOfType<V06BattleUI>();
            _log.AppendLine("=== V0.7 大厅巡检（真实射线点击） ===");
            if (_shell == null) { Finish("找不到 V06ShellUI"); yield break; }
            _log.AppendLine("皮肤：已加载图 " + V06UiV07.CachedCount + " 张；动画套数 " + V06UiV07.AnimSetsLoaded);
            _log.AppendLine("音频：" + V06Audio.StatusForTest);
            _log.AppendLine("存档：" + V06Save.SavePathForTest + (File.Exists(V06Save.SavePathForTest) ? "（存在）" : "（不存在）"));

            // 1) 大厅：静态体检
            _shell.Show();
            yield return new WaitForSeconds(stepDelay);
            AuditPage("大厅");
            AuditSkins();

            // 2) 点遍每个导航
            string[] pages = new string[] { "codex", "deck", "profile", "net", "checkin", "shop", "trade", "home" };
            for (int i = 0; i < pages.Length; i++)
            {
                bool ok = ClickByName("Nav_" + pages[i]);
                yield return new WaitForSeconds(stepDelay);
                bool switched = _shell.CurrentPage == pages[i];
                _log.AppendLine("  导航 → " + pages[i] + "：" + (ok && switched ? "OK" : "失败(点击=" + ok + " 当前页=" + _shell.CurrentPage + ")"));
                if (!switched) { AddIssue("导航按钮 " + pages[i] + " 点击无效"); }
                AuditPage(Pages[pagIndex(pages[i])]);
                if (pages[i] == "checkin") { yield return StartCoroutine(TestCheckIn()); }
                if (pages[i] == "shop") { yield return StartCoroutine(TestShop()); }
                if (pages[i] == "trade") { yield return StartCoroutine(TestTrade()); }
                Debug.Log("[V06AutoTest] 页面 " + pages[i] + " 完成，点击 " + _clicks + " 次，问题 " + _issues.Count + " 条");
            }

            // 音频：点过一堆按钮后，音效缓存应该 > 1
            _log.AppendLine("  音频：点击后缓存音效 " + V06Audio.CachedClips + " 个（含 BGM），BGM=" + V06Audio.CurrentBgm);
            if (V06Audio.CachedClips < 2) { AddIssue("点击音效没有真正加载（缓存 " + V06Audio.CachedClips + " 个）"); }

            // 3) 图鉴
            yield return StartCoroutine(TestCodex());
            Debug.Log("[V06AutoTest] 图鉴完成");
            // 4) 组卡
            yield return StartCoroutine(TestDeck());
            Debug.Log("[V06AutoTest] 组卡完成");
            // 5) 单人开局 + 战斗点击
            yield return StartCoroutine(TestBattle());
            Debug.Log("[V06AutoTest] 战斗完成，准备写报告");

            WriteReport(_clickFails == 0 ? "巡检完成（点击失败 " + _clickFails + " 次）" : "巡检完成（有失败点击）");
            Debug.Log("[V06AutoTest] DONE 问题 " + _issues.Count + " 条");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static string[] Pages = new string[] { "大厅", "图鉴", "组卡组", "个人资料", "联机", "每日签到", "商店", "交换" };
        private static int pagIndex(string id)
        {
            switch (id)
            {
                case "codex": return 1;
                case "deck": return 2;
                case "profile": return 3;
                case "net": return 4;
                case "checkin": return 5;
                case "shop": return 6;
                case "trade": return 7;
            }
            return 0;
        }

        // ───────────────────────── 各页面交互 ─────────────────────────

        private IEnumerator TestCodex()
        {
            ClickByName("Nav_codex");
            yield return new WaitForSeconds(stepDelay);
            int before = _shell.CurrentPage == "codex" ? 1 : 0;
            bool cell = ClickByName("CodexCell0");
            yield return new WaitForSeconds(stepDelay);
            _log.AppendLine("  图鉴：点第 1 格 " + (cell ? "OK" : "失败"));
            if (!cell) { AddIssue("图鉴格子点不动"); }
            bool next = ClickByName("CodexNext");
            yield return new WaitForSeconds(stepDelay);
            _log.AppendLine("  图鉴：翻页 " + (next ? "OK" : "失败"));
            AuditPage("图鉴（第 2 页）");
        }

        private IEnumerator TestDeck()
        {
            ClickByName("Nav_deck");
            yield return new WaitForSeconds(stepDelay);
            int picks = 0;
            for (int i = 0; i < 3; i++)
            {
                if (ClickByName("DeckCell" + i)) { picks++; }   // 卡墙格子名是 DeckCell（旧名 DeckPick 早已不存在，会误报"点不动"）
                yield return new WaitForSeconds(0.05f);
            }
            _log.AppendLine("  组卡：点了 " + picks + "/3 张卡");
            if (picks < 3) { AddIssue("组卡左侧卡池有 " + (3 - picks) + " 个格子点不动"); }
            bool save = ClickByName("DeckSave");
            yield return new WaitForSeconds(stepDelay);
            _log.AppendLine("  组卡：保存 " + (save ? "OK" : "失败"));
            AuditPage("组卡组");
        }

        private IEnumerator TestCheckIn()
        {
            int coins = V06Save.Data.coins, packs = V06Save.Data.packs, gems = V06Save.Data.gems;
            bool couldClaim = V06Save.CanCheckIn();
            bool clicked = ClickByName("CheckBtn");
            yield return new WaitForSeconds(0.35f);
            int dCoins = V06Save.Data.coins - coins, dPacks = V06Save.Data.packs - packs, dGems = V06Save.Data.gems - gems;
            bool changed = dCoins + dPacks + dGems > 0;
            string info = InfoTextOf("CheckInfo");
            _log.AppendLine("  签到：可领=" + couldClaim + " 点击=" + (clicked ? "OK" : "失败")
                + " 奖励 金币+" + dCoins + " 卡包+" + dPacks + " 宝石+" + dGems + " 文案=\"" + Trim(info, 30) + "\"");
            if (couldClaim && !changed) { AddIssue("今天明明可领，点了签到却没发奖励"); }
            if (!couldClaim && info.IndexOf("已签到", System.StringComparison.Ordinal) < 0)
            {
                AddIssue("已签到时界面没有提示『今天已签到』（文案：" + Trim(info, 24) + "）");
            }
            AuditPage("每日签到");
        }

        private IEnumerator TestShop()
        {
            bool clicked = false;
            Button[] btns = FindAllButtons();
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i].name.StartsWith("ShopBuy"))
                {
                    clicked = Click(btns[i].gameObject);
                    break;
                }
            }
            yield return new WaitForSeconds(stepDelay);
            _log.AppendLine("  商店：购买按钮 " + (clicked ? "可点" : "不存在（本轮是占位页）"));
            AuditPage("商店");
        }

        private IEnumerator TestTrade()
        {
            bool clicked = ClickByName("TradeOfferBtn");
            yield return new WaitForSeconds(stepDelay);
            _log.AppendLine("  交换：发起交换 " + (clicked ? "OK" : "不存在（本轮是占位页）"));
            AuditPage("交换");
        }

        private IEnumerator TestBattle()
        {
            ClickByName("Nav_home");
            yield return new WaitForSeconds(stepDelay);
            bool started = ClickByName("BtnSolo");
            yield return new WaitForSeconds(0.5f);
            bool inBattle = _battle != null && _battle.Engine != null && !_shell.Visible;
            _log.AppendLine("  单人开局：" + (started ? "点了" : "点不到") + "，进战斗=" + inBattle
                + (_battle != null && _battle.Engine != null ? ("（第 " + _battle.Engine.Round + " 轮）") : ""));
            if (!inBattle) { AddIssue("单人开局失败/没进战斗"); yield break; }
            // 战绩数量必须在开打前取样：打完那一刻 ShowResult 已经 RecordMatch 过了
            int recsBefore = V06Save.Data.records.Count;

            // 战斗里模拟玩家：点手牌 → 点空槽；没有部署就点让过
            int deployed = 0, passes = 0;
            for (int i = 0; i < battleActions; i++)
            {
                if (_battle.Engine == null || _battle.Engine.Finished || _shell.Visible) { break; }
                if (_battle.IsZoomOpen) { _battle.HideZoomForTest(); yield return null; continue; }
                if (_battle.IsResponding) { ClickByName("BtnDecline"); yield return new WaitForSeconds(0.05f); continue; }
                if (!_battle.IsPlayerTurn) { yield return new WaitForSeconds(0.12f); continue; }
                bool acted = false;
                for (int h = 0; h < 6 && !acted; h++)
                {
                    GameObject hand = FindWidget("Hand" + h);
                    if (hand == null || !hand.activeInHierarchy) { continue; }
                    if (!Click(hand)) { continue; }
                    yield return null;
                    for (int s = 0; s < 6 && !acted; s++)
                    {
                        GameObject slot = FindWidget(_battle.WidgetSlotName(_battle.PlayerCamp, s));
                        if (slot == null) { continue; }
                        string before = BattleFingerprint();
                        if (Click(slot) && BattleFingerprint() != before) { acted = true; deployed++; }
                    }
                }
                if (!acted)
                {
                    if (ClickByName("BtnPass")) { passes++; }
                    else { AddIssue("战斗里连『让过』都点不动（可能有层挡住）"); break; }
                }
                yield return new WaitForSeconds(0.12f);
            }
            _log.AppendLine("  战斗：部署/出牌 " + deployed + " 次，让过 " + passes + " 次，点击失败 " + _clickFails + " 次");

            // 继续推进直到打完（上限 300 次，正常 12 轮约 120 次）
            int extra = 0;
            while (_battle.Engine != null && !_battle.Engine.Finished && extra < 900)
            {
                extra++;
                if (_battle.IsZoomOpen) { _battle.HideZoomForTest(); yield return null; continue; }
                if (_battle.IsResponding) { ClickByName("BtnDecline"); yield return new WaitForSeconds(0.05f); continue; }
                if (!_battle.IsPlayerTurn) { yield return new WaitForSeconds(0.05f); continue; }
                bool acted2 = false;
                for (int h = 0; h < 4 && !acted2; h++)
                {
                    GameObject hand = FindWidget("Hand" + h);
                    if (hand == null || !hand.activeInHierarchy) { continue; }
                    if (!Click(hand)) { continue; }
                    yield return null;
                    for (int s = 0; s < 6 && !acted2; s++)
                    {
                        GameObject slot = FindWidget(_battle.WidgetSlotName(_battle.PlayerCamp, s));
                        if (slot == null) { continue; }
                        string b2 = BattleFingerprint();
                        if (Click(slot) && BattleFingerprint() != b2) { acted2 = true; }
                    }
                }
                if (!acted2) { ClickByName("BtnPass"); }
                yield return new WaitForSeconds(0.05f);
            }
            _log.AppendLine("  战斗：额外推进 " + extra + " 步，结束=" + (_battle.Engine != null && _battle.Engine.Finished));
            if (_battle.Engine != null && _battle.Engine.Finished)
            {
                yield return new WaitForSeconds(0.3f);
                _log.AppendLine("  战斗：已打完（" + _battle.Engine.FinishReason + "），结算记录数 " + V06Save.Data.records.Count
                    + "（新增 " + (V06Save.Data.records.Count - recsBefore) + "）");
                if (V06Save.Data.records.Count <= recsBefore) { AddIssue("打完一局但战绩没有写入存档"); }
                if (V06Audio.CurrentBgm != "battle") { AddIssue("战斗中 BGM 没切到 battle（当前 " + V06Audio.CurrentBgm + "）"); }
                // 结算演出回归：面板要出现，全屏特效层不能堆（堆了就是"屏幕发白卡死"）
                bool panelShown = _battle.ResultPanelVisible;
                yield return new WaitForSeconds(1.2f);
                int screenFx = _battle.ScreenFxCount;
                _log.AppendLine("  结算：面板显示=" + panelShown + "，1.2 秒后全屏特效层=" + screenFx);
                if (!panelShown) { AddIssue("打完一局但结算面板没出现"); }
                if (screenFx > 3) { AddIssue("结算特效层堆了 " + screenFx + " 张（应≤3，否则会发白卡死）"); }
                bool back = ClickByName("BtnBackLobby");
                yield return new WaitForSeconds(0.3f);
                _log.AppendLine("  回大厅：" + (back && _shell.Visible ? "OK" : "失败"));
                if (!(back && _shell.Visible)) { AddIssue("结算后回大厅按钮不可用"); }
            }
            else
            {
                _log.AppendLine("  （本轮没打完，属于正常：巡检只打若干步）");
            }
        }

        private string BattleFingerprint()
        {
            if (_battle == null || _battle.Engine == null) { return "none"; }
            var sb = new StringBuilder();
            V06Engine e = _battle.Engine;
            sb.Append(e.Round).Append('/').Append(e.Active).Append('/').Append(e.Players[0].MainActionsUsed);
            for (int c = 0; c < 2; c++)
            {
                for (int s = 0; s < 6; s++)
                {
                    V06Unit u = e.Players[c].At(s);
                    sb.Append(u == null ? "." : (u.Uid + ":" + u.CurHp));
                }
            }
            return sb.ToString();
        }

        // ───────────────────────── 静态体检 ─────────────────────────

        private Transform ShellRoot
        {
            get
            {
                if (_shell == null) { return null; }
                var canvas = _shell.transform.Find("ShellCanvas");
                if (canvas == null) { return null; }
                return canvas.GetChild(canvas.childCount - 1);
            }
        }

        private void AuditPage(string label)
        {
            Transform root = ShellRoot;
            if (root == null) { return; }
            int texts = 0, overflowW = 0, overflowH = 0, zeroRect = 0;
            var sample = new StringBuilder();
            Text[] all = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Text t = all[i];
                if (!t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) { continue; }
                texts++;
                Rect r = t.rectTransform.rect;
                if (r.width <= 1f || r.height <= 1f) { zeroRect++; continue; }
                float pw = t.preferredWidth;
                float ph = t.preferredHeight;
                // 判定要按"真实会不会被切掉"来，不然中文换行会被误报：
                //   横向：只有"不换行"的文本才会真的横向溢出
                //   纵向：换行了但高度不够 + Truncate → 真被截断
                if (t.horizontalOverflow == HorizontalWrapMode.Overflow && pw > r.width * 1.02f)
                {
                    overflowW++;
                    if (sample.Length < 160) { sample.Append("[" + Trim(t.text, 14) + " " + Mathf.RoundToInt(pw) + ">" + Mathf.RoundToInt(r.width) + "] "); }
                }
                else if (t.verticalOverflow == VerticalWrapMode.Truncate && ph > r.height * 1.05f)
                {
                    overflowH++;
                    if (sample.Length < 160) { sample.Append("(截断 " + Trim(t.text, 12) + " " + Mathf.RoundToInt(ph) + ">" + Mathf.RoundToInt(r.height) + ") "); }
                }
            }
            _log.AppendLine("  体检[" + label + "]：文字 " + texts + "，横向溢出 " + overflowW + "，纵向溢出 " + overflowH + "，零尺寸 " + zeroRect
                + (sample.Length > 0 ? ("　例：" + sample) : ""));
            if (overflowW > 0) { AddIssue(label + "：有 " + overflowW + " 处文字不换行溢出（" + sample.ToString().Trim() + "）"); }
            if (overflowH > 0) { AddIssue(label + "：有 " + overflowH + " 处文字被截断（" + sample.ToString().Trim() + "）"); }
        }

        private void AuditSkins()
        {
            string[] mustHave = new string[] { "bg_home", "bg_codex", "bg_deckbuilder", "bg_profile", "bg_shop",
                "bg_checkin", "bg_trade", "home_bar_top", "avatar_01", "home_entry_battle", "home_entry_shop",
                "home_entry_checkin", "home_entry_codex", "home_entry_profile", "panel_main", "tab_normal",
                "icon_nav_battle", "icon_nav_shop", "icon_nav_checkin", "icon_nav_codex", "icon_nav_profile",
                "icon_nav_deck", "icon_nav_trade", "codex_cell", "codex_cell_locked", "checkin_cell_normal",
                "checkin_cell_ready", "checkin_cell_claimed", "checkin_board", "deck_panel_right", "panel_main",
                "banner_victory", "banner_defeat", "banner_draw", "reward_glow" };
            var missing = new List<string>();
            for (int i = 0; i < mustHave.Length; i++)
            {
                if (V06UiV07.Skin(mustHave[i]) == null) { missing.Add(mustHave[i]); }
            }
            _log.AppendLine("  皮肤自检：" + (mustHave.Length - missing.Count) + "/" + mustHave.Length + " 命中"
                + (missing.Count > 0 ? ("；缺：" + string.Join("、", missing.ToArray())) : ""));
            if (missing.Count > 0) { AddIssue("皮肤缺图 " + missing.Count + " 张：" + string.Join("、", missing.ToArray())); }
        }

        // ───────────────────────── 点击工具（真实射线） ─────────────────────────

        private Button[] FindAllButtons()
        {
            Transform root = ShellRoot;
            return root == null ? new Button[0] : root.GetComponentsInChildren<Button>(true);
        }

        private bool ClickByName(string goName)
        {
            GameObject go = FindWidget(goName);
            if (go == null) { return false; }
            return Click(go);
        }

        private bool Click(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) { _clickFails++; return false; }
            var rt = go.GetComponent<RectTransform>();
            var canvas = go.GetComponentInParent<Canvas>();
            if (rt == null || canvas == null) { _clickFails++; return false; }
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null) { _clickFails++; return false; }
            Canvas.ForceUpdateCanvases();
            Vector3 world = rt.TransformPoint(rt.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, world);
            var ped = new PointerEventData(EventSystem.current);
            ped.position = screen;
            var hits = new List<RaycastResult>();
            raycaster.Raycast(ped, hits);
            if (hits.Count == 0) { _clickFails++; AddIssue("控件 " + go.name + " 射线打不中（可能被隐藏或移出屏幕）"); return false; }
            GameObject hitGo = hits[0].gameObject;
            Transform t = hitGo.transform;
            bool same = false;
            while (t != null) { if (t.gameObject == go) { same = true; break; } t = t.parent; }
            if (!same)
            {
                _clickFails++;
                AddIssue("控件 " + go.name + " 被 " + hitGo.name + " 挡住");
                return false;
            }
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitGo);
            if (handler == null) { _clickFails++; AddIssue("控件 " + go.name + " 没有点击处理"); return false; }
            ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
            _clicks++;
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

        private string InfoTextOf(string widgetName)
        {
            GameObject go = FindWidget(widgetName);
            if (go == null) { return ""; }
            Text t = go.GetComponent<Text>();
            return t == null ? "" : t.text;
        }

        private static string Trim(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            s = s.Replace("\n", "/");
            return s.Length <= n ? s : s.Substring(0, n);
        }

        private void AddIssue(string s)
        {
            if (!_issues.Contains(s)) { _issues.Add(s); }
        }

        private void WriteReport(string tail)
        {
            _log.AppendLine();
            _log.AppendLine("=== 问题清单（" + _issues.Count + " 条） ===");
            for (int i = 0; i < _issues.Count; i++) { _log.AppendLine("  " + (i + 1) + ". " + _issues[i]); }
            _log.AppendLine();
            _log.AppendLine("点击 " + _clicks + " 次，失败 " + _clickFails + " 次。收尾：" + tail);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../reports/v07-shell-autotest.txt"));
            try { File.WriteAllText(path, _log.ToString(), new UTF8Encoding(false)); Debug.Log("[V06AutoTest] 报告 → " + path); }
            catch (System.Exception ex) { Debug.LogError("[V06AutoTest] 写报告失败：" + ex.Message); }
            Finish(tail);
        }

        private void Finish(string why)
        {
            if (_done) { return; }
            _done = true;
            Debug.Log("[V06AutoTest] 收尾：" + why + "（问题 " + _issues.Count + " 条）");
        }
    }
}
