using System;
using System.Collections.Generic;

namespace Jieqiao
{
    /// <summary>战场上的一格：前排 2 格、后排 2 格。</summary>
    public struct Slot
    {
        public bool Front;
        public int Index;

        public Slot(bool front, int index)
        {
            Front = front;
            Index = index;
        }

        public string Label
        {
            get { return (Front ? "前" : "后") + (Index + 1); }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>一张实体卡（同名多张用 Uid 区分，对应文档里「#29」那种编号）。</summary>
    public sealed class CardInstance
    {
        public int Uid;
        public CardDef Def;

        public CardInstance(int uid, CardDef def)
        {
            Uid = uid;
            Def = def;
        }

        public string Short
        {
            get { return Def.Name + "#" + Uid; }
        }
    }

    /// <summary>场上的一个单位，包含累计伤害、装备、晋阶与行动标记。</summary>
    public sealed class UnitState
    {
        public CardInstance Card;
        public Slot Slot;
        public int Damage;
        public CardDef Upgrade;
        public CardDef Equipment;
        public bool AttackedThisRound;
        public int TempAtkMod;
        /// <summary>部署时的轮次与「己方第几次主行动」，用于判定新单位何时可以行动。</summary>
        public int DeployedRound;
        public int DeployedAtActionIndex;

        public int BaseAtk
        {
            get { return Upgrade != null ? Upgrade.Atk : Card.Def.Atk; }
        }

        public int MaxHp
        {
            get { return Upgrade != null ? Upgrade.Hp : Card.Def.Hp; }
        }

        public int CurrentHp
        {
            get { return MaxHp - Damage; }
        }

        public int Atk
        {
            get
            {
                int a = BaseAtk;
                if (Equipment != null && Equipment.Effect == Effect.EquipPlus1Attack)
                {
                    a += 1;
                }
                a += TempAtkMod;
                return a < 0 ? 0 : a;
            }
        }

        /// <summary>晋阶会覆盖关键词（破阵先锋失去护卫），装备可额外赋予远射。</summary>
        public bool HasGuard
        {
            get { return Upgrade != null ? Upgrade.Guard : Card.Def.Guard; }
        }

        public bool HasRanged
        {
            get
            {
                bool baseRanged = Upgrade != null ? Upgrade.Ranged : Card.Def.Ranged;
                bool fromEquip = Equipment != null && Equipment.Effect == Effect.EquipRanged;
                return baseRanged || fromEquip;
            }
        }

        public bool IsWounded
        {
            get { return Damage > 0; }
        }

        public string Short
        {
            get { return Card.Short; }
        }

        /// <summary>「攻击 / 当前生命 / 最大生命」的一行式显示。</summary>
        public string StatLine
        {
            get
            {
                string kw = string.Empty;
                if (HasGuard)
                {
                    kw += " 护卫";
                }
                if (HasRanged)
                {
                    kw += " 远射";
                }
                string extra = string.Empty;
                if (Equipment != null)
                {
                    extra += " [" + Equipment.Name + "]";
                }
                if (Upgrade != null)
                {
                    extra += " (晋阶:" + Upgrade.Name + ")";
                }
                string mark = AttackedThisRound ? " 已攻击" : string.Empty;
                if (TempAtkMod != 0)
                {
                    extra += " 攻击" + TempAtkMod;
                }
                return string.Format("{0,-10} {1}攻/{2}血 于{3}{4}{5}{6}",
                    Card.Short, Atk, CurrentHp, Slot.Label, kw, extra, mark);
            }
        }
    }

    public sealed class ReservedCard
    {
        public CardDef Def;
        public bool Used;
    }

    public sealed class PlayerState
    {
        public string Name;
        public int Index;
        public int Camp;
        public int Supply;
        public int Carryover;
        public int ActionsLeft;

        public List<CardInstance> Deck = new List<CardInstance>();
        public List<CardInstance> Hand = new List<CardInstance>();
        public List<CardInstance> Discard = new List<CardInstance>();
        public List<UnitState> Board = new List<UnitState>();
        public List<ReservedCard> Reserve = new List<ReservedCard>();

        // 每轮重置的公开标记
        public bool CoverUsed;
        public bool RepositionUsed;
        public bool ScoutUsed;
        public bool ReserveUsedThisRound;
        public bool ClearFrontBonusUsed;
        /// <summary>V0.3 §6.6 强攻：每方每轮一次额度，第 4 轮起开放。</summary>
        public bool PowerStrikeUsed;
        /// <summary>V0.3 §12.1「本轮自动放弃掩护」设置：只影响是否弹窗，不自动花费补给；每轮复位。</summary>
        public bool AutoDeclineCover;

        /// <summary>本局已经行使过多少次主行动，用于新单位行动资格。</summary>
        public int ActionIndexThisRound;

        /// <summary>领航单位的实体卡 Uid（换牌时不能被换下）。</summary>
        public int VanguardUid = -1;

        public bool CanDeploy
        {
            get { return Board.Count < 4; }
        }

        public UnitState At(Slot slot)
        {
            for (int i = 0; i < Board.Count; i++)
            {
                if (Board[i].Slot.Front == slot.Front && Board[i].Slot.Index == slot.Index)
                {
                    return Board[i];
                }
            }
            return null;
        }

        public bool SlotFree(Slot slot)
        {
            return At(slot) == null;
        }

        public List<UnitState> FrontUnits
        {
            get { return Row(true); }
        }

        public List<UnitState> BackUnits
        {
            get { return Row(false); }
        }

        public List<UnitState> Row(bool front)
        {
            var list = new List<UnitState>();
            for (int i = 0; i < Board.Count; i++)
            {
                if (Board[i].Slot.Front == front)
                {
                    list.Add(Board[i]);
                }
            }
            list.Sort(delegate (UnitState a, UnitState b) { return a.Slot.Index.CompareTo(b.Slot.Index); });
            return list;
        }

        public int FindUnitIndex(int uid)
        {
            for (int i = 0; i < Board.Count; i++)
            {
                if (Board[i].Card.Uid == uid)
                {
                    return i;
                }
            }
            return -1;
        }

        public ReservedCard FindReserve(string id)
        {
            for (int i = 0; i < Reserve.Count; i++)
            {
                if (Reserve[i].Def.Id == id)
                {
                    return Reserve[i];
                }
            }
            return null;
        }

        public PlayerState Clone()
        {
            var p = new PlayerState
            {
                Name = Name,
                Index = Index,
                Camp = Camp,
                Supply = Supply,
                Carryover = Carryover,
                ActionsLeft = ActionsLeft,
                CoverUsed = CoverUsed,
                RepositionUsed = RepositionUsed,
                ScoutUsed = ScoutUsed,
                ReserveUsedThisRound = ReserveUsedThisRound,
                ClearFrontBonusUsed = ClearFrontBonusUsed,
                PowerStrikeUsed = PowerStrikeUsed,
                AutoDeclineCover = AutoDeclineCover,
                ActionIndexThisRound = ActionIndexThisRound,
                VanguardUid = VanguardUid,
            };
            for (int i = 0; i < Deck.Count; i++)
            {
                p.Deck.Add(Deck[i]);
            }
            for (int i = 0; i < Hand.Count; i++)
            {
                p.Hand.Add(Hand[i]);
            }
            for (int i = 0; i < Discard.Count; i++)
            {
                p.Discard.Add(Discard[i]);
            }
            for (int i = 0; i < Reserve.Count; i++)
            {
                p.Reserve.Add(new ReservedCard { Def = Reserve[i].Def, Used = Reserve[i].Used });
            }
            for (int i = 0; i < Board.Count; i++)
            {
                UnitState u = Board[i];
                p.Board.Add(new UnitState
                {
                    Card = u.Card,
                    Slot = u.Slot,
                    Damage = u.Damage,
                    Upgrade = u.Upgrade,
                    Equipment = u.Equipment,
                    AttackedThisRound = u.AttackedThisRound,
                    TempAtkMod = u.TempAtkMod,
                    DeployedRound = u.DeployedRound,
                    DeployedAtActionIndex = u.DeployedAtActionIndex,
                });
            }
            return p;
        }
    }

    public enum GamePhase
    {
        Setup,
        Mulligan,
        Playing,
        Finished,
    }

    public sealed class GameState
    {
        public int Round;
        public int FirstPlayerThisRound;
        public int CurrentPlayer;
        public GamePhase Phase;
        public PlayerState[] Players = new PlayerState[2];
        public int Winner = -1; // -1 未定，0/1 胜者，2 平局
        public string FinishReason;

        /// <summary>V0.3 §1.3：终局说明要给出各项损失，所以按来源累计（下标为玩家）。</summary>
        public int[] LossToll = new int[2];
        public int[] LossDirect = new int[2];
        public int[] LossClear = new int[2];
        public int[] LossTide = new int[2];

        public PlayerState Me(int i)
        {
            return Players[i];
        }

        public PlayerState Opponent(int i)
        {
            return Players[1 - i];
        }

        public bool IsOver
        {
            get { return Phase == GamePhase.Finished; }
        }

        public GameState Clone()
        {
            var g = new GameState
            {
                Round = Round,
                FirstPlayerThisRound = FirstPlayerThisRound,
                CurrentPlayer = CurrentPlayer,
                Phase = Phase,
                Winner = Winner,
                FinishReason = FinishReason,
            };
            g.Players[0] = Players[0].Clone();
            g.Players[1] = Players[1].Clone();
            for (int i = 0; i < 2; i++)
            {
                g.LossToll[i] = LossToll[i];
                g.LossDirect[i] = LossDirect[i];
                g.LossClear[i] = LossClear[i];
                g.LossTide[i] = LossTide[i];
            }
            return g;
        }
    }

    public sealed class LogEntry
    {
        public int Round;
        public string Text;

        public override string ToString()
        {
            return "[R" + Round + "] " + Text;
        }
    }

    /// <summary>确定性随机：同种子必须复现同一局（文档要求可重放）。</summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            _state = seed == 0 ? 2463534242u : (uint)seed;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                return 0;
            }
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return (int)(_state % (uint)maxExclusive);
        }

        public void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
