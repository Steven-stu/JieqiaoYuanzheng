#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 命令行 / CI 入口：把四套检查打包成一次性运行的方法。
///
/// 用法（Windows PowerShell）：
///   &amp; 'Unity.exe' -batchmode -nographics -projectPath 'D:\...\CardGame' `
///       -executeMethod V06BatchRunner.RunAll -quit -logFile -
///
/// 也可以单独跑：
///   -executeMethod V06BatchRunner.RunRuleChecks
///   -executeMethod V06BatchRunner.RunV06Checks
///   -executeMethod V06BatchRunner.RunLayer2Checks
///   -executeMethod V06BatchRunner.RunSim
///
/// 任何一个失败，进程退出码为 1（方便 CI 判定）。
/// </summary>
public static class V06BatchRunner
{
    private static int _failTotal;

    [MenuItem("界桥远征/运行全部检查 %#r")]
    public static void RunAllMenu()
    {
        RunAll();
        EditorUtility.DisplayDialog("界桥远征", "检查完成，失败项：" + _failTotal + "\n详细结果见 Console。", "好");
    }

    public static void RunAll()
    {
        _failTotal = 0;
        var sb = new StringBuilder();
        sb.AppendLine("===== 界桥远征 V0.6 检查汇总 =====");
        sb.AppendLine(TimeStamp());

        sb.AppendLine();
        sb.AppendLine("--- 1) RuleChecks（V0.3 定点规则，50 条）---");
        sb.Append(RunRuleChecksInternal());

        sb.AppendLine();
        sb.AppendLine("--- 2) V06Checks（卡池/预组/卡面覆盖，14 条）---");
        sb.Append(RunV06ChecksInternal());

        sb.AppendLine();
        sb.AppendLine("--- 3) V06Layer2Checks（装备/附印/战况/晋阶/支援/响应，18 条）---");
        sb.Append(RunLayer2Internal());

        sb.AppendLine();
        sb.AppendLine("--- 4) V06Sim.SelfCheck（每步不变量，8 局）---");
        sb.Append(RunSelfCheckInternal(8));

        sb.AppendLine();
        sb.AppendLine("--- 5) V06ExpansionChecks（V0.7 兽群/少女，11 条）---");
        sb.Append(RunExpansionInternal());

        sb.AppendLine();
        sb.AppendLine("--- 6) V06Sim.SelfCheck（含 V0.7 全部 13 套预组，12 局）---");
        sb.Append(RunSelfCheckInternal(12, Jieqiao.V06DeckLibrary.Count));

        sb.AppendLine();
        sb.AppendLine("===== 结束：失败项合计 " + _failTotal + " =====");
        Debug.Log(sb.ToString());
        if (_failTotal > 0 && Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("界桥远征/检查 RuleChecks")]
    public static void RunRuleChecks() { Debug.Log(RunRuleChecksInternal()); }

    [MenuItem("界桥远征/检查 V06Checks")]
    public static void RunV06Checks() { Debug.Log(RunV06ChecksInternal()); }

    [MenuItem("界桥远征/检查 V06Layer2Checks")]
    public static void RunLayer2Checks() { Debug.Log(RunLayer2Internal()); }

    [MenuItem("界桥远征/检查 V06ExpansionChecks")]
    public static void RunExpansionChecks() { Debug.Log(RunExpansionInternal()); }

    [MenuItem("界桥远征/模拟 10 局（无头）")]
    public static void RunSim() { Debug.Log(RunSimInternal(10, 1000, false)); }

    [MenuItem("界桥远征/卡面覆盖报告")]
    public static void RunArtCoverage() { Debug.Log(RunCoverageInternal()); }

    // ───────────────────────── 内部实现 ─────────────────────────

    private static string RunRuleChecksInternal()
    {
        string s = RuleChecks.Run();
        _failTotal += CountFail(s);
        return s;
    }

    private static string RunV06ChecksInternal()
    {
        string s = V06Checks.Run();
        _failTotal += CountFail(s);
        return s;
    }

    private static string RunLayer2Internal()
    {
        string s = V06Layer2Checks.Run();
        _failTotal += CountFail(s);
        return s;
    }

    private static string RunSelfCheckInternal(int games)
    {
        string s = V06Sim.SelfCheck(games);
        if (s.Contains("异常")) { _failTotal++; }
        return s;
    }

    private static string RunSelfCheckInternal(int games, int deckCount)
    {
        string s = V06Sim.SelfCheck(games, deckCount);
        if (s.Contains("异常")) { _failTotal++; }
        return s;
    }

    private static string RunExpansionInternal()
    {
        string s = V06ExpansionChecks.Run();
        _failTotal += CountFail(s);
        return s;
    }

    private static string RunSimInternal(int gamesPerPair, int seedBase, bool testDecks)
    {
        // testDecks = true 时只跑 8..10 号测试预组（含装备/附印/战况/响应）
        if (testDecks)
        {
            return V06Sim.RunTrial(gamesPerPair * 3, seedBase, "测试牌库", 20, 7, 1, 0, "-batch");
        }
        return V06Sim.RunTrial(gamesPerPair, seedBase, "全部预组轮换", 20, 7, 1, 0, "-batch");
    }

    private static string RunCoverageInternal()
    {
        return "效果覆盖：\n" + V06Sim.EffectCoverage() + "\n卡面覆盖见 V06Checks 的『卡面素材覆盖』断言。";
    }

    private static int CountFail(string text)
    {
        if (string.IsNullOrEmpty(text)) { return 0; }
        int n = 0;
        int idx = 0;
        while ((idx = text.IndexOf("❌", idx, StringComparison.Ordinal)) >= 0) { n++; idx++; }
        return n;
    }

    private static string TimeStamp()
    {
        return "运行时间 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
               + "　Unity " + Application.unityVersion
               + "　平台 " + Application.platform
               + "　batchmode=" + Application.isBatchMode;
    }
}
#endif
