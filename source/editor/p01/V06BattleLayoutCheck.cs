#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using Jieqiao;
using Jieqiao.UI;

/// <summary>
/// 战斗界面遮挡自检：在 Play 模式、战斗界面开着的时候跑。
///
/// 做法：把每个可交互控件的中心点丢给 EventSystem 做射线，看命中的最上层
/// 是不是它自己（或它的子节点）。布局改版最容易犯的错就是"元素互相压住"，
/// 这种错肉眼在 1600×900 缩到 1280×720 时不一定看得出，但一定点不到。
///
/// 菜单：界桥远征/检查战斗界面遮挡
/// </summary>
public static class V06BattleLayoutCheck
{
    private static readonly string[] Prefixes =
        { "Hand", "Slot", "Reserve", "Btn", "Respond", "DeckTab", "Shop", "Entry" };

    [MenuItem("界桥远征/检查战斗界面遮挡")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[战斗布局] 请先进入 Play 模式并处于对局中");
            return;
        }
        V06BattleUI ui = Object.FindObjectOfType<V06BattleUI>();
        if (ui == null)
        {
            Debug.LogError("[战斗布局] 找不到 V06BattleUI");
            return;
        }
        if (EventSystem.current == null)
        {
            Debug.LogError("[战斗布局] 没有 EventSystem，无法做射线检查");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("===== 战斗界面遮挡自检 =====");
        int checkedCount = 0, blocked = 0, offscreen = 0;

        Canvas canvas = ui.GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas == null ? null : canvas.transform as RectTransform;

        foreach (GameObject go in ActiveChildren(ui.transform))
        {
            if (!Interesting(go.name)) { continue; }
            RectTransform rt = go.transform as RectTransform;
            Image img = go.GetComponent<Image>();
            if (rt == null || img == null || !img.raycastTarget) { continue; }

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                (corners[0] + corners[2]) * 0.5f);

            // 屏幕外的（手牌位多于实际手牌时会被 SetActive(false)，这里再兜一层）
            if (center.x < 0 || center.y < 0 || center.x > Screen.width || center.y > Screen.height) { offscreen++; continue; }
            checkedCount++;

            var ped = new PointerEventData(EventSystem.current) { position = center };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);

