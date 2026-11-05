using System;
using System.Collections.Generic;

namespace Jieqiao
{
    public enum PendingKind
    {
        None,
        Cover,
        /// <summary>通用侦察：看牌库顶 3 张选 1。</summary>
        ScoutPick,
        /// <summary>V0.3 §9.1 侦察员入场：玩家选择保持或交换牌库顶 2 张。</summary>
        Reorder,
        /// <summary>V0.3 §9.2 应急调度：玩家从按牌名聚合的候选里选 1 张。</summary>
        SearchPick,
    }

    /// <summary>
    /// 界桥远征 V0.2 的规则引擎。
    ///
    /// 设计约束（来自文档第 10 章「规则层面的取舍」）：
    ///  - 全部判定集中在这里，UI/AI 只消费 LegalActions，不重复实现规则；
    ///  - 结算顺序严格按 6.5 的七步，胜负在整次行动结算完之后才判定；
    ///  - 随机只来自 Rng，同种子必须复现同一局（可重放）。
    /// </summary>
    public sealed class GameEngine
    {
        public const int CampStart = 20;
        public const int HandLimit = 8;
        public const int ActionsPerRound = 3;
        public const int CarryoverMax = 2;
        public const int TideStartsAtRound = 7;
        /// <summary>V0.3 §6.6：强攻从第 4 轮起开放。</summary>
        public const int PowerStrikeStartsAtRound = 4;
        /// <summary>V0.3 §6.6：强攻额外支付 2 补给，本次主动伤害 +1。</summary>
        public const int PowerStrikeCost = 2;
        public const int PowerStrikeBonus = 1;

        public GameState S { get; private set; }
        public List<LogEntry> Log { get; private set; }
        public PendingKind Pending { get; private set; }
        /// <summary>当前挂起的行动（掩护窗口用），供 UI/AI 预览。</summary>
        public GameAction PendingAction
        {
            get { return _pendingAction; }
        }
        /// <summary>侦察揭示的牌（按牌库顶顺序）。</summary>
        public List<CardInstance> PendingScoutCards { get; private set; }
        /// <summary>侦察员调序窗口里被查看的牌（牌库顶 1~2 张，仅本人可见）。</summary>
        public List<CardInstance> PendingReorderCards { get; private set; }
        /// <summary>应急调度窗口的候选牌（按牌名聚合后的代表实体，仅本人可见）。</summary>
        public List<CardInstance> PendingSearchCards { get; private set; }

        private Rng _rng;
        private int _nextUid = 1;
        private GameAction _pendingAction;
        private CardDef _pendingCardDef;
        private int _preCamp0;
        private int _preCamp1;
        private int _firstRoundStarter;
        private bool[] _mulliganDone = new bool[2];

        public GameEngine()
        {
            Log = new List<LogEntry>();
            PendingScoutCards = new List<CardInstance>();
            PendingReorderCards = new List<CardInstance>();
            PendingSearchCards = new List<CardInstance>();
            S = new GameState();
            S.Phase = GamePhase.Setup;
        }

        // ── 开局 ───────────────────────────────────────────────────────────

        public void Start(int seed, string vanguard0 = "guard_shield", string vanguard1 = "guard_shield",
            int precon0 = 0, int precon1 = 0)
        {
            _rng = new Rng(seed);
            _nextUid = 1;
            Log.Clear();
            Pending = PendingKind.None;
            PendingReorderCards.Clear();
            PendingSearchCards.Clear();
            _mulliganDone[0] = false;
            _mulliganDone[1] = false;

            S = new GameState();
            S.Players[0] = NewPlayer(0, "甲", vanguard0, CardCatalog.PreconDeckById(precon0));
            S.Players[1] = NewPlayer(1, "乙", vanguard1, CardCatalog.PreconDeckById(precon1));
            S.Round = 0;
            S.Phase = GamePhase.Mulligan;
            S.Winner = -1;
            S.FinishReason = null;

            _firstRoundStarter = _rng.Next(2);
            AddLog(string.Format("开局：{0}先手（第 1 轮）。公开预备区双方各 6 张已公开。预组：甲「{1}」/ 乙「{2}」。",
                S.Players[_firstRoundStarter].Name, CardCatalog.PreconNames[precon0],
                CardCatalog.PreconNames[precon1]));
            for (int i = 0; i < 2; i++)
            {
                AddLog(string.Format("{0} 起手：{1}（领航：{2}）", S.Players[i].Name,
                    HandText(S.Players[i]), VanguardName(vanguard0, vanguard1, i)));
            }
        }

        /// <summary>V0.3 §3.4：开局前校验构筑，不合法时给出具体原因且不开始对局。</summary>
        public bool TryStart(int seed, int precon0, int precon1, string vanguard0, string vanguard1, out string error)
        {
            error = null;
            string e0, e1;
            if (!CardCatalog.ValidateDeck(CardCatalog.PreconDeckById(precon0), vanguard0, CardCatalog.PreconReserve, out e0))
            {
                error = "甲：" + e0;
                return false;
            }
            if (!CardCatalog.ValidateDeck(CardCatalog.PreconDeckById(precon1), vanguard1, CardCatalog.PreconReserve, out e1))
            {
                error = "乙：" + e1;
                return false;
            }
            Start(seed, vanguard0, vanguard1, precon0, precon1);
            return true;
        }

        private static string VanguardName(string v0, string v1, int player)
        {
            CardDef d = CardCatalog.ById(player == 0 ? v0 : v1);
            return d == null ? "?" : d.Name;
        }

        private PlayerState NewPlayer(int index, string name, string vanguardId, string[] deckIds)
        {
            var p = new PlayerState { Name = name, Index = index, Camp = CampStart };
            for (int i = 0; i < deckIds.Length; i++)
            {
                CardDef d = CardCatalog.ById(deckIds[i]);
                p.Deck.Add(new CardInstance(_nextUid++, d));
            }
            for (int i = 0; i < CardCatalog.PreconReserve.Length; i++)
            {
                p.Reserve.Add(new ReservedCard { Def = CardCatalog.ById(CardCatalog.PreconReserve[i]), Used = false });
            }
            _rng.Shuffle(p.Deck);

            // 领航单位：实体卡直接进手牌，再从剩余 23 张抽 4
            string vid = vanguardId;
            if (!CardCatalog.IsVanguardCandidate(CardCatalog.ById(vid)))
            {
                vid = "guard_shield";
            }
            int idx = p.Deck.FindIndex(c => c.Def.Id == vid);
            if (idx < 0)
            {
                vid = "assault";
                idx = p.Deck.FindIndex(c => c.Def.Id == vid);
            }
            if (idx >= 0)
            {
                CardInstance vanguard = p.Deck[idx];
                p.Deck.RemoveAt(idx);
                p.Hand.Add(vanguard);
                p.VanguardUid = vanguard.Uid;
            }
            DrawCards(p, 4, false);
            return p;
        }

        /// <summary>换牌：至多 2 张随机起手牌，领航单位不能换。返回是否已进入对局。</summary>
        public void ConfirmMulligan(int player, List<int> cardUids)
        {
            if (S.Phase != GamePhase.Mulligan || _mulliganDone[player])
            {
                return;
            }
            PlayerState p = S.Players[player];
            var swap = new List<CardInstance>();
            if (cardUids != null)
            {
                for (int i = 0; i < cardUids.Count && swap.Count < CarryoverMax; i++)
                {
                    int idx = p.Hand.FindIndex(c => c.Uid == cardUids[i]);
                    if (idx < 0)
                    {
                        continue;
                    }
                    CardInstance card = p.Hand[idx];
                    // 领航单位按规定不能换下：它是起手里唯一确定的那张基础单位
                    if (IsVanguard(p, card))
                    {
                        continue;
                    }
                    p.Hand.RemoveAt(idx);
                    swap.Add(card);
                }
            }
            if (swap.Count > 0)
            {
                DrawCards(p, swap.Count, false);
                for (int i = 0; i < swap.Count; i++)
                {
                    p.Deck.Add(swap[i]);
                }
                _rng.Shuffle(p.Deck);
                AddLog(string.Format("{0} 换牌 {1} 张。", p.Name, swap.Count));
            }
            else
            {
                AddLog(string.Format("{0} 不换牌。", p.Name));
            }
            _mulliganDone[player] = true;

            if (_mulliganDone[0] && _mulliganDone[1])
            {
                StartRound(1);
            }
        }

