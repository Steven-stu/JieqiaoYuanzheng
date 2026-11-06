using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Jieqiao.UI
{
    /// <summary>持有数量。JsonUtility 不支持 Dictionary，所以用列表。</summary>
    [Serializable]
    public class V06OwnedEntry
    {
        public string id;
        public int n;
    }

    /// <summary>一套自定义卡组（可直接转成 V06Precon 开局）。</summary>
    [Serializable]
    public class V06DeckSave
    {
        public string name = "新卡组";
        public string vanguard;
        public string[] units = new string[0];      // 8 种，每种 2 张
        public string[] tactics = new string[0];    // 2 种，每种 2 张
        public string[] extras = new string[0];     // 至多 4 张各 1
        public string[] upgrades = new string[0];   // 预备：3 张晋阶
        public string[] supports = new string[0];   // 预备：3 张支援
        public string cover = "";

        public int MainCount
        {
            get
            {
                int n = units.Length * 2 + tactics.Length * 2;
                if (extras != null) { n += extras.Length; }
                return n;
            }
        }

        public bool IsLegal()
        {
            // V0.7 自由组卡(§4):主牌共 24 张,构成不限;同名合计 ≤2。
            // 存储:units/tactics 每个 ID 展开成 2 张,extras 每个 ID 1 张。
            if (upgrades == null || upgrades.Length != 3) { return false; }
            if (supports == null || supports.Length != 3) { return false; }
            if (MainCount != 24) { return false; }
            if (units == null || units.Length == 0) { return false; }
            if (units.Length > 12 || (tactics != null && tactics.Length > 12)) { return false; }
            if (extras != null && extras.Length > 24) { return false; }
            // 领航必须来自主牌单位
            if (!string.IsNullOrEmpty(vanguard) && Array.IndexOf(units, vanguard) < 0) { return false; }
            return SameNameWithinLimit();
        }

        /// <summary>同名上限:按规范化卡名合计 ≤2(units/tactics 各算 2 张,extras 算 1 张)。</summary>
        public bool SameNameWithinLimit()
        {
            Dictionary<string, int> count = new Dictionary<string, int>();
            AddCounts(count, units, 2);
            AddCounts(count, tactics, 2);
            AddCounts(count, extras, 1);
            foreach (KeyValuePair<string, int> kv in count)
            {
                if (kv.Value > 2) { return false; }
            }
            return true;
        }

        private static void AddCounts(Dictionary<string, int> map, string[] ids, int per)
        {
            if (ids == null) { return; }
            for (int i = 0; i < ids.Length; i++)
            {
                V06Card c = V06Catalog.ById(ids[i]);
                string key = c == null ? ids[i] : c.Name;
                if (key == null) { key = ids[i]; }
                int cur;
                map.TryGetValue(key, out cur);
                map[key] = cur + per;
            }
        }

        /// <summary>给玩家看的"还差什么"清单(保存/开局时用)。</summary>
        public string LegalProblems()
        {
            List<string> p = new List<string>();
            if (upgrades == null || upgrades.Length != 3) { p.Add("预备晋阶需 3 张"); }
            if (supports == null || supports.Length != 3) { p.Add("预备支援需 3 张"); }
            if (MainCount != 24) { p.Add("主牌 " + MainCount + "/24"); }
            if (units == null || units.Length == 0) { p.Add("至少要 1 种单位"); }
            if (!string.IsNullOrEmpty(vanguard) && units != null && Array.IndexOf(units, vanguard) < 0) { p.Add("领航不在主牌里"); }
            if (!SameNameWithinLimit()) { p.Add("同名超过 2 张"); }
            return p.Count == 0 ? "合法" : string.Join("、", p.ToArray());
        }

        public V06Precon ToPrecon()
        {
            V06Precon p = new V06Precon();
            p.Name = name;
            p.Vanguard = vanguard;
            p.Units = units;
            p.Tactics = tactics;
            p.Extras = extras;
            p.Upgrades = upgrades;
            p.Supports = supports;
            p.IsTestDeck = true;
            return p;
        }
    }

    /// <summary>一局的记录。</summary>
    [Serializable]
    public class V06RecordEntry
    {
        public string time;
        public string myDeck;
        public string foeDeck;
        public int result;          // 1 胜 / 0 平 / -1 负
        public int rounds;
        public int myFlag;
        public int foeFlag;
        public string mode;         // "单人" / "联机"
    }

    /// <summary>存档（单个 JSON 文件，放在 persistentDataPath）。</summary>
    [Serializable]
    public class V06SaveData
    {
        public int version = 1;
        public string playerName = "远征者";
        public int level = 1;
        public int exp;
        public int coins = 800;
        public int gems = 60;
        public int packs = 2;
        public bool devMode;
        public string avatarId = "";
        public string title = "见习远征者";
        // 累计统计
        public int matches;
        public int wins;
        public int losses;
        public int draws;
        public int streakBest;
        public int flagDiffSum;
        public int roundsSum;
        // 列表
        public List<V06RecordEntry> records = new List<V06RecordEntry>();
        public List<V06OwnedEntry> owned = new List<V06OwnedEntry>();
        public List<V06DeckSave> decks = new List<V06DeckSave>();
        // 签到
        public string lastCheckin = "";
        public int streak;
        public string monthKey = "";
        public int monthDays;
        public List<int> claimedDays = new List<int>();
        // 商店（V0.7）：每日特惠的日期键 + 已购次数统计
        public string dailyShopDay = "";
        public int shopBuys;

        public int WinRate
        {
            get { return matches <= 0 ? 0 : Mathf.RoundToInt(wins * 100f / matches); }
        }

        public int AvgRounds
        {
            get { return matches <= 0 ? 0 : Mathf.RoundToInt(roundsSum / (float)matches); }
        }
    }

    /// <summary>存档读写 + 卡牌持有 + 结算记录 + 签到。纯本地，不联网。</summary>
    public static class V06Save
    {
        public static V06SaveData Data;

        /// <summary>开发者模式:全部卡牌视为已拥有、货币充足、商店免费、签到不限。</summary>
        public static bool DevMode;

        /// <summary>开关开发者模式;开启时把货币补满并存档。</summary>
        public static string SetDevMode(bool on)
        {
            DevMode = on;
            if (Data != null) { Data.devMode = on; }
            if (on)
            {
                if (Data == null) { Load(); }
                if (Data != null)
                {
                    Data.coins = 999999;
                    Data.gems = 999999;
                    Data.packs = 99;
                    if (Data.level < 20) { Data.level = 20; }
                    Data.exp = 0;
                    Data.devMode = true;
                    Save();
                }
            }
            return on ? "开发者模式：开（全部卡牌可用、货币充足）" : "开发者模式：关";
        }

        private static string PathOf
        {
            get { return Path.Combine(Application.persistentDataPath, "jieqiao-v06-save.json"); }
        }

        public static string SavePathForTest { get { return PathOf; } }

        public static V06SaveData Load()
        {
            try
            {
                if (File.Exists(PathOf))
                {
                    string json = File.ReadAllText(PathOf);
                    V06SaveData d = JsonUtility.FromJson<V06SaveData>(json);
                    if (d != null)
                    {
                        if (d.records == null) { d.records = new List<V06RecordEntry>(); }
                        if (d.owned == null) { d.owned = new List<V06OwnedEntry>(); }
                        if (d.decks == null) { d.decks = new List<V06DeckSave>(); }
                        if (d.claimedDays == null) { d.claimedDays = new List<int>(); }
                        Data = d;
                        EnsureDefaults();
                          ApplyDevFlag();
                        return Data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[V06Save] 读取存档失败，改用新存档：" + ex.Message);
            }
            Data = new V06SaveData();
              DevMode = false;
            EnsureDefaults();
            Save();
            return Data;
        }

        /// <summary>启动参数里是否带 -dev / --dev。</summary>
        public static bool HasDevArg()
        {
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] == "-dev" || a[i] == "--dev" || a[i] == "-开发者") { return true; }
                }
            }
            catch (Exception) { }
            return false;
        }

        /// <summary>从存档恢复开发者模式开关。</summary>
        public static void ApplyDevFlag()
        {
            DevMode = (Data != null && Data.devMode) || HasDevArg();
            if (DevMode && Data != null && !Data.devMode)
            {
                Data.devMode = true;
                Data.coins = 999999;
                Data.gems = 999999;
                Data.packs = 99;
                if (Data.level < 20) { Data.level = 20; }
                Save();
            }
        }

        public static void Save()
        {
            if (Data == null) { return; }
            try
            {
                File.WriteAllText(PathOf, JsonUtility.ToJson(Data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[V06Save] 写入存档失败：" + ex.Message);
            }
        }

        /// <summary>首次进入时给一套基础收藏：基础 192 张各 2 张 + 可部署的一阶兽/少女各 2 张。</summary>
        public static void EnsureDefaults()
        {
            if (Data == null) { Data = new V06SaveData(); }
            if (Data.owned.Count == 0)
            {
                for (int i = 0; i < V06Catalog.All.Count; i++)
                {
                    V06Card c = V06Catalog.All[i];
                    int n = 0;
                    if (!c.Exp) { n = 2; }
                    else if (c.Category == V06Category.Unit && c.Cost >= 1 && c.Cost <= 4) { n = 2; }
                    if (n > 0) { Data.owned.Add(new V06OwnedEntry { id = c.Id, n = n }); }
                }
            }
            if (Data.decks.Count == 0)
            {
                // 预置两套：八套官方预组里的两套，方便直接开打
                Data.decks.Add(FromPrecon(V06DeckLibrary.Get(0)));
                Data.decks.Add(FromPrecon(V06DeckLibrary.Get(4)));
            }
            if (string.IsNullOrEmpty(Data.avatarId)) { Data.avatarId = "avatar_01"; }
        }

        public static V06DeckSave FromPrecon(V06Precon p)
        {
            V06DeckSave d = new V06DeckSave();
            if (p == null) { return d; }
            d.name = p.Name;
            d.vanguard = p.Vanguard;
            d.units = p.Units == null ? new string[0] : (string[])p.Units.Clone();
            d.tactics = p.Tactics == null ? new string[0] : (string[])p.Tactics.Clone();
            d.extras = p.Extras == null ? new string[0] : (string[])p.Extras.Clone();
            d.upgrades = p.Upgrades == null ? new string[0] : (string[])p.Upgrades.Clone();
            d.supports = p.Supports == null ? new string[0] : (string[])p.Supports.Clone();
            return d;
        }

        public static int OwnedOf(string cardId)
        {
            if (DevMode) { return 99; }
            if (Data == null || string.IsNullOrEmpty(cardId)) { return 0; }
            for (int i = 0; i < Data.owned.Count; i++)
            {
                if (Data.owned[i].id == cardId) { return Data.owned[i].n; }
            }
            return 0;
        }

        public static void Grant(string cardId, int n)
        {
            if (Data == null || string.IsNullOrEmpty(cardId) || n == 0) { return; }
            for (int i = 0; i < Data.owned.Count; i++)
            {
                if (Data.owned[i].id == cardId) { Data.owned[i].n = Mathf.Max(0, Data.owned[i].n + n); return; }
            }
            if (n > 0) { Data.owned.Add(new V06OwnedEntry { id = cardId, n = n }); }
        }

        public static int DistinctOwned()
        {
            if (Data == null) { return 0; }
            int n = 0;
            for (int i = 0; i < Data.owned.Count; i++) { if (Data.owned[i].n > 0) { n++; } }
            return n;
        }

        public static int TotalOwned()
        {
            if (Data == null) { return 0; }
            int n = 0;
            for (int i = 0; i < Data.owned.Count; i++) { n += Data.owned[i].n; }
            return n;
        }

        // ───────────────────────── 对局记录 ─────────────────────────

        public static void RecordMatch(V06Engine e, int myCamp, string myDeck, string foeDeck, string mode)
        {
            if (Data == null || e == null) { return; }
            V06RecordEntry r = new V06RecordEntry();
            r.time = DateTime.Now.ToString("MM-dd HH:mm");
            r.myDeck = myDeck;
            r.foeDeck = foeDeck;
            r.rounds = e.Round;
            r.myFlag = e.Players[myCamp].FlagHp;
            r.foeFlag = e.Players[1 - myCamp].FlagHp;
            r.mode = mode;
            if (e.Winner == myCamp) { r.result = 1; Data.wins++; }
            else if (e.Winner == -2) { r.result = 0; Data.draws++; }
            else { r.result = -1; Data.losses++; }
            Data.matches++;
            Data.roundsSum += e.Round;
            Data.flagDiffSum += r.myFlag - r.foeFlag;
            Data.records.Insert(0, r);
            while (Data.records.Count > 40) { Data.records.RemoveAt(Data.records.Count - 1); }
            // 小奖励
            Data.coins += r.result > 0 ? 60 : (r.result == 0 ? 30 : 15);
            Data.exp += r.result > 0 ? 40 : 20;
            while (Data.exp >= 100) { Data.exp -= 100; Data.level++; Data.gems += 5; }
            Save();
        }

        // ───────────────────────── 签到 ─────────────────────────

        public static string TodayKey()
        {
            return DateTime.Now.ToString("yyyyMMdd");
        }

        public static bool CanCheckIn()
        {
            if (DevMode) { return true; }
            return CanCheckInRaw();
        }

        private static bool CanCheckInRaw()
        {
            if (Data == null) { return false; }
            string today = TodayKey();
            if (Data.lastCheckin == today) { return false; }
            return true;
        }

        /// <summary>签到奖励表：第 1–7 天。</summary>
        public static string CheckInRewardText(int day)
        {
            switch (day % 7)
            {
                case 1: return "金币 +100";
                case 2: return "金币 +150";
                case 3: return "卡包 +1";
                case 4: return "宝石 +10";
                case 5: return "金币 +200";
                case 6: return "卡包 +1";
                default: return "宝石 +30、卡包 +1";
            }
        }

        /// <summary>执行签到，返回本次奖励说明；今天已领返回 null。</summary>
        public static string DoCheckIn()
        {
            if (Data == null || !CanCheckIn()) { return null; }
            string today = TodayKey();
            string month = DateTime.Now.ToString("yyyyMM");
            string yesterday = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
            if (Data.lastCheckin == yesterday) { Data.streak++; } else { Data.streak = 1; }
            Data.lastCheckin = today;
            if (Data.monthKey != month) { Data.monthKey = month; Data.monthDays = 0; Data.claimedDays.Clear(); }
            Data.monthDays++;
            int day = ((Data.streak - 1) % 7) + 1;
            if (!Data.claimedDays.Contains(day)) { Data.claimedDays.Add(day); }
            switch (day)
            {
                case 1: Data.coins += 100; break;
                case 2: Data.coins += 150; break;
                case 3: Data.packs += 1; break;
                case 4: Data.gems += 10; break;
                case 5: Data.coins += 200; break;
                case 6: Data.packs += 1; break;
                default: Data.gems += 30; Data.packs += 1; break;
            }
            Save();
            return "第 " + day + " 天：" + CheckInRewardText(day);
        }

        /// <summary>测试用：抹掉今日签到与存档文件。</summary>
        public static void ResetForTest()
        {
            try { if (File.Exists(PathOf)) { File.Delete(PathOf); } } catch (Exception) { }
            Data = null;
        }
    }
}