            GameObject top = null;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].gameObject == null) { continue; }
                top = hits[i].gameObject;
                break;
            }
            if (top == null)
            {
                blocked++;
                sb.AppendLine("  ❌ " + Path(go.transform, ui.transform) + "：中心点没有任何控件接收射线（点不到）");
                continue;
            }
            if (top == go || top.transform.IsChildOf(go.transform) || go.transform.IsChildOf(top.transform))
            {
                continue;
            }
            blocked++;
            sb.AppendLine("  ❌ " + Path(go.transform, ui.transform) + "：被 " + Path(top.transform, ui.transform) + " 压住");
        }

        sb.AppendLine(blocked == 0
            ? "结果：" + checkedCount + " 个控件全部可点，无遮挡 ✅"
            : "结果：" + checkedCount + " 个控件中 " + blocked + " 个有问题 ❌");
        if (offscreen > 0) { sb.AppendLine("（另有 " + offscreen + " 个控件在屏幕外，已跳过）"); }
        Debug.Log(sb.ToString());
    }

    private static bool Interesting(string name)
    {
        if (string.IsNullOrEmpty(name)) { return false; }
        for (int i = 0; i < Prefixes.Length; i++) { if (name.StartsWith(Prefixes[i])) { return true; } }
        return false;
    }

    private static IEnumerable<GameObject> ActiveChildren(Transform root)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform cur = stack.Pop();
            for (int i = 0; i < cur.childCount; i++)
            {
                Transform child = cur.GetChild(i);
                if (!child.gameObject.activeInHierarchy) { continue; }
                yield return child.gameObject;
                stack.Push(child);
            }
        }
    }

    private static string Path(Transform t, Transform root)
    {
        var parts = new List<string>();
        while (t != null && t != root)
        {
            string n = t.gameObject.name;
            Text txt = t.GetComponent<Text>();
            if (txt != null && !string.IsNullOrEmpty(txt.text))
            {
                string label = txt.text.Replace("\n", " ");
                if (label.Length > 10) { label = label.Substring(0, 10) + "…"; }
                n += "[" + label + "]";
            }
            parts.Insert(0, n);
            t = t.parent;
        }
        return string.Join("/", parts.ToArray());
    }

    // ───────────────────────── 选中互斥回归 ─────────────────────────

    /// <summary>
    /// 选中互斥回归：手牌 / 预备 / 单位三种选择必须互斥。
    ///
    /// 历史缺陷：`OnHandClicked` 只清 `_selUid`、`OnSlotClicked` 只清 `_selHand`，
    /// 都不清 `_selReserve`。于是"先点预备、再点手牌"会两个选中态并存，
    /// 再点己方格位时 `OnSlotClicked` 永远先走预备分支 → 手牌怎么点都部署不出去。
    /// 菜单：界桥远征/检查选中互斥
    /// </summary>
    [MenuItem("界桥远征/检查选中互斥")]
    public static void RunSelectionExclusive()
    {
        if (!Application.isPlaying) { Debug.LogError("[选中互斥] 请先进入 Play 模式"); return; }
        V06BattleUI ui = Object.FindObjectOfType<V06BattleUI>();
        if (ui == null) { Debug.LogError("[选中互斥] 找不到 V06BattleUI"); return; }

        // 脚本重载会把 _engine 清空，这里自动开一局合法卡组，保证检查随时能跑
        if (V06Save.Data == null) { V06Save.Load(); }
        if (ui.Engine == null)
        {
            if (V06Save.Data == null || V06Save.Data.decks == null) { Debug.LogError("[选中互斥] 没有存档卡组"); return; }
            V06DeckSave legal = null;
            for (int i = 0; i < V06Save.Data.decks.Count; i++)
            {
                V06DeckSave d = V06Save.Data.decks[i];
                if (d != null && d.IsLegal()) { legal = d; break; }
            }
            if (legal == null) { Debug.LogError("[选中互斥] 没有合法卡组可开局"); return; }
            string startErr;
            if (!ui.StartMatchDeck(legal.ToPrecon(), V06DeckLibrary.Get(1), 20261106, false, 0, out startErr))
            {
                Debug.LogError("[选中互斥] 开局失败：" + startErr);
                return;
            }
            ui.paused = false;
        }
        if (ui.Engine == null) { Debug.LogError("[选中互斥] 没有进行中的对局"); return; }

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(V06BattleUI);
        var fHand = type.GetField("_selHand", flags);
        var fRes = type.GetField("_selReserve", flags);
        var fUid = type.GetField("_selUid", flags);
        var mRefresh = type.GetMethod("Refresh", flags);
        if (fHand == null || fRes == null || fUid == null) { Debug.LogError("[选中互斥] 取不到选中字段"); return; }

        V06Player me = ui.Engine.Players[0];
        // 战斗画布可能因为回过大厅、或脚本重载后 _canvas 字段被清空而被整块隐藏；
        // 不打开的话所有点击都会落空（这种"假失败"很坑，所以直接按层级强制打开）。
        ui.SetVisualVisible(true);
        Canvas[] canvases = ui.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++) { canvases[i].gameObject.SetActive(true); }
        Transform chain = ui.transform;
        while (chain != null)
        {
            if (!chain.gameObject.activeSelf) { chain.gameObject.SetActive(true); }
            chain = chain.parent;
        }
        var sb = new StringBuilder();
        sb.AppendLine("===== 选中互斥回归 =====");
        int pass = 0, fail = 0;

        // 每个用例前把行动权与资源还给玩家，否则点击会被"对手回合"挡掉
        System.Action reset = delegate
        {
            ui.Engine.Active = 0;
            me.MainActionsUsed = 0;
            me.ActionPoints = 9;
            // 清掉上一用例残留的选中态，否则"点单位"会先被手牌分支吃掉（会误判成失败）
            var mClear = type.GetMethod("ClearSelection", flags);
            if (mClear != null) { mClear.Invoke(ui, null); }
            if (mRefresh != null) { mRefresh.Invoke(ui, null); }
        };

        // 用例1：预备 → 手牌
        reset();
        InvokeHandler(ui, "Reserve0");
        int r1 = (int)fRes.GetValue(ui);
        InvokeHandler(ui, "Hand0");
        int h2 = (int)fHand.GetValue(ui), r2 = (int)fRes.GetValue(ui);
        Report(sb, ref pass, ref fail, r1 == 0 && r2 == -1 && h2 == 0,
            "预备→手牌：点预备 reserve=" + r1 + "；点手牌 hand=" + h2 + " reserve=" + r2);

        // 用例2：接着点我方空格，手牌必须能部署（原 bug 的后果）
        int handBefore = me.Hand.Count;
        int slot = -1;
        for (int i = 0; i < 6; i++) { if (me.At(i) == null) { slot = i; break; } }
        if (slot >= 0)
        {
            InvokeHandler(ui, "Slot" + slot);
            Report(sb, ref pass, ref fail, me.Hand.Count == handBefore - 1,
                "手牌部署：点 Slot" + slot + " 手牌 " + handBefore + "→" + me.Hand.Count);
        }

        // 用例3：手牌 → 预备
        reset();
        InvokeHandler(ui, "Hand0");
        int h3 = (int)fHand.GetValue(ui);
        InvokeHandler(ui, "Reserve1");
        int r3 = (int)fRes.GetValue(ui), h4 = (int)fHand.GetValue(ui);
        Report(sb, ref pass, ref fail, h3 == 0 && r3 == 1 && h4 == -1,
            "手牌→预备：点手牌 hand=" + h3 + "；点预备 reserve=" + r3 + " hand=" + h4);

        // 用例4：单位 → 手牌
        reset();
        int mySlot = -1;
        for (int i = 0; i < 6; i++) { if (me.At(i) != null) { mySlot = i; break; } }
        if (mySlot >= 0)
        {
            InvokeHandler(ui, "Slot" + mySlot);
            int uBefore = (int)fUid.GetValue(ui);
            InvokeHandler(ui, "Hand0");
            int uAfter = (int)fUid.GetValue(ui), h5 = (int)fHand.GetValue(ui);
            Report(sb, ref pass, ref fail, uBefore >= 0 && uAfter == -1 && h5 == 0,
                "单位→手牌：点单位 uid=" + uBefore + "；点手牌 uid=" + uAfter + " hand=" + h5);
        }

        sb.AppendLine(fail == 0 ? "结果：" + pass + " 项全部通过 ✅" : "结果：" + pass + " 通过 / " + fail + " 失败 ❌");
        Debug.Log(sb.ToString());
    }

    private static void Report(StringBuilder sb, ref int pass, ref int fail, bool ok, string detail)
    {
        if (ok) { pass++; sb.AppendLine("  ✅ " + detail); }
        else { fail++; sb.AppendLine("  ❌ " + detail); }
    }

    private static void Click(V06BattleUI ui, string widgetName)
    {
        GameObject go = FindDeep(ui.transform, widgetName);
        if (go == null) { return; }
        Button btn = go.GetComponent<Button>();
        if (btn != null && btn.interactable) { btn.onClick.Invoke(); }
    }

    /// <summary>
    /// 直接调用按钮绑定的那几个回调（按钮 onClick 的 runtime 监听在脚本重载后会被清空，
    /// 所以自动化里不能依赖点按钮本身；这里调的是按钮真正调用的同一个入口）。
    /// </summary>
    private static void InvokeHandler(V06BattleUI ui, string widgetName)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(V06BattleUI);
        GameObject go = FindDeep(ui.transform, widgetName);
        if (go == null) { return; }
        Button btn = go.GetComponent<Button>();
        if (btn != null && !btn.interactable) { return; }

        if (widgetName.StartsWith("Hand"))
        {
            int i;
            if (int.TryParse(widgetName.Substring(4), out i))
            {
                type.GetMethod("OnHandClicked", flags).Invoke(ui, new object[] { i });
            }
        }
        else if (widgetName.StartsWith("Reserve"))
        {
            int i;
            if (int.TryParse(widgetName.Substring(7), out i))
            {
                type.GetMethod("OnReserveClicked", flags).Invoke(ui, new object[] { i });
            }
        }
        else if (widgetName.StartsWith("Slot"))
        {
            int idx;
            if (int.TryParse(widgetName.Substring(4), out idx))
            {
                type.GetMethod("OnSlotClicked", flags).Invoke(ui, new object[] { idx / 6, idx % 6 });
            }
        }
    }

    private static GameObject FindDeep(Transform root, string name)
    {
        var stack = new Stack<Transform>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Transform cur = stack.Pop();
            for (int i = 0; i < cur.childCount; i++)
            {
                Transform ch = cur.GetChild(i);
                if (ch.gameObject.name == name) { return ch.gameObject; }
                stack.Push(ch);
            }
        }
        return null;
    }

    // ─────────────── 换位确认 / 战术落点回归 ───────────────

    /// <summary>
    /// 换位必须"玩家确认"，战术落点必须"玩家指定"。
    ///
    /// 实机反馈的两个问题：
    /// ① 只想点另一只单位看看，结果手一抖两只就换了位置 —— 换位是改局面的操作，不能这么自动发生；
    /// ② 红雾换位（S-V03）自动挑空格，玩家决定不了换到哪一格。
    ///
    /// 菜单：界桥远征/检查换位与战术落点
    /// </summary>
    [MenuItem("界桥远征/检查换位与战术落点")]
    public static void RunSwapAndSlotTactic()
    {
        if (!Application.isPlaying) { Debug.LogError("[换位落点] 请先进入 Play 模式"); return; }
        V06BattleUI ui = Object.FindObjectOfType<V06BattleUI>();
        if (ui == null) { Debug.LogError("[换位落点] 找不到 V06BattleUI"); return; }
        if (!PrepareBattle(ui, "[换位落点]")) { return; }

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var type = typeof(V06BattleUI);
        var mClear = type.GetMethod("ClearSelection", flags);
        var mRefresh = type.GetMethod("Refresh", flags);

        V06Engine eng = ui.Engine;
        V06Player me = eng.Players[0];
        V06Player foe = eng.Players[1];

        var sb = new StringBuilder();
        sb.AppendLine("===== 换位确认 / 战术落点回归 =====");
        int pass = 0, fail = 0;

        // 造一个干净局面：我方 前排0 = 甲，后排4 = 乙；手牌只留一张指定牌
        System.Action<string, int, int> setup = delegate (string handCard, int aSlot, int bSlot)
        {
            for (int i = 0; i < V06Slot.Total; i++) { me.Slots[i] = null; foe.Slots[i] = null; }
            if (aSlot >= 0) { me.Slots[aSlot] = MakeTestUnit(me, "U-H01", aSlot); }
            if (bSlot >= 0) { me.Slots[bSlot] = MakeTestUnit(me, "U-H02", bSlot); }
            me.Hand.Clear();
            if (!string.IsNullOrEmpty(handCard)) { me.Hand.Add(handCard); }
            me.ActionPoints = 9;
            me.MainActionsUsed = 0;
            me.SwappedThisRound = 0;
            eng.Active = 0;
            if (mClear != null) { mClear.Invoke(ui, null); }
            if (mRefresh != null) { mRefresh.Invoke(ui, null); }
        };

        // ── 用例1：没开换位模式，点另一只单位 → 只切换选中，绝不换位 ──
        setup("", 0, 4);
        int uidA = me.At(0) == null ? -1 : me.At(0).Uid;
        int uidB = me.At(4) == null ? -1 : me.At(4).Uid;
        InvokeHandler(ui, "Slot0");
        InvokeHandler(ui, "Slot4");
        bool posKept = me.At(0) != null && me.At(0).Uid == uidA && me.At(4) != null && me.At(4).Uid == uidB;
        Report(sb, ref pass, ref fail, posKept && !ui.SwapConfirmVisible && me.SwappedThisRound == 0,
            "点到另一只单位不换位：位置未变=" + posKept + " 弹确认=" + ui.SwapConfirmVisible
            + " 已换位=" + (me.SwappedThisRound > 0));

        // ── 用例2：开了换位模式点目标 → 先弹确认，位置仍然不动 ──
        setup("", 0, 4);
        uidA = me.At(0).Uid; uidB = me.At(4).Uid;
        InvokeHandler(ui, "Slot0");
        ui.SetSwapMode(true);
        InvokeHandler(ui, "Slot4");
        bool waiting = ui.SwapConfirmVisible && me.At(0) != null && me.At(0).Uid == uidA && me.SwappedThisRound == 0;
        Report(sb, ref pass, ref fail, waiting,
            "换位模式点目标先确认：弹确认=" + ui.SwapConfirmVisible + " 位置未变=" + (me.At(0) != null && me.At(0).Uid == uidA)
            + " 已换位=" + (me.SwappedThisRound > 0));

        // ── 用例3：点"取消" → 位置不变，确认框关掉 ──
        ui.ClickSwapConfirm(false);
        bool cancelled = !ui.SwapConfirmVisible && me.At(0) != null && me.At(0).Uid == uidA && me.SwappedThisRound == 0;
        Report(sb, ref pass, ref fail, cancelled,
            "取消换位：确认框关=" + (!ui.SwapConfirmVisible) + " 位置未变=" + (me.At(0) != null && me.At(0).Uid == uidA));

        // ── 用例4：再来一次并点"确定" → 真的换位 ──
        setup("", 0, 4);
        uidA = me.At(0).Uid; uidB = me.At(4).Uid;
        InvokeHandler(ui, "Slot0");
        ui.SetSwapMode(true);
        InvokeHandler(ui, "Slot4");
        ui.ClickSwapConfirm(true);
        bool swapped = me.At(0) != null && me.At(0).Uid == uidB && me.At(4) != null && me.At(4).Uid == uidA;
        Report(sb, ref pass, ref fail, swapped && !ui.SwapConfirmVisible,
            "确定换位后真的互换：位置互换=" + swapped + " 已换位=" + (me.SwappedThisRound > 0));

        // ── 用例5：S-V03 红雾换位 → 选单位后进入"等落点"，落点由玩家点 ──
        setup("S-V03", 3, -1);
        int uid3 = me.At(3) == null ? -1 : me.At(3).Uid;
        int handBefore5 = me.Hand.Count;
        InvokeHandler(ui, "Hand0");
        InvokeHandler(ui, "Slot3");
        bool pending = ui.PendingTactic && me.At(3) != null && me.At(3).Uid == uid3 && me.Hand.Count == handBefore5;
        Report(sb, ref pass, ref fail, pending,
            "红雾换位选单位后等落点：等待落点=" + ui.PendingTactic + " 单位仍在3=" + (me.At(3) != null && me.At(3).Uid == uid3)
            + " 手牌未消耗=" + (me.Hand.Count == handBefore5));

        // ── 用例6：点同排空格（非法落点）→ 不执行，继续等 ──
        InvokeHandler(ui, "Slot5");
        bool stillWaiting = ui.PendingTactic && me.At(3) != null && me.At(3).Uid == uid3 && me.At(5) == null;
        Report(sb, ref pass, ref fail, stillWaiting,
            "点同排空格不算落点：仍等待=" + ui.PendingTactic + " 单位未动=" + (me.At(3) != null && me.At(3).Uid == uid3));

        // ── 用例7：点玩家自己选的落点（前排第 3 格=slot2，不是引擎默认的 slot0）→ 落到该格 ──
        InvokeHandler(ui, "Slot2");
        bool landed = me.At(2) != null && me.At(2).Uid == uid3 && me.At(3) == null && me.At(0) == null;
        Report(sb, ref pass, ref fail, landed,
            "红雾换位落到玩家选的格子：slot2=" + (me.At(2) != null && me.At(2).Uid == uid3)
            + " 原格已空=" + (me.At(3) == null) + " 默认格仍空=" + (me.At(0) == null));

        sb.AppendLine(fail == 0 ? "结果：" + pass + " 项全部通过 ✅" : "结果：" + pass + " 通过 / " + fail + " 失败 ❌");
        Debug.Log(sb.ToString());
        if (mClear != null) { mClear.Invoke(ui, null); }
    }

    /// <summary>造一只测试单位（引擎的 MakeUnit 是私有的，这里直接填字段就够用）。</summary>
    private static V06Unit MakeTestUnit(V06Player p, string cardId, int slot)
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
        return u;
    }

    /// <summary>脚本重载后自动开一局并把各层画布点开（否则点击全落空，误判成失败）。</summary>
    private static bool PrepareBattle(V06BattleUI ui, string tag)
    {
        if (V06Save.Data == null) { V06Save.Load(); }
        if (ui.Engine == null)
        {
            if (V06Save.Data == null || V06Save.Data.decks == null) { Debug.LogError(tag + " 没有存档卡组"); return false; }
            V06DeckSave legal = null;
            for (int i = 0; i < V06Save.Data.decks.Count; i++)
            {
                V06DeckSave d = V06Save.Data.decks[i];
                if (d != null && d.IsLegal()) { legal = d; break; }
            }
            if (legal == null) { Debug.LogError(tag + " 没有合法卡组可开局"); return false; }
            string startErr;
            if (!ui.StartMatchDeck(legal.ToPrecon(), V06DeckLibrary.Get(1), 20261107, false, 0, out startErr))
            {
                Debug.LogError(tag + " 开局失败：" + startErr);
                return false;
            }
            ui.paused = false;
        }
        if (ui.Engine == null) { Debug.LogError(tag + " 没有进行中的对局"); return false; }
        ui.SetVisualVisible(true);
        Canvas[] canvases = ui.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++) { canvases[i].gameObject.SetActive(true); }
        Transform chain = ui.transform;
        while (chain != null)
        {
            if (!chain.gameObject.activeSelf) { chain.gameObject.SetActive(true); }
            chain = chain.parent;
        }
        // 战斗音乐在无音频设备时可能报错，这些检查不需要声音
        return true;
    }
}
#endif
