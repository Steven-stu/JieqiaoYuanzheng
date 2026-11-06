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

public sealed class V07DeckTestRunner : MonoBehaviour
{
    static readonly StringBuilder SB = new StringBuilder();
    static void L(string s) { SB.AppendLine(s); Debug.Log("[DeckV5] " + s); }

    IEnumerator Start()
    {
        float w = 0f;
        while (V06ShellUI.Instance == null && w < 20f) { w += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
        var shell = V06ShellUI.Instance;
        if (shell == null) { L("无 ShellUI"); Flush(); yield break; }
        shell.ShowPageForTest("deck");
        yield return new WaitForSecondsRealtime(0.8f);
        var page = shell.PageForTest("deck");
        var root = shell.RootForTest;
        L("存档卡组数=" + V06Save.Data.decks.Count);

        // A. 列表每页 4 行 + 翻页
        L("列表行 R0=" + TextOf(page, "DeckRow0"));
        L("列表行 R3=" + TextOf(page, "DeckRow3"));
        L("页码=" + TextOf(page, "DeckListPage"));
        int before = V06Save.Data.decks.Count;
        var next = Find(page, "DeckListNext");
        if (next != null) { Click(next.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        L("翻页后 页码=" + TextOf(page, "DeckListPage") + " R0=" + TextOf(page, "DeckRow0"));
        var prev = Find(page, "DeckListPrev");
        if (prev != null) { Click(prev.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.4f); }
        L("回上页 页码=" + TextOf(page, "DeckListPage"));

        // B. 逐行/逐按钮做遮挡检测
        string[] probes = new string[] { "DeckRow0", "DeckRow1", "DeckRow2", "DeckRow3", "DeckListPrev", "DeckListNext", "DeckNewEmpty", "DeckRename", "DeckDelete", "DeckSave", "DeckFill", "DeckUse", "DeckAdd", "DeckDel" };
        for (int i = 0; i < probes.Length; i++)
        {
            var t = Find(page, probes[i]);
            if (t == null) { L("!! 缺元素 " + probes[i]); continue; }
            if (!t.gameObject.activeInHierarchy) { L("   " + probes[i] + " 未激活"); continue; }
            var hit = RaycastTop(t);
            bool blocked = hit != null && hit != t.gameObject && !hit.transform.IsChildOf(t);
            L((blocked ? "!! 被遮挡 " : "可点 ") + probes[i] + (blocked ? " 顶上=" + hit.name : ""));
        }

        // C. 新建空卡组 → 命名框能否拿到键盘焦点
        var newBtn = Find(page, "DeckNewEmpty");
        if (newBtn != null) { Click(newBtn.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.5f); }
        var layer = Find(root, "NameLayer");
        var inputRt = Find(root, "NameInput");
        var input = inputRt == null ? null : inputRt.GetComponent<InputField>();
        L("命名弹窗=" + (layer != null) + " 输入框=" + (input != null));
        if (input != null)
        {
            for (int f = 0; f < 4; f++) { yield return null; }
            bool focused = input.isFocused;
            var sel = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            L("输入框 isFocused=" + focused + " EventSystem选中=" + (sel == null ? "null" : sel.name));
            input.text = "命名测试";
            yield return new WaitForSecondsRealtime(0.2f);
            var ok = Find(root, "Ok");
            if (ok != null) { Click(ok.GetComponent<Button>()); yield return new WaitForSecondsRealtime(0.5f); }
        }
        var created = V06Save.Data.decks[V06Save.Data.decks.Count - 1];
        L("命名后最新卡组名=" + created.name + " 弹窗关闭=" + (Find(root, "NameLayer") == null));
        L("卡组数 " + before + " -> " + V06Save.Data.decks.Count + "（新建应为 +1）");
        Flush();
    }

    static GameObject RaycastTop(RectTransform t)
    {
        if (EventSystem.current == null) { return null; }
        var ped = new PointerEventData(EventSystem.current);
        ped.position = RectTransformUtility.WorldToScreenPoint(null, t.TransformPoint(t.rect.center));
        var ray = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, ray);
        return ray.Count == 0 ? null : ray[0].gameObject;
    }

    static string TextOf(RectTransform page, string name)
    {
        var r = Find(page, name);
        if (r == null) { return "(无)"; }
        if (r.GetComponent<Text>() != null) { return r.GetComponent<Text>().text; }
        var t = Find(r, "T");
        return t == null ? "(无文字)" : t.GetComponent<Text>().text;
    }

    static void Click(Button b)
    {
        if (b == null || EventSystem.current == null) { return; }
        var ped = new PointerEventData(EventSystem.current);
        ped.button = PointerEventData.InputButton.Left;
        ped.position = RectTransformUtility.WorldToScreenPoint(null, b.transform.position);
        ExecuteEvents.Execute(b.gameObject, ped, ExecuteEvents.pointerClickHandler);
    }

    static RectTransform Find(RectTransform root, string name)
    {
        if (root == null) { return null; }
        if (root.name == name) { return root; }
        var all = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++) { if (all[i].name == name) { return all[i]; } }
        return null;
    }

    static void Flush()
    {
        try
        {
            System.IO.Directory.CreateDirectory("reports");
            System.IO.File.WriteAllText("reports/deck-list-test2.txt", SB.ToString(), new System.Text.UTF8Encoding(true));
        }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
    }
}

public static class V07DeckTest
{
    [MenuItem("界桥远征/测试组卡页(实机点击)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[DeckV5] 请先进入 Play 模式"); return; }
        var go = new GameObject("V07DeckTestRunner");
        go.AddComponent<V07DeckTestRunner>();
        Debug.Log("[DeckV5] 启动");
    }
}
#endif