        private bool IsVanguard(PlayerState p, CardInstance card)
        {
            return card.Uid == p.VanguardUid;
        }

        // ── 轮次 ───────────────────────────────────────────────────────────

        public int BaseSupplyForRound(int round)
        {
            if (round <= 1)
            {
                return 3;
            }
            return round == 2 ? 4 : 5;
        }

        private void StartRound(int round)        {
            S.Round = round;
            S.Phase = GamePhase.Playing;
            S.FirstPlayerThisRound = (round % 2 == 1) ? _firstRoundStarter : 1 - _firstRoundStarter;

            for (int i = 0; i < 2; i++)
            {
                PlayerState p = S.Players[i];
                p.Supply = BaseSupplyForRound(round) + p.Carryover;
                p.Carryover = 0;
                p.ActionsLeft = ActionsPerRound;
                p.ActionIndexThisRound = 0;
                p.CoverUsed = false;
                p.RepositionUsed = false;
                p.ScoutUsed = false;
                p.ReserveUsedThisRound = false;
                p.ClearFrontBonusUsed = false;
                // V0.3 §4.1：重置强攻额度与「本轮自动放弃掩护」设置
                p.PowerStrikeUsed = false;
                p.AutoDeclineCover = false;
                for (int u = 0; u < p.Board.Count; u++)
                {
                    p.Board[u].AttackedThisRound = false;
                    p.Board[u].TempAtkMod = 0;
                }
            }

            AddLog(string.Format("── 第 {0} 轮开始：{1}先手。补给 甲{2} / 乙{3}。",
                round, S.Players[S.FirstPlayerThisRound].Name,
                S.Players[0].Supply, S.Players[1].Supply));

            if (round >= 2)
            {
                DrawCards(S.Players[0], 2, true);
                DrawCards(S.Players[1], 2, true);
            }

            S.CurrentPlayer = S.FirstPlayerThisRound;
        }

        private void EndRound()
        {
            // 第 7 轮起轮末界潮
            if (S.Round >= TideStartsAtRound)
            {
                int dmg = S.Round - (TideStartsAtRound - 1);
                _preCamp0 = S.Players[0].Camp;
                _preCamp1 = S.Players[1].Camp;
                S.Players[0].Camp -= dmg;
                S.Players[1].Camp -= dmg;
                S.LossTide[0] += dmg;
                S.LossTide[1] += dmg;
                AddLog(string.Format("界潮：双方各受 {0} 点营地伤害（甲{1}→{2}，乙{3}→{4}）。",
                    dmg, _preCamp0, S.Players[0].Camp, _preCamp1, S.Players[1].Camp));
                CheckWinner(_preCamp0, _preCamp1, "界潮");
                if (S.IsOver)
                {
                    return;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                PlayerState p = S.Players[i];
                p.Carryover = Math.Min(CarryoverMax, Math.Max(0, p.Supply));
            }
            StartRound(S.Round + 1);
        }

        // ── 抽牌与循环 ─────────────────────────────────────────────────────

        private void DrawCards(PlayerState p, int count, bool announce)
        {
            var drawn = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if (p.Deck.Count == 0)
                {
                    if (p.Discard.Count == 0)
                    {
                        break;
                    }
                    p.Deck.AddRange(p.Discard);
                    p.Discard.Clear();
                    _rng.Shuffle(p.Deck);
                    AddLog(string.Format("{0} 牌库耗尽，弃牌区洗成新牌库（{1} 张）。", p.Name, p.Deck.Count));
                }
                CardInstance card = p.Deck[0];
                p.Deck.RemoveAt(0);
                if (p.Hand.Count >= HandLimit)
                {
                    p.Discard.Add(card);
                    drawn.Add(card.Def.Name + "(超上限弃置)");
                }
                else
                {
                    p.Hand.Add(card);
                    drawn.Add(card.Def.Name);
                }
            }
            if (announce && drawn.Count > 0)
            {
                AddLog(string.Format("{0} 抽牌：{1}", p.Name, string.Join("、", drawn.ToArray())));
            }
        }

        private void ShuffleDiscardIntoDeck(PlayerState p)
        {
            if (p.Deck.Count > 0)
            {
                return;
            }
            if (p.Discard.Count == 0)
            {
                return;
            }
            p.Deck.AddRange(p.Discard);
            p.Discard.Clear();
            _rng.Shuffle(p.Deck);
            AddLog(string.Format("{0} 牌库耗尽，弃牌区洗成新牌库（{1} 张）。", p.Name, p.Deck.Count));
        }

        // ── 行动资格 ───────────────────────────────────────────────────────

        /// <summary>单位是否已到达可以行动（攻击/晋阶）的时点。</summary>
        public bool CanAct(UnitState u, PlayerState owner)
        {
            if (u.DeployedRound < S.Round)
            {
                return true;
            }
            return u.DeployedRound == S.Round && owner.ActionIndexThisRound > u.DeployedAtActionIndex;
        }

        public bool CanAttackNow(UnitState u, PlayerState owner)
        {
            if (u.AttackedThisRound || !CanAct(u, owner))
            {
                return false;
            }
            return u.Slot.Front || u.HasRanged;
        }

        // ── 合法行动枚举 ───────────────────────────────────────────────────

