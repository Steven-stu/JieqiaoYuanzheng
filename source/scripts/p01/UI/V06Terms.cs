using System.Collections.Generic;

namespace Jieqiao.UI
{
    /// <summary>
    /// 术语表（V0.7《第二轮落地方案》§1）。
    ///
    /// 每一条解释都按**当前引擎实现**写，不按设计意图写。改规则时必须同步改这里，
    /// 否则玩家会读到与实战不一致的说明——这比没有说明更糟。
    /// 对应实现位置写在各条注释里，便于回归时核对。
    ///
    /// 约束：Short 一律 ≤25 字（文档 §1.1），且不出现别的术语键（文档 §1.3）。
    /// </summary>
    public static class V06Terms
    {
        public sealed class Term
        {
            public string Key;      // 术语键
            public string Short;    // ≤25 字，卡牌放大面板的主解释
            public string Extra;    // 可选补充，仅在放大面板另起一行
            public Term(string key, string shortText, string extra)
            {
                Key = key; Short = shortText; Extra = extra;
            }
        }

        // ── 34 条：21 个真实关键词 + 13 个规则词 ──
        private static readonly Term[] _terms = new Term[]
        {
            // 关键词（与 V06Catalog/V06ExpansionCards 的 Keywords 字段一一对应）
            new Term("成长",     "轮末在场则计数+1，满值换形或强化", "V06Engine.GrowthStep"),
            new Term("进化",     "计数满值自动换下一形态，不再重跑能力", "V06Engine.Evolve"),
            new Term("成长值",   "兽类轮末积累的计数，换形后归零",     "V06Engine.GrowthStep"),
            new Term("护卫",     "位于前排时，敌人必须优先攻击它",     "V06Engine.AttackTargets"),
            new Term("坚壁",     "受到主动攻击的伤害减1，最低0",       "V06Engine.ModifyAttackDamage"),
            new Term("突袭",     "部署当轮即可主动攻击，仍限一次",     "V06Engine.CanAttackFrom"),
            new Term("远射",     "可攻击任意排敌兵，且不被目标还手",   "V06Engine.AttackTargets"),
            new Term("飞行",     "在后方也能攻击敌前排，仍会被还手",   "V06Engine.CanAttackFrom"),
            new Term("潜行",     "免被单独指定；主动出手或换轮解除",   "V06Engine.Targetable"),
            new Term("汲取",     "每轮首次主动伤到敌单位时，自身回复1",  "V06Engine.FinalAttack"),
            new Term("协攻",     "己方前排至少2个单位时，主动攻击+1",  "V06Engine.FinalAttack"),
            new Term("穿透",     "可自选敌兵目标，仍不能越过整排打旗",   "V06Engine.AttackTargets"),
            new Term("余烬",     "阵亡时对随机一个未藏身敌兵造成1伤",  "V06Engine.Kill"),
            new Term("融合",     "两兽合成一体，占1次主行动，不花费用", "V06Engine.DoFuse"),
            new Term("回响",     "从手牌部署成功后，牌库顶进手牌",      "V06Engine.ApplyEnter"),
            new Term("夜行",     "奇数轮主动攻击+1",                   "V06Engine.FinalAttack"),
            new Term("再生",     "每轮末回复自身1点生命，不超上限",     "V06Engine.EndRound"),
            new Term("维修",     "进场时治疗另一个己方受伤构装2",       "V06Effects.RepairConstruct2"),
            new Term("应援",     "在场时其他友方主动攻击+1，不叠加",    "V06Engine.FinalAttack"),
            new Term("过载",     "本轮主动攻击+2，轮末对自身造成1伤",   "V06Engine.EndRound"),
            new Term("龙威",     "进场时使一个敌兵本轮下次攻击−1",      "V06Effects.DragonFear"),
            new Term("少女",     "题材标签，不改变任何战斗数值",        "无机制引用"),

            // 规则词
            new Term("入场",     "单位进场时结算；换形不再重跑",        "V06Engine.ApplyEnter"),
            new Term("被击败时", "生命归零离场时结算；合成消耗不触发",  "V06Engine.Kill"),
            new Term("保留",     "变化后沿用的指定内容，不是回满血",    "如 J05 保留护卫"),
            new Term("生成",     "在自己空格产生新兵，满场则不产生",    "V06Engine.SpawnToken"),
            new Term("抽",       "牌库顶进手牌；牌库空则该方旗−1",      "V06Engine.Draw"),
            new Term("弃",       "移入自己的弃牌区，不是本局移除",      "V06Player.Discard"),
            new Term("若/每",    "若＝条件成立才结算；每＝按写明频率",   "卡文语义，无独立实现"),
            new Term("远征旗",   "己方旗血归零即败；同次双方归零则平",  "V06Engine.CheckWinner"),
            new Term("压力阶段", "第8轮末起双方旗扣1/1/2/2/3",          "V06Engine.EndRound"),
            new Term("晋阶",     "付费换基础攻血，已受伤害保持不变",     "V06Effects2.ApplyUpgrade"),
            new Term("前排/后排", "后排单位需远射或飞行才能攻击",        "V06Engine.CanAttackFrom"),
            new Term("效果伤害", "直接扣生命，不引发还手，不吃减伤",     "V06Engine.EffectDamage"),
        };

