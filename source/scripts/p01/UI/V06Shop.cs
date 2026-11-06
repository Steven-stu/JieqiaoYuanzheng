using System.Collections.Generic;
using UnityEngine;

namespace Jieqiao.UI
{
    /// <summary>
    /// 商店（V0.7）：卡包货架 + 每日特惠 + 宝石宝箱。
    ///
    /// 设计约束：
    ///   · 只做"买卡包 → 开包得卡"这一条闭环，不做抽卡概率公示之外的复杂机制；
    ///   · 卡池只取"可部署"的牌（费用 ≥ 1），进化形态/融合体仍然只能靠进化与融合获得；
    ///   · 一切数值都在 Defs 里，改价格只改这一处。
    /// </summary>
    public static class V06Shop
    {
        public enum Currency { Coins, Gems, Packs }

        public sealed class Def
        {
            public string Id;
            public string Name;
            public string Skin;        // 货架上的卡包图（shop_pack_*）
            public Currency Pay;
            public int Price;
            public int Count;          // 开包张数
            public string Bias;        // "B" 偏兽 / "G" 偏少女 / "" 均衡
            public int GuaranteeCost;  // 保底一张费用 ≥ 该值的牌（0 = 无保底）
            public string Desc;
        }

        public static readonly Def[] Defs = new Def[]
        {
            new Def { Id = "starter", Name = "基础卡包", Skin = "shop_pack_starter", Pay = Currency.Coins, Price = 200, Count = 5,
                      Bias = "", GuaranteeCost = 0, Desc = "5 张随机卡，均衡取自全部可部署牌。" },
            new Def { Id = "beast",   Name = "兽群卡包", Skin = "shop_pack_beast",   Pay = Currency.Coins, Price = 220, Count = 5,
                      Bias = "B", GuaranteeCost = 0, Desc = "5 张，偏向 V0.7 兽群（B01–B14）。" },
            new Def { Id = "girl",    Name = "少女卡包", Skin = "shop_pack_girl",    Pay = Currency.Coins, Price = 220, Count = 5,
                      Bias = "G", GuaranteeCost = 0, Desc = "5 张，偏向 V0.7 少女（G01–G16）。" },
            new Def { Id = "daily",   Name = "每日特惠", Skin = "shop_pack_daily",   Pay = Currency.Coins, Price = 120, Count = 3,
                      Bias = "", GuaranteeCost = 0, Desc = "每天限 1 次，120 金币 3 张。" },
            new Def { Id = "premium", Name = "高级宝箱", Skin = "shop_pack_starter", Pay = Currency.Gems,  Price = 60,  Count = 5,
                      Bias = "", GuaranteeCost = 3, Desc = "5 张，保底 1 张 3 费以上。" },
        };

        public static Def Find(string id)
        {
            for (int i = 0; i < Defs.Length; i++) { if (Defs[i].Id == id) { return Defs[i]; } }
            return null;
        }

        /// <summary>每日特惠今天是否还能买。</summary>
        public static bool DailyAvailable
        {
            get
            {
                if (V06Save.DevMode) { return true; }
                if (V06Save.Data == null) { return false; }
                return V06Save.Data.dailyShopDay != V06Save.TodayKey();
            }
        }

        /// <summary>能不能买（买得起 + 限购没超）。</summary>
        public static bool CanBuy(Def d, out string why)
        {
            why = "";
            if (V06Save.DevMode) { return true; }
            if (d == null || V06Save.Data == null) { why = "存档未就绪"; return false; }
            if (d.Id == "daily" && !DailyAvailable) { why = "今天的特惠已经买过了"; return false; }
            V06SaveData s = V06Save.Data;
            if (d.Pay == Currency.Coins && s.coins < d.Price) { why = "金币不足（需要 " + d.Price + "）"; return false; }
            if (d.Pay == Currency.Gems && s.gems < d.Price) { why = "宝石不足（需要 " + d.Price + "）"; return false; }
            if (d.Pay == Currency.Packs && s.packs < d.Price) { why = "卡包不足（需要 " + d.Price + "）"; return false; }
            return true;
        }

