using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// V0.7 扩展卡池：兽群（可成长进化 / 融合）+ 少女。
    ///
    /// 与 V06Catalog 的关系：本文件只提供数据，`V06Catalog` 的静态构造函数会把
    /// `V06ExpansionCards.All` 并进 `V06Catalog.All`，因此 `ById` / 计数 / 断言
    /// 全部照常工作，不需要改任何调用点。
    ///
    /// 设计稿见 CardGame/兽群与少女_扩展卡池_V0.7设计.md。
    ///
    /// 三条硬约定（断言会核对）：
    ///   ① 进化形态与融合体 Cost = -1，只能由引擎生成，不能部署、不进牌库；
    ///   ② 每个「成长」卡的阈值与进化目标必须在本文件里成对存在；
    ///   ③ 融合表无序：FusionResult(a,b) == FusionResult(b,a)。
    /// </summary>
    public static class V06ExpansionCards
    {
        public static readonly List<V06Card> All = new List<V06Card>
        {
            // ── 一阶兽（可部署）：身材低于同费曲线，作为成长的补偿 ──
            new V06Card { Exp = true, Id = "B01", Name = "荒原狼崽", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "兽群", Text = "成长2 → 岩背荒狼。突袭。", Keywords = new string[] { "成长", "突袭" } },
            new V06Card { Exp = true, Id = "B02", Name = "灰毛幼熊", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "兽群", Text = "成长2 → 铁爪巨熊。护卫。", Keywords = new string[] { "成长", "护卫" } },
            new V06Card { Exp = true, Id = "B03", Name = "崖巢雏鹰", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 1, Tags = "兽群", Text = "成长2 → 铁羽猎鹰。飞行。", Keywords = new string[] { "成长", "飞行" } },
            new V06Card { Exp = true, Id = "B04", Name = "石纹幼蛇", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "兽群", Text = "成长2 → 毒牙巨蟒。潜行。", Keywords = new string[] { "成长", "潜行" } },
            new V06Card { Exp = true, Id = "B05", Name = "铁壳甲虫", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "兽群", Text = "成长2 → 攻城甲虫。护卫。", Keywords = new string[] { "成长", "护卫" } },
            new V06Card { Exp = true, Id = "B06", Name = "剑齿幼兽", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "兽群", Text = "成长2 → 影刃剑齿虎。", Keywords = new string[] { "成长" } },
            new V06Card { Exp = true, Id = "B07", Name = "灰烬蜥", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "兽群", Text = "成长2 → 熔火蜥王。余烬。", Keywords = new string[] { "成长", "余烬" } },
            new V06Card { Exp = true, Id = "B08", Name = "触须幼兽", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "兽群", Text = "成长2 → 深海触手。", Keywords = new string[] { "成长" } },
            new V06Card { Exp = true, Id = "B09", Name = "荒原重蹄兽", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "兽群", Text = "成长3 → 铁角犀兽。护卫。", Keywords = new string[] { "成长", "护卫" } },
            new V06Card { Exp = true, Id = "B10", Name = "风翼翼兽", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "兽群", Text = "成长3 → 疾风翼龙兽。飞行。", Keywords = new string[] { "成长", "飞行" } },
            new V06Card { Exp = true, Id = "B11", Name = "岩隙幼蛛", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "兽群", Text = "成长2 → 织网巨蛛。潜行。", Keywords = new string[] { "成长", "潜行" } },
            new V06Card { Exp = true, Id = "B12", Name = "冰角幼鹿", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "兽群", Text = "成长2 → 霜角巨鹿。再生。", Keywords = new string[] { "成长", "再生" } },
            new V06Card { Exp = true, Id = "B13", Name = "荒原灰獾", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "兽群", Text = "成长2 → 铁背灰獾。突袭。", Keywords = new string[] { "成长", "突袭" } },
            new V06Card { Exp = true, Id = "B14", Name = "白骨幼蝎", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "兽群", Text = "成长2 → 骨甲巨蝎。穿透。", Keywords = new string[] { "成长", "穿透" } },

            // ── 二阶兽（不可部署）：成长3 → 三阶 ──
            new V06Card { Exp = true, Id = "B15", Name = "岩背荒狼", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 4, Tags = "兽群", Text = "由荒原狼崽进化。成长3 → 霜牙狼王。突袭。", Keywords = new string[] { "成长", "进化", "突袭" } },
            new V06Card { Exp = true, Id = "B16", Name = "铁爪巨熊", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 6, Tags = "兽群", Text = "由灰毛幼熊进化。成长3 → 裂山古熊。护卫。", Keywords = new string[] { "成长", "进化", "护卫" } },
            new V06Card { Exp = true, Id = "B17", Name = "铁羽猎鹰", Category = V06Category.Unit, Cost = -1, Atk = 2, Hp = 3, Tags = "兽群", Text = "由崖巢雏鹰进化。成长3 → 风暴鹰后。飞行·远射。", Keywords = new string[] { "成长", "进化", "飞行", "远射" } },
            new V06Card { Exp = true, Id = "B18", Name = "毒牙巨蟒", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 3, Tags = "兽群", Text = "由石纹幼蛇进化。成长3 → 双首蛇神。潜行·汲取。", Keywords = new string[] { "成长", "进化", "潜行", "汲取" } },
            new V06Card { Exp = true, Id = "B19", Name = "攻城甲虫", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 6, Tags = "兽群", Text = "由铁壳甲虫进化。成长3 → 熔岩甲虫王。护卫·坚壁。", Keywords = new string[] { "成长", "进化", "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "B20", Name = "影刃剑齿虎", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 3, Tags = "兽群", Text = "由剑齿幼兽进化。成长3 → 夜魇领主豹。潜行·穿透。", Keywords = new string[] { "成长", "进化", "潜行", "穿透" } },
            new V06Card { Exp = true, Id = "B21", Name = "熔火蜥王", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 5, Tags = "兽群", Text = "由灰烬蜥进化。成长3 → 火山古龙兽。余烬。", Keywords = new string[] { "成长", "进化", "余烬" } },
            new V06Card { Exp = true, Id = "B22", Name = "深海触手", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 5, Tags = "兽群", Text = "由触须幼兽进化。成长3 → 深渊巨章。远射。", Keywords = new string[] { "成长", "进化", "远射" } },
            new V06Card { Exp = true, Id = "B23", Name = "铁角犀兽", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 7, Tags = "兽群", Text = "由荒原重蹄兽进化。终极形态：成长满时永久+1/+1。护卫·坚壁。", Keywords = new string[] { "成长", "进化", "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "B24", Name = "疾风翼龙兽", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 5, Tags = "兽群", Text = "由风翼翼兽进化。终极形态：成长满时永久+1/+1。飞行·突袭。", Keywords = new string[] { "成长", "进化", "飞行", "突袭" } },
            new V06Card { Exp = true, Id = "B25", Name = "织网巨蛛", Category = V06Category.Unit, Cost = -1, Atk = 2, Hp = 5, Tags = "兽群", Text = "由岩隙幼蛛进化。终极形态：成长满时永久+1/+1。潜行·远射。", Keywords = new string[] { "成长", "进化", "潜行", "远射" } },
            new V06Card { Exp = true, Id = "B26", Name = "霜角巨鹿", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 6, Tags = "兽群", Text = "由冰角幼鹿进化。终极形态：成长满时永久+1/+1。再生·坚壁。", Keywords = new string[] { "成长", "进化", "再生", "坚壁" } },
            new V06Card { Exp = true, Id = "B27", Name = "铁背灰獾", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 4, Tags = "兽群", Text = "由荒原灰獾进化。终极形态：成长满时永久+1/+1。突袭·护卫。", Keywords = new string[] { "成长", "进化", "突袭", "护卫" } },
            new V06Card { Exp = true, Id = "B28", Name = "骨甲巨蝎", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 4, Tags = "兽群", Text = "由白骨幼蝎进化。终极形态：成长满时永久+1/+1。穿透·潜行。", Keywords = new string[] { "成长", "进化", "穿透", "潜行" } },

            // ── 三阶兽（不可部署）：成长满时永久 +1/+1 ──
            new V06Card { Exp = true, Id = "B29", Name = "霜牙狼王", Category = V06Category.Unit, Cost = -1, Atk = 6, Hp = 6, Tags = "兽群", Text = "由岩背荒狼进化。终极形态：成长满时永久+1/+1。突袭·协攻。", Keywords = new string[] { "成长", "进化", "突袭", "协攻" } },
            new V06Card { Exp = true, Id = "B30", Name = "裂山古熊", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 9, Tags = "兽群", Text = "由铁爪巨熊进化。终极形态：成长满时永久+1/+1。护卫·坚壁。", Keywords = new string[] { "成长", "进化", "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "B31", Name = "风暴鹰后", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 5, Tags = "兽群", Text = "由铁羽猎鹰进化。终极形态：成长满时永久+1/+1。飞行·远射。", Keywords = new string[] { "成长", "进化", "飞行", "远射" } },
            new V06Card { Exp = true, Id = "B32", Name = "双首蛇神", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 6, Tags = "兽群", Text = "由毒牙巨蟒进化。终极形态：成长满时永久+1/+1。潜行·汲取。", Keywords = new string[] { "成长", "进化", "潜行", "汲取" } },
            new V06Card { Exp = true, Id = "B33", Name = "熔岩甲虫王", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 8, Tags = "兽群", Text = "由攻城甲虫进化。终极形态：成长满时永久+1/+1。护卫·坚壁·余烬。", Keywords = new string[] { "成长", "进化", "护卫", "坚壁", "余烬" } },
            new V06Card { Exp = true, Id = "B34", Name = "夜魇领主豹", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 6, Tags = "兽群", Text = "由影刃剑齿虎进化。终极形态：成长满时永久+1/+1。潜行·穿透·夜行。", Keywords = new string[] { "成长", "进化", "潜行", "穿透", "夜行" } },
            new V06Card { Exp = true, Id = "B35", Name = "火山古龙兽", Category = V06Category.Unit, Cost = -1, Atk = 6, Hp = 8, Tags = "兽群", Text = "由熔火蜥王进化。终极形态：成长满时永久+1/+1。余烬·坚壁。", Keywords = new string[] { "成长", "进化", "余烬", "坚壁" } },
            new V06Card { Exp = true, Id = "B36", Name = "深渊巨章", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 7, Tags = "兽群", Text = "由深海触手进化。终极形态：成长满时永久+1/+1。远射·汲取。", Keywords = new string[] { "成长", "进化", "远射", "汲取" } },

            // ── 融合体（不可部署）：只能由两只指定兽融合 ──
            new V06Card { Exp = true, Id = "B37", Name = "穹翼狼王", Category = V06Category.Unit, Cost = -1, Atk = 6, Hp = 7, Tags = "兽群", Text = "融合体（+1/+1）：荒狼系 + 猎鹰系（任意形态）。飞行·突袭·协攻。", Keywords = new string[] { "融合", "飞行", "突袭", "协攻" } },
            new V06Card { Exp = true, Id = "B38", Name = "熔甲攻城兽", Category = V06Category.Unit, Cost = -1, Atk = 6, Hp = 9, Tags = "兽群", Text = "融合体（+1/+1）：甲虫系 + 熔蜥系（任意形态）。护卫·坚壁·余烬。", Keywords = new string[] { "融合", "护卫", "坚壁", "余烬" } },
            new V06Card { Exp = true, Id = "B39", Name = "双首蛇豹", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 6, Tags = "兽群", Text = "融合体（+1/+1）：巨蟒系 + 剑齿系（任意形态）。潜行·汲取·穿透。", Keywords = new string[] { "融合", "潜行", "汲取", "穿透" } },
            new V06Card { Exp = true, Id = "B40", Name = "深渊重蹄", Category = V06Category.Unit, Cost = -1, Atk = 5, Hp = 10, Tags = "兽群", Text = "融合体（+1/+1）：触手系 + 重蹄系（任意形态）。护卫·远射。", Keywords = new string[] { "融合", "护卫", "远射" } },
            new V06Card { Exp = true, Id = "B41", Name = "双生狼蛛", Category = V06Category.Unit, Cost = -1, Atk = 3, Hp = 4, Tags = "兽群", Text = "融合体（+1/+1）：荒狼系 + 岩蛛系（任意形态）。突袭·潜行。", Keywords = new string[] { "融合", "突袭", "潜行" } },
            new V06Card { Exp = true, Id = "B42", Name = "熔岩雏鹰", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 4, Tags = "兽群", Text = "融合体（+1/+1）：猎鹰系 + 熔蜥系（任意形态）。飞行·余烬。", Keywords = new string[] { "融合", "飞行", "余烬" } },
            new V06Card { Exp = true, Id = "B43", Name = "霜角毒蝰", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 5, Tags = "兽群", Text = "融合体（+1/+1）：冰鹿系 + 巨蟒系（任意形态）。再生·汲取。", Keywords = new string[] { "融合", "再生", "汲取" } },
            new V06Card { Exp = true, Id = "B44", Name = "骨刺灰獾", Category = V06Category.Unit, Cost = -1, Atk = 4, Hp = 5, Tags = "兽群", Text = "融合体（+1/+1）：灰獾系 + 骨蝎系（任意形态）。突袭·穿透。", Keywords = new string[] { "融合", "突袭", "穿透" } },

            // ── 少女向单位（全部可部署）：沿用现有命名体系，只写「单位名称」，不写人名。
            // Tags 沿用现有写法：种族在前、少女组标记在后（HasTag 种族判定照常生效）。
            new V06Card { Exp = true, Id = "G01", Name = "风纪剑姬", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "人类·少女", Text = "突袭；入场：对一个敌前排单位造成1点效果伤害。", Keywords = new string[] { "突袭" } },
            new V06Card { Exp = true, Id = "G02", Name = "夜巡枪手", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "血族·少女", Text = "远射；入场：对一个敌单位造成1点效果伤害。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "G03", Name = "神社巫女", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "精灵·少女", Text = "入场：可治疗另一个己方单位2。", Keywords = new string[] { "少女" } },
            new V06Card { Exp = true, Id = "G04", Name = "偶像应援", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "人类·少女", Text = "应援：己方其他单位攻击+1（不叠加）。入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "应援" } },
            new V06Card { Exp = true, Id = "G05", Name = "街头格斗少女", Category = V06Category.Unit, Cost = 3, Atk = 4, Hp = 3, Tags = "人类·少女", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Exp = true, Id = "G06", Name = "忍装少女", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Exp = true, Id = "G07", Name = "工坊机械师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "矮人·少女", Text = "维修；入场：若有空格，生成T08。", Keywords = new string[] { "维修" } },
            new V06Card { Exp = true, Id = "G08", Name = "观星术士", Category = V06Category.Unit, Cost = 3, Atk = 1, Hp = 4, Tags = "精灵·少女", Text = "远射；入场：看己方牌库顶2张，可交换顺序；不抽牌。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "G09", Name = "剑道少女", Category = V06Category.Unit, Cost = 4, Atk = 5, Hp = 5, Tags = "人类·少女", Text = "夜行。", Keywords = new string[] { "夜行" } },
            new V06Card { Exp = true, Id = "G10", Name = "祭典舞姬", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "精灵·少女", Text = "应援：己方其他单位攻击+1（不叠加）。入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "应援" } },
            new V06Card { Exp = true, Id = "G11", Name = "摔角少女", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "兽人·少女", Text = "护卫；入场：可治疗另一个己方单位2。", Keywords = new string[] { "护卫" } },
            new V06Card { Exp = true, Id = "G12", Name = "弓道少女", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "人类·少女", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "G13", Name = "水泳部少女", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "人类·少女", Text = "突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Exp = true, Id = "G14", Name = "随军医女", Category = V06Category.Unit, Cost = 3, Atk = 1, Hp = 5, Tags = "人类·少女", Text = "回响；入场：可治疗另一个己方单位2。", Keywords = new string[] { "回响" } },
            new V06Card { Exp = true, Id = "G15", Name = "重装少女", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 7, Tags = "矮人·少女", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "G16", Name = "冰术少女", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "精灵·少女", Text = "远射；入场：对一个敌前排单位造成1点效果伤害。", Keywords = new string[] { "远射" } },
        };

        public static int BeastCount { get { return 44; } }
        public static int GirlCount { get { return 16; } }
    }

    /// <summary>成长/进化/融合的规则数据与判定。数值与卡面文字一一对应（断言核对）。</summary>
    public static class V06Expansion
    {
        /// <summary>成长阈值 N；0 表示不成长。</summary>
        public static int GrowThreshold(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) { return 0; }
            switch (cardId)
            {
                // 一阶（2）
                case "B01": case "B02": case "B03": case "B04": case "B05": case "B06": case "B07": case "B08":
                case "B11": case "B12": case "B13": case "B14":
                    return 2;
                // 一阶（3）：身材高于 1 费/2 费的那两只
                case "B09": case "B10":
                    return 3;
                // 二阶（3）
                case "B15": case "B16": case "B17": case "B18": case "B19": case "B20": case "B21": case "B22":
                // 终极形态（3）：满成长时永久 +1/+1
                case "B23": case "B24": case "B25": case "B26": case "B27": case "B28":
                case "B29": case "B30": case "B31": case "B32": case "B33": case "B34": case "B35": case "B36":
                    return 3;
            }
            return 0;   // 融合体 B37–B44 不成长
        }

        /// <summary>进化目标；null 表示终态（满成长改给永久 +1/+1）。</summary>
        public static string EvolutionOf(string cardId)
        {
            switch (cardId)
            {
                case "B01": return "B15"; case "B02": return "B16"; case "B03": return "B17"; case "B04": return "B18";
                case "B05": return "B19"; case "B06": return "B20"; case "B07": return "B21"; case "B08": return "B22";
                case "B09": return "B23"; case "B10": return "B24"; case "B11": return "B25"; case "B12": return "B26";
                case "B13": return "B27"; case "B14": return "B28";
                case "B15": return "B29"; case "B16": return "B30"; case "B17": return "B31"; case "B18": return "B32";
                case "B19": return "B33"; case "B20": return "B34"; case "B21": return "B35"; case "B22": return "B36";
            }
            return null;
        }

        /// <summary>是否为「只能靠进化/融合得到」的单位（Cost = -1，不可部署、不进牌库）。</summary>
        public static bool IsEvolveOnly(string cardId)
        {
            V06Card c = V06Catalog.ById(cardId);
            if (c == null || c.Category != V06Category.Unit) { return false; }
            return c.Cost < 0;
        }

        /// <summary>
        /// 进化线编号（1–14）。同一只兽的任何形态都算同一条线，
        /// 融合按"线"匹配——否则一阶兽一进化就再也凑不成融合对（实测 40 局只有 6 局出现融合机会）。
        /// 返回 0 表示不属于兽群进化线（融合体、少女）。
        /// </summary>
        public static int LineOf(string cardId)
        {
            switch (cardId)
            {
                case "B01": case "B15": case "B29": return 1;
                case "B02": case "B16": case "B30": return 2;
                case "B03": case "B17": case "B31": return 3;
                case "B04": case "B18": case "B32": return 4;
                case "B05": case "B19": case "B33": return 5;
                case "B06": case "B20": case "B34": return 6;
                case "B07": case "B21": case "B35": return 7;
                case "B08": case "B22": case "B36": return 8;
                case "B09": case "B23": return 9;
                case "B10": case "B24": return 10;
                case "B11": case "B25": return 11;
                case "B12": case "B26": return 12;
                case "B13": case "B27": return 13;
                case "B14": case "B28": return 14;
            }
            return 0;
        }

        /// <summary>某条线的代表卡（一阶），用于表格与提示显示。</summary>
        public static string LineRep(int line)
        {
            if (line < 1 || line > 14) { return null; }
            return "B" + (line < 10 ? "0" + line : "" + line);
        }

        /// <summary>系名（卡面文字与提示用）。</summary>
        public static string LineName(int line)
        {
            switch (line)
            {
                case 1: return "荒狼系";
                case 2: return "巨熊系";
                case 3: return "猎鹰系";
                case 4: return "巨蟒系";
                case 5: return "甲虫系";
                case 6: return "剑齿系";
                case 7: return "熔蜥系";
                case 8: return "触手系";
                case 9: return "重蹄系";
                case 10: return "翼兽系";
                case 11: return "岩蛛系";
                case 12: return "冰鹿系";
                case 13: return "灰獾系";
                case 14: return "骨蝎系";
            }
            return "?";
        }

        // 融合表：{ 线A, 线B, 结果编号 }，任意形态都成立
        private static readonly int[,] FusionLines = new int[,]
        {
            { 1, 3, 37 },    // 荒狼 + 猎鹰 → B37 穹翼狼王
            { 5, 7, 38 },    // 甲虫 + 熔蜥 → B38 熔甲攻城兽
            { 4, 6, 39 },    // 巨蟒 + 剑齿 → B39 双首蛇豹
            { 8, 9, 40 },    // 触手 + 重蹄 → B40 深渊重蹄
            { 1, 11, 41 },   // 荒狼 + 岩蛛 → B41 双生狼蛛
            { 3, 7, 42 },    // 猎鹰 + 熔蜥 → B42 熔岩雏鹰
            { 12, 4, 43 },   // 冰鹿 + 巨蟒 → B43 霜角毒蝰
            { 13, 14, 44 },  // 灰獾 + 骨蝎 → B44 骨刺灰獾
        };

        /// <summary>两只兽的融合结果（按进化线匹配、无序）；不构成融合对时返回 null。</summary>
        public static string FusionResult(string a, string b)
        {
            int la = LineOf(a);
            int lb = LineOf(b);
            if (la == 0 || lb == 0 || la == lb) { return null; }
            for (int i = 0; i < FusionLines.GetLength(0); i++)
            {
                int x = FusionLines[i, 0];
                int y = FusionLines[i, 1];
                if ((la == x && lb == y) || (la == y && lb == x))
                {
                    int rid = FusionLines[i, 2];
                    return "B" + (rid < 10 ? "0" + rid : "" + rid);
                }
            }
            return null;
        }

        /// <summary>某张卡参与的融合组合（系名形式，供界面提示用）。</summary>
        public static List<string> FusionPartners(string cardId)
        {
            List<string> r = new List<string>();
            int line = LineOf(cardId);
            if (line == 0) { return r; }
            for (int i = 0; i < FusionLines.GetLength(0); i++)
            {
                int other;
                if (FusionLines[i, 0] == line) { other = FusionLines[i, 1]; }
                else if (FusionLines[i, 1] == line) { other = FusionLines[i, 0]; }
                else { continue; }
                int rid = FusionLines[i, 2];
                string res = "B" + (rid < 10 ? "0" + rid : "" + rid);
                r.Add(LineName(other) + "→" + res);
            }
            return r;
        }

        public static int FusionPairCount { get { return FusionLines.GetLength(0); } }

        /// <summary>读融合表：col 0/1 返回该线的一阶代表卡编号，col 2 返回结果编号。</summary>
        public static string FusionPairAt(int i, int col)
        {
            if (col < 2) { return LineRep(FusionLines[i, col]); }
            int rid = FusionLines[i, 2];
            return "B" + (rid < 10 ? "0" + rid : "" + rid);
        }

        /// <summary>读融合表：线编号（只接受 col 0/1）。</summary>
        public static int FusionLineAt(int i, int col) { return FusionLines[i, col]; }
    }
}