        public List<GameAction> LegalActions()
        {
            var list = new List<GameAction>();
            if (S.Phase != GamePhase.Playing || S.IsOver)
            {
                return list;
            }
            int me = S.CurrentPlayer;
            PlayerState p = S.Players[me];
            PlayerState o = S.Opponent(me);
            if (p.ActionsLeft <= 0)
            {
                return list;
            }

            // 部署
            if (p.Board.Count < TotalSlots)
            {
                for (int i = 0; i < p.Hand.Count; i++)
                {
                    CardInstance c = p.Hand[i];
                    if (c.Def.Type != CardType.Unit || c.Def.Cost > p.Supply)
                    {
                        continue;
                    }
                    for (int slot = 0; slot < TotalSlots; slot++)
                    {
                        var s = SlotAt(slot);
                        if (!p.SlotFree(s))
                        {
                            continue;
                        }
                        if (c.Def.Effect == Effect.EntryHealWounded2)
                        {
                            var wounded = WoundedUnits(p, -1);
                            if (wounded.Count == 0)
                            {
                                list.Add(DeployAction(c, s, -1));
                            }
                            else
                            {
                                for (int w = 0; w < wounded.Count; w++)
                                {
                                    list.Add(DeployAction(c, s, wounded[w].Card.Uid));
                                }
                            }
                        }
                        else
                        {
                            list.Add(DeployAction(c, s, -1));
                        }
                    }
                }
            }

            // 战术
            for (int i = 0; i < p.Hand.Count; i++)
            {
                CardInstance c = p.Hand[i];
                if (c.Def.Type != CardType.Tactic || c.Def.Cost > p.Supply)
                {
                    continue;
                }
                switch (c.Def.Effect)
                {
                    case Effect.Damage3AnyEnemyUnit:
                        for (int u = 0; u < o.Board.Count; u++)
                        {
                            list.Add(TacticAction(c, o.Board[u].Card.Uid));
                        }
                        break;
                    case Effect.HealWounded2:
                        var own = WoundedUnits(p, -1);
                        for (int w = 0; w < own.Count; w++)
                        {
                            list.Add(TacticAction(c, own[w].Card.Uid));
                        }
                        break;
                    case Effect.Draw2:
                        list.Add(TacticAction(c, -1));
                        break;
                    case Effect.SuppressAttack2:
                        for (int u = 0; u < o.Board.Count; u++)
                        {
                            list.Add(TacticAction(c, o.Board[u].Card.Uid));
                        }
                        break;
                }
            }

            // 装备
            for (int i = 0; i < p.Hand.Count; i++)
            {
                CardInstance c = p.Hand[i];
                if (c.Def.Type != CardType.Equipment || c.Def.Cost > p.Supply)
                {
                    continue;
                }
                for (int u = 0; u < p.Board.Count; u++)
                {
                    list.Add(EquipAction(c, p.Board[u].Card.Uid));
                }
            }

            // 预备区：晋阶与支援共用每轮一次额度
            if (!p.ReserveUsedThisRound)
            {
                for (int r = 0; r < p.Reserve.Count; r++)
                {
                    ReservedCard rc = p.Reserve[r];
                    if (rc.Used || rc.Def.Cost > p.Supply)
                    {
                        continue;
                    }
                    if (rc.Def.Type == CardType.Upgrade)
                    {
                        for (int u = 0; u < p.Board.Count; u++)
                        {
                            UnitState unit = p.Board[u];
                            if (unit.Card.Def.Id != rc.Def.UpgradesFrom || unit.Upgrade != null)
                            {
                                continue;
                            }
                            if (!CanAct(unit, p))
                            {
                                continue;
                            }
                            list.Add(ReserveAction(rc, unit.Card.Uid));
                        }
                    }
                    else if (rc.Def.Type == CardType.Support)
                    {
                        switch (rc.Def.Effect)
                        {
                            case Effect.Damage3AnyEnemyUnit:
                                for (int u = 0; u < o.Board.Count; u++)
                                {
                                    list.Add(ReserveAction(rc, o.Board[u].Card.Uid));
                                }
                                break;
                            case Effect.HealWounded4:
                                var w4 = WoundedUnits(p, -1);
                                for (int w = 0; w < w4.Count; w++)
                                {
                                    list.Add(ReserveAction(rc, w4[w].Card.Uid));
                                }
                                break;
                            case Effect.SearchBasicUnitLe3:
                                if (HasSearchableBasic(p) && p.Hand.Count < HandLimit)
                                {
                                    list.Add(ReserveAction(rc, -1));
                                }
                                break;
                        }
                    }
                }
            }

            // 攻击
            bool powerStrike = PowerStrikeAvailable(p);
            for (int u = 0; u < p.Board.Count; u++)
            {
                UnitState atk = p.Board[u];
                if (!CanAttackNow(atk, p))
                {
                    continue;
                }
                foreach (UnitState tgt in LegalAttackTargets(atk, o))
                {
                    list.Add(AttackAction(atk, tgt.Card.Uid, false, false));
                    if (powerStrike)
                    {
                        list.Add(AttackAction(atk, tgt.Card.Uid, false, true));
                    }
                }
                if (o.FrontUnits.Count == 0)
                {
                    list.Add(AttackAction(atk, -1, true, false));
                    if (powerStrike)
                    {
                        list.Add(AttackAction(atk, -1, true, true));
                    }
                }
            }

            // 通用侦察
            if (!p.ScoutUsed && p.Supply >= 1 && p.Hand.Count < HandLimit
                && (p.Deck.Count > 0 || p.Discard.Count > 0))
            {
                list.Add(new GameAction { Kind = ActionKind.Scout, Label = "通用侦察（1 补给，看牌库顶 3 选 1）" });
            }

            // 免费换防
            if (!p.RepositionUsed)
            {
                foreach (GameAction rep in LegalRepositions(p))
                {
                    list.Add(rep);
                }
            }

            list.Add(new GameAction { Kind = ActionKind.EndRound, Label = "结束本轮（放弃剩余行动）" });
            return list;
        }

        /// <summary>
        /// 6.1 合法攻击目标：先护卫、再前排、前排空了才能打后排或营地。
        /// </summary>
        public List<UnitState> LegalAttackTargets(UnitState attacker, PlayerState defender)
        {
            var result = new List<UnitState>();
            var front = defender.FrontUnits;
            if (front.Count > 0)
            {
                bool hasGuard = false;
                for (int i = 0; i < front.Count; i++)
                {
                    if (front[i].HasGuard)
                    {
                        hasGuard = true;
                        break;
                    }
                }
                for (int i = 0; i < front.Count; i++)
                {
                    if (!hasGuard || front[i].HasGuard)
                    {
                        result.Add(front[i]);
                    }
                }
            }
            else
            {
                var back = defender.BackUnits;
                for (int i = 0; i < back.Count; i++)
                {
                    result.Add(back[i]);
                }
            }
            return result;
        }

        private List<UnitState> WoundedUnits(PlayerState p, int excludeUid)
        {
            var list = new List<UnitState>();
            for (int i = 0; i < p.Board.Count; i++)
            {
                UnitState u = p.Board[i];
                if (u.IsWounded && u.Card.Uid != excludeUid)
                {
                    list.Add(u);
                }
            }
            return list;
        }

        private bool HasSearchableBasic(PlayerState p)
        {
            for (int i = 0; i < p.Deck.Count; i++)
            {
                CardDef d = p.Deck[i].Def;
                if (d.Type == CardType.Unit && d.Cost <= 3)
                {
                    return true;
                }
            }
            return false;
        }

        private List<GameAction> LegalRepositions(PlayerState p)
        {
            var list = new List<GameAction>();
            for (int i = 0; i < p.Board.Count; i++)
            {
                UnitState u = p.Board[i];
                for (int slot = 0; slot < TotalSlots; slot++)
                {
                    var s = SlotAt(slot);
                    // 5.3：换防只能「移动到另一排的空位」或「前后排互换」，
                    // 不提供同排横向移动，所以同排空位不算合法目标。
                    if (s.Front == u.Slot.Front)
                    {
                        continue;
                    }
                    if (p.SlotFree(s))
                    {
                        list.Add(RepositionAction(u, null, s));
                    }
                }
            }
            // 前后排互换
            for (int i = 0; i < p.Board.Count; i++)
            {
                UnitState front = p.Board[i];
                if (!front.Slot.Front)
                {
                    continue;
                }
                for (int j = 0; j < p.Board.Count; j++)
                {
                    UnitState back = p.Board[j];
                    if (back.Slot.Front)
                    {
                        continue;
                    }
                    list.Add(RepositionAction(front, back, back.Slot));
                }
            }
            return list;
        }

        // ── 行动工厂 ───────────────────────────────────────────────────────

        private GameAction DeployAction(CardInstance c, Slot s, int healTargetUid)
        {
            string label = "部署 " + c.Def.Name + "(费" + c.Def.Cost + ") → " + s.Label;
            if (c.Def.Effect == Effect.EntryHealWounded2)
            {
                label += healTargetUid >= 0 ? "，维修目标 #" + healTargetUid : "，无需维修目标";
            }
            return new GameAction
            {
                Kind = ActionKind.Deploy, CardUid = c.Uid, ToSlot = s, TargetUid = healTargetUid, Label = label,
            };
        }

        private GameAction TacticAction(CardInstance c, int targetUid)
        {
            string label = "战术 " + c.Def.Name + "(费" + c.Def.Cost + ")" + (targetUid >= 0 ? " → #" + targetUid : string.Empty);
            return new GameAction { Kind = ActionKind.Tactic, CardUid = c.Uid, TargetUid = targetUid, Label = label };
        }