        /// <summary>买一个卡包，返回开出的卡名列表；失败返回 null 并给 err。</summary>
        public static List<string> Buy(Def d, out string err, out List<string> cardIds)
        {
            err = "";
            cardIds = new List<string>();
            string why;
            if (!CanBuy(d, out why)) { err = why; return null; }
            V06SaveData s = V06Save.Data;
            if (!V06Save.DevMode)
            {
                if (d.Pay == Currency.Coins) { s.coins -= d.Price; }
                else if (d.Pay == Currency.Gems) { s.gems -= d.Price; }
                else { s.packs -= d.Price; }
            }
            s.shopBuys++;

            List<string> pool = Pool("");
            List<string> biasPool = string.IsNullOrEmpty(d.Bias) ? null : Pool(d.Bias);
            List<string> names = new List<string>();
            if (pool.Count == 0) { err = "卡池为空"; return null; }
            // 保底：先塞一张费用够的
            if (d.GuaranteeCost > 0)
            {
                List<string> high = new List<string>();
                for (int i = 0; i < pool.Count; i++)
                {
                    V06Card c = V06Catalog.ById(pool[i]);
                    if (c != null && c.Cost >= d.GuaranteeCost) { high.Add(pool[i]); }
                }
                if (high.Count > 0)
                {
                    string pick = high[Random.Range(0, high.Count)];
                    V06Save.Grant(pick, 1);
                    cardIds.Add(pick);
                    names.Add(NameOf(pick));
                }
            }
            while (cardIds.Count < d.Count)
            {
                // 偏置包：60% 概率从"该阵营扩展卡"里抽，其余从全卡池抽
                bool useBias = biasPool != null && biasPool.Count > 0 && Random.value < 0.6f;
                List<string> src = useBias ? biasPool : pool;
                string pick = src[Random.Range(0, src.Count)];
                V06Save.Grant(pick, 1);
                cardIds.Add(pick);
                names.Add(NameOf(pick));
            }
            if (d.Id == "daily") { s.dailyShopDay = V06Save.TodayKey(); }
            V06Save.Save();
            return names;
        }

        /// <summary>开一个存档里已有的卡包（货币栏的"卡包"数量）。</summary>
        public static List<string> OpenPack(out string err, out List<string> cardIds)
        {
            err = "";
            cardIds = new List<string>();
            if (V06Save.Data == null) { err = "存档未就绪"; return null; }
            if (!V06Save.DevMode && V06Save.Data.packs <= 0) { err = "没有卡包了"; return null; }
            if (!V06Save.DevMode) { V06Save.Data.packs--; }
            List<string> pool = Pool("");
            List<string> names = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                string pick = pool[Random.Range(0, pool.Count)];
                V06Save.Grant(pick, 1);
                cardIds.Add(pick);
                names.Add(NameOf(pick));
            }
            V06Save.Save();
            return names;
        }

        /// <summary>可用卡池：只收"能部署"的牌（单位费用 1–4）。传 bias 则只收该阵营的扩展卡。</summary>
        private static List<string> Pool(string bias)
        {
            List<string> pool = new List<string>();
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (c.Category != V06Category.Unit) { continue; }
                if (c.Cost < 1 || c.Cost > 4) { continue; }
                bool exp = c.Id.Length > 0 && (c.Id[0] == 'B' || c.Id[0] == 'G');
                if (!string.IsNullOrEmpty(bias))
                {
                    // 偏置池：只要该前缀的扩展卡
                    if (exp && c.Id[0] == bias[0]) { pool.Add(c.Id); }
                    continue;
                }
                pool.Add(c.Id);
            }
            return pool;
        }

        private static string NameOf(string id)
        {
            V06Card c = V06Catalog.ById(id);
            return c == null ? id : c.Name;
        }
    }
}
