using System;
using System.Collections.Generic;
using Jieqiao;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 界桥远征 2D 灰模界面。
    ///
    /// 灰模原则：不用任何贴图，卡牌就是带边框的色块 + 文字，但它必须是
    /// 「能直接点」的真实对局界面：点手牌 → 合法目标高亮 → 点目标执行；
    /// 点自己单位 → 敌方可攻击目标高亮。
    ///
    /// 合法性判定完全来自 GameEngine.LegalActions()，界面只负责把结果画出来，
    /// 因此不会出现「点了却违反规则」的分叉。
    /// </summary>
    public sealed class Battle2DUI : MonoBehaviour
    {
        // ── 布局常量（设计空间 1600x900，左上角原点，整体等比缩放）──────
        private const float BattleX = 20f;
        private const float BattleW = 1200f;
        private const float CardW = 104f;
        private const float CardH = 128f;
        private const float HandW = 96f;
        private const float HandH = 122f;
        private const float CampYEnemy = 12f;
        private const float BackYEnemy = 56f;
        private const float FrontYEnemy = 196f;
        private const float MidY = 336f;
        private const float FrontYMine = 442f;
        private const float BackYMine = 582f;
        private const float CampYMine = 722f;
        private const float HandY = 764f;

        private const float SlotX0 = 509f;
        private const float SlotX1 = 627f;
        private const float HandX0 = 196f;
        private const float HandStep = 104f;

        private const float RightX = 1240f;
        private const float RightW = 340f;

        private GameEngine _engine;
        private HeuristicAi _ai;
        private bool _vsAi = true;
        private float _aiTimer;
        private bool _aiRunning;

        private CardView[] _myBoard;      // 0 前1, 1 前2, 2 后1, 3 后2
        private CardView[] _enemyBoard;
        private CardView[] _hand;
        private CardView[] _reserve;
        private Image _myCampFill;
        private Image _enemyCampFill;
        private Text _myCampText;
        private Text _enemyCampText;
        private Button _myCampBtn;
        private Button _enemyCampBtn;
        private Text _statusText;
        private Text _logText;
        private Text _hintText;
        private Text _enemyReserveText;
        private Text _myPanelText;
        private Text _discardMine;
        private Text _discardFoe;
        private Transform _root;
        private RectTransform _layoutRoot;
        private GameObject _overlay;
        /// <summary>V0.3 §6.6 强攻：点目标前先决定这次攻击是否支付 2 补给。</summary>
        private bool _powerStrikeMode;
        private Button _powerStrikeBtn;
        private Button _autoCoverBtn;
        private Button _winBtn;
        /// <summary>卡牌详情浮层当前展示的牌（第一次点击手牌时只"看"，不直接使用）。</summary>
        private CardInstance _detailCard;
        // ── 结算可读性：让玩家看得见"谁死了、谁的营地为什么掉血" ──
        private int _lastCamp0 = -1;
        private int _lastCamp1 = -1;
        private int _lastLogSeen;
        private string _settleText;
        private float _settleTimer;
        private float _flash0;
        private float _flash1;
        private int _delta0;
        private int _delta1;

        // 选择状态
        private List<GameAction> _actions = new List<GameAction>();
        private List<GameAction> _candidates = new List<GameAction>();
        private CardInstance _selCard;
        private string _selReserve;
        private UnitState _selUnit;
        private bool _repositionMode;
        private readonly List<int> _mulliganPicks = new List<int>();
        /// <summary>换牌浮层当前在为哪位玩家收选项（双人同屏时 0 → 1）。</summary>
        private int _mulliganViewer;
        /// <summary>一次性的提示文字（例如「这张牌为什么打不出」）。</summary>
        private string _selHint;
        /// <summary>界面整体缩放：小屏时可以放大看清卡面文字。</summary>
        private float _zoom = 1f;

        // 自适应铺满：窗口比 16:9 更宽时，把多余的宽度横向分配出去，
        // 而不是留黑边。三组元素各自记录设计位置，按额外的宽度重新摆放。
        private readonly List<KeyValuePair<RectTransform, Vector2>> _centerGroup = new List<KeyValuePair<RectTransform, Vector2>>();
        private readonly List<KeyValuePair<RectTransform, Vector2>> _rightGroup = new List<KeyValuePair<RectTransform, Vector2>>();
        private readonly List<KeyValuePair<RectTransform, float>> _stretchGroup = new List<KeyValuePair<RectTransform, float>>();
        private float _appliedExtraW = float.NegativeInfinity;
        /// <summary>当前布局总宽度（设计空间 1600 加上横向多出来的部分）。</summary>
        private float _layoutW = UiKit.RefW;

        private void RegisterCenter(RectTransform rt)
        {
            _centerGroup.Add(new KeyValuePair<RectTransform, Vector2>(rt, rt.anchoredPosition));
        }

        private void RegisterRight(RectTransform rt)
        {
            _rightGroup.Add(new KeyValuePair<RectTransform, Vector2>(rt, rt.anchoredPosition));
        }

        private void RegisterStretch(RectTransform rt)
        {
            _stretchGroup.Add(new KeyValuePair<RectTransform, float>(rt, rt.sizeDelta.x));
        }

        private void Start()
        {
            // Unity 默认只在 Game 视图有焦点时推进玩家循环；焦点跑掉后 AI 回合
            // 和界面缩放都会停住。原型是回合制，后台运行不会误操作，直接打开。
            Application.runInBackground = true;
            BuildScene();
            NewGame(202);
        }

        private void Update()
        {
            ApplyLayoutScale();
            if (_engine == null)
            {
                return;
            }
            // 结算提示与营地闪红：即使不在 AI 回合也要走的计时
            if (_settleTimer > 0f)
            {
                _settleTimer -= Time.deltaTime;
                if (_settleTimer <= 0f)
                {
                    _settleText = null;
                    if (!_engine.S.IsOver)
                    {
                        Refresh();
                    }
                }
            }
            ApplyCampFlash();
            if (Input.GetKeyDown(KeyCode.F11))
            {
                ToggleWindowMode();
            }
            if (_engine.S.IsOver || !_aiRunning)
            {
                return;
            }
            _aiTimer -= Time.deltaTime;
            if (_aiTimer > 0f)
            {
                return;
            }
            // 挂起的决定如果属于玩家（例如 AI 打你、该你决定是否掩护），
            // 必须停下来等玩家，不能让 AI 顺手替他决定。
            if (_engine.Pending != PendingKind.None && PendingNeedsHuman())
            {
                _aiRunning = false;
                Refresh();
                return;
            }
            _aiTimer = 0.4f;
            _ai.Step(_engine);
            if (PendingNeedsHuman() || _engine.S.Phase == GamePhase.Mulligan)
            {
                _aiRunning = false;
            }
            ClearSelection();
            Refresh();
        }

        // ── 缩放 ──────────────────────────────────────────────────

        /// <summary>缩放界面（0.6–2.2 倍）。</summary>
        public void SetZoom(float z)
        {
            _zoom = Mathf.Clamp(z, 0.6f, 2.2f);
            ApplyLayoutScale();
            if (_hintText != null)
            {
                _hintText.text = "界面缩放 " + (_zoom * 100f).ToString("0") + "%（＋/－ 调整，「适应」复位）";
            }
        }

        /// <summary>
        /// 按当前画布尺寸等比缩放整个布局。设计空间固定 1600x900，
        /// 取宽高比例中较小的那个，保证任何窗口比例下都不裁切。
        /// </summary>
        private void ApplyLayoutScale()
        {
            if (_layoutRoot == null)
            {
                return;
            }
            var canvasRt = _layoutRoot.parent as RectTransform;
            if (canvasRt == null || canvasRt.rect.width <= 1f || canvasRt.rect.height <= 1f)
            {
                return;
            }
            float cw = canvasRt.rect.width;
            float ch = canvasRt.rect.height;

            // 先按高度铺满（保证卡牌和文字size不变形），再看有多少额外宽度可以横向利用。
            float s = ch / UiKit.RefH * _zoom;
            float extraW = 0f;
            if (s > 0f)
            {
                float widthAtThisScale = cw / s;
                if (widthAtThisScale >= UiKit.RefW)
                {
                    extraW = widthAtThisScale - UiKit.RefW;
                }
                else
                {
                    // 窗口比设计空间还窄：退回等比缩放，宁可留上下，也不裁切
                    s = Mathf.Min(cw / UiKit.RefW, ch / UiKit.RefH) * _zoom;
                }
            }
            if (s <= 0f)
            {
                s = 1f;
            }
            // 每帧比对实际值：CanvasScaler 首帧可能报过渡尺寸，只在变化时计算
            // 会把错误比例缓存下来，导致界面溢出或被压小。
            if (Mathf.Abs(_layoutRoot.localScale.x - s) >= 0.001f)
            {
                _layoutRoot.localScale = new Vector3(s, s, 1f);
            }
            if (Mathf.Abs(extraW - _appliedExtraW) >= 0.5f)
            {
                _appliedExtraW = extraW;
                ApplyExtraWidth(extraW);
            }
        }

        /// <summary>把多余的宽度分配给战场与右栏，避免左右留大片空白。</summary>
        private void ApplyExtraWidth(float extraW)
        {
            // 布局根容器本身也要加宽，否则元素会超出居中容器的右边界。
            _layoutW = UiKit.RefW + extraW;
            if (_layoutRoot != null)
            {
                _layoutRoot.sizeDelta = new Vector2(_layoutW, UiKit.RefH);
            }
            for (int i = 0; i < _centerGroup.Count; i++)
            {
                RectTransform rt = _centerGroup[i].Key;
                Vector2 b = _centerGroup[i].Value;
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(b.x + extraW * 0.5f, b.y);
                }
            }
            for (int i = 0; i < _rightGroup.Count; i++)
            {
                RectTransform rt = _rightGroup[i].Key;
                Vector2 b = _rightGroup[i].Value;
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(b.x + extraW, b.y);
                }
            }
            for (int i = 0; i < _stretchGroup.Count; i++)
            {
                RectTransform rt = _stretchGroup[i].Key;
                float baseW = _stretchGroup[i].Value;
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(baseW + extraW, rt.sizeDelta.y);
                }
            }
        }

        // ── 建界面骨架（一次）─────────────────────────────────────

        private void BuildScene()
        {
            // uGUI 点击要靠 EventSystem 转发；场景里没有它，所有按钮都不响应鼠标。
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                esGo.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiKit.RefW, UiKit.RefH);
            scaler.matchWidthOrHeight = 0.5f;

            var layoutGo = new GameObject("LayoutRoot", typeof(RectTransform));
            layoutGo.transform.SetParent(canvasGo.transform, false);
            _layoutRoot = layoutGo.GetComponent<RectTransform>();
            _layoutRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _layoutRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _layoutRoot.pivot = new Vector2(0.5f, 0.5f);
            _layoutRoot.anchoredPosition = Vector2.zero;
            _layoutRoot.sizeDelta = new Vector2(UiKit.RefW, UiKit.RefH);
            _root = layoutGo.transform;
            ApplyLayoutScale();

            // 战斗背景：铺满整个画布（挂在 Canvas 下，不受 LayoutRoot 缩放影响）。
            // 素材：Assets/Resources/Art/battle_bg.png（暮色浮岛与断裂星环）。
            Sprite bgSprite = Resources.Load<Sprite>("Art/battle_bg");
            if (bgSprite != null)
            {
                var bgGo = new GameObject("BattleBg", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(canvasGo.transform, false);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                bgRt.SetAsFirstSibling();
                var bgImg = bgGo.GetComponent<Image>();
                bgImg.sprite = bgSprite;
                bgImg.preserveAspect = false;
                bgImg.color = new Color(0.72f, 0.76f, 0.84f, 1f);
                bgImg.raycastTarget = false;
            }
            // 背景之上压一层深色薄纱，保证灰模文字可读
            UiKit.Panel(_root, "Bg", 0f, 0f, UiKit.RefW, UiKit.RefH, new Color(UiKit.Bg.r, UiKit.Bg.g, UiKit.Bg.b, 0.52f));

            // 敌方营地条（点击＝直击营地）
            Image enemyStrip = UiKit.Panel(_root, "EnemyCampStrip", BattleX, CampYEnemy, BattleW, 34f, new Color(0.10f, 0.09f, 0.11f));
            _enemyCampFill = UiKit.Panel(_root, "EnemyCampFill", BattleX, CampYEnemy, BattleW * 0.5f, 34f, UiKit.EnemyCamp);
            _enemyCampText = UiKit.Label(_root, "EnemyCampText", BattleX + 12f, CampYEnemy, BattleW - 24f, 34f,
                "", 17, TextAnchor.MiddleLeft, UiKit.TextMain);
            _enemyCampText.raycastTarget = true;
            _enemyCampBtn = _enemyCampText.gameObject.AddComponent<Button>();
            _enemyCampBtn.onClick.AddListener(OnCampClicked);
            RegisterStretch(enemyStrip.rectTransform);
            RegisterStretch(_enemyCampFill.rectTransform);
            RegisterStretch(_enemyCampText.rectTransform);

            // 我的营地条
            Image myStrip = UiKit.Panel(_root, "MyCampStrip", BattleX, CampYMine, BattleW, 34f, new Color(0.09f, 0.11f, 0.10f));
            _myCampFill = UiKit.Panel(_root, "MyCampFill", BattleX, CampYMine, BattleW * 0.5f, 34f, UiKit.MyCamp);
            _myCampText = UiKit.Label(_root, "MyCampText", BattleX + 12f, CampYMine, BattleW - 24f, 34f,
                "", 17, TextAnchor.MiddleLeft, UiKit.TextMain);
            _myCampText.raycastTarget = true;
            _myCampBtn = _myCampText.gameObject.AddComponent<Button>();
            _myCampBtn.onClick.AddListener(OnCampClicked);
            RegisterStretch(myStrip.rectTransform);
            RegisterStretch(_myCampFill.rectTransform);
            RegisterStretch(_myCampText.rectTransform);

            // 棋盘：每方前排 2 格、后排 2 格
            _enemyBoard = new CardView[4];
            _myBoard = new CardView[4];
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? SlotX0 : SlotX1;
                if (i < 2)
                {
                    _enemyBoard[i] = new CardView(_root, "EnemySlot" + i, x, FrontYEnemy, CardW, CardH);
                    _myBoard[i] = new CardView(_root, "MySlot" + i, x, FrontYMine, CardW, CardH);
                }
                else
                {
                    _enemyBoard[i] = new CardView(_root, "EnemySlot" + i, x, BackYEnemy, CardW, CardH);
                    _myBoard[i] = new CardView(_root, "MySlot" + i, x, BackYMine, CardW, CardH);
                }
                RegisterCenter(_enemyBoard[i].Rt);
                RegisterCenter(_myBoard[i].Rt);
            }

            // 中间操作带
            Image midStrip = UiKit.Panel(_root, "MidStrip", BattleX, MidY, BattleW, 94f, new Color(0.10f, 0.11f, 0.14f));
            _hintText = UiKit.Label(_root, "Hint", BattleX + 10f, MidY + 4f, BattleW - 170f, 30f,
                "", 18, TextAnchor.MiddleCenter, UiKit.Selected);
            _winBtn = UiKit.Button(_root, "BtnWindow", BattleX + BattleW - 150f, MidY + 4f, 140f, 28f,
                "窗口/全屏 F11", UiKit.ButtonNormal, ToggleWindowMode);
            RegisterStretch(_winBtn.GetComponent<RectTransform>());
            RegisterStretch(midStrip.rectTransform);
            RegisterStretch(_hintText.rectTransform);

            float by = MidY + 40f;
            RegisterCenter(UiKit.Button(_root, "BtnCancel", 176f, by, 90f, 44f, "取消选择", UiKit.ButtonNormal, () =>
            {
                ClearSelection();
                Refresh();
            }).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnRepos", 274f, by, 100f, 44f, "免费换防", UiKit.ButtonNormal, OnRepositionClicked).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnScout", 382f, by, 100f, 44f, "通用侦察", UiKit.ButtonNormal, OnScoutClicked).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnEnd", 490f, by, 116f, 44f, "结束本轮", new Color(0.42f, 0.20f, 0.22f), OnEndRoundClicked).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnNew", 614f, by, 90f, 44f, "重开一局", UiKit.ButtonNormal,
                () => NewGame(Environment.TickCount & 0xffff)).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnMode", 712f, by, 128f, 44f, "切换：双人同屏", UiKit.ButtonNormal, () =>
            {
                _vsAi = !_vsAi;
                ClearSelection();
                Refresh();
            }).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnZoomOut", 848f, by, 46f, 44f, "－", UiKit.ButtonNormal, () => SetZoom(_zoom - 0.15f)).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnZoomIn", 900f, by, 46f, 44f, "＋", UiKit.ButtonNormal, () => SetZoom(_zoom + 0.15f)).GetComponent<RectTransform>());
            RegisterCenter(UiKit.Button(_root, "BtnZoomReset", 952f, by, 76f, 44f, "适应", UiKit.ButtonNormal, () => SetZoom(1f)).GetComponent<RectTransform>());

            // V0.3 §6.6 强攻开关：本轮额度未用时，点目标前先切到强攻。
            _powerStrikeBtn = UiKit.Button(_root, "BtnPower", 1034f, by, 74f, 44f, "强攻", UiKit.ButtonNormal, () =>
            {
                _powerStrikeMode = !_powerStrikeMode;
                ClearSelection();
                Refresh();
            });
            RegisterCenter(_powerStrikeBtn.GetComponent<RectTransform>());
            // V0.3 §12.1 本轮自动放弃掩护（随时可撤回，每轮自动复位，不自动花补给）。
            _autoCoverBtn = UiKit.Button(_root, "BtnAutoCover", 1104f, by, 96f, 44f, "自动过掩", UiKit.ButtonNormal, () =>
            {
                PlayerState who = _engine.S.Players[Viewer];
                who.AutoDeclineCover = !who.AutoDeclineCover;
                Refresh();
            });
            RegisterCenter(_autoCoverBtn.GetComponent<RectTransform>());

            // 手牌
            _hand = new CardView[8];
            for (int i = 0; i < 8; i++)
            {
                _hand[i] = new CardView(_root, "Hand" + i, HandX0 + i * HandStep, HandY, HandW, HandH);
                RegisterCenter(_hand[i].Rt);
            }

            // 右栏
            Image rightPanel = UiKit.Panel(_root, "RightPanel", RightX, 12f, RightW, 876f, new Color(0.09f, 0.10f, 0.12f));
            RegisterRight(rightPanel.rectTransform);
            _statusText = UiKit.Label(_root, "Status", RightX + 10f, 16f, RightW - 20f, 44f,
                "", 16, TextAnchor.UpperLeft, UiKit.TextMain);
            RegisterRight(_statusText.rectTransform);

            Image logPanel = UiKit.Panel(_root, "LogPanel", RightX + 10f, 66f, RightW - 20f, 330f, new Color(0.06f, 0.07f, 0.09f));
            RegisterRight(logPanel.rectTransform);
            _logText = UiKit.Label(_root, "Log", RightX + 16f, 72f, RightW - 32f, 318f,
                "", 13, TextAnchor.LowerLeft, new Color(0.70f, 0.75f, 0.82f));
            RegisterRight(_logText.rectTransform);

            // 2.2：弃牌区属于公开信息，必须能看见。按牌名聚合，避免 24 张时撑爆。
            Text discLabel = UiKit.Label(_root, "DiscardLabel", RightX + 10f, 400f, RightW - 130f, 16f,
                "弃牌区（公开／按牌名聚合）", 12, TextAnchor.MiddleLeft, UiKit.TextDim);
            RegisterRight(discLabel.rectTransform);
            // 12.1：摘要可以截短，但必须能点开看双方全部公开弃牌
            Button discBtn = UiKit.Button(_root, "BtnDiscardAll", RightX + RightW - 116f, 396f, 106f, 22f,
                "弃牌全览", UiKit.ButtonNormal, () => ShowDiscardOverlay());
            RegisterRight(discBtn.GetComponent<RectTransform>());
            _discardMine = UiKit.Label(_root, "DiscardMine", RightX + 10f, 418f, RightW - 20f, 46f,
                "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            RegisterRight(_discardMine.rectTransform);
            _discardFoe = UiKit.Label(_root, "DiscardFoe", RightX + 10f, 466f, RightW - 20f, 46f,
                "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            RegisterRight(_discardFoe.rectTransform);

            Text erLabel = UiKit.Label(_root, "EnemyReserveLabel", RightX + 10f, 516f, RightW - 20f, 18f,
                "敌方预备区（公开）", 13, TextAnchor.MiddleLeft, UiKit.TextDim);
            RegisterRight(erLabel.rectTransform);
            _enemyReserveText = UiKit.Label(_root, "EnemyReserve", RightX + 10f, 534f, RightW - 20f, 38f,
                "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            RegisterRight(_enemyReserveText.rectTransform);

            Text mrLabel = UiKit.Label(_root, "MyReserveLabel", RightX + 10f, 574f, RightW - 20f, 18f,
                "我的预备区（点击调用）", 13, TextAnchor.MiddleLeft, UiKit.Selected);
            RegisterRight(mrLabel.rectTransform);
            _reserve = new CardView[6];
            for (int i = 0; i < 6; i++)
            {
                float x = RightX + 10f + (i % 3) * 108f;
                float y = 592f + (i / 3) * 122f;
                _reserve[i] = new CardView(_root, "Reserve" + i, x, y, 100f, 114f);
                RegisterRight(_reserve[i].Rt);
            }

            _myPanelText = UiKit.Label(_root, "MyPanel", RightX + 10f, 830f, RightW - 20f, 44f,
                "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            RegisterRight(_myPanelText.rectTransform);

            // 元素刚注册完，之前那次 ApplyLayoutScale 是在空分组上跑的，
            // 这里清掉缓存，保证下一帧按真实宽度重新摆一遍。
            _appliedExtraW = float.NegativeInfinity;
        }

        // ── 开局 ──────────────────────────────────────────────────

        public void NewGame(int seed)
        {
            _engine = new GameEngine();
            _ai = new HeuristicAi(seed * 31 + 7);
            _engine.Start(seed);
            _mulliganPicks.Clear();
            _mulliganViewer = 0;
            ClearSelection();
            _aiRunning = false;
            _aiTimer = 0.5f;
            Refresh();
        }

        private void ClearSelection()
        {
            _selCard = null;
            _selReserve = null;
            _selUnit = null;
            _repositionMode = false;
            _selHint = null;
            _candidates.Clear();
        }

        private bool PendingNeedsHuman()
        {
            if (!_vsAi)
            {
                return true;
            }
            if (_engine.Pending == PendingKind.Cover)
            {
                return 1 - _engine.S.CurrentPlayer == 0;
            }
            if (_engine.Pending == PendingKind.ScoutPick)
            {
                return _engine.S.CurrentPlayer == 0;
            }
            // V0.3 §9.1 / §9.2：调序与检索都是「当前行动方」的私密选择
            if (_engine.Pending == PendingKind.Reorder || _engine.Pending == PendingKind.SearchPick)
            {
                return _engine.S.CurrentPlayer == 0;
            }
            return false;
        }

        private bool HumanCanAct
        {
            get
            {
                if (_engine.S.IsOver)
                {
                    return false;
                }
                if (_engine.S.Phase == GamePhase.Mulligan)
                {
                    return true;
                }
                if (!_vsAi)
                {
                    return true;
                }
                if (_engine.Pending == PendingKind.Cover || _engine.Pending == PendingKind.ScoutPick
                    || _engine.Pending == PendingKind.Reorder || _engine.Pending == PendingKind.SearchPick)
                {
                    return PendingNeedsHuman();
                }
                return _engine.S.CurrentPlayer == 0;
            }
        }

        // ── 刷新 ──────────────────────────────────────────────────

        private void Refresh()
        {
            if (_engine == null)
            {
                return;
            }
            DestroyOverlay();
            _actions = _engine.LegalActions();
            DetectSettlement();
            RefreshCamps();
            RefreshStatus();
            RefreshLog();
            RefreshDiscards();
            RefreshEnemyReserve();
            RefreshMyReserve();
            RefreshBoard();
            RefreshHand();
            RefreshToggles();

            _aiRunning = false;
            if (_engine.S.IsOver)
            {
                _hintText.text = "对局结束：" + WinnerText();
                ShowGameOverOverlay();
                return;
            }
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                _hintText.text = "换牌阶段：点手牌选中要换掉的（至多 2 张，领航不能换）";
                ShowMulliganOverlay();
                return;
            }
            if (_engine.Pending == PendingKind.Cover)
            {
                // 掩护是「防守方」的决定。你攻击对手时防守方是 AI，
                // 不能把 AI 的选择弹给你，否则既误导又会让对局卡住。
                if (PendingNeedsHuman())
                {
                    ShowCoverOverlay();
                }
                else
                {
                    _aiRunning = true;
                    _hintText.text = _engine.S.Players[1 - _engine.S.CurrentPlayer].Name + " 正在决定是否掩护…";
                }
                return;
            }
            if (_engine.Pending == PendingKind.ScoutPick)
            {
                if (PendingNeedsHuman())
                {
                    ShowScoutOverlay();
                }
                else
                {
                    _aiRunning = true;
                    _hintText.text = _engine.S.Players[_engine.S.CurrentPlayer].Name + " 正在侦察…";
                }
                return;
            }
            if (_engine.Pending == PendingKind.Reorder)
            {
                if (PendingNeedsHuman())
                {
                    ShowReorderOverlay();
                }
                else
                {
                    _aiRunning = true;
                    _hintText.text = _engine.S.Players[_engine.S.CurrentPlayer].Name + " 正在调整牌库顶顺序…";
                }
                return;
            }
            if (_engine.Pending == PendingKind.SearchPick)
            {
                if (PendingNeedsHuman())
                {
                    ShowSearchOverlay();
                }
                else
                {
                    _aiRunning = true;
                    _hintText.text = _engine.S.Players[_engine.S.CurrentPlayer].Name + " 正在选择检索目标…";
                }
                return;
            }
            if (_vsAi && _engine.S.CurrentPlayer == 1)
            {
                _aiRunning = true;
                _hintText.text = "乙（AI）正在行动…";
                return;
            }
            _hintText.text = DefaultHint();
            if (_settleTimer > 0f && !string.IsNullOrEmpty(_settleText))
            {
                _hintText.text = "【结算】" + _settleText + "\n" + _hintText.text;
            }
        }

        /// <summary>
        /// 每次刷新时比较营地与新增日志，拼出一句"上一步发生了什么"。
        /// 起因：玩家反馈"放出去的突击队员直接消失、AI 行动后我莫名掉血"——
        /// 实际是 V0.3 §6.5-5 的战损（阵亡单位的**所属方**扣营地），
        /// 但界面上没有说明，必须补上可读性。
        /// </summary>
        private void DetectSettlement()
        {
            GameState s = _engine.S;
            int c0 = s.Players[0].Camp;
            int c1 = s.Players[1].Camp;
            if (_lastCamp0 < 0)
            {
                _lastCamp0 = c0;
                _lastCamp1 = c1;
                _lastLogSeen = _engine.Log.Count;
                return;
            }
            var parts = new List<string>();
            for (int i = _lastLogSeen; i < _engine.Log.Count; i++)
            {
                string t = _engine.Log[i].Text;
                // 逐条解析出**真实原因**：不能再写"战损/直击"这种猜测标签
                if (t.Contains("界潮：双方各受 "))
                {
                    parts.Add("界潮 -" + ParseNumAfter(t, "界潮：双方各受 ") + "（双方各受）");
                }
                else if (t.Contains("攻击 敌方营地：主动 "))
                {
                    parts.Add("被直击营地 -" + ParseNumAfter(t, "攻击 敌方营地：主动 "));
                }
                else if (t.Contains("阵亡："))
                {
                    parts.Add("阵亡 " + DeathShort(t) + "，战损 " + ParseNumAfter(t, "承担战损 "));
                }
                else if (t.Contains("首次清空敌方前排"))
                {
                    parts.Add("清线奖励 -1");
                }
                else if (t.Contains("宣布强攻"))
                {
                    parts.Add("强攻 -2补给");
                }
                else if (t.Contains("造成 ") && t.Contains(" 点伤害"))
                {
                    parts.Add("战术命中 -" + ParseNumAfter(t, "造成 "));
                }
            }
            int d0 = c0 - _lastCamp0;
            int d1 = c1 - _lastCamp1;
            if (d0 != 0 || d1 != 0 || parts.Count > 0)
            {
                var seg = new List<string>();
                _delta0 = d0 < 0 ? d0 : 0;
                _delta1 = d1 < 0 ? d1 : 0;
                if (d0 < 0) { seg.Add("甲营地 " + _lastCamp0 + "→" + c0); _flash0 = 0.9f; }
                if (d1 < 0) { seg.Add("乙营地 " + _lastCamp1 + "→" + c1); _flash1 = 0.9f; }
                for (int i = 0; i < parts.Count; i++) { seg.Add(parts[i]); }
                _settleText = string.Join("　", seg.ToArray());
                _settleTimer = 3.5f;
                Debug.Log("[结算] " + _settleText);
            }
            _lastCamp0 = c0;
            _lastCamp1 = c1;
            _lastLogSeen = _engine.Log.Count;
        }

        /// <summary>从「 突击队员#9001 阵亡：甲 → 弃牌区；基础单位 承担战损 1。」里取出关键片段。</summary>
        private static string DeathShort(string logLine)
        {
            int a = logLine.IndexOf("阵亡：");
            if (a < 0)
            {
                return logLine.Trim();
            }
            string head = logLine.Substring(0, a).Trim();
            string rest = logLine.Substring(a + 3).Trim();
            int bar = rest.IndexOf('；');
            string owner = bar > 0 ? rest.Substring(0, bar).Trim() : rest.Trim();
            return head + "（" + owner + "）";
        }

        /// <summary>取标记之后紧跟的整数（例如「承担战损 1」→ 1）。取不到返回 0。</summary>
        private static int ParseNumAfter(string text, string marker)
        {
            int i = text.IndexOf(marker);
            if (i < 0)
            {
                return 0;
            }
            i += marker.Length;
            int start = i;
            while (i < text.Length && char.IsDigit(text[i])) { i++; }
            if (i <= start)
            {
                return 0;
            }
            int v;
            return int.TryParse(text.Substring(start, i - start), out v) ? v : 0;
        }

        /// <summary>营地条掉血后短促闪红，让"哪里变了"一眼可见。</summary>
        private void ApplyCampFlash()
        {
            if (_myCampFill != null)
            {
                Color baseCol = UiKit.MyCamp;
                Color hurt = new Color(0.95f, 0.20f, 0.24f, 1f);
                if (_flash0 > 0f) { _flash0 -= Time.deltaTime; _myCampFill.color = Color.Lerp(baseCol, hurt, Mathf.Clamp01(_flash0 / 0.9f)); }
                else { _myCampFill.color = baseCol; }
            }
            if (_enemyCampFill != null)
            {
                Color baseCol = UiKit.EnemyCamp;
                Color hurt = new Color(0.95f, 0.20f, 0.24f, 1f);
                if (_flash1 > 0f) { _flash1 -= Time.deltaTime; _enemyCampFill.color = Color.Lerp(baseCol, hurt, Mathf.Clamp01(_flash1 / 0.9f)); }
                else { _enemyCampFill.color = baseCol; }
            }
        }

        /// <summary>刷新「强攻」与「自动过掩」两个开关的可用状态与文字。</summary>
        private void RefreshToggles()
        {
            if (_powerStrikeBtn == null || _autoCoverBtn == null || _engine == null)
            {
                return;
            }
            PlayerState viewer = _engine.S.Players[Viewer];
            bool playing = _engine.S.Phase == GamePhase.Playing && !_engine.S.IsOver;
            bool open = _engine.S.Round >= GameEngine.PowerStrikeStartsAtRound;
            bool available = playing && open && !viewer.PowerStrikeUsed && viewer.Supply >= GameEngine.PowerStrikeCost;
            _powerStrikeBtn.interactable = available;
            Text ptext = _powerStrikeBtn.GetComponentInChildren<Text>();
            if (ptext != null)
            {
                ptext.text = !open ? "强攻(未开放)" : (_powerStrikeMode ? "强攻✓" : "强攻");
            }
            Text atext = _autoCoverBtn.GetComponentInChildren<Text>();
            if (atext != null)
            {
                atext.text = viewer.AutoDeclineCover ? "自动过掩✓" : "自动过掩";
            }
            _autoCoverBtn.interactable = true;
        }

        /// <summary>12.1「显示使用后补给与主行动的剩余量」：把出手后的余量直接算给玩家。</summary>
        private string AfterSpend(int supplyCost)
        {
            PlayerState me = _engine.S.Players[_engine.S.CurrentPlayer];
            int supplyAfter = me.Supply - supplyCost;
            int actionsAfter = me.ActionsLeft - 1;
            return string.Format("　→ 出手后 补给 {0}，行动 {1}", supplyAfter < 0 ? 0 : supplyAfter,
                actionsAfter < 0 ? 0 : actionsAfter);
        }

        /// <summary>预备区牌的费用（找不到就按 0 处理）。</summary>
        private int ReserveCost(string reserveId, PlayerState me)
        {
            for (int i = 0; i < me.Reserve.Count; i++)
            {
                if (me.Reserve[i].Def.Id == reserveId)
                {
                    return me.Reserve[i].Def.Cost;
                }
            }
            return 0;
        }

        private string WinnerText()
        {
            GameState s = _engine.S;
            if (s.Winner == 2)
            {
                return "平局　" + (s.FinishReason ?? string.Empty);
            }
            return s.Players[s.Winner].Name + " 获胜　" + (s.FinishReason ?? string.Empty);
        }

        private string DefaultHint()
        {
            if (!string.IsNullOrEmpty(_selHint))
            {
                return _selHint;
            }
            // 换防模式要先于"已选单位=攻击"的分支判断，否则会把换防说成攻击
            if (_repositionMode)
            {
                string repPreview = RepositionPreviewText();
                if (!string.IsNullOrEmpty(repPreview))
                {
                    return repPreview;
                }
                return "换防模式：点自己的单位，再点目标空位或要互换的单位";
            }
            if (_candidates.Count > 0)
            {
                if (_selCard != null)
                {
                    return "已选 " + _selCard.Def.Name + "：点高亮目标执行（青框＝可选目标）"
                           + AfterSpend(_selCard.Def.Cost);
                }
                if (_selReserve != null)
                {
                    PlayerState me0 = _engine.S.Players[_engine.S.CurrentPlayer];
                    int cost = ReserveCost(_selReserve, me0);
                    // 12.1 晋阶预览（完整版）：把晋阶前后的数值一次算清
                    string upgradePreview = UpgradePreviewText(_candidates[0]);
                    if (!string.IsNullOrEmpty(upgradePreview))
                    {
                        return upgradePreview + AfterSpend(cost);
                    }
                    return "已选预备牌 " + _selReserve + "：点高亮目标执行" + AfterSpend(cost);
                }
                if (_selUnit != null)
                {
                    return "已选 " + _selUnit.Card.Short + "：点高亮目标攻击（青框／红旗营地）"
                           + AfterSpend(0);
                }
                return "点高亮目标执行";
            }
            if (_selCard != null)
            {
                return "已选 " + _selCard.Def.Name + "，但现在没有合法目标"
                       + (_selCard.Def.Type == CardType.Equipment ? "（装备需要场上先有己方单位）" : "（检查补给或目标条件）");
            }
            if (_selReserve != null)
            {
                return "已选预备牌，但现在没有合法目标";
            }
            PlayerState me = _engine.S.Players[_engine.S.CurrentPlayer];
            return string.Format("{0} 行动：剩 {1} 次主行动，补给 {2}　点手牌出牌／点自己单位攻击",
                me.Name, me.ActionsLeft, me.Supply);
        }

        /// <summary>
        /// 2.2 规定「本轮掩护、换防、侦察、预备牌调用和清空前排奖励是否已经使用」属于公开信息，
        /// 所以要同时给出双方的标记，不能只显示当前玩家自己的。
        /// </summary>
        private string PublicFlags(PlayerState p)
        {
            var parts = new List<string>();
            if (p.CoverUsed) { parts.Add("掩护✔"); }
            if (p.RepositionUsed) { parts.Add("换防✔"); }
            if (p.ScoutUsed) { parts.Add("侦察✔"); }
            if (p.ReserveUsedThisRound) { parts.Add("预备✔"); }
            if (p.ClearFrontBonusUsed) { parts.Add("清线✔"); }
            // V0.3 §2.2：强攻使用状态也要公开，第 1~3 轮显示「尚未开放」
            if (_engine.S.Round >= GameEngine.PowerStrikeStartsAtRound)
            {
                parts.Add(p.PowerStrikeUsed ? "强攻✔" : "强攻待用");
            }
            else
            {
                parts.Add("强攻未开放");
            }
            return parts.Count == 0 ? "—" : string.Join(" ", parts.ToArray());
        }

        private void RefreshCamps()
        {
            GameState s = _engine.S;
            PlayerState p0 = s.Players[0];
            PlayerState p1 = s.Players[1];
            int viewer = Viewer;
            bool playing = s.Phase == GamePhase.Playing && !s.IsOver;
            string turn0 = playing && s.CurrentPlayer == 0 ? "　◀ 当前" : string.Empty;
            string turn1 = playing && s.CurrentPlayer == 1 ? "　◀ 当前" : string.Empty;
            // 标签：单人模式标出我方/敌方；双人同屏只标阵营名 + 当前行动者
            string tag0 = _vsAi ? "（我方）" : string.Empty;
            string tag1 = _vsAi ? "（敌方）" : string.Empty;
            _myCampText.text = string.Format("甲{0} 营地 {1}/20　补给 {2}　行动 {3}/3　手牌 {4}　牌库 {5}　已用 {6}{7}",
                tag0, p0.Camp, p0.Supply, p0.ActionsLeft, p0.Hand.Count, p0.Deck.Count, PublicFlags(p0), turn0);
            _enemyCampText.text = string.Format("乙{0} 营地 {1}/20　补给 {2}　行动 {3}/3　手牌 {4}　牌库 {5}　已用 {6}{7}",
                tag1, p1.Camp, p1.Supply, p1.ActionsLeft, p1.Hand.Count, p1.Deck.Count, PublicFlags(p1), turn1);
            if (_flash0 > 0f && _delta0 < 0)
            {
                _myCampText.text += "　▼" + (-_delta0);
            }
            if (_flash1 > 0f && _delta1 < 0)
            {
                _enemyCampText.text += "　▼" + (-_delta1);
            }
            _myCampFill.rectTransform.sizeDelta = new Vector2(BattleW * Mathf.Clamp01(p0.Camp / 20f), 34f);
            _enemyCampFill.rectTransform.sizeDelta = new Vector2(BattleW * Mathf.Clamp01(p1.Camp / 20f), 34f);

            // 只有「对方营地」可以点来直击；双人同屏时随行动方切换是哪一条。
            bool canCamp = playing && FindCampAttack() != null;
            if (_myCampBtn != null)
            {
                _myCampBtn.interactable = canCamp && viewer == 1;
            }
            if (_enemyCampBtn != null)
            {
                _enemyCampBtn.interactable = canCamp && viewer == 0;
            }
        }

        private void RefreshStatus()
        {
            GameState s = _engine.S;
            PlayerState cur = s.Players[s.CurrentPlayer];
            string flags = string.Empty;
            if (cur.CoverUsed) { flags += "掩护✔ "; }
            if (cur.RepositionUsed) { flags += "换防✔ "; }
            if (cur.ScoutUsed) { flags += "侦察✔ "; }
            if (cur.ReserveUsedThisRound) { flags += "预备✔ "; }
            if (cur.ClearFrontBonusUsed) { flags += "清线✔ "; }
            _statusText.text = string.Format(
                "{0}　第 {1} 轮\n当前：{2}\n本轮已用：{3}",
                _vsAi ? "单人对 AI" : "双人同屏",
                Math.Max(1, s.Round),
                s.Phase == GamePhase.Mulligan ? "换牌" : cur.Name + "（" + cur.ActionsLeft + " 行动）",
                flags.Length == 0 ? "—" : flags.Trim());

            _myPanelText.text = TideForecast();
        }

        /// <summary>
        /// 12.2 界潮预告：每次主行动后更新本次界潮数值，并按「当前营地不再改变」的假设
        /// 给出轮末结果。措辞必须写明这是假设，不能把尚未发生的操作当成确定结果。
        /// </summary>
        private string TideForecast()
        {
            GameState s = _engine.S;
            if (s.Phase != GamePhase.Playing)
            {
                return string.Empty;
            }
            int tide = s.Round >= GameEngine.TideStartsAtRound
                ? s.Round - (GameEngine.TideStartsAtRound - 1)
                : 0;
            if (tide <= 0)
            {
                return "界潮尚未开始（第 " + GameEngine.TideStartsAtRound + " 轮起）";
            }
            int a = s.Players[0].Camp - tide;
            int b = s.Players[1].Camp - tide;
            string result;
            if (a > 0 && b > 0)
            {
                result = "双方存活，进入第 " + (s.Round + 1) + " 轮";
            }
            else if (a <= 0 && b <= 0)
            {
                int pre0 = s.Players[0].Camp;
                int pre1 = s.Players[1].Camp;
                result = pre0 > pre1 ? "甲胜（同时归零，比较界潮前营地）"
                    : (pre1 > pre0 ? "乙胜（同时归零，比较界潮前营地）" : "平局（同时归零且界潮前相等）");
            }
            else
            {
                result = a <= 0 ? "甲营地归零，乙胜" : "乙营地归零，甲胜";
            }
            return string.Format("本轮界潮双方各受 {0}：若营地数值不再改变，甲 {1} / 乙 {2} → {3}",
                tide, a > 0 ? a : 0, b > 0 ? b : 0, result);
        }

        /// <summary>
        /// 2.2 弃牌区公开。24 张全列会撑爆右栏，所以按牌名聚合并标注张数；
        /// 装备被替换、单位阵亡都会落到这里，玩家据此可以预判洗回牌库后可能抽到什么。
        /// </summary>
        private void RefreshDiscards()
        {
            GameState s = _engine.S;
            _discardMine.text = "甲：" + DiscardSummary(s.Players[0]);
            _discardFoe.text = "乙：" + DiscardSummary(s.Players[1]);
        }

        /// <summary>
        /// 弃牌区摘要。24 张时如果逐张/逐名全列，320px 宽要 4 行、会压到下一行文字，
        /// 所以按「张数多的先显示」排序并最多列 6 个牌名，其余用「…」收尾。
        /// </summary>
        private static string DiscardSummary(PlayerState p)
        {
            if (p.Discard.Count == 0)
            {
                return "（空）";
            }
            var order = new List<string>();
            var counts = new List<int>();
            for (int i = 0; i < p.Discard.Count; i++)
            {
                string name = p.Discard[i].Def.Name;
                int at = order.IndexOf(name);
                if (at < 0)
                {
                    order.Add(name);
                    counts.Add(1);
                }
                else
                {
                    counts[at] = counts[at] + 1;
                }
            }
            // 张数多的排前面：先出现的同名多、信息密度高
            var idx = new List<int>();
            for (int i = 0; i < order.Count; i++)
            {
                idx.Add(i);
            }
            idx.Sort(delegate (int a, int b) { return counts[b].CompareTo(counts[a]); });

            const int MaxNames = 6;
            var sb = new System.Text.StringBuilder();
            sb.Append(p.Discard.Count).Append(" 张 · ");
            int shown = 0;
            for (int i = 0; i < idx.Count && shown < MaxNames; i++, shown++)
            {
                if (shown > 0)
                {
                    sb.Append("、");
                }
                int at = idx[i];
                sb.Append(order[at]);
                if (counts[at] > 1)
                {
                    sb.Append("×").Append(counts[at]);
                }
            }
            if (idx.Count > shown)
            {
                sb.Append("…共 ").Append(idx.Count).Append(" 种");
            }
            return sb.ToString();
        }

        private void RefreshLog()
        {
            List<LogEntry> log = _engine.Log;
            int take = Math.Min(24, log.Count);
            var sb = new System.Text.StringBuilder();
            for (int i = log.Count - take; i < log.Count; i++)
            {
                string line = log[i].Text;
                if (line.Length > 40)
                {
                    line = line.Substring(0, 40) + "…";
                }
                sb.Append(line).Append('\n');
            }
            _logText.text = sb.ToString();
        }

        private void RefreshEnemyReserve()
        {
            PlayerState enemy = _engine.S.Players[1 - Viewer];
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < enemy.Reserve.Count; i++)
            {
                ReservedCard rc = enemy.Reserve[i];
                sb.Append(rc.Def.Name).Append(rc.Used ? "(已用)" : "(" + rc.Def.Cost + ")").Append("  ");
                if (i == 2)
                {
                    sb.Append('\n');
                }
            }
            _enemyReserveText.text = sb.ToString();
        }

        private void RefreshMyReserve()
        {
            PlayerState me = _engine.S.Players[Viewer];
            bool myTurn = _engine.S.CurrentPlayer == Viewer && !_engine.S.IsOver;
            for (int i = 0; i < _reserve.Length; i++)
            {
                if (i >= me.Reserve.Count)
                {
                    _reserve[i].Hide();
                    continue;
                }
                ReservedCard rc = me.Reserve[i];
                string id = rc.Def.Id;
                bool usable = !rc.Used && !me.ReserveUsedThisRound && rc.Def.Cost <= me.Supply
                              && myTurn;
                Color frame = _selReserve == id ? UiKit.Selected : (usable ? new Color(0.35f, 0.42f, 0.30f) : UiKit.Border);
                _reserve[i].SetCardFace(rc.Def.Cost.ToString(),
                    rc.Def.Name,
                    rc.Def.Type == CardType.Upgrade ? "晋阶" : "支援",
                    ShortText(rc.Def.Text) + (rc.Used ? "\n【本局已用】" : string.Empty),
                    usable, frame,
                    usable ? (UnityEngine.Events.UnityAction)(() => OnReserveClicked(id)) : null);
            }
        }

        private static string ShortText(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return s.Length > 26 ? s.Substring(0, 26) + "…" : s;
        }

        /// <summary>
        /// 当前"坐在操作位上"的玩家。单人对 AI 时永远是 0（甲）；
        /// 双人同屏时跟着行动权走。界面所有"我方/可点"的判定都必须用它，
        /// 不能写死 Players[0]——否则乙的回合里乙连自己的空格都点不了。
        /// </summary>
        private int Viewer
        {
            get
            {
                if (_engine == null)
                {
                    return 0;
                }
                return _vsAi ? 0 : _engine.S.CurrentPlayer;
            }
        }

        private void RefreshBoard()
        {
            PlayerState p0 = _engine.S.Players[0];
            PlayerState p1 = _engine.S.Players[1];
            int viewer = Viewer;
            for (int i = 0; i < 4; i++)
            {
                var slot = new Slot(i < 2, i % 2);
                RenderSlot(_myBoard[i], slot, p0.At(slot), true, viewer == 0);
                RenderSlot(_enemyBoard[i], slot, p1.At(slot), false, viewer == 1);
            }
        }

        private void RenderSlot(CardView view, Slot slot, UnitState unit, bool mineVisual, bool interactive)
        {
            if (unit == null)
            {
                // 空槽既可能是部署目标，也可能是「换防到另一排空位」的目标。
                // 之前只认部署动作，导致点了免费换防再点空位毫无反应。
                GameAction deploy = interactive ? FindDeploy(slot) : null;
                GameAction move = interactive ? FindRepositionTo(slot) : null;
                GameAction action = deploy != null ? deploy : move;
                string emptyBody = move != null && deploy == null ? "换防目标" : "空位";
                view.SetEmptySlot(slot.Label, action != null,
                    action != null ? (UnityEngine.Events.UnityAction)(() => Apply(action)) : null);
                if (action != null)
                {
                    view.Body.text = emptyBody;
                }
                return;
            }

            GameAction unitAction = FindTargetOn(unit);
            bool isTarget = unitAction != null;
            bool selected = _selUnit == unit;
            Color frame = selected ? UiKit.Selected : (isTarget ? UiKit.Target : UiKit.Border);

            string body = Keywords(unit);
            if (unitAction != null && unitAction.Kind == ActionKind.Attack)
            {
                string preview = PreviewAttack(unitAction);
                if (!string.IsNullOrEmpty(preview))
                {
                    body = body.Length == 0 ? preview : body + "\n" + preview;
                }
            }
            else if (unitAction != null && unitAction.Kind == ActionKind.Reserve)
            {
                // 12.1 晋阶预览：卡面只放最短的那个数，完整说明在提示栏
                string short0 = PreviewUpgradeShort(unitAction);
                if (!string.IsNullOrEmpty(short0))
                {
                    body = body.Length == 0 ? short0 : body + "\n" + short0;
                }
            }
            else if (unitAction != null && unitAction.Kind == ActionKind.Reposition && unitAction.TargetUid >= 0)
            {
                body = body.Length == 0 ? "换防到此" : body + "\n换防到此";
            }
            else if (unitAction != null && unitAction.Kind == ActionKind.Equip && !string.IsNullOrEmpty(unitAction.SwapNote))
            {
                // V0.3 §8.2 装备是替换不是叠加：直接写在卡面上，避免同名再装一次白亏
                body = body.Length == 0 ? unitAction.SwapNote : body + "\n" + unitAction.SwapNote;
            }
            string stats = string.Format("攻{0}　血{1}/{2}", unit.Atk, unit.CurrentHp, unit.MaxHp);
            int uid = unit.Card.Uid;
            // 自己这一侧的格子：可点（选中/换防/指向己方效果）；
            // 对方那一侧的格子：只有在它是合法目标时可点。
            UnityEngine.Events.UnityAction onClick;
            if (interactive)
            {
                onClick = (UnityEngine.Events.UnityAction)(() => OnMyUnitClicked(slot));
            }
            else if (isTarget)
            {
                onClick = (UnityEngine.Events.UnityAction)(() => Apply(unitAction));
            }
            else
            {
                // 敌方单位不能打时，点它必须给出理由（原来点了毫无反应，玩家只能猜规则）
                onClick = (UnityEngine.Events.UnityAction)(() => ExplainBlockedTarget(unit));
            }
            view.SetUnit(unit.Card.Def.Name + "#" + uid, stats, body, mineVisual, frame, onClick);
        }

        /// <summary>
        /// V0.3 §6.1 / §6.3：解释"为什么这个敌方单位打不了"。
        /// 远射只决定能不能从后排出手、吃不吃反击，**不改变目标顺序**。
        /// </summary>
        private void ExplainBlockedTarget(UnitState target)
        {
            PlayerState foe = _engine.S.Players[1 - _engine.S.CurrentPlayer];
            string msg;
            var front = foe.FrontUnits;
            if (front.Count > 0)
            {
                UnitState guard = front.Find(x => x.HasGuard);
                if (!target.Slot.Front)
                {
                    msg = "敌方前排还没清空，不能打后排或营地（§6.1-3）。瞄准镜只让后排单位能出手、且不吃反击（§6.3），不会让你越过前排。";
                }
                else if (guard != null && !target.HasGuard)
                {
                    msg = "敌方前排有护卫（" + guard.Card.Short + "），必须先打护卫（§6.1-1）。想越过前排只能用战术「精准射击」。";
                }
                else
                {
                    msg = "敌方前排有护卫，只能打护卫（§6.1-1）。";
                }
            }
            else
            {
                msg = "这个单位现在打不了：检查我方单位是否已攻击过／刚部署／站后排又没有远射。";
            }
            if (_hintText != null)
            {
                _hintText.text = "不能打 " + target.Card.Short + "　——　" + msg;
            }
        }

        /// <summary>
        /// 12.1 攻击预览：在引擎副本上真的走一遍 6.5 的结算，再把结果摘要出来。
        /// 关键在于预览与真实结算共用同一份规则（都走 GameEngine.Apply），
        /// 不会出现「预览说会死、实际没死」这种分叉。预告按防守方不使用掩护计算，
        /// 因为掩护是随后才做的决定。
        /// </summary>
        private string PreviewAttack(GameAction attack)
        {
            if (attack == null || attack.Kind != ActionKind.Attack || _engine == null)
            {
                return null;
            }
            try
            {
                GameEngine fork = _engine.Fork();
                int cur = fork.S.CurrentPlayer;
                PlayerState atkSide = fork.S.Players[cur];
                PlayerState defSide = fork.S.Players[1 - cur];
                UnitState atk = fork.FindUnit(atkSide, attack.ActorUid);
                if (atk == null)
                {
                    return null;
                }
                int atkHp0 = atk.CurrentHp;
                bool atkUpgraded = atk.Upgrade != null;
                UnitState tgt = attack.TargetCamp ? null : fork.FindUnit(defSide, attack.TargetUid);
                int tgtHp0 = tgt != null ? tgt.CurrentHp : 0;
                bool tgtUpgraded = tgt != null && tgt.Upgrade != null;
                int defCamp0 = defSide.Camp;
                int atkCamp0 = atkSide.Camp;

                defSide.Supply = 0;
                fork.Apply(attack);

                int active = attack.TargetCamp ? defCamp0 - defSide.Camp : tgtHp0 - tgt.CurrentHp;
                int counter = atkHp0 - atk.CurrentHp;
                bool tgtDead = tgt != null && fork.FindUnit(defSide, attack.TargetUid) == null;
                bool atkDead = fork.FindUnit(atkSide, attack.ActorUid) == null;
                int defCampLoss = defCamp0 - defSide.Camp;
                int atkCampLoss = atkCamp0 - atkSide.Camp;
                int tgtToll = tgtDead ? (tgtUpgraded ? 2 : 1) : 0;
                int atkToll = atkDead ? (atkUpgraded ? 2 : 1) : 0;
                int bonus = defCampLoss - tgtToll;

                var sb = new System.Text.StringBuilder();
                sb.Append("预 主").Append(active > 0 ? active : 0);
                if (!attack.TargetCamp)
                {
                    sb.Append(" 反").Append(counter > 0 ? counter : 0);
                }
                if (tgtDead) { sb.Append(" 敌亡"); }
                if (atkDead) { sb.Append(" 我亡"); }
                if (tgtToll > 0) { sb.Append(" 敌损").Append(tgtToll); }
                if (atkToll > 0) { sb.Append(" 我损").Append(atkToll); }
                if (bonus > 0) { sb.Append(" 清+1"); }
                return sb.ToString();
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 12.1 晋阶预览（短版，放卡面）。完整说明见 UpgradePreviewText()。
        /// </summary>
        private string PreviewUpgradeShort(GameAction a)
        {
            UpgradePreview p = BuildUpgradePreview(a);
            if (p == null)
            {
                return null;
            }
            return "晋→" + p.AtkAfter + "攻" + p.MaxHpAfter + "血";
        }

        /// <summary>
        /// 12.1 晋阶预览（完整版，放提示栏）：攻/最大生命/当前生命/保留伤害/关键词变化/已攻击状态。
        /// </summary>
        private string UpgradePreviewText(GameAction a)
        {
            UpgradePreview p = BuildUpgradePreview(a);
            if (p == null)
            {
                return null;
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("晋阶 ").Append(p.Name).Append("：攻击 ").Append(p.AtkBefore).Append("→").Append(p.AtkAfter)
              .Append("，生命 ").Append(p.HpBefore).Append("/").Append(p.MaxHpBefore)
              .Append("→").Append(p.HpAfter).Append("/").Append(p.MaxHpAfter)
              .Append("（保留 ").Append(p.Damage).Append(" 点伤害）")
              .Append("，关键词 ").Append(p.KeywordBefore).Append("→").Append(p.KeywordAfter);
            if (p.AttackedThisRound)
            {
                sb.Append("；本轮已攻击，晋阶后仍不能再次攻击");
            }
            return sb.ToString();
        }

        private sealed class UpgradePreview
        {
            public string Name;
            public int AtkBefore;
            public int AtkAfter;
            public int HpBefore;
            public int MaxHpBefore;
            public int HpAfter;
            public int MaxHpAfter;
            public int Damage;
            public string KeywordBefore;
            public string KeywordAfter;
            public bool AttackedThisRound;
        }

        /// <summary>
        /// 用晋阶牌的数值重新算一遍走位，不修改任何真实状态。
        /// 攻击力要算上装备与临时修正（晋阶只覆盖基础值，§8.1/§8.2）。
        /// </summary>
        private UpgradePreview BuildUpgradePreview(GameAction a)
        {
            if (a == null || a.Kind != ActionKind.Reserve || _engine == null)
            {
                return null;
            }
            PlayerState me = _engine.S.Players[Viewer];
            UnitState u = _engine.FindUnit(me, a.TargetUid);
            if (u == null)
            {
                return null;
            }
            ReservedCard rc = me.FindReserve(a.ReserveId);
            if (rc == null || rc.Def.Type != CardType.Upgrade)
            {
                return null;
            }
            CardDef up = rc.Def;
            int equipBonus = u.Equipment != null && u.Equipment.Effect == Effect.EquipPlus1Attack ? 1 : 0;
            int atkAfter = up.Atk + equipBonus + u.TempAtkMod;
            if (atkAfter < 0)
            {
                atkAfter = 0;
            }
            return new UpgradePreview
            {
                Name = up.Name,
                AtkBefore = u.Atk,
                AtkAfter = atkAfter,
                HpBefore = u.CurrentHp,
                MaxHpBefore = u.MaxHp,
                HpAfter = up.Hp - u.Damage,
                MaxHpAfter = up.Hp,
                Damage = u.Damage,
                KeywordBefore = u.HasGuard ? "护卫" : (u.HasRanged ? "远射" : "无"),
                KeywordAfter = up.Guard ? "护卫" : (up.Ranged ? "远射" : "无"),
                AttackedThisRound = u.AttackedThisRound,
            };
        }

        /// <summary>12.1 换防预览：说明这次换防会把谁放到哪、以及攻击资格会不会变。</summary>
        private string RepositionPreviewText()
        {
            if (!_repositionMode || _selUnit == null || _candidates.Count == 0)
            {
                return null;
            }
            var dest = new List<string>();
            int swaps = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                GameAction a = _candidates[i];
                if (a.Kind != ActionKind.Reposition)
                {
                    continue;
                }
                if (a.TargetUid >= 0)
                {
                    swaps++;
                }
                else
                {
                    dest.Add(a.ToSlot.Label);
                }
            }
            var sb = new System.Text.StringBuilder();
            sb.Append("换防 ").Append(_selUnit.Card.Short).Append("（").Append(_selUnit.Slot.Label).Append("）：");
            if (dest.Count > 0)
            {
                sb.Append("可移到 ").Append(string.Join("/", dest.ToArray()));
            }
            if (swaps > 0)
            {
                sb.Append(dest.Count > 0 ? "；" : string.Empty).Append("可与 ").Append(swaps).Append(" 个对排单位互换");
            }
            sb.Append("。换防不消耗行动与补给");
            // 12.1 要求预览说明"攻击资格变化"。四种情况必须分开写清楚：
            // 早期版本用字符串裁剪拼这句话，前排远射单位会显示"将不能攻击（但可攻击）"这种自相矛盾的文案。
            if (_selUnit.AttackedThisRound)
            {
                sb.Append("；该单位本轮已攻击，换位后仍不能攻击");
            }
            else if (_selUnit.Slot.Front)
            {
                sb.Append(_selUnit.HasRanged
                    ? "；有远射，换到后排仍可攻击"
                    : "；无远射，换到后排后本轮将不能主动攻击");
            }
            else
            {
                sb.Append(_selUnit.HasRanged
                    ? "；本来就能从后排攻击（远射），换到前排后同样可攻击"
                    : "；换到前排后本轮即可主动攻击");
            }
            return sb.ToString();
        }

        private string Keywords(UnitState u)
        {
            var parts = new List<string>();
            if (u.HasGuard) { parts.Add("护卫"); }
            if (u.HasRanged) { parts.Add("远射"); }
            if (u.Upgrade != null) { parts.Add("已晋阶"); }
            if (u.AttackedThisRound) { parts.Add("已攻击"); }
            string keywords = string.Join(" ", parts.ToArray());

            // 攻击力的增减单独一行并写明来源，避免把「牵制-2」误读成装备导致的。
            var mods = new List<string>();
            if (u.Equipment != null)
            {
                mods.Add(u.Equipment.Effect == Effect.EquipPlus1Attack
                    ? u.Equipment.Name + "+1攻"
                    : u.Equipment.Name + "(赋远射)");
            }
            if (u.TempAtkMod != 0)
            {
                mods.Add("牵制" + (u.TempAtkMod > 0 ? "+" : string.Empty) + u.TempAtkMod + "攻");
            }
            string modline = string.Join(" ", mods.ToArray());

            if (keywords.Length == 0)
            {
                return modline;
            }
            if (modline.Length == 0)
            {
                return keywords;
            }
            return keywords + "\n" + modline;
        }

        private void RefreshHand()
        {
            int viewer = _vsAi ? 0 : _engine.S.CurrentPlayer;
            PlayerState p = _engine.S.Players[viewer];

            for (int i = 0; i < _hand.Length; i++)
            {
                if (i >= p.Hand.Count)
                {
                    _hand[i].Hide();
                    continue;
                }
                CardInstance c = p.Hand[i];
                int uid = c.Uid;
                bool playable = CanPlayNow(c, viewer);
                bool selected = _selCard == c;
                bool detail = _detailCard == c;
                Color frame = selected ? UiKit.Selected : (detail ? UiKit.Target : (playable ? new Color(0.35f, 0.44f, 0.58f) : UiKit.Border));
                string stats = c.Def.Type == CardType.Unit
                    ? c.Def.Atk + "攻 " + c.Def.Hp + "血"
                    : (c.Def.Type == CardType.Tactic ? "战术" : "装备");
                string body = detail ? ("【已查看】再点这张＝使用\n" + ShortText(c.Def.Text)) : ShortText(c.Def.Text);
                if (_mulliganPicks.Contains(uid))
                {
                    body = "【选中换掉】\n" + body;
                }
                bool clickable = _engine.S.Phase == GamePhase.Mulligan
                    ? uid != p.VanguardUid
                    : (HumanCanAct && _engine.S.CurrentPlayer == viewer);
                _hand[i].SetCardFace(c.Def.Cost.ToString(), c.Def.Name, stats, body, playable, frame,
                    clickable ? (UnityEngine.Events.UnityAction)(() => OnHandClicked(c)) : null);
            }
        }

        private bool CanPlayNow(CardInstance c, int player)
        {
            if (_engine.S.Phase != GamePhase.Playing || _engine.S.IsOver || _engine.S.CurrentPlayer != player)
            {
                return false;
            }
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].CardUid == c.Uid)
                {
                    return true;
                }
            }
            return false;
        }

        // ── 点击处理 ──────────────────────────────────────────────

        private void OnHandClicked(CardInstance c)
        {
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                if (c.Uid == _engine.S.Players[_mulliganViewer].VanguardUid)
                {
                    return;
                }
                if (_mulliganPicks.Contains(c.Uid))
                {
                    _mulliganPicks.Remove(c.Uid);
                }
                else if (_mulliganPicks.Count < 2)
                {
                    _mulliganPicks.Add(c.Uid);
                }
                Refresh();
                return;
            }

            if (_selCard == c)
            {
                ClearSelection();
                Refresh();
                return;
            }
            // 第一次点：只看信息（不弹出模态窗，避免遮住手牌与当帧射线问题）
            if (_detailCard == c)
            {
                ConfirmUseDetail();   // 第二次点同一张＝使用
                return;
            }
            ClearSelection();
            _detailCard = c;
            _selHint = CardDetailText(c);
            Refresh();
        }

        /// <summary>首点手牌时写在提示栏的完整信息（含能不能打、二次确认提示）。</summary>
        private string CardDetailText(CardInstance c)
        {
            var sb2 = new System.Text.StringBuilder();
            string kind = c.Def.Type == CardType.Unit ? "单位"
                : c.Def.Type == CardType.Tactic ? "战术"
                : c.Def.Type == CardType.Equipment ? "装备" : "预备牌";
            sb2.Append(c.Def.Name + "（" + kind + "，费 " + c.Def.Cost + "）");
            if (c.Def.Type == CardType.Unit)
            {
                sb2.Append("　" + c.Def.Atk + " 攻 / " + c.Def.Hp + " 生命");
                if (c.Def.Guard) { sb2.Append("　护卫"); }
                if (c.Def.Ranged) { sb2.Append("　远射"); }
            }
            sb2.Append("\n" + c.Def.Text);
            string why = WhyUnplayable(c);
            if (why == null)
            {
                sb2.Append("\n→ 再点这张牌＝使用" + AfterSpend(c.Def.Cost) + "；点别的牌＝看那张");
            }
            else
            {
                sb2.Append("\n→ 现在不能打：" + why);
            }
            return sb2.ToString();
        }

        /// <summary>详情浮层里点了「使用」：进入选目标状态；无目标战术直接结算。</summary>
        private void ConfirmUseDetail()
        {
            CardInstance c = _detailCard;
            _detailCard = null;
            if (c == null)
            {
                return;
            }
            ClearSelection();
            _selCard = c;
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].CardUid == c.Uid)
                {
                    _candidates.Add(_actions[i]);
                }
            }
            // 没有可点目标的战术（补给整备）确认即结算
            if (_candidates.Count == 1
                && _candidates[0].Kind == ActionKind.Tactic
                && _candidates[0].TargetUid < 0)
            {
                Apply(_candidates[0]);
                return;
            }
            if (_candidates.Count == 0)
            {
                _selHint = ExplainUnplayable(c);
            }
            Refresh();
        }

        /// <summary>全屏 ↔ 小窗切换（F11 或界面按钮）。</summary>
        private void ToggleWindowMode()
        {
            bool isFull = Screen.fullScreen || Screen.fullScreenMode != FullScreenMode.Windowed;
            if (isFull)
            {
                Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                Screen.fullScreen = false;
            }
            else
            {
                int w = Screen.currentResolution.width;
                int h = Screen.currentResolution.height;
                Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            }
            if (_hintText != null)
            {
                _hintText.text = (isFull ? "已切到小窗（可拖边角缩放）" : "已切到全屏") + "　——　F11 再切换";
            }
        }

        /// <summary>牌能打出时返回 null；打不出时返回原因。</summary>
        private string WhyUnplayable(CardInstance c)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].CardUid == c.Uid)
                {
                    return null;
                }
            }
            return ExplainUnplayable(c);
        }

        /// <summary>打不出这张牌时给出具体原因，而不是让玩家对着没反应的卡猜。</summary>
        private string ExplainUnplayable(CardInstance c)
        {
            PlayerState me = _engine.S.Players[_engine.S.CurrentPlayer];
            PlayerState foe = _engine.S.Players[1 - _engine.S.CurrentPlayer];
            string head = c.Def.Name + " 现在打不出：";
            if (me.Supply < c.Def.Cost)
            {
                return head + "补给不足（需要 " + c.Def.Cost + "，当前 " + me.Supply + "）";
            }
            if (c.Def.Type == CardType.Unit && !me.CanDeploy)
            {
                return head + "战场 4 格已满，先清出一个位置";
            }
            if (c.Def.Type == CardType.Equipment)
            {
                return me.Board.Count == 0
                    ? head + "装备需要场上先有己方单位"
                    : head + "没有可装备的单位";
            }
            if (c.Def.Type == CardType.Tactic && foe.Board.Count == 0)
            {
                return head + "敌方场上没有单位可作用";
            }
            return head + "没有合法目标";
        }

        private void OnMyUnitClicked(Slot slot)
        {
            PlayerState me = _engine.S.Players[Viewer];
            UnitState unit = me.At(slot);
            if (unit == null)
            {
                return;
            }

            // 已选牌/预备牌时，点自己是「指向己方单位」的效果目标
            GameAction targeted = FindTargetOn(unit);
            if (targeted != null && (_selCard != null || _selReserve != null || _selUnit != null))
            {
                Apply(targeted);
                return;
            }

            if (_repositionMode)
            {
                _selUnit = unit;
                _candidates.Clear();
                for (int i = 0; i < _actions.Count; i++)
                {
                    GameAction a = _actions[i];
                    if (a.Kind == ActionKind.Reposition && a.ActorUid == unit.Card.Uid)
                    {
                        _candidates.Add(a);
                    }
                }
                Refresh();
                if (_candidates.Count == 0)
                {
                    _hintText.text = unit.Card.Short + " 没有可去的空位或可互换单位";
                }
                return;
            }

            if (_selUnit == unit)
            {
                ClearSelection();
                Refresh();
                return;
            }

            ClearSelection();
            _selUnit = unit;
            for (int i = 0; i < _actions.Count; i++)
            {
                GameAction a = _actions[i];
                if (a.Kind == ActionKind.Attack && a.ActorUid == unit.Card.Uid && a.PowerStrike == _powerStrikeMode)
                {
                    _candidates.Add(a);
                }
            }
            // 开了强攻但这一步做不了（补给不足等）：退回普通攻击，避免点不出任何目标
            if (_candidates.Count == 0 && _powerStrikeMode)
            {
                _powerStrikeMode = false;
                for (int i = 0; i < _actions.Count; i++)
                {
                    GameAction a = _actions[i];
                    if (a.Kind == ActionKind.Attack && a.ActorUid == unit.Card.Uid)
                    {
                        _candidates.Add(a);
                    }
                }
            }
            Refresh();
            if (_candidates.Count == 0)
            {
                _hintText.text = unit.Card.Short + " 本轮不能攻击（已攻击／刚部署／后排无远射／无合法目标）";
            }
            else if (_powerStrikeMode)
            {
                _hintText.text = "强攻模式：本次攻击额外支付 " + GameEngine.PowerStrikeCost + " 补给，主动伤害 +"
                    + GameEngine.PowerStrikeBonus + "（点目标确认，点「强攻」可取消）";
            }
        }

        private void OnReserveClicked(string id)
        {
            if (_selReserve == id)
            {
                ClearSelection();
                Refresh();
                return;
            }
            ClearSelection();
            _selReserve = id;
            for (int i = 0; i < _actions.Count; i++)
            {
                GameAction a = _actions[i];
                if (a.Kind == ActionKind.Reserve && a.ReserveId == id)
                {
                    _candidates.Add(a);
                }
            }
            // 无目标的支援（应急调度）直接执行
            if (_candidates.Count == 1 && _candidates[0].TargetUid < 0)
            {
                Apply(_candidates[0]);
                return;
            }
            Refresh();
        }

        private void OnEnemyCampClicked()
        {
            OnCampClicked();
        }

        /// <summary>
        /// 点击营地发起直击。双人同屏时两侧玩家都要能点「对方的营地」，
        /// 所以两个营地条都挂同一个处理器——合法性完全由 _candidates 决定。
        /// </summary>
        private void OnCampClicked()
        {
            GameAction camp = FindCampAttack();
            if (camp != null)
            {
                Apply(camp);
                return;
            }
            _hintText.text = "敌方前排不为空时不能直击营地；先清掉前排单位";
        }

        private void OnRepositionClicked()
        {
            if (_vsAi && _engine.S.CurrentPlayer != 0)
            {
                return;
            }
            ClearSelection();
            _repositionMode = true;
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Kind == ActionKind.Reposition)
                {
                    _candidates.Add(_actions[i]);
                    break;
                }
            }
            Refresh();
            if (_candidates.Count == 0)
            {
                _hintText.text = "本轮换防已用过，或没有可换的位置";
            }
        }

        private void OnScoutClicked()
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Kind == ActionKind.Scout)
                {
                    Apply(_actions[i]);
                    return;
                }
            }
            _hintText.text = "现在不能侦察（补给不足／已用／手牌已满／牌库与弃牌区都空）";
        }

        private void OnEndRoundClicked()
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Kind == ActionKind.EndRound)
                {
                    Apply(_actions[i]);
                    return;
                }
            }
        }

        // ── 候选目标查找 ──────────────────────────────────────────

        private GameAction FindDeploy(Slot slot)
        {
            GameAction fallback = null;
            for (int i = 0; i < _candidates.Count; i++)
            {
                GameAction a = _candidates[i];
                if (a.Kind != ActionKind.Deploy || a.ToSlot.Front != slot.Front || a.ToSlot.Index != slot.Index)
                {
                    continue;
                }
                // 维修师这类有入场治疗的单位会为同一格生成多个候选（是否顺带治疗某个单位），
                // 优先选带治疗目标的那一个，避免白白浪费入场效果。
                if (a.TargetUid >= 0)
                {
                    return a;
                }
                if (fallback == null)
                {
                    fallback = a;
                }
            }
            return fallback;
        }

        /// <summary>换防模式下「移到这一格」的候选动作（5.3 只允许跨排）。</summary>
        private GameAction FindRepositionTo(Slot slot)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                GameAction a = _candidates[i];
                if (a.Kind != ActionKind.Reposition || a.TargetUid >= 0)
                {
                    continue;
                }
                if (a.ToSlot.Front == slot.Front && a.ToSlot.Index == slot.Index)
                {
                    return a;
                }
            }
            return null;
        }

        private GameAction FindTargetOn(UnitState unit)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                GameAction a = _candidates[i];
                // 部署牌的目标是「格子」不是单位；它的 TargetUid 只是入场治疗对象，
                // 不能因为点了一个受伤单位就顺手把它部署出去。
                if (a.Kind == ActionKind.Reposition || a.Kind == ActionKind.Deploy)
                {
                    continue;
                }
                // 装备牌的「被装备单位」记在 ActorUid，其余效果记在 TargetUid
                int subject = a.Kind == ActionKind.Equip ? a.ActorUid : a.TargetUid;
                if (subject == unit.Card.Uid)
                {
                    return a;
                }
            }
            if (_repositionMode)
            {
                for (int i = 0; i < _candidates.Count; i++)
                {
                    GameAction a = _candidates[i];
                    if (a.Kind == ActionKind.Reposition && a.TargetUid == unit.Card.Uid)
                    {
                        return a;
                    }
                }
            }
            return null;
        }

        private GameAction FindCampAttack()
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].TargetCamp)
                {
                    return _candidates[i];
                }
            }
            return null;
        }

        private void Apply(GameAction a)
        {
            if (a == null)
            {
                return;
            }
            _engine.Apply(a);
            ClearSelection();
            Refresh();
        }

        // ── 浮层（换牌 / 掩护 / 侦察 / 终局）────────────────────────

        /// <summary>
        /// 清掉浮层。Destroy 延迟到帧末生效，期间旧面板仍然参与射线检测，
        /// 所以先 SetActive(false) 立刻让它不可点，再销毁，并扫掉残留的 Overlay。
        /// </summary>
        private void DestroyOverlay()
        {
            if (_overlay != null)
            {
                _overlay.SetActive(false);
                Destroy(_overlay);
                _overlay = null;
            }
            if (_root == null)
            {
                return;
            }
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                Transform child = _root.GetChild(i);
                if (child != null && child.name == "Overlay")
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private RectTransform NewOverlay(float w, float h)
        {
            DestroyOverlay();
            _overlay = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            _overlay.transform.SetParent(_root, false);
            var rt = _overlay.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2((_layoutW - w) * 0.5f, -(UiKit.RefH - h) * 0.5f - 30f);
            rt.sizeDelta = new Vector2(w, h);
            var img = _overlay.GetComponent<Image>();
            img.color = new Color(0.05f, 0.06f, 0.08f, 0.97f);
            _overlay.transform.SetAsLastSibling();
            return rt;
        }

        /// <summary>V0.3 §9.1 侦察员入场：只给本人看牌库顶 2 张，由玩家选择保持或交换。</summary>
        private void ShowReorderOverlay()
        {
            RectTransform panel = NewOverlay(560f, 268f);
            int count = _engine.PendingReorderCards.Count;
            UiKit.Label(panel, "T", 16f, 10f, 528f, 32f,
                "侦察员调序（仅你可见，确认后继续这次部署）", 19, TextAnchor.MiddleCenter, UiKit.Selected);
            for (int i = 0; i < count; i++)
            {
                CardInstance c = _engine.PendingReorderCards[i];
                var view = new CardView(panel, "Order" + i, 90f + i * 150f, 52f, 130f, 150f);
                view.SetCardFace(c.Def.Cost.ToString(), c.Def.Name,
                    c.Def.Type == CardType.Unit ? c.Def.Atk + "攻 " + c.Def.Hp + "血" : "非单位",
                    "牌库顶第 " + (i + 1) + " 张", true, UiKit.Target, null);
            }
            UiKit.Label(panel, "Q", 16f, 206f, 528f, 22f,
                "当前顺序：第1张 " + (count > 0 ? _engine.PendingReorderCards[0].Def.Name : "?")
                + "　第2张 " + (count > 1 ? _engine.PendingReorderCards[1].Def.Name : "?"),
                14, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Button(panel, "Keep", 56f, 228f - 6f, 200f, 40f, "保持顺序", UiKit.ButtonNormal, () =>
            {
                _engine.ResolveReorder(false);
                ClearSelection();
                Refresh();
            });
            UiKit.Button(panel, "Swap", 300f, 222f, 200f, 40f, "交换两张", new Color(0.18f, 0.42f, 0.30f), () =>
            {
                _engine.ResolveReorder(true);
                ClearSelection();
                Refresh();
            });
        }

        /// <summary>V0.3 §9.2 应急调度：候选按牌名聚合显示（不展示牌库顺序），确认才付费。</summary>
        private void ShowSearchOverlay()
        {
            int count = _engine.PendingSearchCards.Count;
            RectTransform panel = NewOverlay(Mathf.Max(560f, 120f + count * 124f), 280f);
            UiKit.Label(panel, "T", 20f, 10f, panel.sizeDelta.x - 40f, 32f,
                "应急调度：按牌名选择（费用不高于 3 的基础单位，仅你可见）", 18, TextAnchor.MiddleCenter, UiKit.Selected);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                CardInstance c = _engine.PendingSearchCards[i];
                var view = new CardView(panel, "Pick" + i, 60f + i * 124f, 52f, 112f, 148f);
                view.SetCardFace(c.Def.Cost.ToString(), c.Def.Name, c.Def.Atk + "攻 " + c.Def.Hp + "血",
                    ShortText(c.Def.Text), true, UiKit.Target, () =>
                    {
                        _engine.ResolveSearchPick(index);
                        ClearSelection();
                        Refresh();
                    });
            }
            UiKit.Button(panel, "CancelSearch", 60f, 214f, 200f, 44f, "取消（不付费）", UiKit.ButtonNormal, () =>
            {
                _engine.CancelSearch();
                ClearSelection();
                Refresh();
            });
        }

        /// <summary>V0.3 §12.1：弃牌区可以聚合或截短，但必须能查看双方全部公开弃牌。</summary>
        private void ShowDiscardOverlay()
        {
            GameState s = _engine.S;
            RectTransform panel = NewOverlay(680f, 360f);
            UiKit.Label(panel, "T", 16f, 10f, 648f, 32f, "弃牌区全览（公开信息）", 20, TextAnchor.MiddleCenter, UiKit.Selected);
            UiKit.Label(panel, "D0", 20f, 48f, 640f, 130f, "甲（" + s.Players[0].Discard.Count + " 张）：" + DiscardFull(s.Players[0]),
                14, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Label(panel, "D1", 20f, 184f, 640f, 130f, "乙（" + s.Players[1].Discard.Count + " 张）：" + DiscardFull(s.Players[1]),
                14, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Button(panel, "CloseDisc", 240f, 316f, 200f, 40f, "关闭", UiKit.ButtonNormal, () =>
            {
                DestroyOverlay();
                Refresh();
            });
        }

        private static string DiscardFull(PlayerState p)
        {
            if (p.Discard.Count == 0)
            {
                return "（空）";
            }
            var parts = new List<string>();
            for (int i = 0; i < p.Discard.Count; i++)
            {
                parts.Add(p.Discard[i].Def.Name);
            }
            return string.Join("、", parts.ToArray());
        }

        private void ShowCoverOverlay()
        {
            RectTransform panel = NewOverlay(660f, 330f);
            int defender = 1 - _engine.S.CurrentPlayer;
            int attacker = _engine.S.CurrentPlayer;
            GameAction pending = _engine.PendingAction;
            PlayerState def = _engine.S.Players[defender];
            PlayerState atk = _engine.S.Players[attacker];

            UiKit.Label(panel, "T", 16f, 10f, 628f, 34f,
                "掩护窗口　——　由 " + def.Name + " 决定（每轮一次）", 21, TextAnchor.MiddleCenter, UiKit.Selected);
            UiKit.Label(panel, "D", 20f, 44f, 620f, 30f,
                atk.Name + " 正在行动：" + (pending != null ? pending.Label : "造成伤害"),
                15, TextAnchor.MiddleCenter, UiKit.TextMain);

            // V0.3 §12.1：并列显示「不使用」与「使用」两种分支的单位生命、阵亡、战损、清线与营地结果。
            string noCover, withCover;
            CoverBranchText(pending, def, atk, false, out noCover);
            CoverBranchText(pending, def, atk, true, out withCover);
            UiKit.Label(panel, "A", 20f, 78f, 620f, 40f, "不使用掩护：" + noCover, 14, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Label(panel, "B", 20f, 122f, 620f, 40f, "使用掩护（-1 补给，" + def.Supply + "→" + (def.Supply - 1) + "）：" + withCover,
                14, TextAnchor.UpperLeft, UiKit.Selected);
            UiKit.Label(panel, "Q", 20f, 168f, 620f, 24f,
                "只有使用与放弃两项；0 主动伤害不弹窗。补给不足或本轮额度已用时不能使用。",
                13, TextAnchor.MiddleCenter, UiKit.TextDim);

            UiKit.Button(panel, "Use", 64f, 206f, 250f, 54f, "支付 1 补给，减伤 1", new Color(0.18f, 0.42f, 0.30f), () =>
            {
                _engine.ResolveCover(true);
                ClearSelection();
                Refresh();
            });
            UiKit.Button(panel, "Pass", 346f, 206f, 250f, 54f, "不支付，承受全额伤害", UiKit.ButtonNormal, () =>
            {
                _engine.ResolveCover(false);
                ClearSelection();
                Refresh();
            });
        }

        /// <summary>给出某一掩护分支的结果描述（供掩护窗口并列显示）。</summary>
        private void CoverBranchText(GameAction pending, PlayerState def, PlayerState atk, bool covered, out string text)
        {
            if (pending == null)
            {
                text = "—";
                return;
            }
            int dmg;
            if (pending.Kind == ActionKind.Attack)
            {
                UnitState attackerUnit = _engine.FindUnit(atk, pending.ActorUid);
                if (attackerUnit == null)
                {
                    text = "—";
                    return;
                }
                int[] branches = _engine.AttackDamageBranches(attackerUnit, pending.PowerStrike, true);
                dmg = covered ? branches[1] : branches[0];
            }
            else
            {
                dmg = Math.Max(0, 3 - (covered ? 1 : 0));
            }

            if (pending.Kind == ActionKind.Attack && pending.TargetCamp)
            {
                text = string.Format("{0} 营地 {1}→{2}（无单位阵亡，不产生战损与清线）",
                    def.Name, def.Camp, def.Camp - dmg);
                return;
            }

            UnitState target = _engine.FindUnit(def, pending.TargetUid);
            if (target == null)
            {
                text = "—";
                return;
            }
            int hpAfter = target.CurrentHp - dmg;
            if (hpAfter > 0)
            {
                text = string.Format("{0} 生命 {1}→{2}，存活，无战损", target.Card.Short, target.CurrentHp, hpAfter);
                return;
            }
            int toll = target.Upgrade != null ? 2 : 1;
            bool isLastFront = target.Slot.Front && def.FrontUnits.Count == 1;
            bool clear = isLastFront && !atk.ClearFrontBonusUsed;
            int campLoss = toll + (clear ? 1 : 0);
            text = string.Format("{0} 生命 {1}→{2} 阵亡：{3} 承担战损 {4}{5} → 营地 {6}→{7}",
                target.Card.Short, target.CurrentHp, hpAfter, def.Name, toll,
                clear ? " + 首次清线 1" : string.Empty, def.Camp, def.Camp - campLoss);
        }

        private void ShowScoutOverlay()
        {
            int count = _engine.PendingScoutCards.Count;
            RectTransform panel = NewOverlay(100f + count * 124f, 268f);
            UiKit.Label(panel, "T", 10f, 10f, 80f + count * 124f, 32f,
                "侦察：选 1 张加入手牌", 20, TextAnchor.MiddleCenter, UiKit.Selected);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                CardInstance c = _engine.PendingScoutCards[i];
                var view = new CardView(panel, "Scout" + i, 60f + i * 124f, 56f, 112f, 140f);
                view.SetCardFace(c.Def.Cost.ToString(), c.Def.Name,
                    c.Def.Type == CardType.Unit ? c.Def.Atk + "攻 " + c.Def.Hp + "血" : "非单位",
                    ShortText(c.Def.Text), true, UiKit.Target, () =>
                    {
                        _engine.ResolveScoutPick(index);
                        ClearSelection();
                        Refresh();
                    });
            }
            UiKit.Button(panel, "Skip", 60f, 210f, 180f, 44f, "随便拿第一张", UiKit.ButtonNormal, () =>
            {
                _engine.ResolveScoutPick(0);
                ClearSelection();
                Refresh();
            });
        }

        private void ShowMulliganOverlay()
        {
            // 双人同屏时两个人要先后各换一次牌；单人模式只处理甲，乙由 AI 代换。
            PlayerState me = _engine.S.Players[_mulliganViewer];
            string title = _vsAi
                ? "换牌阶段：点手牌选中要换掉的（至多 2 张，领航单位不能换）"
                : me.Name + " 换牌：点手牌选中要换掉的（至多 2 张，领航单位不能换）";
            RectTransform panel = NewOverlay(720f, 320f);
            UiKit.Label(panel, "T", 20f, 12f, 680f, 32f, title, 19, TextAnchor.MiddleCenter, UiKit.Selected);
            for (int i = 0; i < me.Hand.Count; i++)
            {
                CardInstance c = me.Hand[i];
                bool isVanguard = c.Uid == me.VanguardUid;
                bool picked = _mulliganPicks.Contains(c.Uid);
                string body = isVanguard ? "领航单位（不可换）" : (picked ? "将换掉" : "可换");
                var view = new CardView(panel, "M" + i, 26f + i * 122f, 56f, 112f, 148f);
                view.SetCardFace(c.Def.Cost.ToString(), c.Def.Name,
                    c.Def.Type == CardType.Unit ? c.Def.Atk + "攻 " + c.Def.Hp + "血" : "非单位",
                    body, !isVanguard, picked ? UiKit.Selected : UiKit.Border,
                    isVanguard ? null : (UnityEngine.Events.UnityAction)(() => OnHandClicked(c)));
            }
            UiKit.Button(panel, "Ok", 190f, 244f, 170f, 50f, "确认换牌（" + _mulliganPicks.Count + "）",
                new Color(0.18f, 0.42f, 0.30f), () => ConfirmMulliganFor(_mulliganViewer, new List<int>(_mulliganPicks)));
            UiKit.Button(panel, "No", 380f, 244f, 170f, 50f, "不换牌直接开始", UiKit.ButtonNormal,
                () => ConfirmMulliganFor(_mulliganViewer, new List<int>()));
        }

        /// <summary>
        /// 让指定玩家确认换牌；双人同屏时轮到下一个人继续换，直到两人都确认。
        /// </summary>
        private void ConfirmMulliganFor(int player, List<int> picks)
        {
            _engine.ConfirmMulligan(player, picks);
            _mulliganPicks.Clear();
            if (_engine.S.Phase == GamePhase.Mulligan)
            {
                if (_vsAi)
                {
                    _engine.ConfirmMulligan(1, _ai.PickMulligan(_engine, 1));
                }
                else
                {
                    _mulliganViewer = 1 - player;
                }
            }
            else
            {
                _mulliganViewer = 0;
            }
            ClearSelection();
            Refresh();
        }

        private void ShowGameOverOverlay()
        {
            RectTransform panel = NewOverlay(760f, 340f);
            UiKit.Label(panel, "T", 16f, 14f, 728f, 44f, WinnerText(), 22, TextAnchor.MiddleCenter, UiKit.Selected);
            UiKit.Label(panel, "D", 16f, 58f, 728f, 28f,
                "营地 甲 " + _engine.S.Players[0].Camp + " / 乙 " + _engine.S.Players[1].Camp
                + "（内部保留有符号数值，负值即已过量击杀）",
                15, TextAnchor.MiddleCenter, UiKit.TextMain);
            // V0.3 §1.3：终局说明必须给出结算前生命、各项损失与判定结果
            UiKit.Label(panel, "R", 20f, 92f, 720f, 120f,
                _engine.S.FinishReason == null ? "（无）" : _engine.S.FinishReason,
                14, TextAnchor.UpperLeft, UiKit.TextDim);
            UiKit.Button(panel, "Again", 96f, 258f, 210f, 52f, "新开一局（同种子）", new Color(0.18f, 0.42f, 0.30f),
                () => NewGame(202));
            UiKit.Button(panel, "Rand", 344f, 258f, 210f, 52f, "新开一局（随机）", UiKit.ButtonNormal,
                () => NewGame(Environment.TickCount & 0xffff));
            UiKit.Button(panel, "DiscLog", 504f, 258f, 160f, 52f, "看全部日志", UiKit.ButtonNormal,
                () => ShowDiscardOverlay());
        }
    }
}
