// 由 scratch/v06/03_战斗规则与卡池_V0.6.md 脚本生成，勿手改。
// 生成时间：2026-11-03 22:20:40
using System.Collections.Generic;

namespace Jieqiao
{
    public enum V06Category { Unit, Tactic, Equipment, Seal, Status, Response, Upgrade, Support, Token, Weather, Encounter, Event }

    /// <summary>V0.6 卡池条目。数值为设计提案，尚未经过平衡验证。</summary>
    public sealed class V06Card
    {
        public string Id;
        public string Name;
        public V06Category Category;
        public int Cost = -1;
        public int Atk;
        public int Hp;
        public string Tags;
        public string Text;
        public string[] Keywords;
        /// <summary>是否为 V0.7 扩展卡（兽群 B* / 少女 G*），用于把"基础卡池"与"扩展"分开核对。</summary>
        public bool Exp;
        public override string ToString() { return Id + " " + Name; }
    }

    /// <summary>V0.6 八套入门预组：每套 24 主牌 = 单位 8 种各 2 + 战术 4 种各 2。</summary>
    public sealed class V06Precon
    {
        public string Name;
        public string Vanguard;
        public string[] Units;
        public string[] Tactics;
        public string[] Upgrades;
        public string[] Supports;
        /// <summary>额外卡（装备/附印/战况/响应），各 1 张；官方预组为空。</summary>
        public string[] Extras;
        /// <summary>是否为"测试预组"（只用于自测，不进正常开局列表）。</summary>
        public bool IsTestDeck;
        public int MainCount
        {
            get
            {
                int n = Units.Length * 2 + Tactics.Length * 2;
                if (Extras != null) { n += Extras.Length; }
                return n;
            }
        }
    }

    public static class V06Catalog
    {
        /// <summary>
        /// V0.7 扩展卡池（兽群 44 + 少女 16）由 V06ExpansionCards 提供，
        /// 在这里并进 All，保证 ById / 计数 / 断言无需改动。
        /// 静态构造函数保证任何静态成员访问之前一定已经合并完成。
        /// </summary>
        static V06Catalog()
        {
            All.AddRange(V06ExpansionCards.All);
        }

        public static readonly string[] KeywordList = new string[]
        {
            "护卫", "远射", "坚壁", "穿透", "突袭", "回响", "再生", "汲取", "余烬", "夜行", "协攻", "维修", "过载", "龙威", "潜行", "飞行",
            // V0.7 扩展：兽群的成长/进化/融合 + 少女的应援光环
            "成长", "进化", "融合", "应援"
        };