        private GameAction EquipAction(CardInstance c, int unitUid)
        {
            // V0.3 §8.2：每单位最多 1 件装备，新装备**替换**旧装备而不是叠加。
            // 目标已有装备时必须提示会替换下什么；同名再装一次是纯亏（花 1 补给 + 1 行动、属性不变）。
            UnitState target = FindUnit(Actor, unitUid);
            string swap = null;
            if (target != null && target.Equipment != null)
            {
                swap = target.Equipment.Id == c.Def.Id
                    ? "同名替换：属性不变，白花 1 补给 + 1 行动"
                    : "替换下 " + target.Equipment.Name;
            }
            string label = "装备 " + c.Def.Name + "(费" + c.Def.Cost + ") → #" + unitUid;
            if (swap != null)
            {
                label += "（" + swap + "）";
            }
            return new GameAction
            {
                Kind = ActionKind.Equip, CardUid = c.Uid, ActorUid = unitUid,
                SwapNote = swap, Label = label,
            };
        }

        private GameAction ReserveAction(ReservedCard rc, int targetUid)
        {
            string kind = rc.Def.Type == CardType.Upgrade ? "晋阶" : "支援";
            string label = kind + " " + rc.Def.Name + "(费" + rc.Def.Cost + ")"
                           + (targetUid >= 0 ? " → #" + targetUid : string.Empty);
            return new GameAction { Kind = ActionKind.Reserve, ReserveId = rc.Def.Id, TargetUid = targetUid, Label = label };
        }

        private GameAction AttackAction(UnitState atk, int targetUid, bool camp, bool powerStrike)
        {
            string baseLabel = "攻击 " + atk.Card.Short + " → " + (camp ? "敌方营地" : "#" + targetUid);
            return new GameAction
            {
                Kind = ActionKind.Attack, ActorUid = atk.Card.Uid, TargetUid = targetUid, TargetCamp = camp,
                PowerStrike = powerStrike,
                Label = powerStrike
                    ? baseLabel + "（强攻 -" + PowerStrikeCost + "补给，主动伤害 +" + PowerStrikeBonus + "）"
                    : baseLabel,
            };
        }

        /// <summary>
        /// V0.3 §16.8 要求的对照组开关（C0/C2 关闭强攻，C1/C3 开放）。
        /// 默认开启；只在批量对照实验里临时关掉，不影响正常对局。
        /// </summary>
        public bool PowerStrikeRuleEnabled = true;

        // ── V0.6 对照实验开关（默认值 = 现有 V0.3 行为，不影响正常对局）──
        /// <summary>前排格数：默认 2，V0.6 提案为 3。</summary>
        public int FrontSlots = 2;
        /// <summary>后排格数：默认 2，V0.6 提案为 3。</summary>
        public int BackSlots = 2;
        /// <summary>阵亡战损：默认开；V0.6 提案关闭。</summary>
        public bool TollRuleEnabled = true;
        /// <summary>清线奖励：默认开；V0.6 无此机制。</summary>
        public bool ClearBonusRuleEnabled = true;

        public int TotalSlots
        {
            get { return FrontSlots + BackSlots; }
        }

        public Slot SlotAt(int i)
        {
            return i < FrontSlots ? new Slot(true, i) : new Slot(false, i - FrontSlots);
        }

        /// <summary>V0.3 §6.6：第 4 轮起、本轮额度未用、补给够支付 2 点，才可以声明强攻。</summary>
        public bool PowerStrikeAvailable(PlayerState p)
        {
            return PowerStrikeRuleEnabled
                   && S.Round >= PowerStrikeStartsAtRound && !p.PowerStrikeUsed && p.Supply >= PowerStrikeCost;
        }

        /// <summary>本次攻击在「不掩护」与「使用掩护」两种分支下的主动伤害预览（V0.3 §12.1 双分支）。</summary>
        public int[] AttackDamageBranches(UnitState atk, bool powerStrike, bool coverAvailable)
        {
            int baseValue = Math.Max(0, atk.Atk) + (powerStrike ? PowerStrikeBonus : 0);
            int uncovered = Math.Max(0, baseValue);
            int covered = Math.Max(0, baseValue - 1);
            return coverAvailable ? new int[] { uncovered, covered } : new int[] { uncovered, uncovered };
        }

        private GameAction RepositionAction(UnitState a, UnitState b, Slot to)
        {
            string label = b == null
                ? "换防 " + a.Card.Short + " " + a.Slot.Label + " → " + to.Label
                : "换防 " + a.Card.Short + " 与 " + b.Card.Short + " 互换";
            return new GameAction { Kind = ActionKind.Reposition, ActorUid = a.Card.Uid, TargetUid = b == null ? -1 : b.Card.Uid, ToSlot = to, Label = label };
        }

        // ── 执行 ───────────────────────────────────────────────────────────

        public LogEntry Apply(GameAction action)
        {
            if (action == null || S.Phase != GamePhase.Playing || S.IsOver || Pending != PendingKind.None)
            {
                return null;
            }
            _preCamp0 = S.Players[0].Camp;
            _preCamp1 = S.Players[1].Camp;

            switch (action.Kind)
            {
                case ActionKind.Deploy:
                    return DoDeploy(action);
                case ActionKind.Tactic:
                    return DoTactic(action);
                case ActionKind.Equip:
                    return DoEquip(action);
                case ActionKind.Reserve:
                    return DoReserve(action);
                case ActionKind.Attack:
                    return DoAttack(action);
                case ActionKind.Reposition:
                    return DoReposition(action);
                case ActionKind.Scout:
                    return DoScout(action);
                case ActionKind.EndRound:
                    return DoEndRound();
            }
            return null;
        }

        private PlayerState Actor
        {
            get { return S.Players[S.CurrentPlayer]; }
        }

        private PlayerState Target
        {
            get { return S.Players[1 - S.CurrentPlayer]; }
        }

        private LogEntry DoDeploy(GameAction a)
        {
            PlayerState p = Actor;
            int idx = p.Hand.FindIndex(c => c.Uid == a.CardUid);
            if (idx < 0)
            {
                return null;
            }
            CardInstance card = p.Hand[idx];
            if (card.Def.Type != CardType.Unit || card.Def.Cost > p.Supply || !p.SlotFree(a.ToSlot) || p.Board.Count >= TotalSlots)
            {
                return null;
            }
            p.Hand.RemoveAt(idx);
            p.Supply -= card.Def.Cost;
            var unit = new UnitState
            {
                Card = card,
                Slot = a.ToSlot,
                DeployedRound = S.Round,
                DeployedAtActionIndex = p.ActionIndexThisRound,
            };
            p.Board.Add(unit);

            string extra = string.Empty;
            if (card.Def.Effect == Effect.EntryHealWounded2)
            {
                UnitState healTarget = FindUnit(p, a.TargetUid);
                if (healTarget != null && healTarget.IsWounded)
                {
                    int before = healTarget.CurrentHp;
                    healTarget.Damage = Math.Max(0, healTarget.Damage - 2);
                    extra = string.Format("；维修 {0} {1}→{2}", healTarget.Card.Short, before, healTarget.CurrentHp);
                }
            }
            else if (card.Def.Effect == Effect.EntryScoutTop2)
            {
                // V0.3 §9.1：由玩家决定保持或交换，引擎不再代选。
                if (p.Deck.Count >= 2)
                {
                    PendingReorderCards.Clear();
                    PendingReorderCards.Add(p.Deck[0]);
                    PendingReorderCards.Add(p.Deck[1]);
                    Pending = PendingKind.Reorder;
                    AddLog(string.Format("{0} 部署 {1} 到 {2}（-{3}补给）；侦察员查看牌库顶 2 张，等待调序。",
                        p.Name, card.Def.Name, a.ToSlot.Label, card.Def.Cost));
                    return null;
                }
                extra = "；牌库不足 2 张，无需调序";
            }

            LogEntry e = AddLog(string.Format("{0} 部署 {1} 到 {2}（-{3}补给）{4}",
                p.Name, card.Def.Name, a.ToSlot.Label, card.Def.Cost, extra));
            FinishAction();
            return e;
        }

