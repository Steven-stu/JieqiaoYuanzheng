using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// V0.7 第三批卡池：N61–N140（80 张，全部为可部署单位）。
    ///
    /// 与 V06Catalog 的关系：与 V06ExpansionCards 完全相同——`V06Catalog` 的静态构造函数
    /// 会把本文件的 `All` 并进 `V06Catalog.All`，因此 ById / 计数 / 图鉴 / 组卡池 / 核对断言
    /// 全部自动跟随，无需改任何调用点。
    ///
    /// 设计稿：`界桥远征_文档/界桥远征_第三批卡池_N61-N140_设计.md`
    /// 美术：`Assets/Resources/Art/units/<卡号>.png`（文件名即卡号，UI 按卡号自动取图）。
    ///
    /// 三条硬约定（与设计稿 §0 一致，改卡时不要破）：
    ///   ① 只用现有 21 个关键词，不新增关键词（术语表不需要跟着改）；
    ///   ② 入场效果只允许用 V06Effects.EnterSpec 里已实现的 9 种写法，且卡号必须在该表登记；
    ///   ③ 身材不得超过同费既有基准（设计稿 §1），不做「同费且攻血不低、效果更强」的严格压制。
    /// </summary>
    public static class V06CardsN
    {
        public static readonly List<V06Card> All = new List<V06Card>
        {
            // ───────────────── 组一之一：N61–N70 精灵/人类前排打击与法系 ─────────────────
            new V06Card { Exp = true, Id = "N61", Name = "绯樱太刀使", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "精灵·少女", Text = "突袭；入场：对一个敌前排单位造成1点效果伤害。", Keywords = new string[] { "突袭" } },
            new V06Card { Exp = true, Id = "N62", Name = "苍海航术师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "人类·少女", Text = "远射；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N63", Name = "白金圣枪姬", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "人类·少女", Text = "护卫；入场：可治疗另一个己方单位2。", Keywords = new string[] { "护卫" } },
            new V06Card { Exp = true, Id = "N64", Name = "墨羽夜行者", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "血族·少女", Text = "潜行；入场：对一个敌单位造成1点效果伤害。", Keywords = new string[] { "潜行" } },
            new V06Card { Exp = true, Id = "N65", Name = "琉璃星术师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "远射；入场：看己方牌库顶2张，可交换顺序；不抽牌。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N66", Name = "赤砂拳斗姬", Category = V06Category.Unit, Cost = 2, Atk = 3, Hp = 2, Tags = "兽人·少女", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Exp = true, Id = "N67", Name = "紫藤毒药师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "远射；汲取。", Keywords = new string[] { "远射", "汲取" } },
            new V06Card { Exp = true, Id = "N68", Name = "银杏弩骑士", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "人类·少女", Text = "远射；坚壁。", Keywords = new string[] { "远射", "坚壁" } },
            new V06Card { Exp = true, Id = "N69", Name = "青瓷结界师", Category = V06Category.Unit, Cost = 3, Atk = 1, Hp = 5, Tags = "精灵·少女", Text = "护卫；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "护卫" } },
            new V06Card { Exp = true, Id = "N70", Name = "暮金竖琴师", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "精灵·少女", Text = "应援：己方其他单位主动攻击时攻击+1（不叠加）。入场：可治疗另一个己方单位2。", Keywords = new string[] { "应援" } },

            // ───────────────── 组一之二：N71–N80 法系与支援补全 ─────────────────
            new V06Card { Exp = true, Id = "N71", Name = "冰莓魔导师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "精灵·少女", Text = "远射；入场：对一个敌前排单位造成1点效果伤害。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N72", Name = "黑曜审判姬", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "人类·少女", Text = "坚壁；入场：对一个敌单位造成1点效果伤害。", Keywords = new string[] { "坚壁" } },
            new V06Card { Exp = true, Id = "N73", Name = "珊瑚潮汐使", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "精灵·少女", Text = "再生；入场：可治疗另一个己方单位2。", Keywords = new string[] { "再生" } },
            new V06Card { Exp = true, Id = "N74", Name = "金铃狐巫", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "兽裔·少女", Text = "远射；入场：若有空格，生成荒原奇美拉。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N75", Name = "霜银豹猎手", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "兽裔·少女", Text = "突袭；潜行。", Keywords = new string[] { "突袭", "潜行" } },
            new V06Card { Exp = true, Id = "N76", Name = "绛月龙枪姬", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "龙裔·少女", Text = "龙威；突袭。", Keywords = new string[] { "龙威", "突袭" } },
            new V06Card { Exp = true, Id = "N77", Name = "翠羽风信使", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "精灵·少女", Text = "回响；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "回响" } },
            new V06Card { Exp = true, Id = "N78", Name = "琥珀遗物师", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "人类·少女", Text = "坚壁；入场：对一个敌单位造成1点效果伤害。", Keywords = new string[] { "坚壁" } },
            new V06Card { Exp = true, Id = "N79", Name = "蓝焰机巧师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "矮人·少女", Text = "维修；远射。", Keywords = new string[] { "维修", "远射" } },
            new V06Card { Exp = true, Id = "N80", Name = "白蔷薇领航员", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "人类·少女", Text = "协攻；护卫。", Keywords = new string[] { "协攻", "护卫" } },

            // ───────────────── 组一之三：N101–N110 剑与盾第二波 ─────────────────
            new V06Card { Exp = true, Id = "N101", Name = "青焰灯术师", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N102", Name = "绯羽猎鹰使", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "人类·少女", Text = "远射；入场：若有空格，生成霜翼雏龙。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N103", Name = "银砂锁链卫", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "人类·少女", Text = "坚壁；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "坚壁" } },
            new V06Card { Exp = true, Id = "N104", Name = "紫苑符剑师", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "精灵·少女", Text = "穿透。", Keywords = new string[] { "穿透" } },
            new V06Card { Exp = true, Id = "N105", Name = "苍玉冰斧姬", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "矮人·少女", Text = "坚壁；穿透。", Keywords = new string[] { "坚壁", "穿透" } },
            new V06Card { Exp = true, Id = "N106", Name = "金蕊花药师", Category = V06Category.Unit, Cost = 3, Atk = 1, Hp = 5, Tags = "精灵·少女", Text = "再生；入场：可治疗另一个己方单位2。", Keywords = new string[] { "再生" } },
            new V06Card { Exp = true, Id = "N107", Name = "夜珀猫剑客", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "兽裔·少女", Text = "潜行；夜行。", Keywords = new string[] { "潜行", "夜行" } },
            new V06Card { Exp = true, Id = "N108", Name = "朱砂遗迹学者", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "人类·少女", Text = "入场：看己方牌库顶2张，可交换顺序；不抽牌。", Keywords = new string[0] },
            new V06Card { Exp = true, Id = "N109", Name = "月蓝星轮使", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "远射；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N110", Name = "白荆圣盾姬", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "人类·少女", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },

            // ───────────────── 组一之四：N121–N130 速度与消耗 ─────────────────
            new V06Card { Exp = true, Id = "N121", Name = "绀蝶时计师", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "精灵·少女", Text = "远射；汲取。", Keywords = new string[] { "远射", "汲取" } },
            new V06Card { Exp = true, Id = "N122", Name = "丹枫扇舞姬", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "精灵·少女", Text = "协攻；入场：一个己方单位本轮攻击+1。", Keywords = new string[] { "协攻" } },
            new V06Card { Exp = true, Id = "N123", Name = "银鸢弩哨", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "人类·少女", Text = "远射；潜行。", Keywords = new string[] { "远射", "潜行" } },
            new V06Card { Exp = true, Id = "N124", Name = "碧潮珍珠使", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "精灵·少女", Text = "远射；入场：可治疗另一个己方单位2。", Keywords = new string[] { "远射" } },
            new V06Card { Exp = true, Id = "N125", Name = "赤铜铸剑师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "矮人·少女", Text = "维修。", Keywords = new string[] { "维修" } },
            new V06Card { Exp = true, Id = "N126", Name = "黛梅夜巡官", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "血族·少女", Text = "夜行；潜行。", Keywords = new string[] { "夜行", "潜行" } },
            new V06Card { Exp = true, Id = "N127", Name = "雾金鹿灵使", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "精灵·少女", Text = "再生；入场：若有空格，生成灵鹿牧者。", Keywords = new string[] { "再生" } },
            new V06Card { Exp = true, Id = "N128", Name = "紫晶龙弩姬", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "龙裔·少女", Text = "远射；龙威。", Keywords = new string[] { "远射", "龙威" } },
            new V06Card { Exp = true, Id = "N129", Name = "白曜旗枪卫", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "人类·少女", Text = "护卫；突袭。", Keywords = new string[] { "护卫", "突袭" } },
            new V06Card { Exp = true, Id = "N130", Name = "蓝鸦巡星师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "精灵·少女", Text = "远射；入场：看己方牌库顶2张，可交换顺序；不抽牌。", Keywords = new string[] { "远射" } },

            // ───────────────── 组二：N81–N91 巨兽与龙（前排支柱） ─────────────────
            new V06Card { Exp = true, Id = "N81", Name = "雷脊巨狼", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "兽群", Text = "突袭；协攻。", Keywords = new string[] { "突袭", "协攻" } },
            new V06Card { Exp = true, Id = "N82", Name = "赤岩甲龟", Category = V06Category.Unit, Cost = 4, Atk = 2, Hp = 7, Tags = "兽群", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "N83", Name = "幽蓝双头蛇", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "兽群", Text = "汲取；潜行。", Keywords = new string[] { "汲取", "潜行" } },
            new V06Card { Exp = true, Id = "N84", Name = "金鬃雷狮", Category = V06Category.Unit, Cost = 4, Atk = 5, Hp = 5, Tags = "兽群", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Exp = true, Id = "N85", Name = "深渊铁颚鲨", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "兽群", Text = "穿透；汲取。", Keywords = new string[] { "穿透", "汲取" } },
            new V06Card { Exp = true, Id = "N86", Name = "紫晶巨蝎", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "兽群", Text = "穿透；坚壁。", Keywords = new string[] { "穿透", "坚壁" } },
            new V06Card { Exp = true, Id = "N87", Name = "古木鹿王", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "树灵", Text = "再生；入场：可治疗另一个己方单位2。", Keywords = new string[] { "再生" } },
            new V06Card { Exp = true, Id = "N88", Name = "黑焰翼狮", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 4, Tags = "兽群", Text = "飞行；突袭。", Keywords = new string[] { "飞行", "突袭" } },
            new V06Card { Exp = true, Id = "N89", Name = "银霜长颈古龙", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "龙族", Text = "龙威；坚壁。", Keywords = new string[] { "龙威", "坚壁" } },
            new V06Card { Exp = true, Id = "N90", Name = "赤沙骨翼龙", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "龙类野兽", Text = "飞行；余烬。", Keywords = new string[] { "飞行", "余烬" } },
            new V06Card { Exp = true, Id = "N91", Name = "苍穹雷冠龙", Category = V06Category.Unit, Cost = 4, Atk = 5, Hp = 4, Tags = "龙族", Text = "龙威；突袭。", Keywords = new string[] { "龙威", "突袭" } },

            // ───────────────── 组三：N92–N94 构装 ─────────────────
            new V06Card { Exp = true, Id = "N92", Name = "遗迹六足巡机", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "构装", Text = "维修；远射。", Keywords = new string[] { "维修", "远射" } },
            new V06Card { Exp = true, Id = "N93", Name = "月白浮空守机", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "构装", Text = "坚壁；护卫。", Keywords = new string[] { "坚壁", "护卫" } },
            new V06Card { Exp = true, Id = "N94", Name = "黑金攻城机甲", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "构装", Text = "过载；坚壁。", Keywords = new string[] { "过载", "坚壁" } },

            // ───────────────── 组四之一：N95–N100 小兽与灵（低费填充） ─────────────────
            new V06Card { Exp = true, Id = "N95", Name = "蜜桃团雀", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 1, Tags = "兽群", Text = "飞行；回响。", Keywords = new string[] { "飞行", "回响" } },
            new V06Card { Exp = true, Id = "N96", Name = "薄荷小獭", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "兽群", Text = "再生。", Keywords = new string[] { "再生" } },
            new V06Card { Exp = true, Id = "N97", Name = "星铃幼鹿", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "兽群", Text = "坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Exp = true, Id = "N98", Name = "雪团角兔", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 1, Tags = "兽群", Text = "潜行；突袭。", Keywords = new string[] { "潜行", "突袭" } },
            new V06Card { Exp = true, Id = "N99", Name = "琥珀小蜥", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "兽群", Text = "突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Exp = true, Id = "N100", Name = "云芽花灵", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "灵体", Text = "回响；入场：可治疗另一个己方单位2。", Keywords = new string[] { "回响" } },

            // ───────────────── 组二续：N111–N115 巨兽 ─────────────────
            new V06Card { Exp = true, Id = "N111", Name = "熔角巨野猪", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "兽群", Text = "突袭；过载。", Keywords = new string[] { "突袭", "过载" } },
            new V06Card { Exp = true, Id = "N112", Name = "苍苔铁背熊", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "兽群", Text = "护卫；再生。", Keywords = new string[] { "护卫", "再生" } },
            new V06Card { Exp = true, Id = "N113", Name = "雷晶巨螳", Category = V06Category.Unit, Cost = 3, Atk = 4, Hp = 3, Tags = "兽群", Text = "突袭；穿透。", Keywords = new string[] { "突袭", "穿透" } },
            new V06Card { Exp = true, Id = "N114", Name = "暮羽狮鹫王", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "龙类野兽", Text = "飞行；协攻。", Keywords = new string[] { "飞行", "协攻" } },
            new V06Card { Exp = true, Id = "N115", Name = "玄金环翼龙", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "龙族", Text = "龙威；飞行。", Keywords = new string[] { "龙威", "飞行" } },

            // ───────────────── 组三续：N116 构装 ─────────────────
            new V06Card { Exp = true, Id = "N116", Name = "远古棱盾机", Category = V06Category.Unit, Cost = 3, Atk = 1, Hp = 6, Tags = "构装", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },

            // ───────────────── 组四续：N117–N120 小兽与灵 ─────────────────
            new V06Card { Exp = true, Id = "N117", Name = "蜜铃小熊", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "兽群", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Exp = true, Id = "N118", Name = "青芽团龟", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "兽群", Text = "再生；护卫。", Keywords = new string[] { "再生", "护卫" } },
            new V06Card { Exp = true, Id = "N119", Name = "星绒飞鼠", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 1, Tags = "兽群", Text = "飞行；夜行。", Keywords = new string[] { "飞行", "夜行" } },
            new V06Card { Exp = true, Id = "N120", Name = "月泡水母灵", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "灵体", Text = "远射；汲取。", Keywords = new string[] { "远射", "汲取" } },

            // ───────────────── 组二续：N131–N134 巨兽与龙 ─────────────────
            new V06Card { Exp = true, Id = "N131", Name = "霜牙剑齿虎", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "兽群", Text = "潜行；突袭。", Keywords = new string[] { "潜行", "突袭" } },
            new V06Card { Exp = true, Id = "N132", Name = "赤晶地穴蜈", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "兽群", Text = "潜行；坚壁。", Keywords = new string[] { "潜行", "坚壁" } },
            new V06Card { Exp = true, Id = "N133", Name = "苍岩独角巨猿", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "兽群", Text = "坚壁；入场：对一个敌单位造成1点效果伤害。", Keywords = new string[] { "坚壁" } },
            new V06Card { Exp = true, Id = "N134", Name = "暮紫幽翼龙", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "龙族", Text = "飞行；龙威。", Keywords = new string[] { "飞行", "龙威" } },

            // ───────────────── 组三续：N135 构装 ─────────────────
            new V06Card { Exp = true, Id = "N135", Name = "远古日晷战机", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "构装", Text = "远射；入场：若有空格，生成守备无人机。", Keywords = new string[] { "远射" } },

            // ───────────────── 组二续：N136 巨兽 ─────────────────
            new V06Card { Exp = true, Id = "N136", Name = "深蓝重装蟹", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 7, Tags = "兽群", Text = "坚壁；护卫。", Keywords = new string[] { "坚壁", "护卫" } },

            // ───────────────── 组四续：N137–N140 小兽与灵 ─────────────────
            new V06Card { Exp = true, Id = "N137", Name = "蜜杏绒刺球", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "兽群", Text = "坚壁；余烬。", Keywords = new string[] { "坚壁", "余烬" } },
            new V06Card { Exp = true, Id = "N138", Name = "青铃小山羊", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "兽群", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Exp = true, Id = "N139", Name = "星帽小章鱼", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "兽群", Text = "远射；再生。", Keywords = new string[] { "远射", "再生" } },
            new V06Card { Exp = true, Id = "N140", Name = "月绒蝴蝶灵", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "灵体", Text = "飞行；回响。", Keywords = new string[] { "飞行", "回响" } },
        };
    }
}