        public static readonly List<V06Card> All = new List<V06Card>
        {
            new V06Card { Id = "U-H01", Name = "雁回剑士", Category = V06Category.Unit, Cost = 2, Atk = 3, Hp = 3, Tags = "人类", Text = "无。", Keywords = new string[0] },
            new V06Card { Id = "U-H02", Name = "公会盾卫", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 4, Tags = "人类", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-H03", Name = "城墙弩手", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "人类", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "U-H04", Name = "旅途药师", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "人类", Text = "入场：可治疗另一个己方单位2。", Keywords = new string[0] },
            new V06Card { Id = "U-H05", Name = "先遣女骑士", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "人类", Text = "突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "U-H06", Name = "灯塔法师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "人类", Text = "入场：对一个敌单位造成1点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "U-H07", Name = "边境老兵", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "人类", Text = "坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "U-H08", Name = "公会信使", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "人类", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-E01", Name = "白枝游侠", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "精灵", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "U-E02", Name = "林地守望者", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 4, Tags = "精灵", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-E03", Name = "露泉祭司", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "精灵", Text = "入场：可治疗另一个己方单位2。", Keywords = new string[0] },
            new V06Card { Id = "U-E04", Name = "树影斥候", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "精灵", Text = "潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "U-E05", Name = "藤甲树卫", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "树灵", Text = "护卫；再生。", Keywords = new string[] { "护卫", "再生" } },
            new V06Card { Id = "U-E06", Name = "萤火精灵", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "灵体", Text = "回响；入场：若有空格，生成T02。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-E07", Name = "古林牧鹿人", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "精灵", Text = "入场：若有空格，生成T01。", Keywords = new string[0] },
            new V06Card { Id = "U-E08", Name = "冠木长老", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 7, Tags = "树灵", Text = "护卫；入场：可治疗另一个己方单位2。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-D01", Name = "山城斧兵", Category = V06Category.Unit, Cost = 2, Atk = 3, Hp = 3, Tags = "矮人", Text = "无。", Keywords = new string[0] },
            new V06Card { Id = "U-D02", Name = "铁誓盾兵", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "矮人", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Id = "U-D03", Name = "符文火枪手", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "矮人", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "U-D04", Name = "工坊助手", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 3, Tags = "矮人", Text = "维修。", Keywords = new string[] { "维修" } },
            new V06Card { Id = "U-D05", Name = "爆破工程师", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "矮人", Text = "入场：对一个敌前排单位造成1点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "U-D06", Name = "锤阵队长", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "矮人", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "U-D07", Name = "符钢傀儡", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "构装", Text = "坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "U-D08", Name = "深矿勘探师", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "矮人", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-G01", Name = "哥布林短刀手", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "哥布林", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "U-G02", Name = "哥布林投弹手", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "哥布林", Text = "余烬。", Keywords = new string[] { "余烬" } },
            new V06Card { Id = "U-G03", Name = "废料修理师", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "哥布林", Text = "维修。", Keywords = new string[] { "维修" } },
            new V06Card { Id = "U-G04", Name = "战帮号手", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "哥布林", Text = "入场：若有空格，生成T03。", Keywords = new string[0] },
            new V06Card { Id = "U-G05", Name = "山桥巨魔", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 7, Tags = "巨魔", Text = "再生。", Keywords = new string[] { "再生" } },
            new V06Card { Id = "U-G06", Name = "岩甲巨魔", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 6, Tags = "巨魔", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-G07", Name = "陷坑猎手", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "哥布林", Text = "潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "U-G08", Name = "商队谈判者", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "哥布林", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-V01", Name = "绯月侍从", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 3, Tags = "血族", Text = "夜行。", Keywords = new string[] { "夜行" } },
            new V06Card { Id = "U-V02", Name = "暮色剑士", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 4, Tags = "血族", Text = "汲取。", Keywords = new string[] { "汲取" } },
            new V06Card { Id = "U-V03", Name = "夜钟弩手", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "血族", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "U-V04", Name = "古堡管家", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "血族", Text = "入场：可治疗另一个己方单位2。", Keywords = new string[0] },
            new V06Card { Id = "U-V05", Name = "石像鬼门卫", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "构装", Text = "护卫；坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Id = "U-V06", Name = "红雾使者", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 2, Tags = "血族", Text = "潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "U-V07", Name = "夜宴决斗家", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "血族", Text = "夜行；汲取。", Keywords = new string[] { "汲取", "夜行" } },
            new V06Card { Id = "U-V08", Name = "守墓术士", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "血族", Text = "入场：若有空格，生成T04。", Keywords = new string[0] },
            new V06Card { Id = "U-O01", Name = "荒原斧手", Category = V06Category.Unit, Cost = 2, Atk = 3, Hp = 3, Tags = "兽人", Text = "协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "U-O02", Name = "红砂骑手", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "兽人", Text = "突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "U-O03", Name = "狼裔斥候", Category = V06Category.Unit, Cost = 1, Atk = 2, Hp = 1, Tags = "兽裔", Text = "潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "U-O04", Name = "部族盾卫", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "兽人", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-O05", Name = "狐裔寻路人", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "兽裔", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-O06", Name = "战歌萨满", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 4, Tags = "兽人", Text = "入场：一个己方单位本轮攻击+1。", Keywords = new string[0] },
            new V06Card { Id = "U-O07", Name = "猎巨兽勇士", Category = V06Category.Unit, Cost = 4, Atk = 5, Hp = 4, Tags = "兽人", Text = "穿透。", Keywords = new string[] { "穿透" } },
            new V06Card { Id = "U-O08", Name = "荒原驯兽师", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 4, Tags = "人类", Text = "入场：若有空格，生成T05。", Keywords = new string[0] },
            new V06Card { Id = "U-R01", Name = "龙巢侍卫", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 4, Tags = "人类", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-R02", Name = "风翼亚龙", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "龙类野兽", Text = "飞行。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "U-R03", Name = "焰尾地龙", Category = V06Category.Unit, Cost = 2, Atk = 3, Hp = 2, Tags = "龙类野兽", Text = "余烬。", Keywords = new string[] { "余烬" } },
            new V06Card { Id = "U-R04", Name = "龙语学者", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 2, Tags = "精灵", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-R05", Name = "鳞盾盟卫", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 5, Tags = "龙裔", Text = "坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "U-R06", Name = "雷冠龙使", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 5, Tags = "龙族", Text = "龙威。", Keywords = new string[] { "龙威" } },
            new V06Card { Id = "U-R07", Name = "霜翼龙卫", Category = V06Category.Unit, Cost = 4, Atk = 3, Hp = 6, Tags = "龙族", Text = "飞行。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "U-R08", Name = "拾鳞匠人", Category = V06Category.Unit, Cost = 2, Atk = 2, Hp = 3, Tags = "矮人", Text = "入场：若有空格，生成T07。", Keywords = new string[0] },
            new V06Card { Id = "U-M01", Name = "勘测步行机", Category = V06Category.Unit, Cost = 1, Atk = 1, Hp = 3, Tags = "构装", Text = "入场：看己方牌库顶2张，可交换顺序；不抽牌。", Keywords = new string[0] },
            new V06Card { Id = "U-M02", Name = "守门机兵", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 5, Tags = "构装", Text = "坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "U-M03", Name = "高台射线机", Category = V06Category.Unit, Cost = 3, Atk = 2, Hp = 3, Tags = "构装", Text = "远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "U-M04", Name = "维修工蜂", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "构装", Text = "维修；入场：若有空格，生成T08。", Keywords = new string[] { "维修" } },
            new V06Card { Id = "U-M05", Name = "折盾卫机", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 5, Tags = "构装", Text = "护卫；入场：若有空格，生成T06。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "U-M06", Name = "突进步行机", Category = V06Category.Unit, Cost = 3, Atk = 3, Hp = 3, Tags = "构装", Text = "突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "U-M07", Name = "档案保管机", Category = V06Category.Unit, Cost = 2, Atk = 1, Hp = 3, Tags = "构装", Text = "回响。", Keywords = new string[] { "回响" } },
            new V06Card { Id = "U-M08", Name = "古代重装机", Category = V06Category.Unit, Cost = 4, Atk = 4, Hp = 6, Tags = "构装", Text = "坚壁；入场：对自身造成1点效果伤害。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "S-H01", Name = "剑锋突进", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "一个己方单位本轮攻击+2。", Keywords = new string[0] },
            new V06Card { Id = "S-H02", Name = "战地包扎", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤单位3。", Keywords = new string[0] },
            new V06Card { Id = "S-H03", Name = "远征号令", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "抽2张。", Keywords = new string[0] },
            new V06Card { Id = "S-H04", Name = "盾阵推进", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "一个己方单位本轮获得护卫；然后可将其移入前排空格。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "S-E01", Name = "藤蔓缠足", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "一个敌单位本轮攻击−2，最低0。", Keywords = new string[0] },
            new V06Card { Id = "S-E02", Name = "林间齐射", Category = V06Category.Tactic, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "对一个敌单位造成4点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "S-E03", Name = "月露泉水", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤单位4。", Keywords = new string[0] },
            new V06Card { Id = "S-E04", Name = "树冠小径", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "移动一个己方单位到另一排空格；然后抽1张。", Keywords = new string[0] },
            new V06Card { Id = "S-D01", Name = "破甲重锤", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "对一个敌单位造成3点效果伤害；若其有坚壁，改为4点。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "S-D02", Name = "应急焊补", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤构装单位4，或治疗一个其他受伤己方单位2。", Keywords = new string[0] },
            new V06Card { Id = "S-D03", Name = "工坊备料", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "从己方牌库检索1张装备入手，再洗牌；无装备时不可使用。", Keywords = new string[0] },
            new V06Card { Id = "S-D04", Name = "稳固阵线", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "本轮己方前排单位受到的主动攻击伤害减1。", Keywords = new string[0] },
            new V06Card { Id = "S-G01", Name = "引爆火药桶", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "对敌前排每个单位造成2点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "S-G02", Name = "伏兵集结", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "按顺序在空格生成至多2个T03。", Keywords = new string[0] },
            new V06Card { Id = "S-G03", Name = "废料再用", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "从己方弃牌区选择1张构装单位或装备置于牌库底；然后抽1张。", Keywords = new string[0] },
            new V06Card { Id = "S-G04", Name = "巨魔疗伤", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤单位3；若为巨魔，改为5。", Keywords = new string[0] },
            new V06Card { Id = "S-V01", Name = "夜刃", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "对一个敌单位造成3点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "S-V02", Name = "血术缝合", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "对一个己方当前生命至少2的单位造成1点效果伤害，治疗另一个己方受伤单位4；两目标必须不同。", Keywords = new string[0] },
            new V06Card { Id = "S-V03", Name = "红雾换位", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "移动一个己方单位到另一排空格；其本轮获得潜行，若已主动攻击则不获得潜行。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "S-V04", Name = "古堡来信", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "抽2张。", Keywords = new string[0] },
            new V06Card { Id = "S-O01", Name = "裂阵冲锋", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "一个己方单位本轮攻击+1并获得穿透。", Keywords = new string[] { "穿透" } },
            new V06Card { Id = "S-O02", Name = "战歌回荡", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "己方前排单位本轮攻击各+1。", Keywords = new string[0] },
            new V06Card { Id = "S-O03", Name = "狩猎绳索", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "将一个敌后排单位移入其前排空格；无空格时不可使用。", Keywords = new string[0] },
            new V06Card { Id = "S-O04", Name = "部族医药", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤单位3；然后抽1张。", Keywords = new string[0] },
            new V06Card { Id = "S-R01", Name = "龙息掠地", Category = V06Category.Tactic, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "对敌前排每个单位造成2点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "S-R02", Name = "鳞翼掩护", Category = V06Category.Tactic, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "一个己方单位本轮受到的主动攻击伤害减2。", Keywords = new string[0] },
            new V06Card { Id = "S-R03", Name = "唤来风翼", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "若有空格，生成T07；然后抽1张；无空格时不可使用。", Keywords = new string[0] },
            new V06Card { Id = "S-R04", Name = "古龙凝视", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "一个敌单位本轮攻击−3，最低0。", Keywords = new string[0] },
            new V06Card { Id = "S-M01", Name = "过载射击", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "一个己方构装单位本轮获得过载。", Keywords = new string[] { "过载" } },
            new V06Card { Id = "S-M02", Name = "机体维修", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤构装单位5。", Keywords = new string[0] },
            new V06Card { Id = "S-M03", Name = "战术数据库", Category = V06Category.Tactic, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "抽2张，再将1张手牌放到己方牌库底。", Keywords = new string[0] },
            new V06Card { Id = "S-M04", Name = "电弧脉冲", Category = V06Category.Tactic, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "对一个敌单位造成4点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "Q01", Name = "符钢短剑", Category = V06Category.Equipment, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "装备单位攻击+1。", Keywords = new string[0] },
            new V06Card { Id = "Q02", Name = "加固皮甲", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位最大生命+2，保留已有伤害。", Keywords = new string[0] },
            new V06Card { Id = "Q03", Name = "游侠符镜", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位获得远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "Q04", Name = "矮人重锤", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位攻击+2，且不能获得突袭。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "Q05", Name = "林纹披风", Category = V06Category.Equipment, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "装备单位受到的环境伤害减1，最低0；不作用于远征旗。", Keywords = new string[0] },
            new V06Card { Id = "Q06", Name = "古代装甲板", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位获得坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "Q07", Name = "银纹护符", Category = V06Category.Equipment, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "装备单位受到敌方单体战术的效果伤害减1，最低0。", Keywords = new string[0] },
            new V06Card { Id = "Q08", Name = "绯月细剑", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位攻击+1并获得汲取。", Keywords = new string[] { "汲取" } },
            new V06Card { Id = "Q09", Name = "部族战旗", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备单位获得协攻；其他己方单位不自动获得加成。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "Q10", Name = "龙鳞肩甲", Category = V06Category.Equipment, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "装备单位最大生命+3，保留已有伤害。", Keywords = new string[0] },
            new V06Card { Id = "Q11", Name = "机动喷口", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "仅构装可装备；装备单位获得飞行。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "Q12", Name = "驯兽铃", Category = V06Category.Equipment, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "装备后该单位首次主动攻击结束，若仍存活且有空格，生成T05；每个装备实体每局一次。", Keywords = new string[0] },
            new V06Card { Id = "A01", Name = "守誓符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "附印单位获得护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "A02", Name = "林息符文", Category = V06Category.Seal, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "附印单位获得再生。", Keywords = new string[] { "再生" } },
            new V06Card { Id = "A03", Name = "裂甲符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "附印单位本局获得穿透。", Keywords = new string[] { "穿透" } },
            new V06Card { Id = "A04", Name = "余火符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "附印单位获得余烬。", Keywords = new string[] { "余烬" } },
            new V06Card { Id = "A05", Name = "月痕符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "附印单位获得夜行。", Keywords = new string[] { "夜行" } },
            new V06Card { Id = "A06", Name = "狂战符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "附印单位攻击+1；每轮末对其造成1点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "A07", Name = "鳞息符文", Category = V06Category.Seal, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "附印单位最大生命+2；附上时可移除其1点伤害。", Keywords = new string[0] },
            new V06Card { Id = "A08", Name = "机核符文", Category = V06Category.Seal, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "仅构装可附；附印单位被击败时，其控制者抽1张。", Keywords = new string[0] },
            new V06Card { Id = "F01", Name = "远征营盘", Category = V06Category.Status, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "己方每轮第一次治疗单位时，治疗量+1。", Keywords = new string[0] },
            new V06Card { Id = "F02", Name = "树冠战场", Category = V06Category.Status, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "己方后排单位受到的主动攻击伤害减1，最低0。", Keywords = new string[0] },
            new V06Card { Id = "F03", Name = "工坊防线", Category = V06Category.Status, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "己方构装单位获得坚壁，不与已有坚壁叠加。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "F04", Name = "血族夜宴", Category = V06Category.Status, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "己方每轮第一次成功触发汲取时，额外治疗该单位1。", Keywords = new string[] { "汲取" } },
            new V06Card { Id = "F05", Name = "群龙回翔", Category = V06Category.Status, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "己方飞行单位攻击+1。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "F06", Name = "荒原战鼓", Category = V06Category.Status, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "己方每轮第一次触发协攻时，另给该次攻击+1。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "X01", Name = "举盾", Category = V06Category.Response, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "敌方主动攻击指定己方单位后：本次主动伤害减2。", Keywords = new string[0] },
            new V06Card { Id = "X02", Name = "法术偏转", Category = V06Category.Response, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "敌方单体伤害战术指定己方单位后：该战术对目标伤害减2，最低0。", Keywords = new string[0] },
            new V06Card { Id = "X03", Name = "抢先脱身", Category = V06Category.Response, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "敌方主动攻击指定己方单位后：将目标移到己方后排空格；若原攻击者按原有能力不能攻击该位置，本次攻击取消，费用与行动不退。", Keywords = new string[0] },
            new V06Card { Id = "X04", Name = "封住引线", Category = V06Category.Response, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "敌方使用会生成衍生物的战术后：取消该牌全部生成效果，其他效果照常。", Keywords = new string[0] },
            new V06Card { Id = "X05", Name = "镜盾反射", Category = V06Category.Response, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "敌方主动攻击指定己方单位后：本次主动伤害减1，行动结算完成后对攻击者造成1点效果伤害；攻击者已离场则跳过。", Keywords = new string[0] },
            new V06Card { Id = "X06", Name = "切断过载", Category = V06Category.Response, Cost = 1, Atk = 0, Hp = 0, Tags = "", Text = "敌方对一个单位施加过载的主行动后：取消本次过载的全部效果，费用与行动不退。", Keywords = new string[] { "过载" } },
            new V06Card { Id = "J01", Name = "远征老兵", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 5, Tags = "", Text = "任意费用不高于3的基础单位；保留基础能力。", Keywords = new string[0] },
            new V06Card { Id = "J02", Name = "坚誓卫士", Category = V06Category.Upgrade, Cost = 2, Atk = 2, Hp = 7, Tags = "", Text = "任意费用不高于3的基础单位；额外获得护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "J03", Name = "百步射手", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 5, Tags = "", Text = "任意具有远射的基础单位；保留基础能力。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "J04", Name = "突袭先锋", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 4, Tags = "", Text = "任意具有突袭的基础单位；保留基础能力，不刷新攻击次数。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "J05", Name = "公会骑士长", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 7, Tags = "", Text = "由U-H02晋阶；保留护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "J06", Name = "白枝长弓手", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 5, Tags = "", Text = "由U-E01晋阶；保留远射。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "J07", Name = "铁誓堡垒", Category = V06Category.Upgrade, Cost = 3, Atk = 4, Hp = 8, Tags = "", Text = "由U-D02晋阶；保留护卫与坚壁。", Keywords = new string[] { "护卫", "坚壁" } },
            new V06Card { Id = "J08", Name = "战帮刀队长", Category = V06Category.Upgrade, Cost = 2, Atk = 3, Hp = 5, Tags = "", Text = "由U-G01晋阶；保留协攻。", Keywords = new string[] { "协攻" } },
            new V06Card { Id = "J09", Name = "绯月剑爵", Category = V06Category.Upgrade, Cost = 2, Atk = 4, Hp = 6, Tags = "", Text = "由U-V02晋阶；保留汲取，额外获得夜行。", Keywords = new string[] { "汲取", "夜行" } },
            new V06Card { Id = "J10", Name = "红砂领骑", Category = V06Category.Upgrade, Cost = 2, Atk = 5, Hp = 6, Tags = "", Text = "由U-O02晋阶；保留突袭，不刷新攻击次数。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "J11", Name = "风翼龙骑", Category = V06Category.Upgrade, Cost = 3, Atk = 5, Hp = 7, Tags = "", Text = "由U-R02晋阶；保留飞行。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "J12", Name = "守门人重装型", Category = V06Category.Upgrade, Cost = 3, Atk = 4, Hp = 8, Tags = "", Text = "由U-M02晋阶；保留坚壁。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "P01", Name = "公会增援", Category = V06Category.Support, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "从己方牌库检索1张费用不高于3的单位入手，然后洗牌。", Keywords = new string[0] },
            new V06Card { Id = "P02", Name = "紧急医疗", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤单位4。", Keywords = new string[0] },
            new V06Card { Id = "P03", Name = "破阵术式", Category = V06Category.Support, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "对一个敌单位造成3点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "P04", Name = "林间接应", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "将一个己方单位移入另一排空格，再治疗该单位2。", Keywords = new string[0] },
            new V06Card { Id = "P05", Name = "工坊交货", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "从己方弃牌区选择1张装备入手。", Keywords = new string[0] },
            new V06Card { Id = "P06", Name = "战帮补位", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "按顺序在己方空格生成至多2个T03；无空格时不可用。", Keywords = new string[0] },
            new V06Card { Id = "P07", Name = "夜城援医", Category = V06Category.Support, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "己方所有受伤单位各治疗2；无受伤单位时不可用。", Keywords = new string[0] },
            new V06Card { Id = "P08", Name = "兽群呼应", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "在己方空格生成1个T05；然后对一个敌前排单位造成1点效果伤害，无敌前排时跳过伤害。", Keywords = new string[0] },
            new V06Card { Id = "P09", Name = "龙翼来援", Category = V06Category.Support, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "在己方空格生成1个T07。", Keywords = new string[0] },
            new V06Card { Id = "P10", Name = "维修吊车", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "治疗一个己方受伤构装单位4，再移至己方另一排空格；没有空格时仍可治疗。", Keywords = new string[0] },
            new V06Card { Id = "P11", Name = "遗物回收", Category = V06Category.Support, Cost = 2, Atk = 0, Hp = 0, Tags = "", Text = "从己方弃牌区选择2张不同名单位置于牌库底，再抽1张；不足两种时不可用。", Keywords = new string[0] },
            new V06Card { Id = "P12", Name = "共同战线", Category = V06Category.Support, Cost = 3, Atk = 0, Hp = 0, Tags = "", Text = "本轮己方前排单位获得坚壁，不与已有坚壁叠加。", Keywords = new string[] { "坚壁" } },
            new V06Card { Id = "T01", Name = "茸角灵鹿", Category = V06Category.Token, Cost = -1, Atk = 1, Hp = 3, Tags = "灵兽", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "T02", Name = "微光侍灵", Category = V06Category.Token, Cost = -1, Atk = 1, Hp = 1, Tags = "灵体", Text = "被击败时可治疗一个己方受伤单位1。", Keywords = new string[0] },
            new V06Card { Id = "T03", Name = "战帮帮手", Category = V06Category.Token, Cost = -1, Atk = 1, Hp = 1, Tags = "哥布林", Text = "突袭；协攻。", Keywords = new string[] { "突袭", "协攻" } },
            new V06Card { Id = "T04", Name = "夜蝠群", Category = V06Category.Token, Cost = -1, Atk = 1, Hp = 2, Tags = "野兽", Text = "飞行。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "T05", Name = "荒原猎犬", Category = V06Category.Token, Cost = -1, Atk = 2, Hp = 1, Tags = "野兽", Text = "突袭；不能攻击远征旗。", Keywords = new string[] { "突袭" } },
            new V06Card { Id = "T06", Name = "守备无人机", Category = V06Category.Token, Cost = -1, Atk = 1, Hp = 3, Tags = "构装", Text = "护卫。", Keywords = new string[] { "护卫" } },
            new V06Card { Id = "T07", Name = "风翼雏龙", Category = V06Category.Token, Cost = -1, Atk = 2, Hp = 2, Tags = "龙类野兽", Text = "飞行；突袭；不能攻击远征旗。", Keywords = new string[] { "突袭", "飞行" } },
            new V06Card { Id = "T08", Name = "维修微机", Category = V06Category.Token, Cost = -1, Atk = 0, Hp = 2, Tags = "构装", Text = "维修。", Keywords = new string[] { "维修" } },
            new V06Card { Id = "W01", Name = "晴日", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "无附加规则，测试对照。", Keywords = new string[0] },
            new V06Card { Id = "W02", Name = "残冬寒风", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第3轮主行动阶段结束后，双方前排每个单位各受1点环境伤害，仅一次。", Keywords = new string[0] },
            new V06Card { Id = "W03", Name = "林荫", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "双方后排单位受到单体战术的效果伤害减1，最低0。", Keywords = new string[0] },
            new V06Card { Id = "W04", Name = "熔炉余热", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第4轮主行动阶段结束后，双方前排每个单位各受1点环境伤害，仅一次。", Keywords = new string[0] },
            new V06Card { Id = "W05", Name = "月蚀长夜", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "本场夜行在每轮都生效，以此替代奇数轮条件，仍只加1攻击。", Keywords = new string[] { "夜行" } },
            new V06Card { Id = "W06", Name = "高空侧风", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "双方免费换位只能移入空格，不能互换已占格；卡牌移动不受影响。", Keywords = new string[0] },
            new V06Card { Id = "W07", Name = "魔力乱流", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第3轮和第6轮双方常规抽牌由2张变为1张。", Keywords = new string[0] },
            new V06Card { Id = "W08", Name = "古代照明", Category = V06Category.Weather, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "本场单位不能获得潜行；已有潜行立即解除。", Keywords = new string[] { "潜行" } },
            new V06Card { Id = "C01", Name = "哥布林夺车", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "G", Text = "敌旗16；击破敌旗获胜，其余按普通对局。", Keywords = new string[0] },
            new V06Card { Id = "C02", Name = "巨魔守桥", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "G", Text = "敌旗20；敌方开局前排额外放置1张U-G05，当前生命设为4；击破敌旗获胜。战后锁链交涉属于剧情，不改战斗数值。", Keywords = new string[0] },
            new V06Card { Id = "C03", Name = "树冠猎场", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "E", Text = "敌旗18；敌方首次部署的远射单位本轮可主动攻击一次；击破敌旗获胜。", Keywords = new string[] { "远射" } },
            new V06Card { Id = "C04", Name = "寄生古树", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "E", Text = "敌旗24；若敌方轮末仍有树灵单位，敌旗回复1，不超过24；击破敌旗获胜。", Keywords = new string[0] },
            new V06Card { Id = "C05", Name = "矿车追逐", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "G", Text = "敌旗20；必须在第6轮末前击破敌旗，否则任务失败。", Keywords = new string[0] },
            new V06Card { Id = "C06", Name = "守墓机兵", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "M", Text = "敌旗24；第3、6、9、12轮敌方第一次主行动改为对我方前排全体造成1点效果伤害。", Keywords = new string[0] },
            new V06Card { Id = "C07", Name = "石像鬼夜袭", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "V", Text = "敌旗20；敌方开局前排额外放置1张U-V05；击破敌旗获胜。", Keywords = new string[0] },
            new V06Card { Id = "C08", Name = "月蚀伯爵", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "V", Text = "敌旗24；每轮若敌方至少成功汲取一次，该轮末敌旗回复1，不超过24；击破敌旗获胜。", Keywords = new string[] { "汲取" } },
            new V06Card { Id = "C09", Name = "奇美拉狩猎", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "O", Text = "敌旗26；敌方每轮第一次主行动按轮次循环：前排全体2伤、我方最低当前生命单位3伤、敌前排全体治疗2；第4轮起重复。", Keywords = new string[0] },
            new V06Card { Id = "C10", Name = "空艇防卫", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "R", Text = "敌旗20；击破敌旗，或第6轮全部结算后己方旗仍大于0即获胜；平局仍未完成。", Keywords = new string[0] },
            new V06Card { Id = "C11", Name = "龙族试炼", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "R", Text = "敌旗24；敌方开局前排额外放置1张U-R06，不触发其入场；击破敌旗代表完成试炼，剧情中不杀龙。", Keywords = new string[0] },
            new V06Card { Id = "C12", Name = "王庭核心", Category = V06Category.Encounter, Cost = -1, Atk = 0, Hp = 0, Tags = "M", Text = "启用第6.3节覆翼王两阶段规则与12轮期限，替代普通敌旗败北和战场压力。", Keywords = new string[0] },
            new V06Card { Id = "E01", Name = "公会预付", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第1轮补充行动点后，我方额外获得1点，仍受9点上限。", Keywords = new string[0] },
            new V06Card { Id = "E02", Name = "新伙伴的援手", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "完成起手后，从我方牌库检索1张费用不高于2的单位入手，再洗牌；无目标则跳过。", Keywords = new string[0] },
            new V06Card { Id = "E03", Name = "树根通道", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "我方每轮首次免费换位完成后，可治疗此次移动的一个己方单位1。", Keywords = new string[0] },
            new V06Card { Id = "E04", Name = "署名样机", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "我方本场首次部署构装单位后，为其生成并装备一张Q06；不从牌库取牌，若已有装备则不替换。", Keywords = new string[0] },
            new V06Card { Id = "E05", Name = "舞会邀约", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第1轮敌方首次主动攻击的伤害减1，最低0。", Keywords = new string[0] },
            new V06Card { Id = "E06", Name = "公开罪证", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第3轮开始时，对敌旗造成2点效果伤害，仅一次。", Keywords = new string[0] },
            new V06Card { Id = "E07", Name = "竞技通行", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "我方本场首次部署的基础单位最大生命+1，保留已有伤害。", Keywords = new string[0] },
            new V06Card { Id = "E08", Name = "风眠航标", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "我方第1轮首次部署的飞行单位可立即主动攻击一次；仍受每轮一次上限。", Keywords = new string[] { "飞行" } },
            new V06Card { Id = "E09", Name = "镜像标记", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "敌方本场首次部署的单位不触发入场能力，其其他能力正常。", Keywords = new string[0] },
            new V06Card { Id = "E10", Name = "试车许可", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "启用第6.2节主角机甲开场、起手和失败规则。", Keywords = new string[0] },
            new V06Card { Id = "E11", Name = "共同航路", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "第5轮开始时，我方额外抽1张，仅一次。", Keywords = new string[0] },
            new V06Card { Id = "E12", Name = "龙翼支援", Category = V06Category.Event, Cost = -1, Atk = 0, Hp = 0, Tags = "", Text = "只用于C12：进入覆翼王第二阶段时对敌旗造成3伤并永久移除王庭外壳，按第6.3节结算，同一事件不重复触发。", Keywords = new string[0] },
        };

        public static readonly List<V06Precon> Precons = new List<V06Precon>
        {
            new V06Precon { Name = "人类远征团", Vanguard = "U-H02", Units = new string[] { "U-H01", "U-H02", "U-H03", "U-H04", "U-H05", "U-H06", "U-H07", "U-H08" }, Tactics = new string[] { "S-H01", "S-H02", "S-H03", "S-H04" }, Upgrades = new string[] { "J01", "J02", "J05" }, Supports = new string[] { "P01", "P02", "P03" } },
            new V06Precon { Name = "精灵林卫", Vanguard = "U-E01", Units = new string[] { "U-E01", "U-E02", "U-E03", "U-E04", "U-E05", "U-E06", "U-E07", "U-E08" }, Tactics = new string[] { "S-E01", "S-E02", "S-E03", "S-E04" }, Upgrades = new string[] { "J01", "J02", "J06" }, Supports = new string[] { "P02", "P04", "P12" } },
            new V06Precon { Name = "矮人铁誓", Vanguard = "U-D02", Units = new string[] { "U-D01", "U-D02", "U-D03", "U-D04", "U-D05", "U-D06", "U-D07", "U-D08" }, Tactics = new string[] { "S-D01", "S-D02", "S-D03", "S-D04" }, Upgrades = new string[] { "J01", "J02", "J07" }, Supports = new string[] { "P03", "P05", "P10" } },
            new V06Precon { Name = "哥布林与巨魔", Vanguard = "U-G02", Units = new string[] { "U-G01", "U-G02", "U-G03", "U-G04", "U-G05", "U-G06", "U-G07", "U-G08" }, Tactics = new string[] { "S-G01", "S-G02", "S-G03", "S-G04" }, Upgrades = new string[] { "J01", "J02", "J08" }, Supports = new string[] { "P02", "P06", "P03" } },
            new V06Precon { Name = "绯月血族", Vanguard = "U-V02", Units = new string[] { "U-V01", "U-V02", "U-V03", "U-V04", "U-V05", "U-V06", "U-V07", "U-V08" }, Tactics = new string[] { "S-V01", "S-V02", "S-V03", "S-V04" }, Upgrades = new string[] { "J01", "J02", "J09" }, Supports = new string[] { "P01", "P07", "P03" } },
            new V06Precon { Name = "荒原战盟", Vanguard = "U-O02", Units = new string[] { "U-O01", "U-O02", "U-O03", "U-O04", "U-O05", "U-O06", "U-O07", "U-O08" }, Tactics = new string[] { "S-O01", "S-O02", "S-O03", "S-O04" }, Upgrades = new string[] { "J01", "J02", "J10" }, Supports = new string[] { "P02", "P08", "P12" } },
            new V06Precon { Name = "龙群之契", Vanguard = "U-R02", Units = new string[] { "U-R01", "U-R02", "U-R03", "U-R04", "U-R05", "U-R06", "U-R07", "U-R08" }, Tactics = new string[] { "S-R01", "S-R02", "S-R03", "S-R04" }, Upgrades = new string[] { "J01", "J02", "J11" }, Supports = new string[] { "P01", "P09", "P03" } },
            new V06Precon { Name = "铁庭机组", Vanguard = "U-M02", Units = new string[] { "U-M01", "U-M02", "U-M03", "U-M04", "U-M05", "U-M06", "U-M07", "U-M08" }, Tactics = new string[] { "S-M01", "S-M02", "S-M03", "S-M04" }, Upgrades = new string[] { "J01", "J02", "J12" }, Supports = new string[] { "P01", "P10", "P12" } },
        };

        public static V06Card ById(string id)
        {
            for (int i = 0; i < All.Count; i++) { if (All[i].Id == id) { return All[i]; } }
            return null;
        }

        public static int CountOf(V06Category cat)
        {
            int n = 0;
            for (int i = 0; i < All.Count; i++) { if (All[i].Category == cat) { n++; } }
            return n;
        }

        public static V06Precon PreconByName(string name)
        {
            for (int i = 0; i < Precons.Count; i++) { if (Precons[i].Name == name) { return Precons[i]; } }
            return null;
        }
    }
}