        /// <summary>
        /// V0.3 §9.1 侦察员调序窗口：swap=false 保持原位，swap=true 交换牌库顶两张。
        /// 确认后继续同一次部署的结算（不额外消耗行动）。
        /// </summary>
        public void ResolveReorder(bool swap)
        {
            if (Pending != PendingKind.Reorder)
            {
                return;
            }
            PlayerState p = S.Players[S.CurrentPlayer];
            string first = PendingReorderCards.Count > 0 ? PendingReorderCards[0].Def.Name : "?";
            string second = PendingReorderCards.Count > 1 ? PendingReorderCards[1].Def.Name : "?";
            if (swap && p.Deck.Count >= 2)
            {
                CardInstance t = p.Deck[0];
                p.Deck[0] = p.Deck[1];
                p.Deck[1] = t;
            }
            AddLog(string.Format("{0} 调整牌库顶顺序：{1}、{2} → {3}。",
                p.Name, first, second, swap ? "交换" : "保持"));
            PendingReorderCards.Clear();
            Pending = PendingKind.None;
            FinishAction();
        }

        private LogEntry DoTactic(GameAction a)
        {
            PlayerState p = Actor;
            int idx = p.Hand.FindIndex(c => c.Uid == a.CardUid);
            if (idx < 0)
            {
                return null;
            }
            CardInstance card = p.Hand[idx];
            if (card.Def.Type != CardType.Tactic || card.Def.Cost > p.Supply)
            {
                return null;
            }
            p.Hand.RemoveAt(idx);
            p.Supply -= card.Def.Cost;
            p.Discard.Add(card);

            // 伤害类战术要走掩护窗口
            if (card.Def.Effect == Effect.Damage3AnyEnemyUnit)
            {
                if (CoverAvailable())
                {
                    _pendingAction = a;
                    _pendingCardDef = card.Def;
                    Pending = PendingKind.Cover;
                    return AddLog(string.Format("{0} 使用 {1} → #{2}（伤害 3），等待 {3} 的掩护决定。",
                        p.Name, card.Def.Name, a.TargetUid, Target.Name));
                }
            }
            return ResolveTactic(card, a.TargetUid, false);
        }

        private LogEntry ResolveTactic(CardInstance card, int targetUid, bool covered)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            string extra = string.Empty;
            switch (card.Def.Effect)
            {
                case Effect.Damage3AnyEnemyUnit:
                {
                    UnitState tgt = FindUnit(o, targetUid);
                    if (tgt == null)
                    {
                        return null;
                    }
                    int dmg = Math.Max(0, 3 - (covered ? 1 : 0));
                    tgt.Damage += dmg;
                    extra = string.Format("对 {0} 造成 {1} 点伤害{2}", tgt.Card.Short, dmg, covered ? "（掩护 -1）" : string.Empty);
                    SweepDeaths(S.CurrentPlayer, false, false);
                    break;
                }
                case Effect.HealWounded2:
                {
                    UnitState tgt = FindUnit(p, targetUid);
                    if (tgt != null)
                    {
                        int before = tgt.CurrentHp;
                        tgt.Damage = Math.Max(0, tgt.Damage - 2);
                        extra = string.Format("{0} {1}→{2}", tgt.Card.Short, before, tgt.CurrentHp);
                    }
                    break;
                }
                case Effect.Draw2:
                {
                    DrawCards(p, 2, false);
                    extra = "抽 2 张";
                    break;
                }
                case Effect.SuppressAttack2:
                {
                    UnitState tgt = FindUnit(o, targetUid);
                    if (tgt != null)
                    {
                        tgt.TempAtkMod -= 2;
                        extra = string.Format("{0} 本轮攻击 -2（现 {1}）", tgt.Card.Short, tgt.Atk);
                    }
                    break;
                }
            }
            LogEntry e = AddLog(string.Format("{0} 使用 {1}（-{2}补给）：{3}", p.Name, card.Def.Name, card.Def.Cost, extra));
            FinishActionAfterResolution();
            return e;
        }

        private LogEntry DoEquip(GameAction a)
        {
            PlayerState p = Actor;
            int idx = p.Hand.FindIndex(c => c.Uid == a.CardUid);
            if (idx < 0)
            {
                return null;
            }
            CardInstance card = p.Hand[idx];
            UnitState unit = FindUnit(p, a.ActorUid);
            if (card.Def.Type != CardType.Equipment || card.Def.Cost > p.Supply || unit == null)
            {
                return null;
            }
            p.Hand.RemoveAt(idx);
            p.Supply -= card.Def.Cost;
            string replaced = string.Empty;
            if (unit.Equipment != null)
            {
                p.Discard.Add(new CardInstance(_nextUid++, unit.Equipment));
                replaced = "（替换下 " + unit.Equipment.Name + "）";
            }
            unit.Equipment = card.Def;

            LogEntry e = AddLog(string.Format("{0} 为 {1} 装备 {2}（-{3}补给）{4}",
                p.Name, unit.Card.Short, card.Def.Name, card.Def.Cost, replaced));
            FinishAction();
            return e;
        }

        private LogEntry DoReserve(GameAction a)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            ReservedCard rc = p.FindReserve(a.ReserveId);
            if (rc == null || rc.Used || p.ReserveUsedThisRound || rc.Def.Cost > p.Supply)
            {
                return null;
            }
            if (rc.Def.Type == CardType.Upgrade)
            {
                UnitState unit = FindUnit(p, a.TargetUid);
                if (unit == null || unit.Card.Def.Id != rc.Def.UpgradesFrom || unit.Upgrade != null || !CanAct(unit, p))
                {
                    return null;
                }
                p.Supply -= rc.Def.Cost;
                rc.Used = true;
                p.ReserveUsedThisRound = true;
                int beforeHp = unit.CurrentHp;
                int beforeAtk = unit.Atk;
                unit.Upgrade = rc.Def;
                LogEntry e = AddLog(string.Format(
                    "{0} 晋阶 {1} → {2}（-{3}补给）：攻击 {4}→{5}，生命 {6}/{7}（保留 {8} 点伤害，已攻击状态{9}）",
                    p.Name, unit.Card.Short, rc.Def.Name, rc.Def.Cost, beforeAtk, unit.Atk,
                    unit.CurrentHp, unit.MaxHp, unit.Damage, unit.AttackedThisRound ? "保留" : "无"));
                FinishAction();
                return e;
            }

            // 支援
            // V0.3 §9.2：应急调度先由玩家从按牌名聚合的候选里选牌，确认后才支付费用与行动，
            // 因此这里在扣费之前开窗口；窗口期取消不消耗任何资源。
            if (rc.Def.Effect == Effect.SearchBasicUnitLe3)
            {
                if (p.Hand.Count >= HandLimit || !HasSearchableBasic(p))
                {
                    return null;
                }
                PendingSearchCards.Clear();
                var seen = new List<string>();
                for (int i = 0; i < p.Deck.Count; i++)
                {
                    CardInstance c = p.Deck[i];
                    if (c.Def.Type != CardType.Unit || c.Def.Cost > 3)
                    {
                        continue;
                    }
                    if (seen.Contains(c.Def.Id))
                    {
                        continue;
                    }
                    seen.Add(c.Def.Id);
                    PendingSearchCards.Add(c);
                }
                _pendingAction = a;
                Pending = PendingKind.SearchPick;
                return AddLog(string.Format("{0} 调用支援 {1}，从牌库中按牌名选择（候选 {2} 种），等待确认。",
                    p.Name, rc.Def.Name, PendingSearchCards.Count));
            }

