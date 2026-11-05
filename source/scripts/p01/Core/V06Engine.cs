using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// 界桥远征 V0.6 战斗引擎（纯 C#，不依赖引擎）。
    /// 规则来源：scratch/v06/03_战斗规则与卡池_V0.6.md §2。
    /// 与 V0.3 的 GameEngine 并行存在，默认不影响现成对局。
    ///
    /// 本层已实现：阵位 3+3、单一行动点资源、每轮 4 次主行动、免费换位、
    /// 攻击与反击、强攻、掩护、响应窗口、16 关键词的数值部分、
    /// 战场压力、疲劳伤害、12 轮上限与胜负判定。
    /// 卡牌效果解释器见 V06Effects（分层实现，未实现的牌会被显式记录）。
    /// </summary>
    public sealed class V06Engine
    {
        public const int FlagMaxHp = 20;
        public const int MaxRounds = 12;
        public const int HandLimit = 9;
        public const int MainActionsPerRound = 4;
        public const int PressureStartRound = 7;
        public const int MaxActionPoints = 9;
        public const int DamageCap = 9;

        // ── 可配置项：用于对照实验，默认值 = 文档 V0.6 原文 ──
        /// <summary>远征旗生命（文档 20）。</summary>
        public int CfgFlagHp = FlagMaxHp;
        /// <summary>战场压力起始轮（文档第 7 轮）。</summary>
        public int CfgPressureStart = PressureStartRound;
        /// <summary>
        /// 压力曲线：0 = 文档线性（1,2,3…）；1 = 半速（1,1,2,2,3,3…）。
        /// 默认改为 1：文档线性曲线在第 7–12 轮累计 1+2+3+4+5+6=21 点，
        /// 已经超过 20 点旗生命，造成"打到第 12 轮必然双方同时归零"，
        /// 文档自己的"第 12 轮比旗血"永远走不到（128 局实测上限收尾 0 局）。
        /// 半速曲线累计 12 点，比较规则重新生效（10 局实测平局 0、上限收尾 3 局）。
        /// </summary>
        public int CfgPressureMode = 1;
        /// <summary>每次击败敌方单位，敌方旗额外受到的伤害（0 = 文档原样，无此通道）。</summary>
        public int CfgKillFlagDamage = 0;

        /// <summary>伤害归因：每方 [直击旗, 战场压力, 疲劳]。</summary>
        public int[,] DamageLog = new int[2, 3];
        public const int DmgCombat = 0;
        public const int DmgPressure = 1;
        public const int DmgFatigue = 2;

        public V06Player[] Players = new V06Player[2];
        public int Round = 1;
        public int Initiative;          // 本轮先手方
        public int Active;              // 当前行动方
        public bool Finished;
        public int Winner = -1;         // -1 未定，0/1 胜方，-2 平局
        public string FinishReason = string.Empty;
        public int ConsecutivePasses;
        public V06Action PendingAttack;   // 非 null 表示在响应窗口
        public int PendingAttackerUid;
        public int PendingDamage;         // 已算好的基础伤害，用于响应修正
        public bool PendingVsUnit;
        public int PendingTargetUid;
        public bool PendingCancelled;    // X03 抢先脱身：本次攻击取消
        public int PendingReflect;       // X05 镜盾反射
        public List<string> Log = new List<string>();
        public int RngState;

        /// <summary>本层尚未实现的卡牌效果（用于诚实报告覆盖率）。</summary>
        public static readonly HashSet<string> Unimplemented = new HashSet<string>();
        private static bool _unimplementedInit;

        // ───────────────────────── 构造函数与卡组 ─────────────────────────

        public static List<string> BuildDeck(V06Precon p)
        {
            return V06DeckLibrary.BuildDeck(p);
        }

        public bool TryStart(int seed, int preconA, int preconB, out string error)
        {
            error = string.Empty;
            if (!_unimplementedInit) { V06Effects.RegisterAll(); _unimplementedInit = true; }
            if (V06DeckLibrary.Count == 0) { error = "卡池未载入"; return false; }
            if (preconA < 0 || preconA >= V06DeckLibrary.Count) { error = "预组 A 越界"; return false; }
            if (preconB < 0 || preconB >= V06DeckLibrary.Count) { error = "预组 B 越界"; return false; }
            return StartWith(seed, V06DeckLibrary.Get(preconA), V06DeckLibrary.Get(preconB), out error);
        }

        /// <summary>用"自定义卡组"（V06Precon 对象，例如玩家自己组的牌）开局。</summary>
        public bool TryStartDeck(int seed, V06Precon deckA, V06Precon deckB, out string error)
        {
            if (!_unimplementedInit) { V06Effects.RegisterAll(); _unimplementedInit = true; }
            if (deckA == null || deckB == null) { error = "卡组为空"; return false; }
            return StartWith(seed, deckA, deckB, out error);
        }

        private bool StartWith(int seed, V06Precon preA, V06Precon preB, out string error)
        {
            error = string.Empty;
            RngState = (seed * 1103515245 + 12345) & 0x7fffffff;
            Players[0] = NewPlayer(0, preA, out error);
            if (error.Length > 0) { return false; }
            Players[1] = NewPlayer(1, preB, out error);
            if (error.Length > 0) { return false; }

            Round = 1;
            Initiative = Rand(2);
            Active = Initiative;
            Finished = false;
            Winner = -1;
            FinishReason = string.Empty;
            Log.Clear();

            BeginRoundInitial();
            Log.Add("开局：先手方 " + Initiative + "，第 1 轮");
            return true;
        }

        private V06Player NewPlayer(int camp, V06Precon precon, out string error)
        {
            error = string.Empty;
            V06Player p = new V06Player();
            p.Camp = camp;
            p.NextUid = 1000 * (camp + 1);
            p.FlagHp = CfgFlagHp;
            List<string> deck = BuildDeck(precon);

            // 领航：不高于 3 费的单位，移出牌库加入起手
            string vanguard = precon.Vanguard;
            V06Card vc = V06Catalog.ById(vanguard);
            if (vc == null || vc.Category != V06Category.Unit) { error = "主将非法：" + vanguard; return p; }
            if (vc.Cost > 3) { error = "主将费用超过 3：" + vanguard; return p; }
            int idx = deck.IndexOf(vanguard);
            if (idx < 0) { error = "主将不在牌库：" + vanguard; return p; }
            deck.RemoveAt(idx);
            deck.Add(vanguard);            // 领航本身也回到牌库参与洗牌，再单独抽到起手
            p.VanguardId = vanguard;

            Shuffle(deck);
            // 先手已从牌库取走领航（保证它一定在起手），再抽 4 张
            List<string> rest = new List<string>();
            bool tookVanguard = false;
            for (int i = 0; i < deck.Count; i++)
            {
                if (!tookVanguard && deck[i] == vanguard) { p.Hand.Add(deck[i]); tookVanguard = true; }
                else { rest.Add(deck[i]); }
            }
            if (!tookVanguard) { error = "领航取回失败"; return p; }
            p.Deck = rest;
            for (int i = 0; i < 4 && p.Deck.Count > 0; i++)
            {
                p.Hand.Add(p.Deck[0]);
                p.Deck.RemoveAt(0);
            }
            p.ActionPoints = 0;
            // 预备：3 张不同晋阶 + 3 张不同支援，全部公开，每张每局只能调用一次
            for (int i = 0; i < precon.Upgrades.Length; i++) { p.Reserve.Add(precon.Upgrades[i]); }
            for (int i = 0; i < precon.Supports.Length; i++) { p.Reserve.Add(precon.Supports[i]); }
            return p;
        }

        private int Rand(int n)
        {
            RngState = (RngState * 1103515245 + 12345) & 0x7fffffff;
            return (RngState >> 8) % n;
        }

        private void Shuffle(List<string> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Rand(i + 1);
                string t = deck[i]; deck[i] = deck[j]; deck[j] = t;
            }
        }

        // ───────────────────────── 轮次推进 ─────────────────────────

        private void BeginRoundInitial()
        {
            for (int c = 0; c < 2; c++)
            {
                Players[c].MainActionsUsed = 0;
                // 第 1 轮同样发行动点：min(轮数+2, 7)，没有上轮结转。
                // 之前漏了这一步，第 1 轮行动点为 0，双方都出不了牌（实机对局暴露）。
                Players[c].ActionPoints = Math.Min(MaxActionPoints, Math.Min(Round + 2, 7));
            }
            Active = Initiative;
            ConsecutivePasses = 0;
        }

        /// <summary>进入下一轮：结算轮末，然后开始新一轮。</summary>
        public void AdvanceRound()
        {
            EndRound();
            if (Finished) { return; }
            Round++;
            if (Round > MaxRounds)
            {
                Judge();
                return;
            }
            ClearRoundTemporary();
            for (int c = 0; c < 2; c++)
            {
                V06Player p = Players[c];
                p.MainActionsUsed = 0;
                p.SwappedThisRound = 0;
                p.PowerStrikeUsed = false;
                p.CoverUsed = false;
                p.ResponseUsed = 0;
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = p.At(s);
                    if (u == null) { continue; }
                    u.AttackedThisRound = false;
                    u.DeployedThisRound = false;
                    u.DrainedThisRound = false;
                    u.SneakActive = false;
                }
                // 行动点：min(轮数+2, 7) + 最多 2 点上轮剩余，上限 9
                int carry = Math.Min(2, p.ActionPoints);
                p.ActionPoints = Math.Min(MaxActionPoints, Math.Min(Round + 2, 7) + carry);
            }
            Initiative = 1 - Initiative;
            Active = Initiative;
            ConsecutivePasses = 0;

            // 常规抽牌：第 2 轮起各抽 2
            Draw(0, 2);
            if (Finished) { return; }
            Draw(1, 2);
            if (Finished) { return; }
            Log.Add("进入第 " + Round + " 轮，先手 " + Initiative);
        }

        private void ClearRoundTemporary()
        {
            for (int c = 0; c < 2; c++)
            {
                V06Player p = Players[c];
                p.FrontReduceThisRound = 0;
                p.FirstHealUsed = false;
                p.FirstDrainUsed = false;
                p.FirstAssistUsed = false;
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = p.At(s);
                    if (u == null) { continue; }
                    u.TempAtk = 0;
                    u.ReduceThisRound = 0;
                    u.OverloadThisRound = false;
                    u.DragonFearThisRound = false;
                }
            }
        }

        private void EndRound()
        {
            // 轮末：再生治疗、本轮末伤害、临场压力
            for (int c = 0; c < 2; c++)
            {
                V06Player p = Players[c];
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = p.At(s);
                    if (u == null) { continue; }
                    if (u.Has("再生")) { Heal(u, 1); }
                    if (u.OverloadThisRound) { EffectDamage(u, 1, "过载"); }
                    if (u.SealId == "A06") { EffectDamage(u, 1, "狂战符文"); }
                }
            }
            CleanupDeaths();
            if (Finished) { return; }
            // V0.7 兽群成长：轮末在场 +1，到达阈值即进化（或终极形态永久 +1/+1）
            for (int c = 0; c < 2; c++)
            {
                V06Player p = Players[c];
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = p.Slots[s];   // 直接遍历 Slots：At() 会过滤掉死掉的，成长不看尸体
                    if (u == null || u.CurHp <= 0) { continue; }
                    GrowthStep(u);
                }
            }
            if (Round >= CfgPressureStart)
            {
                int step = Round - CfgPressureStart + 1;
                int dmg = CfgPressureMode == 1 ? (step + 1) / 2 : step;
                Players[0].FlagHp -= dmg;
                Players[1].FlagHp -= dmg;
                DamageLog[0, DmgPressure] += dmg;
                DamageLog[1, DmgPressure] += dmg;
                Log.Add("第 " + Round + " 轮战场压力：双方旗各 −" + dmg);
                CheckWinner();
            }
        }

        private void Judge()
        {
            Finished = true;
            if (Players[0].FlagHp > Players[1].FlagHp) { Winner = 0; }
            else if (Players[1].FlagHp > Players[0].FlagHp) { Winner = 1; }
            else { Winner = -2; }
            FinishReason = "第 " + MaxRounds + " 轮上限：旗 " + Players[0].FlagHp + " : " + Players[1].FlagHp
                + (Winner == -2 ? "（平局）" : "，胜方 " + Winner);
        }

        private void CheckWinner()
        {
            if (Finished) { return; }
            int a = Players[0].FlagHp;
            int b = Players[1].FlagHp;
            if (a <= 0 && b <= 0) { Finished = true; Winner = -2; FinishReason = "双方旗同时归零，平局"; }
            else if (a <= 0) { Finished = true; Winner = 1; FinishReason = "甲方旗归零"; }
            else if (b <= 0) { Finished = true; Winner = 0; FinishReason = "乙方旗归零"; }
        }

        // ───────────────────────── 抽牌与伤害 ─────────────────────────

        public void Draw(int camp, int n)
        {
            V06Player p = Players[camp];
            for (int i = 0; i < n; i++)
            {
                if (p.Deck.Count == 0)
                {
                    p.FlagHp -= 1;
                    DamageLog[camp, DmgFatigue] += 1;
                    Log.Add("疲劳：方 " + camp + " 牌库空，旗 −1");
                    CheckWinner();
                    if (Finished) { return; }
                    continue;
                }
                p.Hand.Add(p.Deck[0]);
                p.Deck.RemoveAt(0);
            }
            while (p.Hand.Count > HandLimit)
            {
                // 超出上限时由持有者选择；AI/自动对局按“弃最贵的无用牌”简化
                int worst = 0;
                int worstCost = -1;
                for (int i = 0; i < p.Hand.Count; i++)
                {
                    V06Card c = V06Catalog.ById(p.Hand[i]);
                    int cost = c == null ? 0 : c.Cost;
                    if (cost > worstCost) { worstCost = cost; worst = i; }
                }
                p.Discard.Add(p.Hand[worst]);
                p.Hand.RemoveAt(worst);
            }
        }

        public void Heal(V06Unit u, int amount)
        {
            if (u == null) { return; }
            // 战况 F01 远征营盘：己方每轮第一次治疗单位时，治疗量 +1
            V06Player owner = Players[u.Camp];
            if (owner.BattlefieldId == "F01" && !owner.FirstHealUsed)
            {
                owner.FirstHealUsed = true;
                amount += 1;
                Log.Add("战况 远征营盘：本次治疗 +1");
            }
            u.Damage = Math.Max(0, u.Damage - amount);
            if (u.Damage < 0) { u.Damage = 0; }
        }

        /// <summary>效果伤害：不触发反击，不受坚壁影响（除非卡文另写）。</summary>
        public void EffectDamage(V06Unit u, int amount, string source)
        {
            if (u == null) { return; }
            u.Damage += amount;
            Log.Add(source + " 对 " + u.Name + " 造成 " + amount + " 点效果伤害");
            if (u.CurHp <= 0) { Kill(u); }
        }

        private void Kill(V06Unit u)
        {
            if (u == null) { return; }
            V06Player owner = Players[u.Camp];
            Log.Add(u.Name + " 被击败");
            // 装备与附印一起弃置
            if (!string.IsNullOrEmpty(u.EquipId)) { owner.Discard.Add(u.EquipId); }
            if (!string.IsNullOrEmpty(u.SealId)) { owner.Discard.Add(u.SealId); }
            if (!u.IsToken) { owner.Discard.Add(u.CardId); }
            string kw = u.Has("余烬") ? "余烬" : string.Empty;
            int uid = u.Uid;
            int camp = u.Camp;
            u.Slot = V06Slot.None;
            for (int s = 0; s < V06Slot.Total; s++)
            {
                if (owner.Slots[s] != null && owner.Slots[s].Uid == uid) { owner.Slots[s] = null; }
            }
            if (CfgKillFlagDamage > 0)
            {
                V06Player foe2 = Players[1 - camp];
                foe2.FlagHp -= CfgKillFlagDamage;
                DamageLog[1 - camp, DmgCombat] += CfgKillFlagDamage;
                Log.Add("击败转化：方 " + (1 - camp) + " 旗 −" + CfgKillFlagDamage);
            }
            if (u.Has("机核")) { Draw(owner.Camp, 1); Log.Add("机核：控制者抽 1 张"); }
            if (kw == "余烬")
            {
                // 余烬：对控制者选择的一个敌单位造成 1 点效果伤害
                V06Player foe = Players[1 - camp];
                List<V06Unit> targets = Targetable(foe);
                if (targets.Count > 0)
                {
                    V06Unit t = targets[Rand(targets.Count)];
                    EffectDamage(t, 1, "余烬");
                }
            }
        }

        public void CleanupDeaths()
        {
            // 注意：必须直接遍历 Slots，不能用 At()——At() 会把“生命 ≤0 的尸体”过滤掉，
            // 导致这些单位既没进弃牌区、也没触发余烬，卡牌凭空消失（由不变量自检发现）。
            for (int c = 0; c < 2; c++)
            {
                V06Player p = Players[c];
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = p.Slots[s];
                    if (u != null && u.CurHp <= 0) { Kill(u); }
                }
            }
        }

        // ───────────────────── V0.7 兽群：成长 / 进化 / 融合 ─────────────────────

        /// <summary>轮末成长一步：到阈值就进化；已是终极形态则永久 +1/+1（每只每局一次）。</summary>
        private void GrowthStep(V06Unit u)
        {
            int need = V06Expansion.GrowThreshold(u.CardId);
            if (need <= 0) { return; }
            if (u.Growth >= need) { return; }
            u.Growth++;
            if (u.Growth < need) { return; }
            string next = V06Expansion.EvolutionOf(u.CardId);
            if (!string.IsNullOrEmpty(next))
            {
                Evolve(u, next);
                return;
            }
            if (!u.GrowthMaxed)
            {
                u.GrowthMaxed = true;
                u.AtkPerm += 1;
                u.HpPerm += 1;
                Log.Add(u.Name + " 成长圆满：永久 +1/+1（" + u.AtkBaseNow + "/" + u.MaxHp + "）");
            }
        }

        /// <summary>
        /// 就地进化：保留 Uid / 槽位 / 已受伤害 / 装备 / 附印，攻血与关键词按新形态叠加。
        /// 进化不触发新形态的入场效果；已受伤害不会致死（最多降到 1 点生命）。
        /// </summary>
        private void Evolve(V06Unit u, string nextId)
        {
            V06Card c = V06Catalog.ById(nextId);
            if (c == null) { return; }
            string oldName = u.Name;
            u.CardId = nextId;
            u.Name = c.Name;
            u.AtkBase = c.Atk;
            u.HpBase = c.Hp;
            u.TagText = c.Tags;
            for (int i = 0; i < c.Keywords.Length; i++) { u.AddKw(c.Keywords[i]); }
            if (u.Damage >= u.MaxHp) { u.Damage = u.MaxHp - 1; if (u.Damage < 0) { u.Damage = 0; } }
            u.Growth = 0;
            u.GrowthMaxed = false;
            Log.Add("进化：" + oldName + " → " + c.Name + "（" + u.AtkBaseNow + "/" + u.CurHp + "）");
        }

        /// <summary>
        /// 融合：把两只指定兽合成一只融合体。规则（V0.7 第一版实测后调整）：
        ///   · 消耗 1 次主行动，**不消耗行动点** —— 否则 2 只换 1 只在节奏上永远是亏的（24 局实测每局 0.04 次）；
        ///   · 融合体登场当轮即可主动攻击（不算作"本轮登场"，所以不必有突袭）；
        ///   · 继承素材已受伤害之和，最多降到 1 点生命；素材卡移出本局。
        /// </summary>
        private bool DoFuse(int camp, V06Action a, out string error)
        {
            error = string.Empty;
            V06Player p = Players[camp];
            V06Unit ua = p.ByUid(a.Uid);
            V06Unit ub = p.ByUid(a.TargetUid);
            if (ua == null || ub == null) { error = "融合素材不在场"; return false; }
            if (ua.Uid == ub.Uid) { error = "素材不能是同一只"; return false; }
            string res = V06Expansion.FusionResult(ua.CardId, ub.CardId);
            if (res == null) { error = "这两只不能融合"; return false; }
            V06Card rc = V06Catalog.ById(res);
            if (rc == null) { error = "融合结果不在卡池"; return false; }
            if (!MainActionsLeft(camp)) { error = "本轮主行动已用完"; return false; }

            int dmgSum = ua.Damage + ub.Damage;
            // 落位：优先两者中靠前的那一格
            int slot = V06Slot.IsFront(ua.Slot) ? ua.Slot : (V06Slot.IsFront(ub.Slot) ? ub.Slot : ub.Slot);
            string names = ua.Name + " + " + ub.Name;
            string ids = ua.CardId + "+" + ub.CardId;

            // 素材离场：卡移出本局（不回收），装备与附印弃置
            RemoveForFusion(p, ua);
            RemoveForFusion(p, ub);

            V06Unit nu = MakeUnit(p, rc, res, slot, true);   // 融合体视为衍生物：离场不进弃牌区
            nu.DeployedThisRound = false;                    // 登场即可攻击
            // 融合仪式加成：融合体额外 +1/+1。
            // 不加这一条时，2 只换 1 只在"身材总和"上永远≤素材之和，AI 与玩家都不会做
            // （第一版实测 24 局只发生 4 次），融合就变成了摆设。
            nu.AtkPerm += 1;
            nu.HpPerm += 1;
            nu.Damage = dmgSum;
            if (nu.Damage >= nu.MaxHp) { nu.Damage = nu.MaxHp - 1; }
            if (nu.Damage < 0) { nu.Damage = 0; }
            nu.FusedFrom = ids;
            p.Slots[slot] = nu;

            p.MainActionsUsed++;
            Log.Add("方 " + camp + " 融合：" + names + " → " + rc.Name
                    + "（" + nu.AtkBaseNow + "/" + nu.CurHp + "，承伤 " + nu.Damage + "）");
            CleanupDeaths();
            if (Finished) { return true; }
            AfterAction();
            return true;
        }

        private void RemoveForFusion(V06Player p, V06Unit u)
        {
            if (!string.IsNullOrEmpty(u.EquipId)) { p.Discard.Add(u.EquipId); }
            if (!string.IsNullOrEmpty(u.SealId)) { p.Discard.Add(u.SealId); }
            if (!u.IsToken) { p.Exile.Add(u.CardId); }   // 素材卡移出本局，保证 30 张守恒
            int slot = u.Slot;
            u.Slot = V06Slot.None;
            if (slot >= 0 && slot < V06Slot.Total && p.Slots[slot] != null && p.Slots[slot].Uid == u.Uid)
            {
                p.Slots[slot] = null;
            }
            for (int s = 0; s < V06Slot.Total; s++)
            {
                if (p.Slots[s] != null && p.Slots[s].Uid == u.Uid) { p.Slots[s] = null; }
            }
        }

        // ───────────────────────── 关键词与目标 ─────────────────────────

        /// <summary>可被敌方单体指定的单位：潜行单位不算。</summary>
        public List<V06Unit> Targetable(V06Player side)
        {
            List<V06Unit> r = new List<V06Unit>();
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = side.At(s);
                if (u == null) { continue; }
                if (u.SneakActive) { continue; }
                r.Add(u);
            }
            return r;
        }

        public bool EnemyFrontEmpty(int camp)
        {
            V06Player foe = Players[1 - camp];
            for (int s = 0; s < V06Slot.FrontCount; s++) { if (foe.At(s) != null) { return false; } }
            return true;
        }

        /// <summary>攻击者可选的敌方单位目标（已应用护卫强制、潜行、远射/飞行射程）。</summary>
        public List<V06Unit> AttackTargets(V06Unit atk)
        {
            List<V06Unit> r = new List<V06Unit>();
            if (atk == null) { return r; }
            V06Player foe = Players[1 - atk.Camp];
            bool ranged = atk.Has("远射");
            bool flying = atk.Has("飞行");
            bool pierce = atk.Has("穿透");

            // 护卫优先：前排存在可指定护卫时必须打护卫
            List<V06Unit> guards = new List<V06Unit>();
            for (int s = 0; s < V06Slot.FrontCount; s++)
            {
                V06Unit u = foe.At(s);
                if (u != null && u.Has("护卫") && !u.SneakActive) { guards.Add(u); }
            }
            if (guards.Count > 0 && !pierce)
            {
                return guards;
            }

            if (ranged)
            {
                // 远射：任意排任意排单位，仍受护卫约束（上面已处理）
                return Targetable(foe);
            }
            // 普通与飞行都只能打敌前排
            for (int s = 0; s < V06Slot.FrontCount; s++)
            {
                V06Unit u = foe.At(s);
                if (u != null && !u.SneakActive) { r.Add(u); }
            }
            return r;
        }

        public bool CanAttackFrom(V06Unit atk)
        {
            if (atk == null || !atk.Alive) { return false; }
            if (atk.AttackedThisRound) { return false; }
            if (atk.DeployedThisRound && !atk.Has("突袭")) { return false; }
            if (V06Slot.IsFront(atk.Slot)) { return true; }
            return atk.Has("远射") || atk.Has("飞行");
        }

        public bool CanAttackFlag(V06Unit atk)
        {
            if (!CanAttackFrom(atk)) { return false; }
            return EnemyFrontEmpty(atk.Camp);
        }

        /// <summary>攻击值的最终数值（含关键词与强攻），限制 0–9。</summary>
        public int FinalAttack(V06Unit atk, bool powerStrike)
        {
            int v = atk.AtkBaseNow;
            if (powerStrike) { v += 1; }
            if (atk.Has("夜行") && (Round % 2 == 1)) { v += 1; }
            if (atk.Has("协攻") && !atk.AttackedThisRound)
            {
                V06Player p = Players[atk.Camp];
                int front = 0;
                for (int s = 0; s < V06Slot.FrontCount; s++) { if (p.At(s) != null) { front++; } }
                if (front >= 2)
                {
                    v += 1;
                    // 战况 F06 荒原战鼓：己方每轮第一次触发协攻时，另给该次攻击 +1
                    if (p.BattlefieldId == "F06" && !p.FirstAssistUsed)
                    {
                        p.FirstAssistUsed = true;
                        v += 1;
                    }
                }
            }
            // 战况 F05 群龙回翔：己方飞行单位攻击 +1
            if (atk.Has("飞行") && Players[atk.Camp].BattlefieldId == "F05") { v += 1; }
            // V0.7 应援光环：己方"其他"单位主动攻击 +1（多张不叠加，最多 +1）
            V06Player cheerSide = Players[atk.Camp];
            for (int cs = 0; cs < V06Slot.Total; cs++)
            {
                V06Unit cheer = cheerSide.At(cs);
                if (cheer == null || cheer.Uid == atk.Uid) { continue; }
                if (cheer.Has("应援")) { v += 1; break; }
            }
            if (atk.OverloadThisRound) { v += 2; }
            if (atk.DragonFearThisRound) { v -= 1; }
            p_Clamp(ref v);
            return v;
        }

        private static void p_Clamp(ref int v)
        {
            if (v > DamageCap) { v = DamageCap; }
            if (v < 0) { v = 0; }
        }

        /// <summary>攻击伤害的关键词/卡牌修正（坚壁、掩护、响应、本轮减伤）。</summary>
        public int ModifyAttackDamage(V06Unit target, int raw, bool applyGuard, int flatReduce)
        {
            int dmg = raw;
            if (applyGuard && target != null)
            {
                bool guard = target.Has("坚壁");
                // 战况 F03 工坊防线：己方构装单位获得坚壁（不与已有坚壁叠加）
                if (!guard && Players[target.Camp].BattlefieldId == "F03"
                    && V06Effects.IsConstruct(target)) { guard = true; }
                if (guard) { dmg -= 1; }
            }
            dmg -= flatReduce;
            if (target != null)
            {
                dmg -= target.ReduceThisRound;
                if (V06Slot.IsFront(target.Slot)) { dmg -= Players[target.Camp].FrontReduceThisRound; }
                // 战况 F02 树冠战场：己方后排单位受到的主动攻击伤害减 1
                if (V06Slot.IsBack(target.Slot) && Players[target.Camp].BattlefieldId == "F02") { dmg -= 1; }
            }
            if (dmg < 0) { dmg = 0; }
            return dmg;
        }

        // ───────────────────────── 合法行动枚举 ─────────────────────────

        public bool MainActionsLeft(int camp)
        {
            return Players[camp].MainActionsUsed < MainActionsPerRound;
        }

        public List<V06Action> LegalActions()
        {
            List<V06Action> list = new List<V06Action>();
            if (Finished) { return list; }
            if (PendingAttack != null) { return LegalResponses(); }
            int camp = Active;
            V06Player p = Players[camp];

            if (!MainActionsLeft(camp))
            {
                list.Add(new V06Action { Kind = V06ActionKind.Pass });
                return list;
            }

            // 部署
            for (int i = 0; i < p.Hand.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Hand[i]);
                if (c == null || c.Category != V06Category.Unit) { continue; }
                if (c.Cost < 0 || c.Cost > p.ActionPoints) { continue; }
                if (!p.AnyEmptySlot()) { continue; }
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    if (p.At(s) != null) { continue; }
                    list.Add(new V06Action { Kind = V06ActionKind.Deploy, CardId = c.Id, Slot = s });
                }
            }

            // 换位（每轮一次，免费，不消耗主行动）
            if (p.SwappedThisRound == 0)
            {
                List<V06Unit> mine = p.Units();
                for (int a = 0; a < mine.Count; a++)
                {
                    for (int b = a + 1; b < mine.Count; b++)
                    {
                        V06Unit ua = mine[a];
                        V06Unit ub = mine[b];
                        if (V06Slot.IsFront(ua.Slot) == V06Slot.IsFront(ub.Slot))
                        {
                            // 同排：需目标格为空；两个已占格不能互换（规则只允许移入空格或前后交换）
                            continue;
                        }
                        list.Add(new V06Action { Kind = V06ActionKind.Swap, Uid = ua.Uid, TargetUid = ub.Uid });
                    }
                }
                // 移入空格（含前后排任一空格）
                for (int a = 0; a < mine.Count; a++)
                {
                    for (int s = 0; s < V06Slot.Total; s++)
                    {
                        if (p.At(s) != null) { continue; }
                        // 只允许“另一排空格”，避免同排横移被当成免费位移
                        if (V06Slot.IsFront(mine[a].Slot) == V06Slot.IsFront(s)) { continue; }
                        list.Add(new V06Action { Kind = V06ActionKind.Swap, Uid = mine[a].Uid, TargetSlot = s });
                    }
                }
            }

            // 攻击
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = p.At(s);
                if (u == null || !CanAttackFrom(u)) { continue; }
                List<V06Unit> tg = AttackTargets(u);
                for (int t = 0; t < tg.Count; t++)
                {
                    list.Add(new V06Action { Kind = V06ActionKind.Attack, Uid = u.Uid, TargetUid = tg[t].Uid });
                    if (!p.PowerStrikeUsed && p.ActionPoints >= 1)
                    {
                        list.Add(new V06Action { Kind = V06ActionKind.Attack, Uid = u.Uid, TargetUid = tg[t].Uid, PowerStrike = true });
                    }
                }
                // 敌前排为空时才能打旗（远射/飞行同样如此）
                if (tg.Count == 0 && CanAttackFlag(u))
                {
                    list.Add(new V06Action { Kind = V06ActionKind.Attack, Uid = u.Uid, ToFlag = true });
                    if (!p.PowerStrikeUsed && p.ActionPoints >= 1)
                    {
                        list.Add(new V06Action { Kind = V06ActionKind.Attack, Uid = u.Uid, ToFlag = true, PowerStrike = true });
                    }
                }
            }

            // 非单位牌：交由效果层判定可实现性
            for (int i = 0; i < p.Hand.Count; i++)
            {
                V06Card c = V06Catalog.ById(p.Hand[i]);
                if (c == null || c.Category == V06Category.Unit) { continue; }
                if (!V06Effects.CanPlay(this, camp, c)) { continue; }
                V06Effects.EnumeratePlays(this, camp, c, list);
            }

            // 预备区：3 张晋阶 + 3 张支援，每张每局一次，同样消耗 1 次主行动
            for (int i = 0; i < p.Reserve.Count; i++)
            {
                string rid = p.Reserve[i];
                if (p.ReserveUsed.Contains(rid)) { continue; }
                V06Card rc = V06Catalog.ById(rid);
                if (rc == null) { continue; }
                if (rc.Cost < 0 || rc.Cost > p.ActionPoints) { continue; }
                if (!V06Effects.CanPlay(this, camp, rc)) { continue; }
                List<V06Action> tmp = new List<V06Action>();
                V06Effects.EnumeratePlays(this, camp, rc, tmp);
                for (int k = 0; k < tmp.Count; k++)
                {
                    tmp[k].FromReserve = true;
                    list.Add(tmp[k]);
                }
            }

            // V0.7 融合（兽群）
            AddFuseActions(list, p);

            list.Add(new V06Action { Kind = V06ActionKind.Pass });
            return list;
        }

        /// <summary>V0.7 融合：枚举场上所有可融合的两只组合（消耗 1 次主行动，不消耗行动点）。</summary>
        private void AddFuseActions(List<V06Action> list, V06Player p)
        {
            List<V06Unit> mine = p.Units();
            for (int a = 0; a < mine.Count; a++)
            {
                for (int b = a + 1; b < mine.Count; b++)
                {
                    string res = V06Expansion.FusionResult(mine[a].CardId, mine[b].CardId);
                    if (res == null) { continue; }
                    list.Add(new V06Action
                    {
                        Kind = V06ActionKind.Fuse,
                        Uid = mine[a].Uid,
                        TargetUid = mine[b].Uid,
                        CardId = res
                    });
                }
            }
        }

        private List<V06Action> LegalResponses()
        {
            List<V06Action> list = new List<V06Action>();
            // 攻击宣布后 Active 已经切到"回应方"，所以回应方就是 Active。
            // （这里原先写成 1 - Active，等于把攻击者的手牌当成响应方手牌，响应牌永远出不来）
            int defender = Active;
            V06Player d = Players[defender];
            // 响应每方每轮 1 张；掩护每方每轮 1 次；两者不可叠加
            if (d.ResponseUsed == 0)
            {
                for (int i = 0; i < d.Hand.Count; i++)
                {
                    V06Card c = V06Catalog.ById(d.Hand[i]);
                    if (c == null || c.Category != V06Category.Response) { continue; }
                    if (c.Cost > d.ActionPoints) { continue; }
                    if (!V06Effects.ResponseLegal(this, defender, c, PendingAttack)) { continue; }
                    list.Add(new V06Action { Kind = V06ActionKind.Respond, CardId = c.Id });
                }
                if (!d.CoverUsed && d.ActionPoints >= 1 && PendingVsUnit)
                {
                    list.Add(new V06Action { Kind = V06ActionKind.Cover });
                }
            }
            list.Add(new V06Action { Kind = V06ActionKind.Decline });
            return list;
        }

        // ───────────────────────── 执行 ─────────────────────────

        public bool Apply(V06Action a, out string error)
        {
            error = string.Empty;
            if (a == null) { error = "空行动"; return false; }
            if (Finished) { error = "对局已结束"; return false; }
            if (PendingAttack != null) { return ApplyResponse(a, out error); }

            int camp = Active;
            switch (a.Kind)
            {
                case V06ActionKind.Deploy: return DoDeploy(camp, a, out error);
                case V06ActionKind.Attack: return DoDeclareAttack(camp, a, out error);
                case V06ActionKind.PlayCard: return V06Effects.Play(this, camp, a, out error);
                case V06ActionKind.Swap: return DoSwap(camp, a, out error);
                case V06ActionKind.Fuse: return DoFuse(camp, a, out error);
                case V06ActionKind.Pass: return DoPass(out error);
            }
            error = "未知行动";
            return false;
        }

        private bool DoDeploy(int camp, V06Action a, out string error)
        {
            error = string.Empty;
            V06Player p = Players[camp];
            V06Card c = V06Catalog.ById(a.CardId);
            if (c == null || c.Category != V06Category.Unit) { error = "不是单位"; return false; }
            if (p.Hand.IndexOf(a.CardId) < 0) { error = "不在手牌"; return false; }
            if (c.Cost > p.ActionPoints) { error = "行动点不足"; return false; }
            if (a.Slot < 0 || a.Slot >= V06Slot.Total) { error = "非法格位"; return false; }
            if (p.At(a.Slot) != null) { error = "格位已占"; return false; }

            p.Hand.Remove(a.CardId);
            p.ActionPoints -= c.Cost;
            p.MainActionsUsed++;
            V06Unit u = MakeUnit(p, c, a.CardId, a.Slot, false);
            u.DeployedThisRound = true;
            p.Slots[a.Slot] = u;
            Log.Add("方 " + camp + " 部署 " + c.Name + " → " + a.Slot);
            ApplyEnter(p, u);
            CleanupDeaths();
            if (Finished) { return true; }
            AfterAction();
            return true;
        }

        private V06Unit MakeUnit(V06Player p, V06Card c, string cardId, int slot, bool token)
        {
            V06Unit u = new V06Unit();
            u.Uid = p.NextUid++;
            u.Camp = p.Camp;
            u.Slot = slot;
            u.CardId = cardId;
            u.Name = c.Name;
            u.AtkBase = c.Atk;
            u.HpBase = c.Hp;
            u.TagText = c.Tags;
            u.IsToken = token;
            for (int i = 0; i < c.Keywords.Length; i++) { u.AddKw(c.Keywords[i]); }
            if (u.Has("潜行")) { u.SneakActive = true; }
            return u;
        }

        public V06Unit SpawnToken(int camp, string tokenId)
        {
            V06Player p = Players[camp];
            int slot = p.FirstEmptySlot();
            if (slot == V06Slot.None) { return null; }
            V06Card c = V06Catalog.ById(tokenId);
            if (c == null) { return null; }
            V06Unit u = MakeUnit(p, c, tokenId, slot, true);
            u.DeployedThisRound = true;
            p.Slots[slot] = u;
            Log.Add("方 " + camp + " 生成 " + c.Name);
            return u;
        }

        public void ApplyEnter(V06Player owner, V06Unit u)
        {
            // 回响：入场抽 1
            if (u.Has("回响")) { Draw(owner.Camp, 1); }
            V06Effects.OnEnter(this, owner, u);
        }

        private bool DoDeclareAttack(int camp, V06Action a, out string error)
        {
            error = string.Empty;
            V06Player p = Players[camp];
            V06Unit atk = p.ByUid(a.Uid);
            if (atk == null) { error = "攻击者不在场"; return false; }
            if (!CanAttackFrom(atk)) { error = "该单位本轮不能主动攻击"; return false; }
            if (a.PowerStrike)
            {
                if (p.PowerStrikeUsed) { error = "本轮已强攻"; return false; }
                if (p.ActionPoints < 1) { error = "行动点不足"; return false; }
            }
            if (a.ToFlag)
            {
                if (!CanAttackFlag(atk)) { error = "敌前排不为空"; return false; }
            }
            else
            {
                V06Player foe = Players[1 - camp];
                V06Unit target = foe.ByUid(a.TargetUid);
                if (target == null) { error = "目标不存在"; return false; }
                List<V06Unit> legal = AttackTargets(atk);
                bool ok = false;
                for (int i = 0; i < legal.Count; i++) { if (legal[i].Uid == target.Uid) { ok = true; break; } }
                if (!ok) { error = "目标不合法（护卫/射程）"; return false; }
            }
            if (a.PowerStrike) { p.ActionPoints -= 1; p.PowerStrikeUsed = true; }
            p.MainActionsUsed++;
            atk.AttackedThisRound = true;
            if (atk.SneakActive) { atk.SneakActive = false; }   // 潜行到自己首次主动攻击前解除

            int raw = FinalAttack(atk, a.PowerStrike);
            PendingAttack = a;
            PendingAttackerUid = atk.Uid;
            PendingVsUnit = !a.ToFlag;
            PendingTargetUid = a.ToFlag ? -1 : a.TargetUid;
            PendingDamage = raw;
            Active = 1 - camp;      // 进入对手响应窗口
            Log.Add("方 " + camp + " 宣布攻击：" + atk.Name + (a.ToFlag ? " → 旗" : " → 目标") + "，攻击值 " + raw);
            return true;
        }

        private bool ApplyResponse(V06Action a, out string error)
        {
            error = string.Empty;
            int defender = Active;
            int campOfAttacker = 1 - defender;
            V06Player d = Players[defender];
            int flatReduce = 0;
            switch (a.Kind)
            {
                case V06ActionKind.Cover:
                    if (d.CoverUsed || d.ActionPoints < 1) { error = "掩护不可用"; return false; }
                    if (!PendingVsUnit) { error = "掩护不能保护远征旗"; return false; }
                    d.ActionPoints -= 1;
                    d.CoverUsed = true;
                    flatReduce = 1;
                    Log.Add("方 " + defender + " 使用掩护（−1）");
                    break;
                case V06ActionKind.Respond:
                    {
                        V06Card c = V06Catalog.ById(a.CardId);
                        if (c == null) { error = "响应牌不存在"; return false; }
                        if (d.Hand.IndexOf(a.CardId) < 0) { error = "不在手牌"; return false; }
                        if (c.Cost > d.ActionPoints) { error = "行动点不足"; return false; }
                        if (d.ResponseUsed > 0) { error = "本轮已使用响应"; return false; }
                        d.Hand.Remove(a.CardId);
                        d.Discard.Add(a.CardId);
                        d.ActionPoints -= c.Cost;
                        d.ResponseUsed = 1;
                        int red = V06Effects.ResponseDamageReduce(this, defender, c, PendingAttack);
                        flatReduce = red;
                        V06Effects2.AfterResponse(this, defender, c, PendingAttack, campOfAttacker);
                        Log.Add("方 " + defender + " 使用响应 " + c.Name + (red > 0 ? "（−" + red + "）" : ""));
                    }
                    break;
                case V06ActionKind.Decline:
                    break;
                default:
                    error = "响应窗口只能响应/掩护/放弃";
                    return false;
            }
            ResolvePending(defender, flatReduce);
            return true;
        }

        private void ResolvePending(int defender, int flatReduce)
        {
            V06Action a = PendingAttack;
            int attacker = 1 - defender;
            PendingAttack = null;
            Active = attacker;
            V06Player ap = Players[attacker];
            V06Player dp = Players[defender];
            V06Unit atk = ap.ByUid(a.Uid);
            if (atk == null) { PendingCancelled = false; PendingReflect = 0; AfterAction(); return; }

            if (PendingCancelled)
            {
                Log.Add("本次攻击被取消（抢先脱身），行动与费用不退");
                PendingCancelled = false;
                PendingReflect = 0;
                if (atk.Has("驯兽铃") && atk.WatcherBellUsed == 0)
                {
                    atk.WatcherBellUsed = 1;
                    SpawnToken(atk.Camp, "T05");
                }
                AfterAction();
                return;
            }

            if (a.ToFlag)
            {
                int dmg = ModifyAttackDamage(null, PendingDamage, false, flatReduce);
                dp.FlagHp -= dmg;
                DamageLog[defender, DmgCombat] += dmg;
                Log.Add(atk.Name + " 直击旗 −" + dmg);
                if (atk.Has("汲取")) { /* 汲取只对单位造成伤害时触发 */ }
            }
            else
            {
                V06Unit tgt = dp.ByUid(a.TargetUid);
                if (tgt != null)
                {
                    int dmg = ModifyAttackDamage(tgt, PendingDamage, true, flatReduce);
                    tgt.Damage += dmg;
                    Log.Add(atk.Name + " 对 " + tgt.Name + " 造成 " + dmg + " 点攻击伤害（剩余 " + tgt.CurHp + "）");
                    // 汲取：每轮首次主动攻击对敌单位造成至少 1 点伤害后治疗自身 1
                    if (atk.Has("汲取") && !atk.DrainedThisRound && dmg >= 1)
                    {
                        atk.DrainedThisRound = true;
                        int drainHeal = 1;
                        V06Player dragon = Players[atk.Camp];
                        // 战况 F04 血族夜宴：己方每轮第一次成功触发汲取时，额外治疗该单位 1
                        if (dragon.BattlefieldId == "F04" && !dragon.FirstDrainUsed)
                        {
                            dragon.FirstDrainUsed = true;
                            drainHeal += 1;
                        }
                        Heal(atk, drainHeal);
                        Log.Add(atk.Name + " 汲取：自愈 " + drainHeal);
                    }
                    // 反击：非远射主动攻击打到前排单位，或飞行单位在后排被非远射攻击
                    bool ranged = atk.Has("远射");
                    if (!ranged)
                    {
                        bool targetInFront = V06Slot.IsFront(tgt.Slot);
                        bool flyingInBack = V06Slot.IsBack(tgt.Slot) && tgt.Has("飞行");
                        if (targetInFront || flyingInBack)
                        {
                            int counter = tgt.AtkBaseNow;
                            p_Clamp(ref counter);
                            if (tgt.Has("坚壁")) { /* 坚壁只减受到的伤害 */ }
                            atk.Damage += counter;
                            Log.Add(tgt.Name + " 反击 " + counter + "（" + atk.Name + " 剩余 " + atk.CurHp + "）");
                        }
                    }
                    CleanupDeaths();
                }
            }
            // X05 镜盾反射：结算完成后对攻击者造成效果伤害
            if (PendingReflect > 0)
            {
                int refl = PendingReflect;
                PendingReflect = 0;
                V06Unit atkNow = ap.ByUid(a.Uid);
                if (atkNow != null) { EffectDamage(atkNow, refl, "镜盾反射"); }
            }
            // 装备 Q12 驯兽铃：该单位首次主动攻击结束后，若存活且有空格则生成 T05
            V06Unit atkAfter = ap.ByUid(a.Uid);
            if (atkAfter != null && atkAfter.EquipId == "Q12" && atkAfter.WatcherBellUsed == 0)
            {
                atkAfter.WatcherBellUsed = 1;
                SpawnToken(atk.Camp, "T05");
            }
            CheckWinner();
            if (Finished) { return; }
            AfterAction();
        }

        private bool DoSwap(int camp, V06Action a, out string error)
        {
            error = string.Empty;
            V06Player p = Players[camp];
            if (p.SwappedThisRound > 0) { error = "本轮已换位"; return false; }
            V06Unit u = p.ByUid(a.Uid);
            if (u == null) { error = "单位不在场"; return false; }
            if (a.TargetUid >= 0)
            {
                V06Unit other = p.ByUid(a.TargetUid);
                if (other == null) { error = "目标单位不在场"; return false; }
                if (V06Slot.IsFront(u.Slot) == V06Slot.IsFront(other.Slot)) { error = "只能前后排交换"; return false; }
                int sa = u.Slot, sb = other.Slot;
                p.Slots[sa] = other; p.Slots[sb] = u;
                u.Slot = sb; other.Slot = sa;
            }
            else
            {
                if (a.TargetSlot < 0 || a.TargetSlot >= V06Slot.Total) { error = "非法格位"; return false; }
                if (p.At(a.TargetSlot) != null) { error = "目标格已占"; return false; }
                if (V06Slot.IsFront(u.Slot) == V06Slot.IsFront(a.TargetSlot)) { error = "只能移到另一排"; return false; }
                int old = u.Slot;
                p.Slots[old] = null;
                p.Slots[a.TargetSlot] = u;
                u.Slot = a.TargetSlot;
            }
            p.SwappedThisRound = 1;
            Log.Add("方 " + camp + " 换位：" + u.Name);
            // 换位不消耗主行动，但本次窗口仍需出一个主行动或让过
            return true;
        }

        private bool DoPass(out string error)
        {
            error = string.Empty;
            int passer = Active;
            int other = 1 - Active;
            ConsecutivePasses++;
            Log.Add("方 " + passer + " 让过");

            // 双方均无剩余主行动机会，或连续双方让过 → 结束主行动阶段
            bool bothDone = !MainActionsLeft(passer) && !MainActionsLeft(other);
            bool doublePass = ConsecutivePasses >= 2;
            // 一方已用完机会且另一方也让过 → 也结束
            bool oneSideExhausted = (!MainActionsLeft(passer) || !MainActionsLeft(other)) && ConsecutivePasses >= 1
                                    && !MainActionsLeft(other);

            if (bothDone || doublePass || oneSideExhausted)
            {
                AdvanceRound();
                return true;
            }
            Active = other;
            return true;
        }

        private void AfterAction()
        {
            if (Finished) { return; }
            ConsecutivePasses = 0;
            Active = 1 - Active;
            // 若接手方已用完主行动机会，交回
            if (!MainActionsLeft(Active))
            {
                Active = 1 - Active;
                if (!MainActionsLeft(Active)) { AdvanceRound(); }
            }
        }

        public V06Engine Clone()
        {
            V06Engine e = new V06Engine();
            e.Players[0] = Players[0] == null ? null : Players[0].Clone();
            e.Players[1] = Players[1] == null ? null : Players[1].Clone();
            e.Round = Round;
            e.Initiative = Initiative;
            e.Active = Active;
            e.Finished = Finished;
            e.Winner = Winner;
            e.FinishReason = FinishReason;
            e.ConsecutivePasses = ConsecutivePasses;
            e.PendingAttack = PendingAttack;
            e.PendingAttackerUid = PendingAttackerUid;
            e.PendingDamage = PendingDamage;
            e.PendingVsUnit = PendingVsUnit;
            e.PendingTargetUid = PendingTargetUid;
            e.PendingCancelled = PendingCancelled;
            e.PendingReflect = PendingReflect;
            e.RngState = RngState;
            e.CfgFlagHp = CfgFlagHp; e.CfgPressureStart = CfgPressureStart; e.CfgPressureMode = CfgPressureMode; e.CfgKillFlagDamage = CfgKillFlagDamage; e.DamageLog = (int[,])DamageLog.Clone();
            e.Log = new List<string>(Log);
            return e;
        }
    }
}
