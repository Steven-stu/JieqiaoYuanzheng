using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>V0.6 阵位：0–2 前排，3–5 后排。</summary>
    public static class V06Slot
    {
        public const int FrontCount = 3;
        public const int BackCount = 3;
        public const int Total = FrontCount + BackCount;
        public const int None = -1;

        public static bool IsFront(int slot) { return slot >= 0 && slot < FrontCount; }
        public static bool IsBack(int slot) { return slot >= FrontCount && slot < Total; }
        public static int Mirror(int slot) { return (slot + FrontCount) % Total; }
    }

    /// <summary>V0.6 单位实例。伤害累加制：当前生命 = 最大生命 − 伤害。</summary>
    public sealed class V06Unit
    {
        public int Uid;
        public int Camp;
        public int Slot = V06Slot.None;
        public string CardId;          // 基础卡编号
        public string Name;
        public int AtkBase;
        public int HpBase;
        public int Damage;
        public int AtkPerm;            // 装备/附印/永久加成
        public int HpPerm;
        public int TempAtk;            // 本轮临时增减（可为负）
        public string TagText;         // 标签原文，用于“构装/巨魔/飞行单位”等判定
        public List<string> Keywords = new List<string>();
        public string EquipId;
        public string SealId;
        public bool IsToken;
        public bool Upgraded;
        public string UpgradeCardId;   // 晋阶后挂在身上的晋阶牌（离场时移出本局）
        public bool DeployedThisRound;
        public bool AttackedThisRound;
        public bool OverloadThisRound;
        public bool DrainedThisRound;      // 汲取每轮一次
        public bool SneakActive;
        public bool DragonFearThisRound;   // 被龙威削弱标记（本轮下一次主动攻击 −1）
        public int ReduceThisRound;        // 本轮受到的主动攻击伤害减免（如 S-R02）
        public int WatcherBellUsed;        // Q12 每局一次
        // ── V0.7 兽群成长 / 进化 / 融合 ──
        public int Growth;                 // 成长计数（轮末 +1，到达阈值触发进化或终极 +1/+1）
        public bool GrowthMaxed;           // 终极形态已领过满成长奖励
        public string FusedFrom;           // 融合体的两只素材编号（"B15+B17"），仅用于显示

        public int MaxHp { get { return HpBase + HpPerm; } }
        public int CurHp { get { return MaxHp - Damage; } }
        public bool Alive { get { return Slot != V06Slot.None && CurHp > 0; } }
        public int AtkBaseNow { get { return AtkBase + AtkPerm + TempAtk; } }

        public bool Has(string kw)
        {
            for (int i = 0; i < Keywords.Count; i++) { if (Keywords[i] == kw) { return true; } }
            return false;
        }

        public void AddKw(string kw)
        {
            if (!Has(kw)) { Keywords.Add(kw); }
        }

        public bool HasTag(string tag)
        {
            if (string.IsNullOrEmpty(TagText)) { return false; }
            if (TagText.Contains(tag)) { return true; }
            // 机甲也具有构装标签；龙裔/龙族归入龙类判定时用专名
            return false;
        }

        public V06Unit Clone()
        {
            V06Unit u = new V06Unit();
            u.Uid = Uid; u.Camp = Camp; u.Slot = Slot; u.CardId = CardId; u.Name = Name;
            u.AtkBase = AtkBase; u.HpBase = HpBase; u.Damage = Damage;
            u.AtkPerm = AtkPerm; u.HpPerm = HpPerm; u.TempAtk = TempAtk;
            u.TagText = TagText; u.EquipId = EquipId; u.SealId = SealId; u.IsToken = IsToken;
            u.Upgraded = Upgraded; u.UpgradeCardId = UpgradeCardId; u.DeployedThisRound = DeployedThisRound;
            u.AttackedThisRound = AttackedThisRound; u.OverloadThisRound = OverloadThisRound;
            u.DrainedThisRound = DrainedThisRound; u.SneakActive = SneakActive;
            u.DragonFearThisRound = DragonFearThisRound; u.ReduceThisRound = ReduceThisRound; u.WatcherBellUsed = WatcherBellUsed;
            u.Growth = Growth; u.GrowthMaxed = GrowthMaxed; u.FusedFrom = FusedFrom;
            u.Keywords = new List<string>(Keywords);
            return u;
        }
    }

    /// <summary>V0.6 一方状态。</summary>
    public sealed class V06Player
    {
        public int Camp;
        public int FlagHp = 20;
        public int ActionPoints;
        public int MainActionsUsed;
        public int SwappedThisRound;
        public bool PowerStrikeUsed;
        public bool CoverUsed;
        public int ResponseUsed;
        public V06Unit[] Slots = new V06Unit[V06Slot.Total];
        public List<string> Deck = new List<string>();
        public List<string> Hand = new List<string>();
        public List<string> Discard = new List<string>();
        public List<string> Exile = new List<string>();          // 晋阶牌等移出本局
        public List<string> UpgradedCards = new List<string>();  // 该基础卡已晋阶过
        public int FrontReduceThisRound; // 本轮己方前排受到的主动攻击伤害减免（如 S-D04）
        public List<string> Reserve = new List<string>();      // 预备 6 张（3 张不同晋阶 + 3 张不同支援）
        public List<string> ReserveUsed = new List<string>();  // 已调用的预备（每张每局一次）
        // 战况的"每轮第一次"触发标记
        public bool FirstHealUsed;
        public bool FirstDrainUsed;
        public bool FirstAssistUsed;
        public string BattlefieldId;    // 场上至多 1 张战况
        public string VanguardId;       // 领航牌编号
        public int NextUid = 1000;

        public int UnitCount()
        {
            int n = 0;
            for (int i = 0; i < Slots.Length; i++) { if (Slots[i] != null && Slots[i].Alive) { n++; } }
            return n;
        }

        public bool AnyEmptySlot()
        {
            for (int i = 0; i < Slots.Length; i++) { if (Slots[i] == null || !Slots[i].Alive) { return true; } }
            return false;
        }

        public int FirstEmptySlot()
        {
            // 生成未指定格子的衍生物时，前排优先，再后排
            for (int i = 0; i < V06Slot.FrontCount; i++) { if (Slots[i] == null || !Slots[i].Alive) { return i; } }
            for (int i = V06Slot.FrontCount; i < V06Slot.Total; i++) { if (Slots[i] == null || !Slots[i].Alive) { return i; } }
            return V06Slot.None;
        }

        public V06Unit At(int slot)
        {
            if (slot < 0 || slot >= Slots.Length) { return null; }
            V06Unit u = Slots[slot];
            return (u != null && u.Alive) ? u : null;
        }

        public V06Unit ByUid(int uid)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] != null && Slots[i].Alive && Slots[i].Uid == uid) { return Slots[i]; }
            }
            return null;
        }

        public List<V06Unit> Units()
        {
            List<V06Unit> r = new List<V06Unit>();
            for (int i = 0; i < Slots.Length; i++) { if (Slots[i] != null && Slots[i].Alive) { r.Add(Slots[i]); } }
            return r;
        }

        public V06Player Clone()
        {
            V06Player p = new V06Player();
            p.Camp = Camp; p.FlagHp = FlagHp; p.ActionPoints = ActionPoints;
            p.MainActionsUsed = MainActionsUsed; p.SwappedThisRound = SwappedThisRound;
            p.PowerStrikeUsed = PowerStrikeUsed; p.CoverUsed = CoverUsed; p.ResponseUsed = ResponseUsed;
            p.BattlefieldId = BattlefieldId; p.NextUid = NextUid; p.VanguardId = VanguardId; p.FrontReduceThisRound = FrontReduceThisRound;
            p.Reserve = new List<string>(Reserve);
            p.ReserveUsed = new List<string>(ReserveUsed);
            p.FirstHealUsed = FirstHealUsed; p.FirstDrainUsed = FirstDrainUsed; p.FirstAssistUsed = FirstAssistUsed;
            p.Deck = new List<string>(Deck);
            p.Hand = new List<string>(Hand);
            p.Discard = new List<string>(Discard);
            p.Exile = new List<string>(Exile);
            p.UpgradedCards = new List<string>(UpgradedCards);
            for (int i = 0; i < Slots.Length; i++) { p.Slots[i] = Slots[i] == null ? null : Slots[i].Clone(); }
            return p;
        }
    }

    public enum V06ActionKind { Deploy, Attack, PlayCard, Swap, Pass, Respond, Cover, Decline, Fuse }

    /// <summary>一次 V0.6 行动。由 LegalActions 生成，界面与 AI 共用同一套枚举。</summary>
    public sealed class V06Action
    {
        public V06ActionKind Kind;
        public string CardId;       // Deploy / PlayCard / Respond
        public int Slot = V06Slot.None;
        public int Uid = -1;        // 施法者 / 目标单位
        public int TargetUid = -1;  // 第二目标
        public int TargetSlot = V06Slot.None;
        public bool PowerStrike;
        public bool ToFlag;         // Attack：目标为远征旗
        public bool FromReserve;    // PlayCard：该牌来自预备区（每张每局一次）

        public override string ToString()
        {
            switch (Kind)
            {
                case V06ActionKind.Deploy: return "部署 " + CardId + " → 槽 " + Slot;
                case V06ActionKind.Attack: return "攻击 " + Uid + (ToFlag ? " → 旗" : " → " + TargetUid) + (PowerStrike ? "（强攻）" : "");
                case V06ActionKind.PlayCard: return "使用 " + CardId + (Uid >= 0 ? " → " + Uid : "") + (TargetUid >= 0 ? " / " + TargetUid : "");
                case V06ActionKind.Swap: return "换位 " + Uid + " ↔ " + TargetUid;
                case V06ActionKind.Pass: return "让过";
                case V06ActionKind.Respond: return "响应 " + CardId;
                case V06ActionKind.Cover: return "掩护";
                case V06ActionKind.Decline: return "放弃";
                case V06ActionKind.Fuse: return "融合 " + Uid + " + " + TargetUid + " → " + CardId;
            }
            return Kind.ToString();
        }
    }
}
