#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using Jieqiao;
using Jieqiao.UI;

/// 模拟玩家巡检:走遍所有页面、点遍可见按钮、进一局对战,收集问题
public sealed class V07PlayerSim : MonoBehaviour
{
    static readonly StringBuilder SB = new StringBuilder();
    static readonly List<string> Issues = new List<string>();
    static void L(string s) { SB.AppendLine(s); Debug.Log("[Sim] " + s); }
    static void Issue(string s) { Issues.Add(s); SB.AppendLine("!! " + s); Debug.LogWarning("[Sim] " + s); }

    static GameObject RayTop(RectTransform t)
    {
        if (EventSystem.current == null) { return null; }
        var ped = new PointerEventData(EventSystem.current);
        ped.position = RectTransformUtility.WorldToScreenPoint(null, t.TransformPoint(t.rect.center));
        var ray = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, ray);
        return ray.Count == 0 ? null : ray[0].gameObject;
    }

    static void ClickBtn(Button b)
    {
        if (b == null || EventSystem.current == null) { return; }
        var ped = new PointerEventData(EventSystem.current);
        ped.button = PointerEventData.InputButton.Left;
        ped.position = RectTransformUtility.WorldToScreenPoint(null, b.transform.position);
        ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
    }

    IEnumerator Start()
    {
        try { yield return StartImpl(); }
        finally { Flush(); }
    }

    IEnumerator StartImpl()
    {
        float w = 0f;
        while (V06ShellUI.Instance == null && w < 25f) { w += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
        var shell = V06ShellUI.Instance;
        if (shell == null) { L("无 ShellUI"); Flush(); yield break; }
        var root = shell.RootForTest;

        // 备份存档,巡检完恢复
        string saveJson = JsonUtility.ToJson(V06Save.Data);

        string[] pages = new string[] { "home", "codex", "deck", "shop", "checkin", "profile", "trade", "net" };
        foreach (var pg in pages)
        {
            shell.ShowPageForTest(pg);
            yield return new WaitForSecondsRealtime(0.5f);
            var page = shell.PageForTest(pg);
            if (page == null || !page.gameObject.activeSelf) { Issue("页面打不开:" + pg); continue; }

            var btns = page.GetComponentsInChildren<Button>(true);
            int visible = 0, blocked = 0;
            var names = new List<string>();
            for (int i = 0; i < btns.Length; i++)
            {
                var b = btns[i];
                if (b == null || !b.gameObject.activeInHierarchy) { continue; }
                visible++;
                var rt = b.GetComponent<RectTransform>();
                if (rt.rect.width < 20f || rt.rect.height < 16f) { Issue(pg + " 按钮太小 " + b.name + " " + rt.rect.width.ToString("0") + "x" + rt.rect.height.ToString("0")); }
                var hit = RayTop(rt);
                if (hit != null && hit != b.gameObject && !hit.transform.IsChildOf(b.transform))
                {
                    blocked++;
                    if (names.Count < 6) { names.Add(b.name + "<-" + hit.name); }
                }
            }
            L("页面 " + pg + ": 可见按钮 " + visible + ", 被遮挡 " + blocked + (names.Count > 0 ? " [" + string.Join(", ", names.ToArray()) + "]" : ""));
            if (blocked > 0) { Issue(pg + " 有 " + blocked + " 个按钮被别的元素压住"); }
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // 商店:每个货架买一次(开发者模式免费)
        shell.ShowPageForTest("shop");
        yield return new WaitForSecondsRealtime(0.5f);
        var shopPage = shell.PageForTest("shop");
        int beforeCoins = V06Save.Data.coins; int beforeOwned = V06Save.TotalOwned();
        for (int k = 0; k < 5; k++)
        {
            var buy = FindExact(shopPage, "Buy" + k) ?? FindExact(shopPage, "ShopBuy" + k);
            if (buy != null) { ClickBtn(buy.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        }
        var openPack = FindExact(shopPage, "OpenPack") ?? FindExact(shopPage, "ShopOpenPack");
        if (openPack != null) { ClickBtn(openPack.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        L("商店:金币 " + beforeCoins + " -> " + V06Save.Data.coins + "(开发者模式应不减), 持有卡 " + beforeOwned + " -> " + V06Save.TotalOwned());

        // 签到
        shell.ShowPageForTest("checkin");
        yield return new WaitForSecondsRealtime(0.4f);
        var checkPage = shell.PageForTest("checkin");
        var checkBtn = FindExact(checkPage, "CheckBtn");
        if (checkBtn != null) { ClickBtn(checkBtn.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        L("签到:连续天数=" + V06Save.Data.streak + " 已领天数=" + (V06Save.Data.claimedDays == null ? 0 : V06Save.Data.claimedDays.Count));

        // 图鉴:点前 3 个格子看详情
        shell.ShowPageForTest("codex");
        yield return new WaitForSecondsRealtime(0.5f);
        var codexPage = shell.PageForTest("codex");
        int codexArt = 0;
        for (int i = 0; i < 3; i++)
        {
            var cell = FindExact(codexPage, "CodexCell" + i);
            if (cell == null) { continue; }
            ClickBtn(cell.GetComponent<Button>());
            yield return new WaitForSecondsRealtime(0.3f);
        }
        L("图鉴:点了 3 个格子");

        // 组卡:一键填充 + 保存
        shell.ShowPageForTest("deck");
        yield return new WaitForSecondsRealtime(0.5f);
        var deckPage = shell.PageForTest("deck");
        var fill = FindExact(deckPage, "DeckFill");
        if (fill != null) { ClickBtn(fill.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.6f); }
        var save = FindExact(deckPage, "DeckSave");
        if (save != null) { ClickBtn(save.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        var d0 = V06Save.Data.decks[0];
        L("组卡:主牌=" + d0.MainCount + " IsLegal=" + d0.IsLegal());

        // 进对战
        shell.ShowPageForTest("home");
        yield return new WaitForSecondsRealtime(0.5f);
        bool started = shell.StartSoloForTest();
        L("开局=" + started);
        yield return new WaitForSecondsRealtime(3.0f);
        var battle = Object.FindObjectOfType<V06BattleUI>();
        if (battle == null) { Issue("对战没进去"); }
        else
        {
            var bb = battle.GetComponentsInChildren<Button>(true);
            int bvis = 0, bblocked = 0;
            for (int i = 0; i < bb.Length; i++)
            {
                if (bb[i] == null || !bb[i].gameObject.activeInHierarchy) { continue; }
                bvis++;
                var hit = RayTop(bb[i].GetComponent<RectTransform>());
                if (hit != null && hit != bb[i].gameObject && !hit.transform.IsChildOf(bb[i].transform)) { bblocked++; }
            }
            L("对战界面:可见按钮 " + bvis + " 被遮挡 " + bblocked);
            // 点几张手牌试试
            int clicked = 0;
            for (int i = 0; i < bb.Length && clicked < 4; i++)
            {
                if (bb[i] == null || !bb[i].gameObject.activeInHierarchy) { continue; }
                if (bb[i].name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                ClickBtn(bb[i]); clicked++;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            L("对战:点了 " + clicked + " 张手牌");
            yield return new WaitForSecondsRealtime(1.0f);
        }

        // 恢复存档
        V06Save.Data = JsonUtility.FromJson<V06SaveData>(saveJson);
        V06Save.Save();
        L("已恢复巡检前存档");

        L("=== 问题统计:" + Issues.Count + " 项 ===");
        Flush();
    }

    static RectTransform FindExact(RectTransform root, string name)
    {
        if (root == null) { return null; }
        var all = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++) { if (all[i].name == name) { return all[i]; } }
        return null;
    }

    static void Flush()
    {
        try
        {
            System.IO.Directory.CreateDirectory("reports");
            System.IO.File.WriteAllText("reports/player-sim.txt", SB.ToString(), new System.Text.UTF8Encoding(true));
        }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
    }
}

public static class V07PlayerSimMenu
{
    [MenuItem("界桥远征/模拟玩家巡检")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[Sim] 请先进入 Play 模式"); return; }
        var go = new GameObject("V07PlayerSim");
        go.AddComponent<V07PlayerSim>();
        Debug.Log("[Sim] 启动");
    }
}
#endif
