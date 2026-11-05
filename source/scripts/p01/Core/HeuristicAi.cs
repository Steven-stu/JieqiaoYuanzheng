using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// 启发式 AI：不追求强度，只要求每一类规则都被用上（部署/攻击/换防/晋阶/支援/侦察/结束），
    /// 这样单人对 AI 也能完整体验整套规则。
    ///
    /// 做法是「克隆状态 → 逐个试算合法行动 → 用局面评分挑最好的一手」。
    /// 克隆只复制引用与数值，深度有限，代价可以接受（4 格战场、8 张手牌）。
    /// </summary>
    public sealed class HeuristicAi
    {
        private readonly Rng _rng;

        public HeuristicAi(int seed)
        {
            _rng = new Rng(seed);
        }

        /// <summary>推进一次决策；返回 false 表示这局已经结束。</summary>
        public bool Step(GameEngine engine)
        {
            if (engine.S.IsOver)
            {
                return false;
            }

            if (engine.Pending == PendingKind.Cover)
            {
                engine.ResolveCover(ShouldCover(engine));
                return !engine.S.IsOver;
            }

            if (engine.Pending == PendingKind.ScoutPick)
            {
                engine.ResolveScoutPick(BestScoutPick(engine));
                return !engine.S.IsOver;
            }

            if (engine.Pending == PendingKind.Reorder)
            {
                engine.ResolveReorder(ShouldSwapTopTwo(engine));
                return !engine.S.IsOver;
            }

            if (engine.Pending == PendingKind.SearchPick)
            {
                engine.ResolveSearchPick(BestSearchPick(engine));
                return !engine.S.IsOver;
            }

            if (engine.S.Phase == GamePhase.Mulligan)
            {
                // 换牌由调用方决定（界面让人选，模拟里用 PickMulligan）
                return true;
            }

            GameAction best = ChooseBest(engine);
            if (best == null)
            {
                return false;
            }
            engine.Apply(best);
            return !engine.S.IsOver;
        }

        /// <summary>AI 的换牌选择：换掉最贵的两张（原型牌组里几乎没有 4 费以上，通常不换）。</summary>
        public List<int> PickMulligan(GameEngine engine, int player)
        {
            var uids = new List<int>();
            PlayerState p = engine.S.Players[player];
            var candidates = new List<CardInstance>();
            for (int i = 0; i < p.Hand.Count; i++)
            {
                if (p.Hand[i].Uid == p.VanguardUid)
                {
                    continue;
                }
                candidates.Add(p.Hand[i]);
            }
            candidates.Sort((a, b) => b.Def.Cost.CompareTo(a.Def.Cost));
            for (int i = 0; i < candidates.Count && i < 2; i++)
            {
                if (candidates[i].Def.Cost >= 4)
                {
                    uids.Add(candidates[i].Uid);
                }
            }
            return uids;
        }

        private bool ShouldCover(GameEngine engine)
        {
            // 只在「用了能保住一个单位」或「挡住致命营地伤害」时才交掩护
            int defender = 1 - engine.S.CurrentPlayer;
            PlayerState me = engine.S.Players[defender];
            if (me.CoverUsed || me.Supply < 1)
            {
                return false;
            }
            GameAction pending = engine.PendingAction;
            if (pending == null)
            {
                return false;
            }
            if (pending.TargetCamp)
            {
                return me.Camp <= 3;
            }
            UnitState target = engine.FindUnit(me, pending.TargetUid);
            if (target == null)
            {
                return false;
            }
            int incoming = 0;
            if (pending.Kind == ActionKind.Attack)
            {
                UnitState atk = engine.FindUnit(engine.S.Players[1 - defender], pending.ActorUid);
                incoming = (atk == null ? 0 : atk.Atk) + (pending.PowerStrike ? GameEngine.PowerStrikeBonus : 0);
            }
            else
            {
                incoming = 3;
            }
            return incoming >= target.CurrentHp;
        }

        /// <summary>V0.3 §9.1 侦察员调序：把更想要的那张放到牌库顶（与旧代办逻辑同样的判断）。</summary>
        private bool ShouldSwapTopTwo(GameEngine engine)
        {
            if (engine.PendingReorderCards.Count < 2)
            {
                return false;
            }
            return ScoutScore(engine.PendingReorderCards[1].Def) < ScoutScore(engine.PendingReorderCards[0].Def);
        }

        /// <summary>V0.3 §9.2 应急调度：候选里挑最想要的牌名。</summary>
        private int BestSearchPick(GameEngine engine)
        {
            int best = 0;
            int bestScore = int.MaxValue;
            for (int i = 0; i < engine.PendingSearchCards.Count; i++)
            {
                int score = ScoutScore(engine.PendingSearchCards[i].Def);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private int BestScoutPick(GameEngine engine)
        {            int best = 0;
            int bestCost = int.MaxValue;
            for (int i = 0; i < engine.PendingScoutCards.Count; i++)
            {
                int score = ScoutScore(engine.PendingScoutCards[i].Def);
                if (score < bestCost)
                {
                    bestCost = score;
                    best = i;
                }
            }
            return best;
        }

        private static int ScoutScore(CardDef d)
        {
            // 数值越低越想要：能立刻打出的单位 > 战术 > 装备
            int s = d.Cost * 10;
            if (d.Type == CardType.Unit)
            {
                s -= 15;
            }
            return s;
        }

        private GameAction ChooseBest(GameEngine engine)
        {
            List<GameAction> actions = engine.LegalActions();
            if (actions.Count == 0)
            {
                return null;
            }

            int me = engine.S.CurrentPlayer;
            GameAction best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < actions.Count; i++)
            {
                GameAction a = actions[i];
                if (a.Kind == ActionKind.EndRound)
                {
                    continue;
                }
                int score = ScoreAction(engine, a, me);
                // 同分时用确定性随机打破平局，避免每局完全一样
                score += _rng.Next(3);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = a;
                }
            }

            // 只有明显没有价值时才结束本轮
            if (best == null || bestScore < 0)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i].Kind == ActionKind.EndRound)
                    {
                        return actions[i];
                    }
                }
            }
            return best ?? actions[actions.Count - 1];
        }

        private int ScoreAction(GameEngine engine, GameAction a, int me)
        {
            GameState snapshot = engine.S.Clone();

            // 试算：在克隆状态上跑一遍
            GameEngine sim = engine.Fork();
            // 注意：调序/检索类动作会「返回 null 并打开选择窗口」，这仍是成功的动作。
            // 早先只判 Apply==null，导致 AI 永远不选侦察员与应急调度（第十一轮修）。
            if (sim.Apply(a) == null && sim.Pending == PendingKind.None)
            {
                return int.MinValue;
            }
            while (sim.Pending == PendingKind.Cover)
            {
                sim.ResolveCover(false);
            }
            while (sim.Pending == PendingKind.ScoutPick)
            {
                sim.ResolveScoutPick(BestScoutPick(sim));
            }
            while (sim.Pending == PendingKind.Reorder)
            {
                sim.ResolveReorder(ShouldSwapTopTwo(sim));
            }
            while (sim.Pending == PendingKind.SearchPick)
            {
                sim.ResolveSearchPick(BestSearchPick(sim));
            }

            int score = Evaluate(sim.S, me) - Evaluate(snapshot, me);

            // 规则偏好：清掉敌方前排、保护自己单位、别浪费补给
            if (a.Kind == ActionKind.Reposition)
            {
                score -= 2; // 换防本身不推进局面，只在必要时做
            }
            if (a.Kind == ActionKind.Scout)
            {
                score -= 3;
            }
            return score;
        }

        /// <summary>局面评分：营地差 + 场面价值 + 手牌/补给余量。</summary>
        public static int Evaluate(GameState s, int me)
        {
            PlayerState mine = s.Players[me];
            PlayerState theirs = s.Players[1 - me];
            // 营地是生命值，越多越好，所以是「我方 − 敌方」。
            // 这里曾经写成 (敌方 − 我方)，导致 AI 把「打掉对面营地」当成掉分，
            // 从而永远不直击营地、不做击杀（击杀要扣对方营地）。这是 AI 的致命符号错误。
            int score = (mine.Camp - theirs.Camp) * 100;
            score += BoardValue(mine) - BoardValue(theirs) + 4;
            score += mine.Supply * 2;
            score += mine.Hand.Count * 3;
            score -= theirs.Supply;
            score -= theirs.Hand.Count * 2;
            return score;
        }

        private static int BoardValue(PlayerState p)
        {
            int v = 0;
            for (int i = 0; i < p.Board.Count; i++)
            {
                UnitState u = p.Board[i];
                v += u.Atk * 3 + u.CurrentHp * 3 + u.MaxHp;
                if (u.HasGuard)
                {
                    v += 4;
                }
                if (u.HasRanged)
                {
                    v += 4;
                }
                if (u.Upgrade != null)
                {
                    v += 6;
                }
                if (u.Equipment != null)
                {
                    v += 3;
                }
                if (u.Slot.Front)
                {
                    v += 2;
                }
            }
            return v;
        }
    }
}