        private static Dictionary<string, Term> _byKey;

        private static Dictionary<string, Term> Map()
        {
            if (_byKey == null)
            {
                _byKey = new Dictionary<string, Term>();
                for (int i = 0; i < _terms.Length; i++) { _byKey[_terms[i].Key] = _terms[i]; }
            }
            return _byKey;
        }

        public static int Count { get { return _terms.Length; } }

        public static string[] AllKeys
        {
            get
            {
                string[] r = new string[_terms.Length];
                for (int i = 0; i < _terms.Length; i++) { r[i] = _terms[i].Key; }
                return r;
            }
        }

        public static bool Has(string key)
        {
            return !string.IsNullOrEmpty(key) && Map().ContainsKey(key);
        }

        public static string ShortOf(string key)
        {
            Term t;
            return (!string.IsNullOrEmpty(key) && Map().TryGetValue(key, out t)) ? t.Short : null;
        }

        /// <summary>一条可显示的解释行，例如 “汲取　每轮首次主动伤到敌兵时，自身回复1”。</summary>
        public static string Explain(string key)
        {
            Term t;
            if (string.IsNullOrEmpty(key) || !Map().TryGetValue(key, out t)) { return null; }
            return t.Key + "　" + t.Short;
        }

        // 卡面文字里出现就该解释的规则词（按这个顺序输出，与卡面阅读顺序一致）
        private static readonly string[,] _textTriggers = new string[,]
        {
            { "入场",     "入场" },
            { "被击败",   "被击败时" },
            { "效果伤害", "效果伤害" },
            { "生成",     "生成" },
            { "保留",     "保留" },
            { "晋阶",     "晋阶" },
            { "融合",     "融合" },
            { "远征旗",   "远征旗" },
            { "击破",     "远征旗" },
            { "压力",     "压力阶段" },
            { "抽",       "抽" },
            { "弃牌",     "弃" },
        };

        /// <summary>这张卡需要解释的术语：先 关键词，再 卡面文字里出现的规则词。</summary>
        public static List<string> OfCard(V06Card c)
        {
            List<string> r = new List<string>();
            if (c == null) { return r; }
            if (c.Keywords != null)
            {
                for (int i = 0; i < c.Keywords.Length; i++)
                {
                    if (Has(c.Keywords[i]) && !r.Contains(c.Keywords[i])) { r.Add(c.Keywords[i]); }
                }
            }
            string text = c.Text ?? "";
            for (int i = 0; i < _textTriggers.GetLength(0); i++)
            {
                if (text.IndexOf(_textTriggers[i, 0], System.StringComparison.Ordinal) < 0) { continue; }
                string key = _textTriggers[i, 1];
                if (Has(key) && !r.Contains(key)) { r.Add(key); }
            }
            // “若／每”：卡面出现“若”或“每”时给一条合并提示
            if (!r.Contains("若/每")
                && (text.IndexOf("若", System.StringComparison.Ordinal) >= 0
                    || text.IndexOf("每", System.StringComparison.Ordinal) >= 0))
            {
                r.Add("若/每");
            }
            return r;
        }

        /// <summary>放大面板用的多行术语说明；没有可解释术语时返回空串。</summary>
        public static string Block(V06Card c)
        {
            List<string> keys = OfCard(c);
            if (keys.Count == 0) { return ""; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("术语：");
            for (int i = 0; i < keys.Count; i++)
            {
                string line = Explain(keys[i]);
                if (line != null) { sb.Append('\n').Append(line); }
            }
            return sb.ToString();
        }
    }
}
