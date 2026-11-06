#if UNITY_EDITOR
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using Jieqiao.UI;

/// 评审截图:进入 Play 模式后运行菜单「界桥远征/生成评审截图」
public sealed class V07ReviewShotRunner : MonoBehaviour
{
    static string Dir { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../reports/review_shots")); } }

    IEnumerator Start()
    {
        Directory.CreateDirectory(Dir);
        var shell = V06ShellUI.Instance;
        if (shell == null) { Debug.LogError("[ReviewShot] 找不到 V06ShellUI"); yield break; }
        yield return new WaitForSecondsRealtime(1.6f);

        string[][] pages = new string[][] {
            new string[]{"home","01_home_大厅"},
            new string[]{"codex","02_codex_图鉴"},
            new string[]{"deck","03_deck_组卡"},
            new string[]{"shop","04_shop_商店"},
            new string[]{"checkin","05_checkin_签到"},
            new string[]{"profile","06_profile_资料"},
            new string[]{"trade","07_trade_交换"},
            new string[]{"net","08_net_对战入口"},
        };
        foreach (var p in pages)
        {
            shell.ShowPageForTest(p[0]);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Dir, p[1] + ".png"));
            Debug.Log("[ReviewShot] " + p[1]);
            yield return new WaitForSecondsRealtime(0.45f);
        }

        // 图鉴详情(一张卡的立绘与文字)
        shell.ShowCodexForTest("U-H07");
        yield return new WaitForSecondsRealtime(1.0f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "09_codex_detail_卡牌详情.png"));

        // 进对战
        shell.ShowPageForTest("home");
        yield return new WaitForSecondsRealtime(0.7f);
        bool ok = shell.StartSoloForTest();
        Debug.Log("[ReviewShot] start solo = " + ok);
        yield return new WaitForSecondsRealtime(3.2f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "10_battle_start_开局.png"));

        yield return new WaitForSecondsRealtime(6.0f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "11_battle_手牌.png"));

        yield return new WaitForSecondsRealtime(7.0f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "12_battle_mid_中期.png"));

        Debug.Log("[ReviewShot] 完成 -> " + Dir);
    }
}

public static class V07ReviewShot
{
    [MenuItem("界桥远征/生成评审截图")]
    public static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[ReviewShot] 请先进入 Play 模式"); return; }
        var go = new GameObject("V07ReviewShotRunner");
        go.AddComponent<V07ReviewShotRunner>();
        Debug.Log("[ReviewShot] 已启动");
    }
}
#endif
