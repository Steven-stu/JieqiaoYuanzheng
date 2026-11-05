using System;
using System.Collections.Generic;

namespace Jieqiao
{
    public enum ActionKind
    {
        Deploy,
        Reposition,
        Attack,
        Tactic,
        Equip,
        Upgrade,
        Support,
        Scout,
        /// <summary>调用公开预备区的晋阶或支援牌（共用每轮一次额度）。</summary>
        Reserve,
        EndRound,
        Mulligan,
    }

    /// <summary>
    /// 一个已完全确定的动作。UI 只负责把 LegalActions 里的项渲染成可点选项，
    /// 规则判定全部留在引擎里，避免界面和规则各写一套。
    /// </summary>
    public sealed class GameAction
    {
        public ActionKind Kind;
        public int CardUid = -1;
        public string ReserveId;
        public int ActorUid = -1;
        public Slot ToSlot;
        public int TargetUid = -1;
        public bool TargetCamp;
        /// <summary>V0.3 §6.6：本次攻击是否使用强攻（额外 -2 补给、主动伤害 +1）。</summary>
        public bool PowerStrike;
        /// <summary>装备替换提示：目标已装备时说明「会替换下什么 / 同名替换没有收益」。</summary>
        public string SwapNote;
        public List<int> MulliganUids;
        public string Label;

        public override string ToString()
        {
            return Label ?? Kind.ToString();
        }
    }
}
