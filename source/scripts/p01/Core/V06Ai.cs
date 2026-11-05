using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>
    /// V0.6 对局 AI：单步贪心 + 局面评估，用于无界面的批量平衡测量。
    /// 目标不是“强”，而是“不自闭、能出牌、能攻击”，以便测出规则本身的节奏问题。
    /// </summary>
    public static class V06Ai
    {
        private static int _seed = 12345;

        private static int NextRand()
        {
            _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
            return _seed;
        }

        public static void Seed(int s) { _seed = s == 0 ? 12345 : s; }

        /// <summary>局面评估：站在 me 的角度，越大越好。</summary>
        public static int Eval(V06Engine e, int me)
        {
            if (e.Finished)
            {
                if (e.Winner == me) { return 100000; }
                if (e.Winner == -2) { return 0; }
                return -100000;
            }
            V06Player a = e.Players[me];
            V06Player b = e.Players[1 - me];
            int s = 0;
            // 旗血最重要：规则里压力会在后期抹平差距，旗血差直接决定胜负
            s += (a.FlagHp - b.FlagHp) * 14;
            s += BoardValue(a) - BoardValue(b);
            s += (a.Hand.Count - b.Hand.Count) * 3;
            s += (int)((a.Deck.Count - b.Deck.Count) * 0.5);
            // 行动点浪费是损失
            s -= (a.ActionPoints - b.ActionPoints) / 3;
            if (e.Active == me && e.PendingAttack == null) { s += 2; }
            return s;
        }

        private static int BoardValue(V06Player p)
        {
            int v = 0;
            for (int s = 0; s < V06Slot.Total; s++)
            {
                V06Unit u = p.At(s);
                if (u == null) { continue; }
                v += u.AtkBaseNow * 3 + u.CurHp * 2;
                if (u.Has("护卫")) { v += 2; }
                if (u.Has("坚壁")) { v += 2; }
                if (u.Has("远射")) { v += 2; }
                if (u.Has("应援")) { v += 3; }          // V0.7 光环：给全队 +1
                if (u.Growth > 0) { v += u.Growth * 2; } // 兽群成长：越接近进化越有价值
                if (V06Slot.IsFront(s)) { v += 1; }
            }
            return v;
        }

        /// <summary>跑完整一局，双方都由 AI 控制。返回实际经过的步数。</summary>
        public static int RunGame(V06Engine e, int maxSteps)
        {
            int steps = 0;
            while (!e.Finished && steps < maxSteps)
            {
                steps++;
                bool progressed = Step(e);
                if (!progressed) { break; }
            }
            return steps;
        }

        /// <summary>执行当前行动方的一个决策。返回 false 表示无法推进。</summary>
        public static bool Step(V06Engine e)
        {
            if (e.Finished) { return false; }
            if (e.PendingAttack != null) { return StepResponse(e); }

            int me = e.Active;
            List<V06Action> acts = e.LegalActions();
            if (acts.Count == 0) { return false; }

            // 换位是免费且不消耗主行动的，必须先单独做掉，否则会被当成浪费一步
            for (int i = 0; i < acts.Count; i++)
            {
                if (acts[i].Kind == V06ActionKind.Swap)
                {
                    string err;
                    if (TryBestSwap(e, me, out err)) { return true; }
                    break;
                }
            }

            V06Action best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < acts.Count; i++)
            {
                V06Engine f = e.Clone();
                string err;
                if (!f.Apply(acts[i], out err)) { continue; }
                // 攻击后进入响应窗口：给对手一个“最优防守”估计
                int sc = ScoreAfter(f, me);
                // 随机扰动，避免镜像对局完全同步
                sc += NextRand() % 7 - 3;
                if (sc > bestScore) { bestScore = sc; best = acts[i]; }
            }
            if (best == null) { return false; }
            string e2;
            return e.Apply(best, out e2);
        }

        private static int ScoreAfter(V06Engine f, int me)
        {
            if (f.PendingAttack != null)
            {
                // 响应窗口：假设对手会选对它最有利的一步
                List<V06Action> rs = f.LegalActions();
                int worst = int.MaxValue;
                for (int i = 0; i < rs.Count; i++)
                {
                    V06Engine g = f.Clone();
                    string err;
                    if (!g.Apply(rs[i], out err)) { continue; }
                    int sc = Eval(g, me);
                    bool endMyTurn = g.Active != me && g.PendingAttack == null;
                    if (endMyTurn) { sc += Eval(g, me) / 20; }
                    if (sc < worst) { worst = sc; }
                }
                if (worst != int.MaxValue) { return worst; }
            }
            return Eval(f, me);
        }

        /// <summary>换位：只在“把高攻单位推到前排能打人 / 把残血单位撤到后排”时做。</summary>
        public static bool TryBestSwap(V06Engine e, int me, out string err)
        {
            err = string.Empty;
            V06Player p = e.Players[me];
            if (p.SwappedThisRound > 0) { return false; }
            int bestGain = 0;
            V06Action best = null;
            List<V06Unit> mine = p.Units();
            for (int i = 0; i < mine.Count; i++)
            {
                V06Unit u = mine[i];
                bool canHit = V06Slot.IsFront(u.Slot) || u.Has("远射") || u.Has("飞行");
                foreach (int slot in new int[] { 0, 1, 2, 3, 4, 5 })
                {
                    if (p.At(slot) != null) { continue; }
                    if (V06Slot.IsFront(u.Slot) == V06Slot.IsFront(slot)) { continue; }
                    int gain = 0;
                    if (!canHit && (V06Slot.IsFront(slot) || u.Has("远射") || u.Has("飞行")))
                    {
                        gain += u.AtkBaseNow * 3;
                    }
                    if (u.CurHp <= 2 && V06Slot.IsBack(slot) && canHit) { gain += 3; }
                    if (gain > bestGain) { bestGain = gain; best = new V06Action { Kind = V06ActionKind.Swap, Uid = u.Uid, TargetSlot = slot }; }
                }
            }
            if (best == null || bestGain <= 0) { return false; }
            return e.Apply(best, out err);
        }

        private static bool StepResponse(V06Engine e)
        {
            List<V06Action> rs = e.LegalActions();
            if (rs.Count == 0) { return false; }
            int defender = e.Active;
            V06Action best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < rs.Count; i++)
            {
                V06Engine f = e.Clone();
                string err;
                if (!f.Apply(rs[i], out err)) { continue; }
                int sc = Eval(f, defender);
                if (sc > bestScore) { bestScore = sc; best = rs[i]; }
            }
            if (best == null) { return false; }
            string e2;
            return e.Apply(best, out e2);
        }
    }
}
