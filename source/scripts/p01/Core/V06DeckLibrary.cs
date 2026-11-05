using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// 牌库集合：8 套官方预组 + 若干"测试预组"。
    ///
    /// 为什么需要测试预组：官方预组的 24 张主牌只有单位 + 战术，
    /// 装备/附印/战况/响应这 32 张根本没有出场机会，
    /// 不加测试牌库就无法验证它们真的能打出来（20 局实测使用次数为 0 就是这个原因）。
    /// 测试预组只在测试与自测里使用，不影响正常开局。
    /// </summary>
    public static class V06DeckLibrary
    {
        private static List<V06Precon> _all;

        public static List<V06Precon> All
        {
            get
            {
                if (_all == null)
                {
                    _all = new List<V06Precon>();
                    for (int i = 0; i < V06Catalog.Precons.Count; i++) { _all.Add(V06Catalog.Precons[i]); }
                    _all.Add(MakeTest("试装·工坊武装", "U-D02",
                        new string[] { "U-D01", "U-D02", "U-D03", "U-D04", "U-D05", "U-D06", "U-D07", "U-D08" },
                        new string[] { "S-D01", "S-D02" },
                        new string[] { "Q01", "Q06", "A01", "A03" },
                        new string[] { "J01", "J02", "J07" },
                        new string[] { "P02", "P03", "P10" }));
                    _all.Add(MakeTest("试装·符印战线", "U-M02",
                        new string[] { "U-M01", "U-M02", "U-M03", "U-M04", "U-M05", "U-M06", "U-M07", "U-M08" },
                        new string[] { "S-M01", "S-M02" },
                        new string[] { "F03", "F05", "X01", "X05" },
                        new string[] { "J01", "J02", "J12" },
                        new string[] { "P02", "P03", "P12" }));
                    _all.Add(MakeTest("试装·血族夜宴", "U-V02",
                        new string[] { "U-V01", "U-V02", "U-V03", "U-V04", "U-V05", "U-V06", "U-V07", "U-V08" },
                        new string[] { "S-V01", "S-V02" },
                        new string[] { "Q08", "A02", "F04", "X03" },
                        new string[] { "J01", "J02", "J09" },
                        new string[] { "P01", "P07", "P04" }));
                    // ── V0.7 扩展测试预组 ──
                    // 兽群：8 种一阶兽 ×2 + 战术 2 种 ×2 + 额外 4 只（含融合素材 B11/B13/B14/B04）
                    // 这套牌能走出 5 条进化线与 5 组融合（B41/B42/B44/B37/B38/B39）。
                    _all.Add(MakeTest("兽群·荒原巢穴", "B01",
                        new string[] { "B01", "B02", "B03", "B05", "B06", "B07", "B09", "B10" },
                        new string[] { "S-O01", "S-O02" },
                        new string[] { "B11", "B13", "B14", "B04" },
                        new string[] { "J01", "J02", "J11" },
                        new string[] { "P02", "P08", "P12" }));
                    // 少女：8 种 ×2 + 战术 2 种 ×2 + 额外 4 位（带应援与远射的高费少女）
                    _all.Add(MakeTest("少女·夜行舞台", "G01",
                        new string[] { "G01", "G03", "G04", "G05", "G06", "G07", "G12", "G13" },
                        new string[] { "S-H01", "S-O04" },
                        new string[] { "G02", "G09", "G11", "G15" },
                        new string[] { "J01", "J02", "J05" },
                        new string[] { "P02", "P07", "P12" }));
                }
                return _all;
            }
        }

        public static int Count { get { return All.Count; } }

        public static V06Precon Get(int index)
        {
            if (index < 0 || index >= All.Count) { return null; }
            return All[index];
        }

        private static V06Precon MakeTest(string name, string vanguard, string[] units, string[] tactics,
            string[] extras, string[] upgrades, string[] supports)
        {
            V06Precon p = new V06Precon();
            p.Name = name;
            p.Vanguard = vanguard;
            p.Units = units;
            p.Tactics = tactics;
            p.Upgrades = upgrades;
            p.Supports = supports;
            p.Extras = extras;
            p.IsTestDeck = true;
            return p;
        }

        /// <summary>
        /// 组牌：单位 8 种各 2 张（16）+ 战术 2 种各 2 张（4）+ 额外卡 4 种各 1 张（4）= 24。
        /// 官方预组的 Tactics 有 4 种、没有 Extras，也正好 24。
        /// </summary>
        public static List<string> BuildDeck(V06Precon p)
        {
            List<string> deck = new List<string>();
            if (p == null) { return deck; }
            for (int i = 0; i < p.Units.Length; i++) { deck.Add(p.Units[i]); deck.Add(p.Units[i]); }
            for (int i = 0; i < p.Tactics.Length; i++) { deck.Add(p.Tactics[i]); deck.Add(p.Tactics[i]); }
            if (p.Extras != null)
            {
                for (int i = 0; i < p.Extras.Length; i++) { deck.Add(p.Extras[i]); }
            }
            return deck;
        }
    }
}