            p.Supply -= rc.Def.Cost;
            rc.Used = true;
            p.ReserveUsedThisRound = true;

            if (rc.Def.Effect == Effect.Damage3AnyEnemyUnit && CoverAvailable())
            {
                _pendingAction = a;
                _pendingCardDef = rc.Def;
                Pending = PendingKind.Cover;
                return AddLog(string.Format("{0} 调用支援 {1} → {2}，等待 {3} 的掩护决定。",
                    p.Name, rc.Def.Name, UnitLabel(o, a.TargetUid), o.Name));
            }

            string extra = string.Empty;
            switch (rc.Def.Effect)
            {
                case Effect.Damage3AnyEnemyUnit:
                {
                    UnitState tgt = FindUnit(o, a.TargetUid);
                    if (tgt != null)
                    {
                        tgt.Damage += 3;
                        extra = string.Format("对 {0} 造成 3 点伤害", tgt.Card.Short);
                        SweepDeaths(S.CurrentPlayer, false, false);
                    }
                    break;
                }
                case Effect.HealWounded4:
                {
                    UnitState tgt = FindUnit(p, a.TargetUid);
                    if (tgt != null)
                    {
                        int before = tgt.CurrentHp;
                        tgt.Damage = Math.Max(0, tgt.Damage - 4);
                        extra = string.Format("{0} {1}→{2}", tgt.Card.Short, before, tgt.CurrentHp);
                    }
                    break;
                }
                case Effect.SearchBasicUnitLe3:
                {
                    int found = p.Deck.FindIndex(c => c.Def.Type == CardType.Unit && c.Def.Cost <= 3);
                    if (found >= 0)
                    {
                        CardInstance got = p.Deck[found];
                        p.Deck.RemoveAt(found);
                        if (p.Hand.Count < HandLimit)
                        {
                            p.Hand.Add(got);
                        }
                        else
                        {
                            p.Discard.Add(got);
                        }
                        _rng.Shuffle(p.Deck);
                        extra = "检索到 " + got.Def.Name + "，然后洗牌";
                    }
                    else
                    {
                        extra = "牌库中没有符合条件的单位";
                    }
                    break;
                }
            }
            LogEntry entry = AddLog(string.Format("{0} 调用支援 {1}（-{2}补给）：{3}", p.Name, rc.Def.Name, rc.Def.Cost, extra));
            FinishActionAfterResolution();
            return entry;
        }

        private bool CoverAvailable()
        {
            PlayerState o = Target;
            return !o.CoverUsed && o.Supply >= 1;
        }

        private LogEntry DoAttack(GameAction a)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            UnitState atk = FindUnit(p, a.ActorUid);
            if (atk == null || !CanAttackNow(atk, p))
            {
                return null;
            }
            // 目标合法性再校验一次，避免 AI/UI 传入过期目标
            if (a.TargetCamp)
            {
                if (o.FrontUnits.Count > 0)
                {
                    return null;
                }
            }
            else
            {
                var legal = LegalAttackTargets(atk, o);
                if (legal.FindIndex(u => u.Card.Uid == a.TargetUid) < 0)
                {
                    return null;
                }
            }

            // V0.3 §6.5-1：选择强攻时先检查轮次、剩余补给及本轮额度，再支付并标记已用；
            // 提交失败不扣资源（因此所有校验都在扣费之前完成）。
            if (a.PowerStrike)
            {
                if (!PowerStrikeAvailable(p))
                {
                    return null;
                }
                p.Supply -= PowerStrikeCost;
                p.PowerStrikeUsed = true;
                AddLog(string.Format("{0} 宣布强攻：支付 {1} 补给，本次主动伤害 +{2}（本轮额度已用）。",
                    p.Name, PowerStrikeCost, PowerStrikeBonus));
            }

            // V0.3 §7.1 / §6.5-2：主动伤害为 0 时跳过掩护窗口（不扣费、不消耗额度）。
            int incoming = Math.Max(0, atk.Atk) + (a.PowerStrike ? PowerStrikeBonus : 0);
            if (incoming <= 0)
            {
                return ResolveAttack(a, false);
            }

            // V0.3 §12.1：开启「本轮自动放弃掩护」时不再弹窗，也不会自动花费补给。
            if (CoverAvailable() && !o.AutoDeclineCover)
            {
                _pendingAction = a;
                Pending = PendingKind.Cover;
                return AddLog(string.Format("{0} 用 {1} 攻击 {2}，等待 {3} 的掩护决定。",
                    p.Name, atk.Card.Short, a.TargetCamp ? "敌方营地" : UnitLabel(o, a.TargetUid), o.Name));
            }
            return ResolveAttack(a, false);
        }

        /// <summary>6.5 一次攻击的完整顺序。</summary>
        private LogEntry ResolveAttack(GameAction a, bool covered)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            UnitState atk = FindUnit(p, a.ActorUid);
            if (atk == null)
            {
                return null;
            }

            // V0.3 §6.6：先取装备与牵制后的当前攻击力并截到最低 0，再加强攻 +1，最后扣掩护。
            int baseValue = Math.Max(0, atk.Atk) + (a.PowerStrike ? PowerStrikeBonus : 0);
            int activeDamage = Math.Max(0, baseValue - (covered ? 1 : 0));
            int counterDamage = 0;
            UnitState tgt = null;
            bool enemyFrontWasNonEmpty = o.FrontUnits.Count > 0;

            if (!a.TargetCamp)
            {
                tgt = FindUnit(o, a.TargetUid);
                if (tgt == null)
                {
                    return null;
                }
                bool noCounter = atk.HasRanged && !atk.Slot.Front;
                counterDamage = noCounter ? 0 : tgt.Atk;
            }

            atk.AttackedThisRound = true;

            string targetName = a.TargetCamp ? "敌方营地" : tgt.Card.Short;
            // 注意：日志前缀「攻击 X：主动 N」是文档与统计脚本的既有口径，强攻标记一律放在最后，
            // 避免破坏按文本归因的解析（第十一轮已因此踩过坑）。
            string detail = string.Format("{0} 攻击 {1}：主动 {2}{3}{4}",
                atk.Card.Short, targetName, activeDamage,
                covered ? "（掩护 -1）" : string.Empty,
                a.PowerStrike ? "（强攻 +" + PowerStrikeBonus + "）" : string.Empty);

            if (a.TargetCamp)
            {
                o.Camp -= activeDamage;
                S.LossDirect[o.Index] += activeDamage;
            }
            else
            {
                tgt.Damage += activeDamage;
            }
            if (counterDamage > 0)
            {
                atk.Damage += counterDamage;
            }
            detail += counterDamage > 0
                ? string.Format("，反击 {0}", counterDamage)
                : (a.TargetCamp ? string.Empty : "，无反击");

            // 阵亡、战损与清线奖励
            var dead = CollectDeaths();
            if (dead.Count > 0)
            {
                detail += "；阵亡：" + DescribeDeaths(dead, p.Index);
            }
            ApplyTolls(dead, p.Index);

            if (ClearBonusRuleEnabled && enemyFrontWasNonEmpty && o.FrontUnits.Count == 0 && !p.ClearFrontBonusUsed)
            {
                p.ClearFrontBonusUsed = true;
                o.Camp -= 1;
                S.LossClear[o.Index] += 1;
                detail += "；首次清空敌方前排，营地额外 -1";
            }

            LogEntry e = AddLog(string.Format("{0} {1}。营地 甲{2} / 乙{3}", p.Name, detail,
                S.Players[0].Camp, S.Players[1].Camp));
            FinishActionAfterResolution();
            return e;
        }

        private List<UnitState> CollectDeaths()
        {
            var dead = new List<UnitState>();
            for (int i = 0; i < 2; i++)
            {
                PlayerState p = S.Players[i];
                for (int u = p.Board.Count - 1; u >= 0; u--)
                {
                    if (p.Board[u].CurrentHp <= 0)
                    {
                        dead.Add(p.Board[u]);
                    }
                }
            }
            return dead;
        }

        private string DescribeDeaths(List<UnitState> dead, int attackerIndex)
        {
            var parts = new List<string>();
            for (int i = 0; i < dead.Count; i++)
            {
                parts.Add(dead[i].Card.Short);
            }
            return string.Join("、", parts.ToArray());
        }

        private void ApplyTolls(List<UnitState> dead, int attackerIndex)
        {
            for (int i = 0; i < 2; i++)
            {
                PlayerState owner = S.Players[i];
                for (int u = owner.Board.Count - 1; u >= 0; u--)
                {
                    UnitState unit = owner.Board[u];
                    if (unit.CurrentHp > 0)
                    {
                        continue;
                    }
                    int toll = unit.Upgrade != null ? 2 : 1;
                    if (TollRuleEnabled)
                    {
                        owner.Camp -= toll;
                        S.LossToll[i] += toll;
                    }
                    else
                    {
                        toll = 0;   // V0.6 实验：单位阵亡不扣旗
                    }
                    if (unit.Equipment != null)
                    {
                        owner.Discard.Add(new CardInstance(_nextUid++, unit.Equipment));
                    }
                    owner.Discard.Add(unit.Card);
                    owner.Board.RemoveAt(u);
                    AddLog(string.Format("  {0} 阵亡：{1} → 弃牌区；{2} 承担战损 {3}。",
                        unit.Card.Short, owner.Name, unit.Upgrade != null ? "晋阶单位" : "基础单位", toll));
                }
            }
        }

        private void SweepDeaths(int attackerIndex, bool activeUnitAttack, bool enemyFrontWasNonEmpty)
        {
            var dead = CollectDeaths();
            if (dead.Count == 0)
            {
                return;
            }
            ApplyTolls(dead, attackerIndex);
            CheckWinner(_preCamp0, _preCamp1, "结算");
        }

        private LogEntry DoReposition(GameAction a)
        {
            PlayerState p = Actor;
            if (p.RepositionUsed)
            {
                return null;
            }
            UnitState mover = FindUnit(p, a.ActorUid);
            if (mover == null)
            {
                return null;
            }
            UnitState other = a.TargetUid >= 0 ? FindUnit(p, a.TargetUid) : null;
            Slot from = mover.Slot;

            if (other != null)
            {
                // 前后排互换
                if (mover.Slot.Front == other.Slot.Front)
                {
                    return null;
                }
                Slot a1 = mover.Slot;
                Slot b1 = other.Slot;
                mover.Slot = b1;
                other.Slot = a1;
                p.RepositionUsed = true;
                return AddLog(string.Format("{0} 免费换防：{1} 与 {2} 互换（{3}↔{4}）。",
                    p.Name, mover.Card.Short, other.Card.Short, a1.Label, b1.Label));
            }

            if (!p.SlotFree(a.ToSlot))
            {
                return null;
            }
            // 5.3：同排横向移动不是合法换防，即使调用方传来的动作合法也要拦掉
            if (a.ToSlot.Front == mover.Slot.Front)
            {
                return null;
            }
            mover.Slot = a.ToSlot;
            p.RepositionUsed = true;
            return AddLog(string.Format("{0} 免费换防：{1} {2} → {3}。", p.Name, mover.Card.Short, from.Label, a.ToSlot.Label));
        }

        private LogEntry DoScout(GameAction a)
        {
            PlayerState p = Actor;
            if (p.ScoutUsed || p.Supply < 1 || p.Hand.Count >= HandLimit)
            {
                return null;
            }
            ShuffleDiscardIntoDeck(p);
            int look = Math.Min(3, p.Deck.Count);
            if (look <= 0)
            {
                return null;
            }
            p.Supply -= 1;
            p.ScoutUsed = true;
            PendingScoutCards.Clear();
            for (int i = 0; i < look; i++)
            {
                PendingScoutCards.Add(p.Deck[i]);
            }
            _pendingAction = a;
            Pending = PendingKind.ScoutPick;
            return AddLog(string.Format("{0} 使用通用侦察（-1 补给），查看牌库顶 {1} 张，等待选择。", p.Name, look));
        }

        private LogEntry DoEndRound()
        {
            PlayerState p = Actor;
            int given = p.ActionsLeft;
            p.ActionsLeft = 0;
            LogEntry e = AddLog(string.Format("{0} 结束本轮，放弃 {1} 次剩余行动。", p.Name, given));
            AdvanceTurn();
            return e;
        }

        // ── 反应窗口 ───────────────────────────────────────────────────────

        public void ResolveCover(bool use)
        {
            if (Pending != PendingKind.Cover || _pendingAction == null)
            {
                return;
            }
            GameAction action = _pendingAction;
            _pendingAction = null;
            Pending = PendingKind.None;

            PlayerState defender = Target;
            bool covered = false;
            if (use && defender.Supply >= 1 && !defender.CoverUsed)
            {
                defender.Supply -= 1;
                defender.CoverUsed = true;
                covered = true;
                AddLog(string.Format("{0} 使用通用掩护（-1 补给），本次伤害 -1。", defender.Name));
            }
            else
            {
                AddLog(string.Format("{0} 放弃掩护。", defender.Name));
            }

            if (action.Kind == ActionKind.Attack)
            {
                ResolveAttack(action, covered);
            }
            else if (action.Kind == ActionKind.Tactic)
            {
                // 战术牌已经在 DoTactic 里进弃牌区，这里按保存的定义直接结算
                ResolveTacticPending(action, covered);
            }
            else if (action.Kind == ActionKind.Reserve)
            {
                ResolveReservePending(action, covered);
            }
        }

        private void ResolveTacticPending(GameAction a, bool covered)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            CardDef def = _pendingCardDef;
            if (def == null)
            {
                FinishActionAfterResolution();
                return;
            }
            UnitState tgt = FindUnit(o, a.TargetUid);
            string extra = string.Empty;
            if (tgt != null)
            {
                int dmg = Math.Max(0, 3 - (covered ? 1 : 0));
                tgt.Damage += dmg;
                extra = string.Format("对 {0} 造成 {1} 点伤害{2}", tgt.Card.Short, dmg, covered ? "（掩护 -1）" : string.Empty);
            }
            AddLog(string.Format("{0} 使用 {1}：{2}", p.Name, def.Name, extra));
            SweepDeaths(S.CurrentPlayer, false, false);
            FinishActionAfterResolution();
        }

        private void ResolveReservePending(GameAction a, bool covered)
        {
            PlayerState p = Actor;
            PlayerState o = Target;
            ReservedCard rc = p.FindReserve(a.ReserveId);
            UnitState tgt = FindUnit(o, a.TargetUid);
            string extra = string.Empty;
            if (tgt != null)
            {
                int dmg = Math.Max(0, 3 - (covered ? 1 : 0));
                tgt.Damage += dmg;
                extra = string.Format("对 {0} 造成 {1} 点伤害{2}", tgt.Card.Short, dmg, covered ? "（掩护 -1）" : string.Empty);
            }
            AddLog(string.Format("{0} 调用支援 {1}：{2}", p.Name, rc != null ? rc.Def.Name : "?", extra));
            SweepDeaths(S.CurrentPlayer, false, false);
            FinishActionAfterResolution();
        }

        /// <summary>
        /// V0.3 §9.2 应急调度：确认候选后支付费用、移出该实体牌、公开牌名、入手、洗剩余牌库。
        /// 取消（CancelSearch）不支付任何资源。
        /// </summary>
        public void ResolveSearchPick(int index)
        {
            if (Pending != PendingKind.SearchPick || _pendingAction == null)
            {
                return;
            }
            GameAction action = _pendingAction;
            PlayerState p = S.Players[S.CurrentPlayer];
            ReservedCard rc = p.FindReserve(action.ReserveId);
            if (rc == null || rc.Used || p.ReserveUsedThisRound || rc.Def.Cost > p.Supply)
            {
                return;
            }
            if (index < 0 || index >= PendingSearchCards.Count)
            {
                index = 0;
            }
            CardInstance pick = PendingSearchCards[index];
            p.Supply -= rc.Def.Cost;
            rc.Used = true;
            p.ReserveUsedThisRound = true;
            p.Deck.Remove(pick);
            p.Hand.Add(pick);
            _rng.Shuffle(p.Deck);
            AddLog(string.Format("{0} 调用支援 {1}（-{2}补给）：取得 {3} 入手，公开牌名并洗牌。",
                p.Name, rc.Def.Name, rc.Def.Cost, pick.Def.Name));
            PendingSearchCards.Clear();
            Pending = PendingKind.None;
            _pendingAction = null;
            FinishAction();
        }

        /// <summary>候选确认前取消应急调度：不支付费用、不消耗行动与预备额度。</summary>
        public void CancelSearch()
        {
            if (Pending != PendingKind.SearchPick)
            {
                return;
            }
            PlayerState p = S.Players[S.CurrentPlayer];
            AddLog(string.Format("{0} 取消应急调度（未支付费用，行动与预备额度保留）。", p.Name));
            PendingSearchCards.Clear();
            Pending = PendingKind.None;
            _pendingAction = null;
        }

        public void ResolveScoutPick(int index){
            if (Pending != PendingKind.ScoutPick)
            {
                return;
            }
            PlayerState p = S.Players[S.CurrentPlayer];
            if (index < 0 || index >= PendingScoutCards.Count)
            {
                index = 0;
            }
            CardInstance pick = PendingScoutCards[index];
            p.Deck.Remove(pick);
            if (p.Hand.Count < HandLimit)
            {
                p.Hand.Add(pick);
            }
            else
            {
                p.Discard.Add(pick);
            }
            // 其余按原顺序放到牌库底
            for (int i = 0; i < PendingScoutCards.Count; i++)
            {
                if (i == index)
                {
                    continue;
                }
                CardInstance c = PendingScoutCards[i];
                p.Deck.Remove(c);
                p.Deck.Add(c);
            }
            AddLog(string.Format("{0} 侦察取得 {1}，其余 {2} 张放到牌库底。",
                p.Name, pick.Def.Name, PendingScoutCards.Count - 1));
            PendingScoutCards.Clear();
            Pending = PendingKind.None;
            _pendingAction = null;
            FinishActionAfterResolution();
        }

        // ── 回合推进与胜负 ─────────────────────────────────────────────────

        /// <summary>不消耗主行动的结算后处理（换防不调用它）。</summary>
        private void FinishAction()
        {
            PlayerState p = Actor;
            p.ActionsLeft--;
            p.ActionIndexThisRound++;
            CheckWinner(_preCamp0, _preCamp1, "行动");
            if (!S.IsOver)
            {
                AdvanceTurn();
            }
        }

        /// <summary>可能造成阵亡的结算：先判定胜负，再推进回合。</summary>
        private void FinishActionAfterResolution()
        {
            CheckWinner(_preCamp0, _preCamp1, "结算");
            PlayerState p = Actor;
            p.ActionsLeft--;
            p.ActionIndexThisRound++;
            if (!S.IsOver)
            {
                AdvanceTurn();
            }
        }

        private void AdvanceTurn()
        {
            if (S.IsOver)
            {
                return;
            }
            if (S.Players[0].ActionsLeft <= 0 && S.Players[1].ActionsLeft <= 0)
            {
                EndRound();
                return;
            }
            int other = 1 - S.CurrentPlayer;
            if (S.Players[other].ActionsLeft > 0)
            {
                S.CurrentPlayer = other;
            }
        }

        private void CheckWinner(int preCamp0, int preCamp1, string phase)
        {
            int a = S.Players[0].Camp;
            int b = S.Players[1].Camp;
            if (a > 0 && b > 0)
            {
                return;
            }
            S.Phase = GamePhase.Finished;
            string breakdown = string.Format(
                "结算前营地 甲{0}/乙{1}，终局 甲{2}/乙{3}；各项损失 甲(战损{4}/直击{5}/清线{6}/界潮{7}) 乙(战损{8}/直击{9}/清线{10}/界潮{11})",
                preCamp0, preCamp1, a, b,
                S.LossToll[0], S.LossDirect[0], S.LossClear[0], S.LossTide[0],
                S.LossToll[1], S.LossDirect[1], S.LossClear[1], S.LossTide[1]);
            if (a <= 0 && b <= 0)
            {
                // 同一次结算双方归零：比较该次结算开始前的营地生命
                if (preCamp0 > preCamp1)
                {
                    S.Winner = 0;
                }
                else if (preCamp1 > preCamp0)
                {
                    S.Winner = 1;
                }
                else
                {
                    S.Winner = 2;
                }
                S.FinishReason = string.Format("{0}导致双方同时归零，按结算前营地（甲{1}/乙{2}）判定{3}。{4}",
                    phase, preCamp0, preCamp1, S.Winner == 2 ? "平局" : "胜者 " + S.Players[S.Winner].Name, breakdown);
            }
            else if (a <= 0)
            {
                S.Winner = 1;
                S.FinishReason = string.Format("{0}后甲营地归零，乙获胜。{1}", phase, breakdown);
            }
            else
            {
                S.Winner = 0;
                S.FinishReason = string.Format("{0}后乙营地归零，甲获胜。{1}", phase, breakdown);
            }
            AddLog("对局结束：" + S.FinishReason);
        }

        // ── 工具 ───────────────────────────────────────────────────────────

        public UnitState FindUnit(PlayerState p, int uid)
        {
            int i = p.FindUnitIndex(uid);
            return i < 0 ? null : p.Board[i];
        }

        private string UnitLabel(PlayerState p, int uid)
        {
            UnitState u = FindUnit(p, uid);
            return u == null ? "#" + uid : u.Card.Short;
        }

        public string HandText(PlayerState p)
        {
            var parts = new List<string>();
            for (int i = 0; i < p.Hand.Count; i++)
            {
                parts.Add(p.Hand[i].Def.Name + "(" + p.Hand[i].Def.Cost + ")");
            }
            return parts.Count == 0 ? "（空）" : string.Join("、", parts.ToArray());
        }

        /// <summary>
        /// 复制一份引擎用于「试算这一手会怎样」。卡牌实体只被引用不被修改，
        /// 所以只深拷贝状态树即可；日志在试算副本里丢弃，避免污染真实对局记录。
        /// </summary>
        public GameEngine Fork()
        {
            var e = new GameEngine();
            e.S = S.Clone();
            e._rng = new Rng(S.Round * 7919 + 13);
            e._nextUid = _nextUid;
            e._firstRoundStarter = _firstRoundStarter;
            e.PowerStrikeRuleEnabled = PowerStrikeRuleEnabled;
            e.FrontSlots = FrontSlots;
            e.BackSlots = BackSlots;
            e.TollRuleEnabled = TollRuleEnabled;
            e.ClearBonusRuleEnabled = ClearBonusRuleEnabled;
            return e;
        }

        public LogEntry AddLog(string text)
        {
            var e = new LogEntry { Round = S.Round, Text = text };
            Log.Add(e);
            return e;
        }
    }
}
