#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using Jieqiao;

/// <summary>
/// V0.6 规则批量自测：跑大量 AI 对局，统计节奏与僵局。
/// 与 ClickPlaytest（V0.3 的点击级测试）分开：这里测的是 V0.6 新引擎。
/// </summary>
public static class V06Sim
{
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run(int gamesPerPair, int seedBase)
    {
        return RunArm(gamesPerPair, seedBase, "V0.6原文", V06Engine.FlagMaxHp, V06Engine.PressureStartRound, 0, 0, "");
    }


    /// <summary>按分类列出效果实现覆盖，并把没实现的编号点名。</summary>
    public static string EffectCoverage()
    {
        V06Effects.RegisterAll();
        var sb = new StringBuilder();
        var byCat = new Dictionary<string, int[]>();
        var missing = new StringBuilder();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            string key = c.Category.ToString();
            if (!byCat.ContainsKey(key)) { byCat[key] = new int[2]; }
            byCat[key][1]++;
            bool impl = V06Effects.Implemented.Contains(c.Id);
            if (impl) { byCat[key][0]++; }
            else
            {
                if (c.Category == V06Category.Token || c.Category == V06Category.Weather
                    || c.Category == V06Category.Encounter || c.Category == V06Category.Event)
                {
                    continue;   // 场景/衍生卡不作为可用卡牌，单独说明
                }
                missing.Append(c.Id + " ");
            }
        }
        int totalImpl = 0, total = 0;
        foreach (var kv in byCat)
        {
            sb.AppendLine("  " + kv.Key.PadRight(12) + kv.Value[0] + " / " + kv.Value[1]);
            totalImpl += kv.Value[0];
            total += kv.Value[1];
        }
        sb.AppendLine("  合计 " + totalImpl + " / " + total);
        sb.AppendLine("  仍不可用（需要额外窗口或不是可出牌卡）：" + (missing.Length == 0 ? "无" : missing.ToString().Trim()));
        int scene = V06Catalog.CountOf(V06Category.Token) + V06Catalog.CountOf(V06Category.Weather)
                    + V06Catalog.CountOf(V06Category.Encounter) + V06Catalog.CountOf(V06Category.Event);
        sb.AppendLine("  场景/衍生卡 " + scene + " 张（衍生物由效果生成、环境/遭遇/事件属于剧情层）");
        return sb.ToString();
    }
    /// <summary>按指定配置跑一个实验臂。</summary>
    public static string RunArm(int gamesPerPair, int seedBase, string armName, int flagHp, int pressureStart, int pressureMode, int killFlagDamage, string csvSuffix)
    {
        Out.Length = 0;
        V06Effects.RegisterAll();
        Out.AppendLine("V0.6 引擎效果覆盖：" + V06Effects.CoverageReport());

        int pairs = V06Catalog.Precons.Count;
        int total = 0, winA = 0, winB = 0, draw = 0, clockEnd = 0, mutualZero = 0;
        long sumRounds = 0;
        int minRounds = 99, maxRounds = 0;
        int flagSum0 = 0, flagSum1 = 0;
        long dmgCombat = 0, dmgPressure = 0, dmgFatigue = 0;
        var roundHist = new int[16];
        var csv = new StringBuilder();
        csv.AppendLine("seed,precon0,precon1,rounds,winner,reason,flag0,flag1,units0,units1,steps");

        int stepsGuard = 4000;
        pairs = V06DeckLibrary.Count;
        for (int a = 0; a < pairs; a++)
        {
            for (int b = 0; b < pairs; b++)
            {
                for (int g = 0; g < gamesPerPair; g++)
                {
                    int seed = seedBase + total * 7919 + 1;
                    V06Engine e = new V06Engine();
                    e.CfgFlagHp = flagHp;
                    e.CfgPressureStart = pressureStart;
                    e.CfgPressureMode = pressureMode;
                    e.CfgKillFlagDamage = killFlagDamage;
                    string err;
                    if (!e.TryStart(seed, a, b, out err))
                    {
                        Out.AppendLine("启动失败 " + a + "v" + b + "：" + err);
                        continue;
                    }
                    V06Ai.Seed(seed);
                    int steps = V06Ai.RunGame(e, stepsGuard);
                    total++;
                    sumRounds += e.Round;
                    if (e.Round < minRounds) { minRounds = e.Round; }
                    if (e.Round > maxRounds) { maxRounds = e.Round; }
                    if (e.Round < roundHist.Length) { roundHist[e.Round]++; }
                    else { roundHist[roundHist.Length - 1]++; }
                    flagSum0 += e.Players[0].FlagHp;
                    flagSum1 += e.Players[1].FlagHp;
                    if (e.Winner == 0) { winA++; }
                    else if (e.Winner == 1) { winB++; }
                    else { draw++; }
                    if (e.FinishReason != null && e.FinishReason.StartsWith("第 12 轮上限")) { clockEnd++; }
                    if (e.FinishReason != null && e.FinishReason.Contains("同时归零")) { mutualZero++; }
                    dmgCombat += e.DamageLog[0, V06Engine.DmgCombat] + e.DamageLog[1, V06Engine.DmgCombat];
                    dmgPressure += e.DamageLog[0, V06Engine.DmgPressure];
                    dmgFatigue += e.DamageLog[0, V06Engine.DmgFatigue] + e.DamageLog[1, V06Engine.DmgFatigue];
                    int un0 = e.Players[0].UnitCount();
                    int un1 = e.Players[1].UnitCount();
                    csv.AppendLine(seed + "," + a + "," + b + "," + e.Round + "," + e.Winner + ","
                        + Quote(e.FinishReason) + "," + e.Players[0].FlagHp + "," + e.Players[1].FlagHp
                        + "," + un0 + "," + un1 + "," + steps);
                }
            }
        }

        Out.AppendLine("【" + armName + "】旗 " + flagHp + "，压力第 " + pressureStart + " 轮起，" + (pressureMode == 1 ? "半速" : "线性") + "曲线");
        Out.AppendLine("对局 " + total + " 局（" + pairs + "×" + pairs + " 预组组合 × " + gamesPerPair + " 种子）");
        Out.AppendLine("  先手胜 " + winA + "（" + Pct(winA, total) + "）　后手胜 " + winB + "（" + Pct(winB, total) + "）　平局 " + draw + "（" + Pct(draw, total) + "）");
        Out.AppendLine("  12 轮上限收尾 " + clockEnd + "（" + Pct(clockEnd, total) + "）　双方旗同时归零 " + mutualZero + "（" + Pct(mutualZero, total) + "）");
        long dmgTot = dmgCombat + dmgPressure + dmgFatigue;
        if (dmgTot == 0) { dmgTot = 1; }
        Out.AppendLine("  旗伤害来源（双方合计）：直击/击败转化 " + dmgCombat + "（" + Pct((int)dmgCombat, (int)dmgTot) + "）　战场压力 " + dmgPressure + "（" + Pct((int)dmgPressure, (int)dmgTot) + "）　疲劳 " + dmgFatigue + "（" + Pct((int)dmgFatigue, (int)dmgTot) + "）");
        Out.AppendLine("  轮数 平均 " + (total == 0 ? "0" : ((double)sumRounds / total).ToString("0.00"))
            + "　min " + minRounds + "　max " + maxRounds);
        Out.AppendLine("  终局旗血 平均 " + Avg(flagSum0, total) + " : " + Avg(flagSum1, total));
        var hist = new StringBuilder("  轮数分布 ");
        for (int i = 1; i < roundHist.Length; i++)
        {
            if (roundHist[i] == 0) { continue; }
            hist.Append(i + "轮=" + roundHist[i] + (i == roundHist.Length - 1 ? "（含更多）" : "") + "　");
        }
        Out.AppendLine(hist.ToString());
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "raw-v06-engine" + csvSuffix + ".csv"));
        try { System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false)); Out.AppendLine("CSV → " + path); }
        catch (Exception ex) { Out.AppendLine("CSV 写入失败：" + ex.Message); }
        return Out.ToString();
    }

    /// <summary>
    /// V0.7 扩展预组专项对局：兽群(11) / 少女(12) / 官方基线(0) 之间的组合，
    /// 统计轮数、胜负、进化与融合次数，用来判断新机制是否真的在跑、节奏是否被拖长。
    /// </summary>
    public static string RunExpansionTrial(int gamesPerPair, int seedBase)
    {
        var sb = new StringBuilder();
        V06Effects.RegisterAll();
        int beast = V06DeckLibrary.Count - 2;
        int girl = V06DeckLibrary.Count - 1;
        int[] combos = new int[] { beast, beast, beast, girl, girl, girl, 0, 0 };
        int total = 0, clockEnd = 0, draw = 0, beastWins = 0, girlWins = 0;
        long sumRounds = 0, evolutions = 0, fusions = 0, growthMaxed = 0;
        long fuseOffers = 0;
        int gamesWithOffer = 0;
        int maxRounds = 0, minRounds = 99;
        sb.AppendLine("【V0.7 扩展预组专项】兽群=" + beast + " 少女=" + girl + " 官方基线=0");
        for (int ci = 0; ci < combos.Length; ci += 2)
        {
            int a = combos[ci], b = combos[ci + 1];
            int cDraw = 0, cClock = 0, cRounds = 0, cGames = 0;
            for (int g = 0; g < gamesPerPair; g++)
            {
                int seed = seedBase + total * 6151 + 3;
                V06Engine e = new V06Engine();
                string err;
                if (!e.TryStart(seed, a, b, out err)) { sb.AppendLine("启动失败 " + a + "v" + b + "：" + err); continue; }
                V06Ai.Seed(seed);
                int guard = 0;
                while (!e.Finished && guard++ < 4000)
                {
                    List<V06Action> acts = e.LegalActions();
                    for (int k = 0; k < acts.Count; k++)
                    {
                        if (acts[k].Kind == V06ActionKind.Fuse) { fuseOffers++; break; }
                    }
                    if (!V06Ai.Step(e)) { break; }
                }
                total++;
                int shownRound = e.Round > V06Engine.MaxRounds ? V06Engine.MaxRounds : e.Round;
                sumRounds += shownRound;
                if (shownRound < minRounds) { minRounds = shownRound; }
                if (shownRound > maxRounds) { maxRounds = shownRound; }
                if (e.FinishReason != null && e.FinishReason.StartsWith("第 12 轮上限")) { clockEnd++; }
                if (e.Winner == -2) { draw++; }
                if (e.Winner == 0) { if (a == beast) { beastWins++; } else if (a == girl) { girlWins++; } }
                if (e.Winner == 1) { if (b == beast) { beastWins++; } else if (b == girl) { girlWins++; } }
                bool offered = false;
                for (int i = 0; i < e.Log.Count; i++)
                {
                    string L = e.Log[i];
                    if (L.StartsWith("进化：")) { evolutions++; }
                    else if (L.IndexOf(" 融合：") >= 0) { fusions++; offered = true; }
                    else if (L.IndexOf("成长圆满") >= 0) { growthMaxed++; }
                }
                if (offered) { gamesWithOffer++; }
                cGames++;
                cRounds += shownRound;
                if (e.Winner == -2) { cDraw++; }
                if (e.FinishReason != null && e.FinishReason.StartsWith("第 12 轮上限")) { cClock++; }
            }
            string aName = a == beast ? "兽群" : (a == girl ? "少女" : "官方" + a);
            string bName = b == beast ? "兽群" : (b == girl ? "少女" : "官方" + b);
            sb.AppendLine("  · " + aName + " vs " + bName + "：" + cGames + " 局，平局 " + cDraw
                + "，12 轮上限 " + cClock + "，平均 " + (cGames == 0 ? "0" : ((double)cRounds / cGames).ToString("0.0")) + " 轮");
        }
        sb.AppendLine("对局 " + total + " 局（4 组组合 × " + gamesPerPair + " 种子）");
        sb.AppendLine("  兽群方胜 " + beastWins + "　少女方胜 " + girlWins + "　平局 " + draw
            + "　12 轮上限收尾 " + clockEnd + "（" + Pct(clockEnd, total) + "）");
        sb.AppendLine("  轮数 平均 " + (total == 0 ? "0" : ((double)sumRounds / total).ToString("0.00"))
            + "　min " + minRounds + "　max " + maxRounds);
        sb.AppendLine("  进化 " + evolutions + " 次（每局 " + (total == 0 ? "0" : ((double)evolutions / total).ToString("0.00"))
            + "）　融合 " + fusions + " 次（每局 " + (total == 0 ? "0" : ((double)fusions / total).ToString("0.00"))
            + "）　满成长终态奖励 " + growthMaxed + " 次");
        sb.AppendLine("  融合机会（AI 每一步里出现过的融合行动）" + fuseOffers + " 次（每局 "
            + (total == 0 ? "0" : ((double)fuseOffers / total).ToString("0.00"))
            + "），有融合机会的对局 " + gamesWithOffer + "/" + total);
        return sb.ToString();
    }

    private static string Quote(string s)
    {
        if (string.IsNullOrEmpty(s)) { return ""; }
        s = s.Replace("\"", "''").Replace(",", "，");
        return "\"" + s + "\"";
    }

    private static string Pct(int n, int total)
    {
        if (total == 0) { return "0%"; }
        return ((double)n * 100.0 / total).ToString("0.0") + "%";
    }

    private static string Avg(int sum, int total)
    {
        if (total == 0) { return "0"; }
        return ((double)sum / total).ToString("0.0");
    }


    /// <summary>
    /// 定局数试验：固定跑 games 局（预组组合与种子轮换），逐局列出结果。
    /// 用于"某个规则方案跑 10 局"这种需要看明细的对比。
    /// </summary>
    public static string RunTrial(int games, int seedBase, string armName, int flagHp, int pressureStart, int pressureMode, int killFlagDamage, string csvSuffix)
    {
        var sb = new StringBuilder();
        V06Effects.RegisterAll();
        sb.AppendLine("【" + armName + "】旗 " + flagHp + "　压力第 " + pressureStart + " 轮起　"
            + (pressureMode == 1 ? "半速" : "线性") + "曲线　击败→敌旗 " + killFlagDamage);
        sb.AppendLine("  局号  预组          种子   轮数  结果        旗      旗伤害来源(直击/压力/疲劳)");

        int winA = 0, winB = 0, draw = 0, clockEnd = 0;
        long sumRounds = 0;
        long dmgCombat = 0, dmgPressure = 0, dmgFatigue = 0;
        var csv = new StringBuilder();
        csv.AppendLine("seed,precon0,precon1,rounds,winner,reason,flag0,flag1,dmgCombat,dmgPressure,dmgFatigue");

        for (int i = 0; i < games; i++)
        {
            int a = i % V06DeckLibrary.Count;
            int b = (i * 3 + 1) % V06DeckLibrary.Count;
            int seed = seedBase + i;
            V06Engine e = new V06Engine();
            e.CfgFlagHp = flagHp;
            e.CfgPressureStart = pressureStart;
            e.CfgPressureMode = pressureMode;
            e.CfgKillFlagDamage = killFlagDamage;
            string err;
            if (!e.TryStart(seed, a, b, out err)) { sb.AppendLine("  ❌ 启动失败：" + err); continue; }
            V06Ai.Seed(seed * 31 + 7);
            V06Ai.RunGame(e, 4000);

            sumRounds += e.Round;
            if (e.Winner == 0) { winA++; } else if (e.Winner == 1) { winB++; } else { draw++; }
            if (e.FinishReason != null && e.FinishReason.StartsWith("第 12 轮上限")) { clockEnd++; }
            int dc = e.DamageLog[0, V06Engine.DmgCombat] + e.DamageLog[1, V06Engine.DmgCombat];
            int dp = e.DamageLog[0, V06Engine.DmgPressure];
            int df = e.DamageLog[0, V06Engine.DmgFatigue] + e.DamageLog[1, V06Engine.DmgFatigue];
            dmgCombat += dc; dmgPressure += dp; dmgFatigue += df;

            string result;
            if (e.Winner == 0) { result = "先手胜"; }
            else if (e.Winner == 1) { result = "后手胜"; }
            else if (e.Winner == -2) { result = "平局"; }
            else { result = "未分"; }
            sb.AppendLine("  " + (i + 1).ToString().PadLeft(2) + "    "
                + (V06DeckLibrary.Get(a).Name + " vs " + V06DeckLibrary.Get(b).Name).PadRight(22)
                + seed + "   " + e.Round.ToString().PadLeft(2) + "轮  " + result.PadRight(8)
                + (e.Players[0].FlagHp + ":" + e.Players[1].FlagHp).PadLeft(7)
                + "   " + dc + "/" + dp + "/" + df);
            csv.AppendLine(seed + "," + a + "," + b + "," + e.Round + "," + e.Winner + ","
                + (e.FinishReason == null ? "" : e.FinishReason.Replace(",", "，")) + ","
                + e.Players[0].FlagHp + "," + e.Players[1].FlagHp + "," + dc + "," + dp + "," + df);
        }

        long dmgTot = dmgCombat + dmgPressure + dmgFatigue;
        if (dmgTot == 0) { dmgTot = 1; }
        sb.AppendLine("  ── 汇总 ──");
        sb.AppendLine("  先手胜 " + winA + "　后手胜 " + winB + "　平局 " + draw
            + "　12 轮上限收尾 " + clockEnd + "　平均 " + ((double)sumRounds / Math.Max(1, games)).ToString("0.0") + " 轮");
        sb.AppendLine("  旗伤害来源占比：直击/击败转化 " + Pct((int)dmgCombat, (int)dmgTot)
            + "　战场压力 " + Pct((int)dmgPressure, (int)dmgTot)
            + "　疲劳 " + Pct((int)dmgFatigue, (int)dmgTot));
        try
        {
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "raw-v06-trial" + csvSuffix + ".csv"));
            System.IO.File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
            sb.AppendLine("  CSV → " + path);
        }
        catch (Exception ex) { sb.AppendLine("  CSV 写入失败：" + ex.Message); }
        return sb.ToString();
    }
    /// <summary>规则不变量自检：卡牌守恒、行动点非负、主行动不超额、格位一致。</summary>
    public static string SelfCheck(int games) { return SelfCheck(games, 8); }

    /// <summary>同上，但把预组轮到前 deckCount 套（V0.7 用 13 套把兽群/少女预组也扫进去）。</summary>
    public static string SelfCheck(int games, int deckCount)
    {
        if (deckCount > V06DeckLibrary.Count) { deckCount = V06DeckLibrary.Count; }
        if (deckCount < 1) { deckCount = 1; }
        var sb = new StringBuilder();
        V06Ai.Seed(999);
        int bad = 0;
        string firstBad = "";
        for (int g = 0; g < games; g++)
        {
            V06Engine e = new V06Engine();
            string err;
            if (!e.TryStart(5000 + g, g % deckCount, (g + 3) % deckCount, out err)) { sb.AppendLine("启动失败：" + err); return sb.ToString(); }
            int guard = 0;
            while (!e.Finished && guard++ < 4000)
            {
                string before = Invariant(e);
                if (before.Length > 0)
                {
                    bad++;
                    if (firstBad.Length == 0) { firstBad = "第 " + g + " 局 第 " + e.Round + " 轮：" + before; }
                    break;
                }
                if (!V06Ai.Step(e)) { break; }
            }
            string after = Invariant(e);
            if (after.Length > 0)
            {
                bad++;
                if (firstBad.Length == 0) { firstBad = "终局（第 " + e.Round + " 轮）：" + after; }
            }
        }
        sb.AppendLine("不变量自检 " + games + " 局（预组 ×" + deckCount + "）：" + (bad == 0 ? "全部通过" : bad + " 处异常"));
        if (firstBad.Length > 0) { sb.AppendLine("  首例 → " + firstBad); }
        return sb.ToString();
    }

    public static string Invariant(V06Engine e)
    {
        for (int c = 0; c < 2; c++)
        {
            V06Player p = e.Players[c];
            if (p.ActionPoints < 0) { return "行动点为负 " + p.ActionPoints; }
            if (p.MainActionsUsed > V06Engine.MainActionsPerRound) { return "主行动超额 " + p.MainActionsUsed; }
            // 24 张主牌 + 6 张预备 = 30 张，任何一张都不能凭空消失或翻倍
            int expected = 30;
            int n = p.Hand.Count + p.Deck.Count + p.Discard.Count + p.Exile.Count
                    + p.Reserve.Count + p.ReserveUsed.Count;
            if (!string.IsNullOrEmpty(p.BattlefieldId)) { n++; }
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = p.At(s);
                if (u == null) { continue; }
                if (!u.IsToken) { n++; }
                if (!string.IsNullOrEmpty(u.EquipId)) { n++; }
                if (!string.IsNullOrEmpty(u.SealId)) { n++; }
                if (u.Slot != s) { return "格位与实体不一致（槽 " + s + " 实为 " + u.Slot + "）"; }
            }
            if (n != expected)
            {
                return "方 " + c + " 卡牌不守恒：手 " + p.Hand.Count + " 库 " + p.Deck.Count
                       + " 弃 " + p.Discard.Count + " 预备 " + p.Reserve.Count + "/" + p.ReserveUsed.Count
                       + " 场上基础 " + BoardBasics(p) + " = " + n + "（应 30）";
            }
        }
        return "";
    }

    private static int BoardBasics(V06Player p)
    {
        int n = 0;
        for (int s = 0; s < V06Slot.Total; s++)
        {
            V06Unit u = p.At(s);
            if (u != null && !u.IsToken) { n++; }
        }
        return n;
    }
}
#endif
