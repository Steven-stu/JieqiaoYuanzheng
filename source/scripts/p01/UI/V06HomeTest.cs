using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Jieqiao.UI
{
    /// <summary>
    /// 开发用：**只针对主页面（大厅）** 的玩家视角实机测试。
    ///
    /// 与全站巡检（V06ShellAutoTest）的分工：
    ///   · 全站巡检走 8 个导航页，但从不点大厅里那 8 个入口大图；
    ///   · 本组件专点大厅：8 个入口、3 个货币芯片、卡组切换、开始对战，并且**用真实射线**判断
    ///     "这个按钮上面有没有别的控件把点击吃掉"（之前装饰层吃点击就是这么抓出来的）。
    ///
    /// 只有编辑器里把 EditorPrefs["V06HomeTest"] 置 1 才跑；正式包永不执行。
    /// 报告写到 reports/v07-home-test.txt。
    /// </summary>
    public sealed class V06HomeTest : MonoBehaviour
    {
        public float stepDelay = 0.35f;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<string> _issues = new List<string>();
        private int _clicks, _fails;
        private V06ShellUI _shell;
        private V06BattleUI _battle;

        private void Start()
        {
            if (!Wanted()) { enabled = false; return; }
            StartCoroutine(Run());
        }

        private static bool Wanted()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("V06HomeTest", false);
#else
            return false;
#endif
        }

        private static string OutPath
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports"));
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                return Path.Combine(dir, "v07-home-test.txt");
            }
        }

        private void AddIssue(string s) { if (!_issues.Contains(s)) { _issues.Add(s); } }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.2f);
            _log.AppendLine("=== 主页面玩家视角测试 ===");
            _shell = FindObjectOfType<V06ShellUI>();
            _battle = FindObjectOfType<V06BattleUI>();
            if (_shell == null) { Finish("找不到 V06ShellUI"); yield break; }
            _shell.Show();
            _shell.ShowPageForTest("home");
            yield return new WaitForSeconds(stepDelay);

            // ① 结构体检：越界 / 零尺寸 / 文字溢出
            AuditLayout();

            // ② 逐个入口：射线命中检查 + 真实点击 + 结果验证
            string[] entries = new string[] { "Entrybattle", "Entrynet", "Entrycodex", "Entrydeck",
                "Entrycheckin", "Entryshop", "Entryprofile", "Entrytrade" };
            string[] expect = new string[] { "battle", "net", "codex", "deck", "checkin", "shop", "profile", "trade" };
            for (int i = 0; i < entries.Length; i++)
            {
                // 先回大厅再做射线探测（上一轮点击会把界面切走，先探测会误报"被遮挡"）
                _shell.Show();
                _shell.ShowPageForTest("home");
                yield return new WaitForSeconds(0.25f);
                string hit = RaycastTop(entries[i]);
                if (hit != entries[i])
                {
                    AddIssue("入口 " + entries[i] + " 被遮挡：射线打到的是 " + (string.IsNullOrEmpty(hit) ? "（空）" : hit));
                }
                bool clicked = Click(entries[i]);
                yield return new WaitForSeconds(stepDelay);
                string where;
                if (expect[i] == "battle")
                {
                    bool inBattle = _battle != null && _battle.Engine != null;
                    where = inBattle ? "进入对局" : "没进对局";
                    if (!inBattle) { AddIssue("入口" + entries[i] + "（开始对战）点了没进对局"); }
                    if (_battle != null) { _battle.BackToLobby(); }
                }
                else
                {
                    where = _shell.CurrentPage;
                    if (where != expect[i]) { AddIssue("入口 " + entries[i] + " 应到 " + expect[i] + "，实际到 " + where); }
                }
                _log.AppendLine("入口 " + entries[i] + "：命中=" + hit + " 点击=" + (clicked ? "OK" : "失败") + " → " + where);
            }

            // ③ 三个货币芯片：都应该跳到商店
            for (int i = 0; i < 3; i++)
            {
                _shell.Show(); _shell.ShowPageForTest("home");
                yield return new WaitForSeconds(0.15f);
                string name = "CurrChip" + i;
                string hit = RaycastTop(name);
                bool ok = Click(name);
                yield return new WaitForSeconds(stepDelay);
                string page = _shell.CurrentPage;
                _log.AppendLine("货币芯片 " + i + "：命中=" + hit + " 点击=" + (ok ? "OK" : "失败") + " → " + page);
                if (!ok || page != "shop") { AddIssue("货币芯片 " + name + " 点了没进商店（落点=" + page + "）"); }
            }

            // ④ 卡组切换：名字应该变
            _shell.Show(); _shell.ShowPageForTest("home");
            yield return new WaitForSeconds(stepDelay);
            string deckA = TextOf("HomeDeck");
            Click("BtnDeckNext");
            yield return new WaitForSeconds(stepDelay);
            string deckB = TextOf("HomeDeck");
            bool switched = deckA != deckB;
            _log.AppendLine("卡组切换：" + Trim(deckA, 40) + " → " + Trim(deckB, 40) + "  " + (switched ? "OK" : "没变化"));
            if (!switched) { AddIssue("大厅的『下一套』没有切换卡组（多套卡组时应有变化）"); }

            // ⑤ 编辑卡组
            Click("BtnGoDeck");
            yield return new WaitForSeconds(stepDelay);
            if (_shell.CurrentPage != "deck") { AddIssue("大厅『编辑卡组』没进组卡页（落点=" + _shell.CurrentPage + "）"); }
            _log.AppendLine("编辑卡组 → " + _shell.CurrentPage);

            // ⑥ 开始对战（大按钮）
            _shell.Show(); _shell.ShowPageForTest("home");
            yield return new WaitForSeconds(stepDelay);
            string soloHit = RaycastTop("BtnSolo");
            bool soloOk = Click("BtnSolo");
            yield return new WaitForSeconds(0.6f);
            bool inMatch = _battle != null && _battle.Engine != null && !_shell.Visible;
            _log.AppendLine("开始对战：命中=" + soloHit + " 点击=" + (soloOk ? "OK" : "失败") + " 进对局=" + inMatch);
            if (!inMatch) { AddIssue("大厅『开始对战』点了没进对局"); }
            if (_battle != null) { _battle.BackToLobby(); }
            yield return new WaitForSeconds(stepDelay);

            // ⑦ 悬停/可点性：所有可点控件都要能被射线打到
            AuditClickable();

            Finish("主页面测试完成");
        }

        // ───────────────────────── 体检 ─────────────────────────

        private void AuditLayout()
        {
            RectTransform page = _shell.PageForTest("home");
            if (page == null) { AddIssue("拿不到大厅页面节点"); return; }
            int bad = 0, zero = 0, overflow = 0;
            RectTransform root = _shell.RootForTest;
            float rootH = root == null ? 750f : root.rect.height;
            int n = page.childCount;
            for (int i = 0; i < n; i++)
            {
                RectTransform c = page.GetChild(i) as RectTransform;
                if (c == null || !c.gameObject.activeSelf) { continue; }
                float x = c.anchoredPosition.x, y = -c.anchoredPosition.y;
                float w = c.rect.width, h = c.rect.height;
                if (w < 1f || h < 1f) { zero++; AddIssue("大厅控件 " + c.name + " 尺寸为 0"); }
                if (x < -2f || y < -2f || x + w > rootH * 0f + 1602f || y + h > 752f)
                {
                    bad++;
                    AddIssue("大厅控件 " + c.name + " 越界：x=" + x.ToString("F0") + " y=" + y.ToString("F0") + " w=" + w.ToString("F0") + " h=" + h.ToString("F0"));
                }
                Text t = c.GetComponent<Text>();
                if (t != null)
                {
                    if (t.horizontalOverflow == HorizontalWrapMode.Overflow && t.preferredWidth > t.rectTransform.rect.width + 2f) { overflow++; AddIssue("文字溢出（横向）：" + c.name); }
                    if (t.verticalOverflow == VerticalWrapMode.Truncate && t.preferredHeight > t.rectTransform.rect.height + 2f) { overflow++; AddIssue("文字溢出（纵向）：" + c.name); }
                }
            }
            _log.AppendLine("结构体检：子节点 " + n + "，越界 " + bad + "，零尺寸 " + zero + "，文字溢出 " + overflow);
        }

        private void AuditClickable()
        {
            Button[] all = _shell.RootForTest.GetComponentsInChildren<Button>(true);
            int checkedCount = 0, blocked = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Button b = all[i];
                if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) { continue; }
                // 只查大厅这一点页面里的按钮
                if (!b.transform.IsChildOf(_shell.PageForTest("home")) && b.name != "BtnSolo") { continue; }
                checkedCount++;
                string top = RaycastTop(b.name);
                if (top != b.name)
                {
                    blocked++;
                    AddIssue("大厅按钮 " + b.name + " 被遮挡：射线打到 " + (string.IsNullOrEmpty(top) ? "（空）" : top));
                }
            }
            _log.AppendLine("可点性检查：大厅按钮 " + checkedCount + " 个，被遮挡 " + blocked + " 个");
        }

        // ───────────────────────── 真实点击与射线 ─────────────────────────

        private GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name) || _shell == null) { return null; }
            Transform[] all = _shell.RootForTest.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) { return all[i].gameObject; }
            }
            return null;
        }

        private string TextOf(string name)
        {
            GameObject go = Find(name);
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

        private GraphicRaycaster Raycaster
        {
            get
            {
                Canvas c = _shell.GetComponentInChildren<Canvas>(true);
                if (c == null) { c = _shell.GetComponentInParent<Canvas>(); }
                return c == null ? null : c.GetComponent<GraphicRaycaster>();
            }
        }

        /// <summary>某控件中心点上，射线打到的**最上层**控件是谁（用来抓"被装饰层吃掉点击"）。</summary>
        private string RaycastTop(string name)
        {
            GameObject go = Find(name);
            if (go == null) { return null; }
            RectTransform rt = go.GetComponent<RectTransform>();
            GraphicRaycaster ray = Raycaster;
            if (rt == null || ray == null || EventSystem.current == null) { return null; }
            Vector3 center = rt.TransformPoint(rt.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, center);
            var ped = new PointerEventData(EventSystem.current);
            ped.position = screen;
            var hits = new List<RaycastResult>();
            ray.Raycast(ped, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                Transform tr = hits[i].gameObject.transform;
                while (tr != null)
                {
                    if (tr.gameObject == go) { return go.name; }
                    tr = tr.parent;
                }
                return hits[i].gameObject.name;
            }
            return "";
        }

        /// <summary>在控件中心真实射线点击（和玩家鼠标点一样）。</summary>
        private bool Click(string name)
        {
            GameObject go = Find(name);
            if (go == null) { _fails++; return false; }
            Button b = go.GetComponent<Button>();
            RectTransform rt = go.GetComponent<RectTransform>();
            GraphicRaycaster ray = Raycaster;
            if (b == null || rt == null || ray == null || EventSystem.current == null) { _fails++; return false; }
            Vector3 center = rt.TransformPoint(rt.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, center);
            var ped = new PointerEventData(EventSystem.current);
            ped.position = screen;
            var hits = new List<RaycastResult>();
            ray.Raycast(ped, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[i].gameObject);
                if (target == null) { continue; }
                _clicks++;
                ExecuteEvents.Execute(target, ped, ExecuteEvents.pointerClickHandler);
                return target == go;
            }
            _fails++;
            return false;
        }

        private void Finish(string tail)
        {
            _log.AppendLine();
            _log.AppendLine("=== 问题清单（" + _issues.Count + " 条） ===");
            for (int i = 0; i < _issues.Count; i++) { _log.AppendLine("  " + (i + 1) + ". " + _issues[i]); }
            _log.AppendLine();
            _log.AppendLine("点击 " + _clicks + " 次，失败 " + _fails + " 次。收尾：" + tail);
            try { File.WriteAllText(OutPath, _log.ToString(), new UTF8Encoding(false)); Debug.Log("[V06HomeTest] 报告 → " + OutPath); }
            catch (System.Exception ex) { Debug.LogError("[V06HomeTest] 写报告失败：" + ex.Message); }
            Debug.Log("[V06HomeTest] 完成，问题 " + _issues.Count + " 条");
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool("V06HomeTest", false);
#endif
        }
    }
}
