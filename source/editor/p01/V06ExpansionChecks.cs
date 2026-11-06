#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Jieqiao;

/// <summary>
/// V0.7 扩展卡池核对：兽群（成长/进化/融合）与少女向单位。
///
/// 分两类：
///   · 数据核对：数量、编号、进化链完整、融合表合法、卡面占位；C# 直接读卡池
///   · 行为核对：用真实 API（Apply / LegalActions / AdvanceRound）演一遍
///     进化、终极形态 +1/+1、融合、应援光环，再跑完整对局确认不变量不破
///
/// 全部走引擎公开接口，不复制规则实现（避免"测的是测试自己的假设"）。
/// </summary>
public static class V06ExpansionChecks
{
    private static int _pass;
    private static int _fail;
    private static readonly StringBuilder Out = new StringBuilder();

    public static string Run()
    {
        _pass = 0;
        _fail = 0;
        Out.Length = 0;

        Counts();
        EvolveChain();
        GrowthData();
        FusionTable();
        Girls();
        ArtPlaceholder();
        EvolveBehavior();
        TopFormBehavior();
        FusionBehavior();
        CheerAura();
        FullGames();

        Out.AppendLine("V0.7 扩展卡池核对：" + _pass + " 通过 / " + _fail + " 失败");
        return Out.ToString();
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok) { _pass++; Out.AppendLine("  ✅ " + name); }
        else { _fail++; Out.AppendLine("  ❌ " + name + "　—　" + detail); }
    }

    // ───────────────────────── 数据核对 ─────────────────────────

    private static int CountPrefix(char p)
    {
        int n = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            string id = V06Catalog.All[i].Id;
            if (id.Length > 0 && id[0] == p) { n++; }
        }
        return n;
    }

    private static void Counts()
    {
        int b = CountPrefix('B');
        int g = CountPrefix('G');
        Check("兽群 44 张（B01–B44）+ 少女 16 张（G01–G16）", b == 44 && g == 16,
            "实际 兽群 " + b + " 张 / 少女 " + g + " 张");
        // 编号连续无空洞
        var miss = new List<string>();
        for (int i = 1; i <= 44; i++)
        {
            string id = "B" + i.ToString("00");
            if (V06Catalog.ById(id) == null) { miss.Add(id); }
        }
        for (int i = 1; i <= 16; i++)
        {
            string id = "G" + i.ToString("00");
            if (V06Catalog.ById(id) == null) { miss.Add(id); }
        }
        Check("扩展编号连续（无空洞）", miss.Count == 0,
            miss.Count == 0 ? "B01–B44、G01–G16 齐全" : "缺：" + string.Join("、", miss.ToArray()));
    }

    private static void EvolveChain()
    {
        var bad = new List<string>();
        int stage1 = 0, evolveOnly = 0, fusionOnly = 0;
        for (int i = 0; i < 44; i++)
        {
            V06Card c = V06Catalog.ById("B" + (i + 1 < 10 ? "0" + (i + 1) : "" + (i + 1)));
            if (c == null) { continue; }
            bool hasFuse = false;
            for (int k = 0; k < V06Expansion.FusionPairCount; k++)
            {
                if (V06Expansion.FusionPairAt(k, 2) == c.Id) { hasFuse = true; }
            }
            if (hasFuse)
            {
                fusionOnly++;
                if (c.Cost != -1) { bad.Add(c.Id + " 融合体却可部署"); }
                if (V06Expansion.GrowThreshold(c.Id) != 0) { bad.Add(c.Id + " 融合体不该成长"); }
                continue;
            }
            if (c.Cost >= 1 && c.Cost <= 4) { stage1++; }
            else if (c.Cost == -1) { evolveOnly++; }
            else { bad.Add(c.Id + " 费用非法 " + c.Cost); }

            string next = V06Expansion.EvolutionOf(c.Id);
            if (next != null)
            {
                V06Card nc = V06Catalog.ById(next);
                if (nc == null) { bad.Add(c.Id + " 进化目标不存在 " + next); }
                else if (nc.Cost != -1) { bad.Add(next + " 进化形态却可部署"); }
                else if (V06Expansion.GrowThreshold(c.Id) <= 0) { bad.Add(c.Id + " 有进化却无成长阈值"); }
            }
        }
        Check("进化链完整：一阶可部署 / 进化形态与融合体不可部署", bad.Count == 0,
            bad.Count == 0 ? "一阶 " + stage1 + " / 进化形态 " + evolveOnly + " / 融合体 " + fusionOnly
                           : string.Join("；", bad.ToArray()));
        Check("兽群共 14 条一阶线 + 14 张二阶 + 8 张三阶 + 8 张融合体", stage1 == 14 && evolveOnly == 14 + 8 && fusionOnly == 8,
            "一阶 " + stage1 + "、进化形态 " + evolveOnly + "（应 22）、融合体 " + fusionOnly + "（应 8）");
    }

    private static void GrowthData()
    {
        var bad = new List<string>();
        int grow = 0, top = 0;
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (!c.Exp || c.Category != V06Category.Unit) { continue; }
            int need = V06Expansion.GrowThreshold(c.Id);
            bool kw = System.Array.IndexOf(c.Keywords, "成长") >= 0;
            if (need > 0 && !kw) { bad.Add(c.Id + " 有成长阈值但无成长关键词"); }
            if (need == 0 && kw) { bad.Add(c.Id + " 有关键词但无成长阈值"); }
            if (need > 0)
            {
                grow++;
                if (need < 2 || need > 3) { bad.Add(c.Id + " 阈值 " + need + " 超出 2–3"); }
                if (V06Expansion.EvolutionOf(c.Id) == null) { top++; }
                if (!c.Text.Contains("成长")) { bad.Add(c.Id + " 卡面文字没写成长"); }
            }
        }
        Check("成长阈值与关键词/卡面文字一致（" + grow + " 张成长，" + top + " 张终极形态）", bad.Count == 0,
            bad.Count == 0 ? "全部一致" : string.Join("；", bad.ToArray()));
        Check("终极形态 14 张（二阶终态 6 + 三阶 8）", top == 14, "实测终极形态 " + top + " 张");
    }

    private static void FusionTable()
    {
        var bad = new List<string>();
        var seen = new HashSet<string>();
        for (int i = 0; i < V06Expansion.FusionPairCount; i++)
        {
            string a = V06Expansion.FusionPairAt(i, 0);
            string b = V06Expansion.FusionPairAt(i, 1);
            string r = V06Expansion.FusionPairAt(i, 2);
            if (a == b) { bad.Add(a + " 素材重复"); }
            if (V06Catalog.ById(a) == null) { bad.Add(a + " 素材A不存在"); }
            if (V06Catalog.ById(b) == null) { bad.Add(b + " 素材B不存在"); }
            V06Card rc = V06Catalog.ById(r);
            if (rc == null) { bad.Add(r + " 结果不存在"); continue; }
            if (System.Array.IndexOf(rc.Keywords, "融合") < 0) { bad.Add(r + " 结果没有融合关键词"); }
            if (rc.Cost != -1) { bad.Add(r + " 结果可部署"); }
            if (V06Expansion.FusionResult(a, b) != r) { bad.Add(r + " 正向查表不一致"); }
            if (V06Expansion.FusionResult(b, a) != r) { bad.Add(r + " 反向查表不一致（表应无序）"); }
            if (!seen.Add(a + "+" + b)) { bad.Add(r + " 组合重复"); }
            // 结果不能再去融合（避免无限链）
            for (int k = 0; k < V06Expansion.FusionPairCount; k++)
            {
                if (V06Expansion.FusionPairAt(k, 0) == r || V06Expansion.FusionPairAt(k, 1) == r) { bad.Add(r + " 还能再融合"); }
            }
        }
        Check("融合表 " + V06Expansion.FusionPairCount + " 组：素材在池、结果唯一、无序可查、结果不可再融合", bad.Count == 0,
            bad.Count == 0 ? "8 组全部合法" : string.Join("；", bad.ToArray()));
        Check("融合组合数 8", V06Expansion.FusionPairCount == 8, "实际 " + V06Expansion.FusionPairCount + " 组");
    }

    private static void Girls()
    {
        var bad = new List<string>();
        int girlTag = 0, cheer = 0;
        for (int i = 0; i < 16; i++)
        {
            string id = "G" + (i + 1 < 10 ? "0" + (i + 1) : "" + (i + 1));
            V06Card c = V06Catalog.ById(id);
            if (c == null) { bad.Add(id + " 不存在"); continue; }
            if (c.Category != V06Category.Unit) { bad.Add(id + " 不是单位"); }
            if (c.Cost < 2 || c.Cost > 4) { bad.Add(id + " 费用 " + c.Cost + " 超出 2–4"); }
            if (c.Atk <= 0 || c.Hp <= 0) { bad.Add(id + " 攻生非法"); }
            if (string.IsNullOrEmpty(c.Tags) || !c.Tags.Contains("少女")) { bad.Add(id + " 缺少少女标签"); }
            else { girlTag++; }
            // 命名规范：只用单位名称，不带人名式后缀（不含「·」分隔的代号）
            if (c.Name.Contains("·")) { bad.Add(id + " 名称含人名式分隔符"); }
            if (System.Array.IndexOf(c.Keywords, "应援") >= 0) { cheer++; }
        }
        Check("少女 16 张：单位名称、费用 2–4、带少女标签", bad.Count == 0,
            bad.Count == 0 ? "16 张全部合规（应援 " + cheer + " 张）" : string.Join("；", bad.ToArray()));
        Check("应援光环至少 2 张（G04 偶像应援 / G10 祭典舞姬）", cheer >= 2, "实际 " + cheer + " 张");
    }

    private static void ArtPlaceholder()
    {
        int ph = 0, own = 0, none = 0, expCount = 0;
        var miss = new StringBuilder();
        for (int i = 0; i < V06Catalog.All.Count; i++)
        {
            V06Card c = V06Catalog.All[i];
            if (!c.Exp) { continue; }
            expCount++;
            UnityEngine.Sprite sp = Jieqiao.UI.V06Art.Portrait(c.Id, c.Cost);
            if (sp == null) { none++; miss.Append(c.Id + " "); continue; }
            if (sp.name.StartsWith("ph:")) { ph++; }
            else if (sp.name == c.Id) { own++; }
        }
        // 2026-11-06：第三批 N61–N140 的 80 张卡面已到位，扩展卡（B/G/N 共 140）应当全是专属图，
        // 占位数必须为 0——留 ph 计数是为了万一以后又交来"无图卡"能立刻看出来。
        Check("扩展卡都有专属卡面（" + expCount + " 张：专属 " + own + " / 文字占位 " + ph + " / 缺图 " + none + "）",
            none == 0 && ph == 0 && own == 140,
            "缺图：" + (miss.Length == 0 ? "无" : miss.ToString().Trim()));
    }

    // ───────────────────────── 行为核对 ─────────────────────────

    /// <summary>开一局并清空干扰：只留我方、行动点给满，得到一个可控的小局面。</summary>
    private static V06Engine FreshEngine(int deckA, int deckB, out string error)
    {
        V06Engine e = new V06Engine();
        e.TryStart(777, deckA, deckB, out error);
        e.Active = 0;
        e.Players[0].ActionPoints = 9;
        e.Players[0].MainActionsUsed = 0;
        return e;
    }

    private static bool ForceDeploy(V06Engine e, int camp, string cardId, int slot, out string error)
    {
        V06Player p = e.Players[camp];
        p.Hand.Add(cardId);          // 测试注入：会破坏"30 张守恒"，所以本组用例不查守恒
        p.ActionPoints = 9;
        e.Active = camp;
        V06Action a = new V06Action { Kind = V06ActionKind.Deploy, CardId = cardId, Slot = slot };
        return e.Apply(a, out error);
    }

    private static void EvolveBehavior()
    {
        string err;
        V06Engine e = FreshEngine(11, 11, out err);
        if (e.Players[0] == null) { Check("进化：一阶兽轮末成长并进化", false, "开局失败 " + err); return; }
        if (!ForceDeploy(e, 0, "B01", 0, out err)) { Check("进化：一阶兽轮末成长并进化", false, "部署失败 " + err); return; }
        V06Unit u = e.Players[0].At(0);
        int atk0 = u.AtkBaseNow, hp0 = u.CurHp;

        e.AdvanceRound();                       // 轮末 → 成长 1
        bool step1 = u.CardId == "B01" && u.Growth == 1;
        e.AdvanceRound();                       // 轮末 → 成长 2 → 进化 B15
        bool evolved = u.CardId == "B15" && u.Name == "岩背荒狼";
        bool stats = u.AtkBaseNow == 4 && u.MaxHp == 4;
        bool reset = u.Growth == 0;
        Check("进化：一阶兽轮末成长并进化（B01 → B15，攻血按新形态）",
            step1 && evolved && stats && reset,
            "第一步 " + (step1 ? "OK" : "失败") + "，进化 " + (evolved ? "OK" : "失败")
            + "，身材 " + (stats ? "OK" : atk0 + "/" + hp0 + "→" + u.AtkBaseNow + "/" + u.MaxHp)
            + "，成长重置 " + (reset ? "OK" : "失败 " + u.Growth));

        // 承伤保留 + 不会致死（B01 是 2/2，受 1 伤后进化成 4/4，应带伤存活）
        V06Engine e2 = FreshEngine(11, 11, out err);
        ForceDeploy(e2, 0, "B01", 0, out err);
        V06Unit u2 = e2.Players[0].At(0);
        u2.Damage = 1;
        e2.AdvanceRound();
        e2.AdvanceRound();
        bool kept = u2.CardId == "B15" && u2.Damage == 1 && u2.CurHp == 3;
        Check("进化保留已受伤害（新形态 4 血、已受 1 伤）", kept,
            "状态 " + u2.CardId + " 伤 " + u2.Damage + " 当前 " + u2.CurHp);

        V06Engine e3 = FreshEngine(11, 11, out err);
        ForceDeploy(e3, 0, "B02", 0, out err);
        V06Unit u3 = e3.Players[0].At(0);
        u3.Damage = 3;                           // 1/4 → 已受 3 伤，进化成 3/6 后应存活
        e3.AdvanceRound();
        e3.AdvanceRound();
        bool safe = u3.CardId == "B16" && u3.CurHp >= 1;
        Check("进化不会因为旧伤害致死（最多留 1 点生命）", safe,
            "状态 " + u3.CardId + " 当前 " + u3.CurHp);
    }

    private static void TopFormBehavior()
    {
        string err;
        V06Engine e = FreshEngine(11, 11, out err);
        // 直接放进战场：B23 是二阶终态（成长 3 满时永久 +1/+1）
        V06Player p = e.Players[0];
        p.Hand.Add("B09");
        p.ActionPoints = 9;
        e.Active = 0;
        V06Action a = new V06Action { Kind = V06ActionKind.Deploy, CardId = "B09", Slot = 0 };
        if (!e.Apply(a, out err)) { Check("终极形态满成长 +1/+1", false, "部署失败 " + err); return; }
        V06Unit u = p.At(0);
        e.AdvanceRound();
        e.AdvanceRound();
        e.AdvanceRound();                        // B09 成长 3 → 进化 B23（4/7）
        bool toB23 = u.CardId == "B23";
        int atk = u.AtkBaseNow, hp = u.MaxHp;
        e.AdvanceRound();
        e.AdvanceRound();
        e.AdvanceRound();                        // B23 成长 3 → 永久 +1/+1
        bool buffed = u.AtkBaseNow == atk + 1 && u.MaxHp == hp + 1 && u.GrowthMaxed;
        Check("终极形态满成长永久 +1/+1（只加一次）",
            toB23 && buffed,
            "进化 " + (toB23 ? "OK" : u.CardId) + "，身材 " + atk + "/" + hp + " → " + u.AtkBaseNow + "/" + u.MaxHp
            + "，标记 " + u.GrowthMaxed);
    }

    private static void FusionBehavior()
    {
        string err;
        V06Engine e = FreshEngine(11, 11, out err);
        if (!ForceDeploy(e, 0, "B01", 0, out err)) { Check("融合：合法组合可执行", false, "部署1失败 " + err); return; }
        if (!ForceDeploy(e, 0, "B11", 1, out err)) { Check("融合：合法组合可执行", false, "部署2失败 " + err); return; }
        e.Active = 0;
        e.Players[0].MainActionsUsed = 0;
        e.Players[0].ActionPoints = 5;
        int apBefore = e.Players[0].ActionPoints;

        List<V06Action> acts = e.LegalActions();
        V06Action fuse = null;
        for (int i = 0; i < acts.Count; i++) { if (acts[i].Kind == V06ActionKind.Fuse) { fuse = acts[i]; break; } }
        if (fuse == null) { Check("融合：合法组合可执行", false, "LegalActions 里没有 Fuse 行动"); return; }
        bool target = fuse.CardId == "B41";
        if (!e.Apply(fuse, out err)) { Check("融合：合法组合可执行", false, "Apply 失败 " + err); return; }

        V06Player p = e.Players[0];
        V06Unit n = p.At(0);
        bool onBoard = n != null && n.CardId == "B41" && n.Name == "双生狼蛛";
        bool stats = onBoard && n.AtkBaseNow == 4 && n.MaxHp == 5;   // 3/4 + 融合仪式 +1/+1
        bool exile = p.Exile.Count == 2;
        bool cost = p.ActionPoints == apBefore;
        bool token = onBoard && n.IsToken;
        bool fresh = onBoard && !n.DeployedThisRound;   // 融合体登场当轮即可攻击
        Check("融合：合法组合可执行（B01+B11 → B41 双生狼蛛，只消耗 1 次主行动）",
            target && onBoard && stats && exile && cost && token && fresh,
            "目标 " + (target ? "OK" : fuse.CardId) + "，上场 " + (onBoard ? "OK" : "失败")
            + "，身材 " + (stats ? "OK" : "4/5 不符") + "，素材移出 " + p.Exile.Count
            + "，行动点 " + apBefore + "→" + p.ActionPoints + "（应不变），登场即可攻击 " + (fresh ? "OK" : "失败"));

        // 承伤继承：两只各受 1 伤 → 融合体承伤 2
        V06Engine e2 = FreshEngine(11, 11, out err);
        ForceDeploy(e2, 0, "B01", 0, out err);
        ForceDeploy(e2, 0, "B11", 1, out err);
        e2.Players[0].At(0).Damage = 1;
        e2.Players[0].At(1).Damage = 1;
        e2.Active = 0;
        e2.Players[0].MainActionsUsed = 0;
        e2.Players[0].ActionPoints = 5;
        List<V06Action> a2 = e2.LegalActions();
        V06Action f2 = null;
        for (int i = 0; i < a2.Count; i++) { if (a2[i].Kind == V06ActionKind.Fuse) { f2 = a2[i]; break; } }
        bool ok2 = f2 != null && e2.Apply(f2, out err);
        V06Unit n2 = ok2 ? e2.Players[0].At(0) : null;
        Check("融合体继承素材总伤害（1+1=2，当前 3 血）",
            ok2 && n2 != null && n2.Damage == 2 && n2.CurHp == 3,
            n2 == null ? "融合失败" : "伤 " + n2.Damage + " 当前 " + n2.CurHp);

        // 非法组合不产生 Fuse 行动：B01 与 B05 不在融合表里
        V06Engine e3 = FreshEngine(11, 11, out err);
        ForceDeploy(e3, 0, "B01", 0, out err);
        ForceDeploy(e3, 0, "B05", 1, out err);
        e3.Active = 0;
        List<V06Action> a3 = e3.LegalActions();
        int fuseCount = 0;
        for (int i = 0; i < a3.Count; i++) { if (a3[i].Kind == V06ActionKind.Fuse) { fuseCount++; } }
        Check("不在融合表里的两只不会出现融合按钮", fuseCount == 0, "实测 " + fuseCount + " 个融合行动");

        // 跨形态融合：B15（二阶荒狼）+ B17（二阶猎鹰）→ B37，验证按"进化线"而不是按编号匹配
        V06Engine e5 = FreshEngine(11, 11, out err);
        ForceDeploy(e5, 0, "B15", 0, out err);
        ForceDeploy(e5, 0, "B17", 1, out err);
        e5.Active = 0;
        e5.Players[0].MainActionsUsed = 0;
        List<V06Action> a5 = e5.LegalActions();
        V06Action f5 = null;
        for (int i = 0; i < a5.Count; i++) { if (a5[i].Kind == V06ActionKind.Fuse) { f5 = a5[i]; break; } }
        bool ok5 = f5 != null && e5.Apply(f5, out err);
        V06Unit n5 = ok5 ? e5.Players[0].At(0) : null;
        Check("跨形态融合（B15 + B17 → B37 穹翼狼王，按进化线匹配）",
            ok5 && n5 != null && n5.CardId == "B37" && n5.AtkBaseNow == 7 && n5.MaxHp == 8,
            n5 == null ? "没有可用融合" : ("结果 " + n5.CardId + " " + n5.AtkBaseNow + "/" + n5.MaxHp));

        // 融合后 30 张守恒：不用注入，直接跑一局真对局
        V06Engine e4 = new V06Engine();
        e4.TryStart(888, 11, 11, out err);
        string bad = "";
        int guard = 0;
        while (!e4.Finished && guard++ < 4000)
        {
            string inv = V06Sim.Invariant(e4);
            if (inv.Length > 0) { bad = inv; break; }
            if (!V06Ai.Step(e4)) { break; }
        }
        Check("兽群对兽群整局（含进化/融合）每步都保持 30 张守恒", bad.Length == 0,
            bad.Length == 0 ? "整局通过（第 " + e4.Round + " 轮收尾）" : bad);
    }

    private static void CheerAura()
    {
        string err;
        V06Engine e = FreshEngine(12, 12, out err);
        ForceDeploy(e, 0, "G12", 0, out err);      // 弓道少女 3/3 远射
        ForceDeploy(e, 0, "G04", 1, out err);      // 偶像应援 1/3（入场会给一个单位本轮 +1，先清掉再测光环）
        V06Unit archer = e.Players[0].At(0);
        V06Unit cheerUnit = e.Players[0].At(1);
        if (archer != null) { archer.TempAtk = 0; }
        if (cheerUnit != null) { cheerUnit.TempAtk = 0; }
        int withCheer = archer == null ? 0 : e.FinalAttack(archer, false);
        int selfCheer = cheerUnit == null ? 0 : e.FinalAttack(cheerUnit, false);
        Check("应援光环：己方其他单位攻击 +1（自身不加）",
            archer != null && withCheer == 4 && selfCheer == 1,
            "弓道少女 " + withCheer + "（应 4），应援自身 " + selfCheer + "（应 1）");

        // 两张应援不叠加：再放一张 G10，弓道少女仍是 4
        ForceDeploy(e, 0, "G10", 2, out err);
        V06Unit archer2 = e.Players[0].At(0);
        if (archer2 != null) { archer2.TempAtk = 0; }
        int twoCheer = archer2 == null ? 0 : e.FinalAttack(archer2, false);
        Check("两张应援不叠加（仍为 +1）", twoCheer == 4, "实测 " + twoCheer + "（应 4）");
    }

    private static void FullGames()
    {
        // 兽群 vs 少女 各 4 局（种子固定），统计进化/融合是否真的发生
        int games = 0, done = 0, evolutions = 0, fusions = 0, bad = 0;
        string firstBad = "";
        var rounds = new List<int>();
        int[] pairs = new int[] { 11, 12, 11, 12, 12, 11, 12, 11 };
        for (int g = 0; g < pairs.Length; g++)
        {
            int a = pairs[g];
            int b = g % 2 == 0 ? 12 : 11;
            V06Engine e = new V06Engine();
            string err;
            if (!e.TryStart(4100 + g, a, b, out err)) { bad++; if (firstBad.Length == 0) { firstBad = "开局失败 " + err; } continue; }
            games++;
            int guard = 0;
            while (!e.Finished && guard++ < 4000)
            {
                string inv = V06Sim.Invariant(e);
                if (inv.Length > 0) { bad++; if (firstBad.Length == 0) { firstBad = "第 " + g + " 局第 " + e.Round + " 轮：" + inv; } break; }
                if (!V06Ai.Step(e)) { break; }
            }
            if (e.Finished) { done++; rounds.Add(e.Round); }
            for (int i = 0; i < e.Log.Count; i++)
            {
                if (e.Log[i].StartsWith("进化：")) { evolutions++; }
                else if (e.Log[i].IndexOf(" 融合：") >= 0) { fusions++; }
            }
        }
        float avgR = 0f;
        for (int i = 0; i < rounds.Count; i++) { avgR += rounds[i]; }
        if (rounds.Count > 0) { avgR /= rounds.Count; }
        Check("兽群/少女预组完整对局 " + games + " 局全部打完、无异常", bad == 0 && done == games && games >= 6,
            "完成 " + done + "/" + games + " 局，异常 " + bad + "（" + firstBad + "）");
        Check("对局里真的发生了进化与融合（进化 " + evolutions + " 次 / 融合 " + fusions + " 次，平均 "
              + avgR.ToString("0.0") + " 轮）",
            evolutions > 0 && fusions > 0,
            "进化 " + evolutions + "、融合 " + fusions);
    }
}
#endif
