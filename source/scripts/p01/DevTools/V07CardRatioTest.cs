#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Jieqiao;
using Jieqiao.UI;

/// 校验卡面比例:组卡卡片墙 / 详情面板 / 战斗手牌 三处立绘框是否都接近 3:4
public sealed class V07CardRatioTest : MonoBehaviour
{
    static readonly StringBuilder SB = new StringBuilder();
    static void L(string s) { SB.AppendLine(s); Debug.Log("[Ratio] " + s); }

    IEnumerator Start()
    {
        float w = 0f;
        while (V06ShellUI.Instance == null && w < 20f) { w += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
        var shell = V06ShellUI.Instance;
        if (shell == null) { L("无 ShellUI"); Flush(); yield break; }

        shell.ShowPageForTest("deck");
        yield return new WaitForSecondsRealtime(0.8f);
        var page = shell.PageForTest("deck");

        int checkedCells = 0, okCells = 0;
        for (int i = 0; i < 24; i++)
        {
            var art = Find(page, "DeckCell" + i + "/Art");
            if (art == null || !art.gameObject.activeInHierarchy) { continue; }
            var img = art.GetComponent<Image>();
            Rect r = img.rectTransform.rect;
            float ratio = r.width / r.height;
            checkedCells++;
            bool ok = Mathf.Abs(ratio - 0.75f) < 0.02f;
            if (ok) { okCells++; }
            if (i < 3) { L("卡片墙 cell" + i + " 立绘框 " + r.width.ToString("0") + "x" + r.height.ToString("0") + " 比例 " + ratio.ToString("0.000") + (ok ? " ✔3:4" : " ✘")); }
        }
        L("卡片墙立绘比例正确 " + okCells + "/" + checkedCells);

        // 详情面板
        var detail = Find(page, "DeckDetailHost");
        if (detail != null)
        {
            var arts = detail.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < arts.Length; i++)
            {
                if (arts[i].name != "Art") { continue; }
                Rect r = arts[i].rectTransform.rect;
                L("详情立绘框 " + r.width.ToString("0") + "x" + r.height.ToString("0") + " 比例 " + (r.width / r.height).ToString("0.000"));
                break;
            }
        }

        // 战斗手牌(进对局后再量)
        shell.StartSoloForTest();
        yield return new WaitForSecondsRealtime(3.0f);
        var battle = Object.FindObjectOfType<V06BattleUI>();
        L("战斗界面=" + (battle == null ? "无" : "有"));
        if (battle != null)
        {
            var rootT = battle.transform;
            var imgs = rootT.GetComponentsInChildren<Image>(true);
            int handLike = 0;
            for (int i = 0; i < imgs.Length; i++)
            {
                Rect r = imgs[i].rectTransform.rect;
                if (r.height > 100f && r.height < 200f && Mathf.Abs(r.width / r.height - 0.75f) < 0.03f)
                {
                    handLike++;
                    if (handLike <= 3) { L("战斗中 3:4 卡框 " + imgs[i].name + " " + r.width.ToString("0") + "x" + r.height.ToString("0")); }
                }
            }
            L("战斗中 3:4 卡框数量=" + handLike);
        }
        Flush();
    }

    static RectTransform Find(RectTransform root, string path)
    {
        if (root == null) { return null; }
        var parts = path.Split('/');
        Transform cur = root;
        for (int i = 0; i < parts.Length; i++)
        {
            Transform next = null;
            for (int c = 0; c < cur.childCount; c++)
            {
                if (cur.GetChild(c).name == parts[i]) { next = cur.GetChild(c); break; }
            }
            if (next == null) { return null; }
            cur = next;
        }
        return cur as RectTransform;
    }

    static void Flush()
    {
        try
        {
            System.IO.Directory.CreateDirectory("reports");
            System.IO.File.WriteAllText("reports/card-ratio.txt", SB.ToString(), new System.Text.UTF8Encoding(true));
        }
        catch (System.Exception e) { Debug.LogWarning(e.Message); }
    }
}

public static class V07CardRatioCheck
{
    [MenuItem("界桥远征/检查卡面比例(3:4)")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[Ratio] 请先进入 Play 模式"); return; }
        var go = new GameObject("V07CardRatioTest");
        go.AddComponent<V07CardRatioTest>();
        Debug.Log("[Ratio] 启动");
    }
}
#endif
