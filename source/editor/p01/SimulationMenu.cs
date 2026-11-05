#if UNITY_EDITOR
using System;
using System.Text;
using Jieqiao;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 菜单 Jieqiao → 跑 N 局 AI 对战模拟。
///
/// 用途：不改一行规则代码就能验证改动效果。文档第 16 章的分析就是靠同类模拟得到的，
/// 这里把同一套引擎接到菜单上，方便反复复现。
/// </summary>
public static class SimulationMenu
{
    [MenuItem("Jieqiao/模拟 100 局（AI vs AI）")]
    public static void Run100()
    {
        Run(100);
    }

    [MenuItem("Jieqiao/模拟 2 局（打印完整记录）")]
    public static void RunVerbose()
    {
        var sb = new StringBuilder();
        for (int seed = 201; seed <= 202; seed++)
        {
            GameEngine eng = PlayOne(seed, out string summary);
            sb.AppendLine($"── 种子 {seed}：{summary}");
            for (int i = Math.Max(0, eng.Log.Count - 25); i < eng.Log.Count; i++)
            {
                sb.AppendLine("   " + eng.Log[i]);
            }
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    private static void Run(int games)
    {
        int p0 = 0, p1 = 0, draw = 0, tide = 0, stalls = 0;
        int roundSum = 0, roundMin = int.MaxValue, roundMax = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int seed = 1; seed <= games; seed++)
        {
            GameEngine eng = PlayOne(seed, out string summary);
            roundSum += eng.S.Round;
            roundMin = Math.Min(roundMin, eng.S.Round);
            roundMax = Math.Max(roundMax, eng.S.Round);
            if (eng.S.Winner == 0) p0++;
            else if (eng.S.Winner == 1) p1++;
            else draw++;
            if (eng.S.FinishReason != null && eng.S.FinishReason.Contains("界潮")) tide++;
            if (!eng.S.IsOver) stalls++;
        }
        sw.Stop();

        Debug.Log(
            $"界桥远征 模拟 {games} 局（{sw.ElapsedMilliseconds} ms）\n" +
            $"胜负：甲 {p0} / 乙 {p1} / 平 {draw}    未结束 {stalls}\n" +
            $"轮次：平均 {(float)roundSum / games:0.0}（{roundMin}–{roundMax}）\n" +
            $"界潮收尾：{tide}/{games}\n" +
            $"说明：这是启发式 AI 的对局，不代表真人体验，也不能当作平衡性结论。");
    }

    private static GameEngine PlayOne(int seed, out string summary)
    {
        var eng = new GameEngine();
        eng.Start(seed);
        var a0 = new HeuristicAi(seed * 7 + 1);
        var a1 = new HeuristicAi(seed * 13 + 2);
        eng.ConfirmMulligan(0, a0.PickMulligan(eng, 0));
        eng.ConfirmMulligan(1, a1.PickMulligan(eng, 1));

        int guard = 0;
        while (!eng.S.IsOver && guard < 3000)
        {
            HeuristicAi ai = eng.S.CurrentPlayer == 0 ? a0 : a1;
            if (!ai.Step(eng))
            {
                break;
            }
            guard++;
        }
        summary = $"第 {eng.S.Round} 轮结束，胜者={eng.S.Winner}，{eng.S.FinishReason}，记录 {eng.Log.Count} 条";
        return eng;
    }
}
#endif
