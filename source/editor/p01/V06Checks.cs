#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Jieqiao;

/// <summary>
/// V0.6 卡池结构核对：由 scratch/v06/03_战斗规则与卡池_V0.6.md 脚本生成的
/// V06Catalog 是否与文档声明的数量、分类、费用区间、关键词覆盖一致。
/// 与 RuleChecks 分开：那边测的是已实现的战斗规则，这边测的是设计数据。
/// </summary>
public static class V06Checks
{
    private static int _pass;
    private static int _fail;
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run()
    {
        _pass = 0;
        _fail = 0;
        Out.Length = 0;

        TotalCount();
        UniqueIds();
        CategoryCounts();
        CostRanges();
        KeywordCoverage();
        TextNotEmpty();
        UpgradeTargets();
        PreconStructure();
        PreconCardLegality();
        PreconVanguard();
        PreconLowCost();
        IdNamespaceNote();
        UnitArtCoverage();

        Out.AppendLine("V0.6 卡池核对：" + _pass + " 通过 / " + _fail + " 失败");
        return Out.ToString();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            _pass++;
            Out.AppendLine("  ✅ " + name);
        }
        else
        {
            _fail++;
            Out.AppendLine("  ❌ " + name + "　—　" + detail);
        }
    }

    private static void TotalCount()
    {
        int n = V06Catalog.All.Count;
        int exp = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++) { if (V06Catalog.All[i].Exp) { exp++; } }
        // 基础 192 + V0.7 扩展 140（B 44 + G 16 + N 80）
        Check("卡池总数 332 条（基础 192 + V0.7 扩展 140）", n == 332 && exp == 140,
            "实际 " + n + " 条（其中扩展 " + exp + " 条）");
    }

    private static void UniqueIds()
    {
        var seen = new Dictionary<string, int>();
        var dup = new List<string>();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            string id = V06Catalog.All[i].Id;
            if (seen.ContainsKey(id))
            {
                dup.Add(id);
            }
            else
            {
                seen[id] = i;
            }
        }
        Check("编号唯一（无重复 ID）", dup.Count == 0,
            dup.Count == 0 ? "全部唯一" : "重复：" + string.Join("、", dup.ToArray()));
    }

    private static void CategoryCounts()
    {
        // 只统计基础 192 条（V0.7 扩展的兽群/少女单独在 V06ExpansionChecks 里核对）
        int unit = 0, tactic = 0, equip = 0, seal = 0, status = 0, resp = 0, up = 0, sup = 0;
        int token = 0, weather = 0, enc = 0, evt = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (c.Exp) { continue; }
            switch (c.Category)
            {
                case V06Category.Unit: unit++; break;
                case V06Category.Tactic: tactic++; break;
                case V06Category.Equipment: equip++; break;
                case V06Category.Seal: seal++; break;
                case V06Category.Status: status++; break;
                case V06Category.Response: resp++; break;
                case V06Category.Upgrade: up++; break;
                case V06Category.Support: sup++; break;
                case V06Category.Token: token++; break;
                case V06Category.Weather: weather++; break;
                case V06Category.Encounter: enc++; break;
                case V06Category.Event: evt++; break;
            }
        }
        bool ok = unit == 64 && tactic == 32 && equip == 12 && seal == 8 && status == 6 && resp == 6
                  && up == 12 && sup == 12 && token == 8 && weather == 8 && enc == 12 && evt == 12;
        Check("基础卡池分类计数与文档一致（64/32/12/8/6/6/12/12/8/8/12/12）", ok,
            string.Format("单位{0} 战术{1} 装备{2} 附印{3} 战况{4} 响应{5} 晋阶{6} 支援{7} 衍生{8} 环境{9} 遭遇{10} 事件{11}",
                unit, tactic, equip, seal, status, resp, up, sup, token, weather, enc, evt));
    }

    private static void CostRanges()
    {
        int badUnit = 0, badPrepared = 0, badScene = 0;
        string sample = string.Empty;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (c.Category == V06Category.Unit || c.Category == V06Category.Token)
            {
                bool okUnit = c.Atk >= 0 && c.Hp > 0;
                if (c.Category == V06Category.Unit)
                {
                    // 可部署单位 1–4 费；V0.7 的进化形态/融合体 Cost = -1（只能由引擎生成）
                    bool evolveOnly = c.Cost == -1 && c.Exp;
                    okUnit = okUnit && (evolveOnly || (c.Cost >= 1 && c.Cost <= 4));
                    // V0.6 允许 0 攻单位（如 T08 维修微机 0/2），只要求生命 > 0
                }
                if (!okUnit)
                {
                    badUnit++;
                    if (sample.Length < 60) { sample += c.Id + " "; }
                }
            }
            else if (c.Category == V06Category.Weather || c.Category == V06Category.Encounter
                     || c.Category == V06Category.Event)
            {
                if (c.Cost != -1) { badScene++; }
            }
            else
            {
                if (c.Cost < 0) { badPrepared++; }
            }
        }
        Check("单位费用 1–4 且攻生有效；场景卡无费用；其余卡有费用",
            badUnit == 0 && badPrepared == 0 && badScene == 0,
            "单位异常 " + badUnit + "（" + sample + "），有费卡无费用 " + badPrepared + "，场景卡有费用 " + badScene);
    }

    private static void KeywordCoverage()
    {
        var missing = new List<string>();
        for (int k = 0; k < V06Catalog.KeywordList.Length; k++)
        {
            string kw = V06Catalog.KeywordList[k];
            bool found = false;
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                string[] kws = V06Catalog.All[i].Keywords;
                for (int j = 0; j < kws.Length; j++)
                {
                    if (kws[j] == kw) { found = true; break; }
                }
                if (found) { break; }
            }
            if (!found) { missing.Add(kw); }
        }
        Check("20 个关键词在卡池中都有落点", V06Catalog.KeywordList.Length == 20 && missing.Count == 0,
            missing.Count == 0 ? "20 条全部被使用" : "未被使用：" + string.Join("、", missing.ToArray()));
    }

    private static void TextNotEmpty()
    {
        int empty = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            if (string.IsNullOrEmpty(V06Catalog.All[i].Text)) { empty++; }
        }
        Check("每条都有效果文本", empty == 0, "空文本 " + empty + " 条");
    }

    private static void UpgradeTargets()
    {
        // 晋阶必须写清对象：要么点名某个单位编号，要么写明「任意费用不高于3的基础单位」
        int noTarget = 0;
        var bad = new List<string>();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (c.Category != V06Category.Upgrade) { continue; }
            bool hasTarget = c.Text.Contains("U-") || c.Text.Contains("晋阶") || c.Text.Contains("任意");
            if (!hasTarget)
            {
                noTarget++;
                bad.Add(c.Id);
            }
        }
        Check("晋阶卡都写明晋阶对象", noTarget == 0,
            noTarget == 0 ? "12 张全部有对象" : "缺对象：" + string.Join("、", bad.ToArray()));
    }

    // ── 八套预组 ──────────────────────────────────────────────
    private static void PreconStructure()
    {
        bool ok = V06Catalog.Precons.Count == 8;
        var bad = new List<string>();
        for (int i = 0; i < V06Catalog.Precons.Count; i++)
        {
            V06Precon p = V06Catalog.Precons[i];
            if (p.Units.Length != 8 || p.Tactics.Length != 4 || p.MainCount != 24
                || p.Upgrades.Length == 0 || p.Supports.Length == 0)
            {
                ok = false;
                bad.Add(p.Name + "(" + p.Units.Length + "/" + p.Tactics.Length + "/" + p.MainCount + ")");
            }
        }
        Check("八套预组各 24 主牌（单位 8 种 ×2 + 战术 4 种 ×2）", ok,
            ok ? "8 套全部合规" : "异常：" + string.Join("、", bad.ToArray()));
    }

    private static void PreconCardLegality()
    {
        var bad = new List<string>();
        for (int i = 0; i < V06Catalog.Precons.Count; i++)
        {
            V06Precon p = V06Catalog.Precons[i];
            for (int u = 0; u < p.Units.Length; u++)
            {
                V06Card c = V06Catalog.ById(p.Units[u]);
                if (c == null) { bad.Add(p.Name + " 缺单位 " + p.Units[u]); }
                else if (c.Category != V06Category.Unit) { bad.Add(p.Name + " " + p.Units[u] + " 不是单位"); }
            }
            for (int t = 0; t < p.Tactics.Length; t++)
            {
                V06Card c = V06Catalog.ById(p.Tactics[t]);
                if (c == null) { bad.Add(p.Name + " 缺战术 " + p.Tactics[t]); }
                else if (c.Category != V06Category.Tactic) { bad.Add(p.Name + " " + p.Tactics[t] + " 不是战术"); }
            }
            for (int j = 0; j < p.Upgrades.Length; j++)
            {
                V06Card c = V06Catalog.ById(p.Upgrades[j]);
                if (c == null) { bad.Add(p.Name + " 缺晋阶 " + p.Upgrades[j]); }
                else if (c.Category != V06Category.Upgrade) { bad.Add(p.Name + " " + p.Upgrades[j] + " 不是晋阶"); }
            }
            for (int s = 0; s < p.Supports.Length; s++)
            {
                V06Card c = V06Catalog.ById(p.Supports[s]);
                if (c == null) { bad.Add(p.Name + " 缺支援 " + p.Supports[s]); }
                else if (c.Category != V06Category.Support) { bad.Add(p.Name + " " + p.Supports[s] + " 不是支援"); }
            }
        }
        // 主牌里不允许同一种单位出现两次（应各 2 张，靠数量而非重复编号）
        var dup = new List<string>();
        for (int i = 0; i < V06Catalog.Precons.Count; i++)
        {
            V06Precon p = V06Catalog.Precons[i];
            var seen = new Dictionary<string, bool>();
            for (int u = 0; u < p.Units.Length; u++)
            {
                if (seen.ContainsKey(p.Units[u])) { dup.Add(p.Name + ":" + p.Units[u]); }
                else { seen[p.Units[u]] = true; }
            }
        }
        Check("预组引用的卡都在卡池内且分类正确", bad.Count == 0,
            bad.Count == 0 ? "8 套 × 27 项引用全部命中" : string.Join("；", bad.ToArray()));
        Check("同一预组内单位编号不重复", dup.Count == 0,
            dup.Count == 0 ? "无重复" : "重复：" + string.Join("、", dup.ToArray()));
    }

    private static void PreconVanguard()
    {
        var bad = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i < V06Catalog.Precons.Count; i++)
        {
            V06Precon p = V06Catalog.Precons[i];
            V06Card v = V06Catalog.ById(p.Vanguard);
            bool inDeck = false;
            for (int u = 0; u < p.Units.Length; u++) { if (p.Units[u] == p.Vanguard) { inDeck = true; break; } }
            if (v == null) { bad.Add(p.Name + " 主将不存在"); continue; }
            if (!inDeck) { bad.Add(p.Name + " 主将不在主牌"); }
            if (v.Cost > 3) { bad.Add(p.Name + " 主将费用 " + v.Cost + " > 3"); }
            sb.Append(v.Name + "(" + v.Cost + "费) ");
        }
        Check("每套主将都在主牌内且费用 ≤ 3", bad.Count == 0,
            bad.Count == 0 ? sb.ToString().Trim() : string.Join("、", bad.ToArray()));
    }

    private static void PreconLowCost()
    {
        // 文档口径：每套「低费单位」= 费用 ≤ 3，共 12–14 张（即 6–7 种 × 2）。
        // 费用 ≤ 2 的更便宜单位只有 8–12 张，作为开局展开余量单独报告。
        var sb = new StringBuilder();
        int bad = 0;
        for (int i = 0; i < V06Catalog.Precons.Count; i++)
        {
            V06Precon p = V06Catalog.Precons[i];
            int low3 = 0, low2 = 0;
            for (int u = 0; u < p.Units.Length; u++)
            {
                V06Card c = V06Catalog.ById(p.Units[u]);
                if (c == null) { continue; }
                if (c.Cost <= 3) { low3++; }
                if (c.Cost <= 2) { low2++; }
            }
            low3 *= 2;
            low2 *= 2;
            sb.Append(p.Name + " ≤3费" + low3 + "/≤2费" + low2 + "，");
            if (low3 < 12 || low3 > 14) { bad++; }
        }
        Check("每套低费单位（≤3 费）12–14 张", bad == 0, sb.ToString().TrimEnd('，'));
    }

    /// <summary>
    /// 卡面素材覆盖：全卡池 **332 张每一张**都要取到自己编号的专属图（不允许占位、兜底、缺图）。
    /// 2026-11-04 补图后标准提高到全量专属；2026-11-06 第三批 N61–N140 共 80 张卡面到位后，
    /// 期望值从 252 提到 332——占位数不再是"可接受"，因为新卡面已经全部导入。
    /// </summary>
    private static void UnitArtCoverage()
    {
        int own = 0, fallback = 0, none = 0, ph = 0;
        var missing = new System.Text.StringBuilder();
        var fb = new System.Text.StringBuilder();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            UnityEngine.Sprite sp = Jieqiao.UI.V06Art.Portrait(c.Id, c.Cost);
            if (sp == null) { none++; missing.Append(c.Id + " "); continue; }
            if (sp.name.StartsWith("ph:")) { ph++; continue; }
            if (sp.name == c.Id) { own++; continue; }
            fallback++;
            fb.Append(c.Id + "(" + sp.name.Split('#')[0] + ") ");
        }
        Check("卡面素材覆盖：332 张全专属（实测 " + own + " 专属 / " + ph + " 占位 / "
              + fallback + " 兜底 / " + none + " 缺图）",
            none == 0 && own == 332 && fallback == 0 && ph == 0,
            "缺图：" + (missing.Length == 0 ? "无" : missing.ToString().Trim())
            + "；兜底：" + (fb.Length == 0 ? "无" : fb.ToString().Trim()));
    }
    private static void IdNamespaceNote()
    {
        // 文档 ID 空间有两处冲突，工程内按下列约定消歧：
        //   ① 伙伴编号 P01–P10（02 章）与支援牌 P01–P12（03 §4.8）撞号 → 卡池保留 P，伙伴改用别的前缀；
        //   ② 遭遇牌已占用 C01–C12，不能再用 C 做伙伴前缀。
        //   ③ 2026-11-06 第三批卡池启用 N 前缀（N61–N140 共 80 条），所以 N 不再是"预留空号"，
        //      这里改为核对"N 只被这一批占用、没有和第二批 B/G 混用，支援/遭遇计数也没被动过"。
        int sup = 0, enc = 0, nUse = 0, nBad = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (c.Category == V06Category.Support) { sup++; }
            if (c.Category == V06Category.Encounter) { enc++; }
            if (c.Id.Length > 0 && c.Id[0] == 'N')
            {
                nUse++;
                // N 系列只放单位，且编号必须落在 N61–N140 区间
                int num;
                if (c.Category != V06Category.Unit
                    || !int.TryParse(c.Id.Substring(1), out num) || num < 61 || num > 140) { nBad++; }
            }
        }
        Check("ID 命名空间无冲突（P 支援 / C 遭遇 / N61–N140 单位）",
            sup == 12 && enc == 12 && nUse == 80 && nBad == 0,
            "支援 " + sup + " 张（P）、遭遇 " + enc + " 张（C）、N 系列 " + nUse + " 条（异常 " + nBad + " 条）");
    }
}
#endif
