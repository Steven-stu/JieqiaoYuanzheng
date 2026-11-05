using System;
using System.Collections.Generic;
using Jieqiao;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 界桥远征文字原型界面。
    ///
    /// 全部 UI 由代码生成，不依赖任何预制体或美术资源：没有图就直接用文字表达
    /// 单位、位置、状态与日志。交互原则来自文档第 12 章——只列出当前合法动作，
    /// 让「能做什么」由规则决定，而不是让玩家猜。
    /// </summary>
    public sealed class PrototypeUI : MonoBehaviour
    {
        private const int PageSize = 11;

        private GameEngine _engine;
        private HeuristicAi _ai;
        private bool _vsAi = true;
        private bool _humanControlsP1;
        private int _page;
        private float _aiTimer;
        private bool _needAiStep;

        private Text _stateText;
        private Text _logText;
        private Text _hintText;
        private Text _handText;
        private Transform _buttonRoot;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<GameAction> _pageActions = new List<GameAction>();
        private readonly List<int> _mulliganPicks = new List<int>();

        private void Start()
        {
            BuildUi();
            NewGame(202);
        }

        private void Update()
        {
            if (!_needAiStep || _engine == null || _engine.S.IsOver)
            {
                return;
            }
            _aiTimer -= Time.deltaTime;
            if (_aiTimer > 0f)
            {
                return;
            }
            _aiTimer = 0.45f;
            _needAiStep = AiShouldAct();
            if (_needAiStep)
            {
                _ai.Step(_engine);
                Refresh();
            }
        }

        // ── 建界面 ─────────────────────────────────────────────────────────

        private static Font _font;

        private static Font UiFont
        {
            get
            {
                if (_font == null)
                {
                    // 需要含中文字形的字体，Arial/LegacyRuntime 没有中文字形
                    string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun", "Noto Sans CJK SC" };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        Font f = Font.CreateDynamicFontFromOSFont(candidates[i], 16);
                        if (f != null)
                        {
                            _font = f;
                            break;
                        }
                    }
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }
                return _font;
            }
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("PrototypeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;

            // 背景
            Image bg = NewImage(canvasGo.transform, "Bg", new Color(0.08f, 0.09f, 0.11f, 1f));
            Stretch(bg.rectTransform);

            // 左：状态 + 日志
            _stateText = NewText(canvasGo.transform, "StateText", 18, TextAnchor.UpperLeft);
            var rt = _stateText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.34f);
            rt.anchorMax = new Vector2(0.58f, 1f);
            rt.offsetMin = new Vector2(16f, 8f);
            rt.offsetMax = new Vector2(-8f, -12f);

            _logText = NewText(canvasGo.transform, "LogText", 15, TextAnchor.LowerLeft);
            var lrt = _logText.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0.58f, 0.34f);
            lrt.offsetMin = new Vector2(16f, 74f);
            lrt.offsetMax = new Vector2(-8f, -4f);
            _logText.color = new Color(0.72f, 0.76f, 0.82f, 1f);

            // 左下：手牌与操作提示
            _handText = NewText(canvasGo.transform, "HandText", 16, TextAnchor.LowerLeft);
            var hrt = _handText.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(0.58f, 0f);
            hrt.pivot = new Vector2(0f, 0f);
            hrt.offsetMin = new Vector2(16f, 10f);
            hrt.offsetMax = new Vector2(-8f, 66f);
            _handText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            // 右：按钮列
            var right = new GameObject("RightPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            right.SetParent(canvasGo.transform, false);
            right.anchorMin = new Vector2(0.58f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.offsetMin = new Vector2(8f, 10f);
            right.offsetMax = new Vector2(-12f, -12f);

            _hintText = NewText(right, "Hint", 17, TextAnchor.UpperLeft);
            var hintRt = _hintText.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0.92f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.offsetMin = Vector2.zero;
            hintRt.offsetMax = Vector2.zero;
            _hintText.color = new Color(0.55f, 0.85f, 0.95f, 1f);

            var stack = new GameObject("ButtonStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
            stack.transform.SetParent(right, false);
            var srt = stack.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 0.91f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var vlg = stack.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.padding = new RectOffset(0, 6, 0, 0);
            _buttonRoot = stack.transform;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text NewText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = UiFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = new Color(0.88f, 0.9f, 0.93f, 1f);
            return t;
        }

        // ── 按钮池 ─────────────────────────────────────────────────────────

        private void ClearButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] != null)
                {
                    Destroy(_buttons[i].gameObject);
                }
            }
            _buttons.Clear();
            _pageActions.Clear();
        }

        private Button AddButton(string label, Color tint, Action onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonRoot, false);
            var img = go.GetComponent<Image>();
            img.color = tint;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = 32f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.GetComponent<Text>();
            t.font = UiFont;
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(0.97f, 0.97f, 0.98f, 1f);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var trt = t.rectTransform;
            Stretch(trt);
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-6f, 0f);
            t.text = label;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.normalColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() =>
            {
                onClick();
                Refresh();
            });
            _buttons.Add(btn);
            return btn;
        }

        private static readonly Color ActionTint = new Color(0.19f, 0.28f, 0.42f, 1f);
        private static readonly Color DangerTint = new Color(0.45f, 0.20f, 0.22f, 1f);
        private static readonly Color GoodTint = new Color(0.17f, 0.35f, 0.26f, 1f);
        private static readonly Color NeutralTint = new Color(0.22f, 0.22f, 0.26f, 1f);

        // ── 局面控制 ───────────────────────────────────────────────────────

        public void NewGame(int seed)
        {
            _engine = new GameEngine();
            _ai = new HeuristicAi(seed * 31 + 7);
            _engine.Start(seed);
            _page = 0;
            _mulliganPicks.Clear();
            _needAiStep = false;
            _aiTimer = 0.3f;
            Refresh();
        }

        private bool AiShouldAct()
        {
            if (_vsAi == false)
            {
                return false;
            }
            if (_engine.Pending == PendingKind.Cover)
            {
                return 1 - _engine.S.CurrentPlayer == 1;
            }
            if (_engine.Pending == PendingKind.ScoutPick)
            {
                return _engine.S.CurrentPlayer == 1;
            }
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                return false;
            }
            return _engine.S.CurrentPlayer == 1;
        }

        private bool HumanTurn
        {
            get
            {
                if (_vsAi == false)
                {
                    return true; // 双人同屏：谁都可以操作
                }
                if (_engine.Pending == PendingKind.Cover)
                {
                    return 1 - _engine.S.CurrentPlayer == 0;
                }
                if (_engine.Pending == PendingKind.ScoutPick)
                {
                    return _engine.S.CurrentPlayer == 0;
                }
                return _engine.S.CurrentPlayer == 0;
            }
        }

        // ── 刷新 ───────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_engine == null)
            {
                return;
            }
            _stateText.text = BuildStateText();
            _logText.text = BuildLogText();
            _handText.text = BuildHandText();
            ClearButtons();

            if (_engine.S.IsOver)
            {
                RenderGameOver();
                _needAiStep = false;
                return;
            }
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                RenderMulligan();
                _needAiStep = false;
                return;
            }
            if (_engine.Pending == PendingKind.Cover)
            {
                RenderCover();
                return;
            }
            if (_engine.Pending == PendingKind.ScoutPick)
            {
                RenderScout();
                return;
            }
            _needAiStep = AiShouldAct();
            if (_needAiStep)
            {
                _hintText.text = "对手（乙）正在行动…";
                AddButton("换一局（新种子）", NeutralTint, () => NewGame(Environment.TickCount & 0xffff));
                return;
            }
            RenderActions();
        }

        private string BuildStateText()
        {
            GameState s = _engine.S;
            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("<b>界桥远征 · 文字原型</b>  第 {0} 轮   先手：{1}\n",
                Math.Max(1, s.Round), s.Round > 0 ? s.Players[s.FirstPlayerThisRound].Name : "-");
            sb.AppendFormat("模式：{0}\n\n", _vsAi ? "单人对 AI" : "双人同屏");

            sb.Append("<b>── 乙（敌方） ──</b>\n");
            sb.Append(PlayerSummary(s.Players[1]));
            sb.Append(BoardText(s.Players[1]));
            sb.Append('\n');
            sb.Append("<b>── 甲（我方） ──</b>\n");
            sb.Append(PlayerSummary(s.Players[0]));
            sb.Append(BoardText(s.Players[0]));
            return sb.ToString();
        }

        private string PlayerSummary(PlayerState p)
        {
            string reserve = string.Empty;
            for (int i = 0; i < p.Reserve.Count; i++)
            {
                ReservedCard rc = p.Reserve[i];
                reserve += (rc.Used ? "<color=#666666>" + rc.Def.Name + "(已用)</color>" : rc.Def.Name) + " ";
            }
            var flags = new List<string>();
            if (p.CoverUsed)
            {
                flags.Add("掩护已用");
            }
            if (p.RepositionUsed)
            {
                flags.Add("换防已用");
            }
            if (p.ScoutUsed)
            {
                flags.Add("侦察已用");
            }
            if (p.ReserveUsedThisRound)
            {
                flags.Add("预备已用");
            }
            if (p.ClearFrontBonusUsed)
            {
                flags.Add("清线奖励已取");
            }
            return string.Format(
                "营地 {0}   补给 {1}(+{2}结转)   行动 {3}   手牌 {4}张   牌库 {5}   弃牌 {6}\n预备区：{7}\n[本轮]{8}\n",
                p.Camp, p.Supply, p.Carryover, p.ActionsLeft, p.Hand.Count, p.Deck.Count, p.Discard.Count,
                reserve.Trim(), flags.Count == 0 ? "（无）" : string.Join("、", flags.ToArray()));
        }

        private string BoardText(PlayerState p)
        {
            var sb = new System.Text.StringBuilder();
            var back = p.BackUnits;
            var front = p.FrontUnits;
            sb.Append("  后排：");
            sb.Append(back.Count == 0 ? "（空）" : RowLine(back));
            sb.Append('\n');
            sb.Append("  前排：");
            sb.Append(front.Count == 0 ? "（空）" : RowLine(front));
            sb.Append('\n');
            return sb.ToString();
        }

        private string RowLine(List<UnitState> units)
        {
            var parts = new List<string>();
            for (int i = 0; i < units.Count; i++)
            {
                parts.Add(units[i].StatLine);
            }
            return string.Join("  |  ", parts.ToArray());
        }

        private string BuildLogText()
        {
            List<LogEntry> log = _engine.Log;
            int take = Math.Min(11, log.Count);
            var sb = new System.Text.StringBuilder();
            for (int i = log.Count - take; i < log.Count; i++)
            {
                sb.Append(log[i]).Append('\n');
            }
            return sb.ToString();
        }

        private string BuildHandText()
        {
            // 单人对 AI：显示自己的手牌；双人同屏：显示当前行动方的手牌（交接式）
            int viewer = _vsAi ? 0 : _engine.S.CurrentPlayer;
            PlayerState p = _engine.S.Players[viewer];
            var parts = new List<string>();
            for (int i = 0; i < p.Hand.Count; i++)
            {
                CardInstance c = p.Hand[i];
                string mark = _mulliganPicks.Contains(c.Uid) ? "✔" : string.Empty;
                parts.Add(string.Format("{0}{1}[{2}]", mark, c.Def.Name, c.Def.Cost));
            }
            string hand = parts.Count == 0 ? "（空）" : string.Join(" ", parts.ToArray());
            string head = _vsAi ? "我的手牌" : p.Name + " 的手牌（当前行动方）";
            PlayerState other = _engine.S.Players[1 - viewer];
            return string.Format("{0}：{1}\n{2}手牌 {3} 张（隐藏）", head, hand, other.Name, other.Hand.Count);
        }

        // ── 各类渲染 ───────────────────────────────────────────────────────

        private void RenderMulligan()
        {
            _hintText.text = "换牌阶段：点手牌勾选要换掉的（至多 2 张，领航不能换）";
            PlayerState me = _engine.S.Players[0];
            for (int i = 0; i < me.Hand.Count; i++)
            {
                CardInstance c = me.Hand[i];
                if (c.Uid == me.VanguardUid)
                {
                    continue;
                }
                int uid = c.Uid;
                string label = (_mulliganPicks.Contains(uid) ? "✔ 换掉 " : "   换掉 ") + c.Def.Name + "(" + c.Def.Cost + ")";
                AddButton(label, _mulliganPicks.Contains(uid) ? DangerTint : ActionTint, () =>
                {
                    if (_mulliganPicks.Contains(uid))
                    {
                        _mulliganPicks.Remove(uid);
                    }
                    else if (_mulliganPicks.Count < 2)
                    {
                        _mulliganPicks.Add(uid);
                    }
                });
            }
            AddButton("确认换牌（" + _mulliganPicks.Count + " 张）", GoodTint, ConfirmMyMulligan);
            AddButton("不换牌直接开始", NeutralTint, () =>
            {
                _mulliganPicks.Clear();
                ConfirmMyMulligan();
            });
        }

        private void ConfirmMyMulligan()
        {
            _engine.ConfirmMulligan(0, new List<int>(_mulliganPicks));
            _mulliganPicks.Clear();
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                // 乙由 AI 决定
                _engine.ConfirmMulligan(1, _ai.PickMulligan(_engine, 1));
            }
            _page = 0;
            Refresh();
        }

        private void RenderCover()
        {
            int defender = 1 - _engine.S.CurrentPlayer;
            GameAction pending = _engine.PendingAction;
            _hintText.text = string.Format("掩护窗口：{0} 的 {1} 即将造成伤害，{2} 是否支付 1 补给减伤 1？",
                _engine.S.Players[_engine.S.CurrentPlayer].Name,
                pending != null ? pending.Label : "行动",
                _engine.S.Players[defender].Name);
            AddButton("使用掩护（-1 补给，本次伤害 -1）", GoodTint, () => _engine.ResolveCover(true));
            AddButton("放弃掩护", NeutralTint, () => _engine.ResolveCover(false));
        }

        private void RenderScout()
        {
            _hintText.text = "侦察：选 1 张加入手牌，其余按原顺序放到牌库底";
            for (int i = 0; i < _engine.PendingScoutCards.Count; i++)
            {
                int index = i;
                CardInstance c = _engine.PendingScoutCards[i];
                AddButton(string.Format("取得 {0}（费{1}）", c.Def.Name, c.Def.Cost), ActionTint,
                    () => _engine.ResolveScoutPick(index));
            }
        }

        private void RenderActions()
        {
            List<GameAction> actions = _engine.LegalActions();
            PlayerState me = _engine.S.Players[_engine.S.CurrentPlayer];
            _hintText.text = string.Format("{0} 行动中：剩 {1} 次主行动，补给 {2}",
                me.Name, me.ActionsLeft, me.Supply);

            // 把「结束本轮」和「换防/侦察」放到最前，其余动作分页
            var primary = new List<GameAction>();
            var rest = new List<GameAction>();
            for (int i = 0; i < actions.Count; i++)
            {
                GameAction a = actions[i];
                if (a.Kind == ActionKind.EndRound || a.Kind == ActionKind.Reposition || a.Kind == ActionKind.Scout)
                {
                    primary.Add(a);
                }
                else
                {
                    rest.Add(a);
                }
            }

            int pages = Math.Max(1, (rest.Count + PageSize - 1) / PageSize);
            if (_page >= pages)
            {
                _page = 0;
            }

            for (int i = 0; i < primary.Count; i++)
            {
                GameAction a = primary[i];
                AddButton(a.Label, a.Kind == ActionKind.EndRound ? DangerTint : NeutralTint, () => Apply(a));
            }

            int start = _page * PageSize;
            for (int i = start; i < rest.Count && i < start + PageSize; i++)
            {
                GameAction a = rest[i];
                AddButton(a.Label, ActionTint, () => Apply(a));
            }

            if (pages > 1)
            {
                int p = _page;
                AddButton(string.Format("第 {0}/{1} 页 — 下一页", p + 1, pages), NeutralTint,
                    () => _page = (p + 1) % pages);
            }
            AddButton("换一局（新种子）", NeutralTint, () => NewGame(Environment.TickCount & 0xffff));
            AddButton(_vsAi ? "切换为双人同屏" : "切换为单人对 AI", NeutralTint, () =>
            {
                _vsAi = !_vsAi;
                _page = 0;
            });
        }

        private void Apply(GameAction a)
        {
            _engine.Apply(a);
            _page = 0;
        }

        private void RenderGameOver()
        {
            GameState s = _engine.S;
            string who = s.Winner == 2 ? "平局" : s.Players[s.Winner].Name + " 获胜";
            _hintText.text = "对局结束：" + who;
            AddButton("再看一遍记录", NeutralTint, () => { });
            AddButton("新开一局（同种子复现）", GoodTint, () => NewGame(202));
            AddButton("新开一局（随机种子）", ActionTint, () => NewGame(Environment.TickCount & 0xffff));
        }
    }
}
