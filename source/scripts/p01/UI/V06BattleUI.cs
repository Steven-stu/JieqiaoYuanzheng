using System;
using System.Collections.Generic;
using Jieqiao;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 界桥远征 V0.6 实机对局界面（玩家 = 甲方，对手 = AI）。
    ///
    /// 与 V0.3 的 Battle2DUI 并行存在，互不影响。
    /// 设计原则：
    ///   · 所有可点目标都来自 V06Engine.LegalActions()，界面只负责把结果画出来；
    ///   · 棋盘 3 前排 + 3 后排，单一行动点资源，每轮 4 次主行动；
    ///   · 控件一次性建好（池化），只刷新内容，避免「新建控件当帧射线穿透」。
    /// </summary>
    public sealed class V06BattleUI : MonoBehaviour
    {
        // ── 布局（设计空间 1600×900，左上角原点）──────────────────
        private const float RefW = 1600f;
        private const float RefH = 900f;
        // 卡框统一 3:4（立绘素材就是 3:4）。
        // 界面对局界面改版：棋盘格 90×120 → 112×149、手牌 96×128 → 112×149，
        // 四行棋盘与手牌全部加大，纵向靠"压缩中间空白带 + 左右两栏合并"腾出来。
        private const float SlotW = 112f;
        private const float SlotH = 149f;
        private const float SlotX0 = 618f;      // 三格的左边界（3×112 + 2×14 = 364，居中于 800）
        private const float SlotStep = 126f;
        private const float EnemyBackY = 44f;
        private const float EnemyFrontY = 196f;
        private const float MineFrontY = 395f;
        private const float MineBackY = 547f;
        private const float EnemyFlagY = 4f;
        private const float MineFlagY = 699f;
        private const float HandY = 737f;
        private const float HandX0 = 24f;
        private const float HandStep = 122f;
        private const float HandW = 112f;
        private const float HandH = 149f;
        private const float HandLiftY = 8f;      // 选中的手牌上抬，给"已选中"第二个信号

        private V06Engine _engine;
        private bool _running;
        private float _aiTimer;
        private int _playerCamp = 0;

        // 池化控件
        private RectTransform _root;
        private RectTransform _layoutRoot;
        private Image[] _slotBg = new Image[12];         // 0-5 我方，6-11 敌方（各自 0-2 前排、3-5 后排）
        private Text[] _slotMain = new Text[12];
        private Text[] _slotSub = new Text[12];
        private Text[] _slotKw = new Text[12];
        private Button[] _slotBtn = new Button[12];
        private Image[] _slotArt = new Image[12];
        // V0.7：GPT 交付的卡框九宫格（普通/选中/目标），叠在格子最上层、只画边框
        private Image[] _slotFrame = new Image[12];
        private static Sprite _frameNormal, _frameSelected, _frameTarget;
        private Image[] _handArt = new Image[9];
        private Image _detailArt;
        private Image[] _reserveBg = new Image[6];
        private Image[] _reserveArt = new Image[6];
        private Text[] _reserveMain = new Text[6];
        private Text[] _reserveSub = new Text[6];
        private int _selReserve = -1;
        // 右键详览浮层（放大卡面 + 全部文字）
        private GameObject _zoomLayer;
        private Image _zoomArt;
        private Text _zoomTitle;
        private Text _zoomBody;
        private Text _zoomHint;
        private UnityEngine.Canvas _canvas;
        // ── V0.7：GPT 交付的 UI 组件接线 ──
        private V06BattleFx _fx;
        private Image _banner;
        private V06CardInfoPanel _infoPanel;
        /// <summary>大厅可见期间暂停（AI 不再自动走）。</summary>
        public bool autoStartOnLoad = true;
        public bool paused;
        /// <summary>联机模式：不跑 AI，行动来自网络；我的行动要发出去。</summary>
        public bool netMode;
        /// <summary>我在联机里坐哪一方。</summary>
        public bool netIsHost = true;
        private bool _resultRecorded;
        /// <summary>结算演出只做一次。Update 每帧都会调 ShowResult，若不加这道闸，
        /// 每帧都会新生成一张全屏叠加图 + 播一次音效 + 重启补间，几十秒内堆出上百张
        /// 全屏图层把渲染拖死（实机表现为"输掉后屏幕发白、卡死"）。</summary>
        private bool _resultShown;
        private bool _fxErrorLogged;
        /// <summary>本实例是否真的开过一局（重载自愈只在这个前提下才允许自己重开）。</summary>
        private bool _everStarted;
        private string _myDeckName = "", _foeDeckName = "";
        private bool _engineNullLogged;
        private int _recoverTried;
        private int _lastSeed, _lastPreconA, _lastPreconB;
        private GraphicRaycaster _raycaster;
        // 对手攻击时的响应按钮（响应牌原本在界面上没有入口，实机点击测试抓出来的缺口）
        private Image[] _respondBg = new Image[3];
        private Text[] _respondText = new Text[3];
        private Text _respondTitle;
        private Text _reserveTitle;
        private readonly List<V06Action> _respondActions = new List<V06Action>();
        private Image[] _handBg = new Image[9];
        private Text[] _handTop = new Text[9];
        private Text[] _handMain = new Text[9];
        private Text[] _handSub = new Text[9];
        private Button[] _handBtn = new Button[9];
        private Image _enemyFlagFill;
        private Image _mineFlagFill;
        private Text _enemyFlagText;
        private Text _mineFlagText;
        private Button _enemyFlagBtn;
        private Text _statusText;
        private Text _hintText;
        private Text _logText;
        private Text _detailText;
        private Button _powerBtn;
        private Button _swapBtn;
        private Button _passBtn;
        private Button _coverBtn;
        private Button _declineBtn;
        private Button _restartBtn;
        private Button _fuseBtn;
        private GameObject _resultPanel;
        private Text _resultText;

        // ── V0.7 第 2 批动画接线（余烬层 / 发牌 / 翻牌 / 进化环 / 融合环 / 旗闪 / 回合横幅）──
        private Image _emberOverlay;
        private Image _turnBannerImg;
        private Text _turnBannerText;
        private Image _battleBg;               // 战斗背景（战况牌生效时整块换图）
        private Sprite _battleBgDefault;
        private Text _fieldLabel;
        private string _fieldApplied = "";
        private Image _pressureFx;          // 第 3 批：轮末压力警告（全屏四周红雾，循环）
        private int _lastHandCount = -1;
        private int _lastMineFlag = -1;
        private int _lastEnemyFlag = -1;
        private int _lastActiveCamp = -99;
        private int _lastRound = -1;
        private readonly Dictionary<int, int> _growthSeen = new Dictionary<int, int>();
        private readonly HashSet<int> _fusedSeen = new HashSet<int>();
        private int _dealPulseToken;

        // 选择状态
        private List<V06Action> _actions = new List<V06Action>();
        private int _selHand = -1;
        private int _selUid = -1;
        private bool _powerStrike;
        private bool _swapMode;
        // ── 提示：分"一次性消息"与"状态引导"两层 ──
        // 一次性消息（点错原因、刚做了什么）由各处 handler 直接赋值，3 秒后自动让位；
        // 状态引导由 BuildGuidance() 按当前局面算出，永远有内容。
        // 之所以做成 property：30 处 `_hint = "..."` 不用逐个改，赋值即自动打时间戳。
        private string _hintMsg;
        private float _hintMsgAt = -99f;
        private const float HintMsgLife = 3f;
        /// <summary>刚用带汲取的单位打了远征旗：等这一击结算完，在提示行说明"打旗不触发汲取"。</summary>
        private bool _drainFlagNote;
        /// <summary>S-V03 这类"单位 + 落点"两段式战术：已选来源单位，等玩家点落点。</summary>
        private int _pendingTacticUid = -1;
        /// <summary>换位确认弹窗的根节点。</summary>
        private GameObject _swapConfirmLayer;
        private string _hint
        {
            get { return _hintMsg; }
            set { _hintMsg = value; _hintMsgAt = Time.unscaledTime; }
        }
        private string _lastError;

        private void Start()
        {
            Application.runInBackground = true;
            V06Audio.Music("menu");
            BuildScene();
            Debug.Log("[V06启动] 战斗界面已建：autoStartOnLoad=" + autoStartOnLoad
                + " canvas=" + (_canvas == null ? "null" : ("order=" + _canvas.sortingOrder + " active=" + _canvas.gameObject.activeSelf)));
            // 大厅已经开着（正式包一进来就在大厅）→ 自己这块画布要藏起来，
            // 否则棋盘/旗条会从大厅底下透出来，看起来像"还停在战斗界面"。
            if (!autoStartOnLoad && V06ShellUI.Instance != null && V06ShellUI.Instance.Visible)
            {
                SetVisualVisible(false);
                paused = true;
                Debug.Log("[V06启动] 大厅在前，战斗画布已隐藏：active=" + (_canvas == null ? "null" : _canvas.gameObject.activeSelf.ToString())
                    + " paused=" + paused);
            }
            if (autoStartOnLoad) { NewGame(20260101, 0, 1); }
            else
            {
                _hint = string.IsNullOrEmpty(_hint) ? "从大厅选择对局开始。" : _hint;
                if (_resultPanel != null) { _resultPanel.SetActive(false); }
            }
        }

        // ───────────────────────── 建界面 ─────────────────────────

        private void BuildScene()
        {
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                esGo.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("V06Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas = canvas;
            _raycaster = canvasGo.GetComponent<GraphicRaycaster>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;

            var layoutGo = new GameObject("LayoutRoot", typeof(RectTransform));
            layoutGo.transform.SetParent(canvasGo.transform, false);
            _layoutRoot = layoutGo.GetComponent<RectTransform>();
            _layoutRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _layoutRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _layoutRoot.pivot = new Vector2(0.5f, 0.5f);
            _layoutRoot.anchoredPosition = Vector2.zero;
            _layoutRoot.sizeDelta = new Vector2(RefW, RefH);
            _root = _layoutRoot;

            // 背景（用户提供的暮色浮岛与断裂星环）；打出战况牌后换成对应场地背景
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
                _battleBg = bgImg;
                _battleBgDefault = bgSprite;
            }
            // 场地名（打出战况牌后显示"场地：工坊防线（我方）"，让玩家知道场上生效的是什么）
            _fieldLabel = UiKit.Label(_root, "FieldLabel", 20f, 42f, 1200f, 24f, "", 15,
                TextAnchor.MiddleLeft, UiKit.Selected);
            _fieldLabel.raycastTarget = false;
            Image scrim = UiKit.Panel(_root, "Scrim", 0f, 0f, RefW, RefH, new Color(0.06f, 0.07f, 0.09f, 0.58f));
            scrim.raycastTarget = false;   // 纯压暗层，不能吃掉点击（实机测试里它挡住过响应按钮）

            // 第 2 批动画：战场余烬氛围层（3 帧 1600×900，慢速循环，纯装饰不吃射线）
            _emberOverlay = UiKit.Panel(_root, "BattleEmbers", 0f, 0f, RefW, RefH, new Color(1f, 1f, 1f, 0.5f));
            _emberOverlay.raycastTarget = false;
            _emberOverlay.color = new Color(1f, 1f, 1f, 0.42f);
            if (!V06UiV07.HasAnim("anim_bg_embers")) { _emberOverlay.gameObject.SetActive(false); }
            else { V06UiV07.PlayAnim(_emberOverlay, "anim_bg_embers", 5f, true); }

            // 敌方旗
            UiKit.Panel(_root, "EnemyFlagBg", 20f, EnemyFlagY, 1200f, 36f, new Color(0.10f, 0.09f, 0.11f));
            _enemyFlagFill = UiKit.Panel(_root, "EnemyFlagFill", 20f, EnemyFlagY, 1200f, 36f, UiKit.EnemyCamp);
            _enemyFlagText = UiKit.Label(_root, "EnemyFlagText", 32f, EnemyFlagY, 1176f, 36f, "", 17, TextAnchor.MiddleLeft, UiKit.TextMain);
            _enemyFlagText.raycastTarget = true;
            _enemyFlagBtn = _enemyFlagText.gameObject.AddComponent<Button>();
            _enemyFlagBtn.onClick.AddListener(OnEnemyFlagClicked);

            // 我方旗
            UiKit.Panel(_root, "MineFlagBg", 20f, MineFlagY, 1200f, 36f, new Color(0.09f, 0.11f, 0.10f));
            _mineFlagFill = UiKit.Panel(_root, "MineFlagFill", 20f, MineFlagY, 1200f, 36f, UiKit.MyCamp);
            _mineFlagText = UiKit.Label(_root, "MineFlagText", 32f, MineFlagY, 1176f, 36f, "", 17, TextAnchor.MiddleLeft, UiKit.TextMain);

            // 12 个格位
            for (int camp = 0; camp < 2; camp++)
            {
                for (int s = 0; s < 6; s++)
                {
                    int idx = camp * 6 + s;
                    float y;
                    if (camp == 1) { y = V06Slot.IsFront(s) ? EnemyFrontY : EnemyBackY; }
                    else { y = V06Slot.IsFront(s) ? MineFrontY : MineBackY; }
                    float x = SlotX0 + s % 3 * SlotStep;
                    Image bg = UiKit.Panel(_root, "Slot" + idx, x, y, SlotW, SlotH, UiKit.CardBg);
                    bg.gameObject.AddComponent<RectMask2D>();
                    var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                    artGo.transform.SetParent(bg.transform, false);
                    _slotArt[idx] = artGo.GetComponent<Image>();
                    // 名称条（顶部 26，14 号）：名字单独一行，不再和数值挤在一起
                    Image topStrip = UiKit.Panel(bg.transform, "TopStrip", 0f, 0f, SlotW, 26f, new Color(0.04f, 0.05f, 0.07f, 0.80f));
                    topStrip.raycastTarget = false;
                    Text main = UiKit.Label(bg.transform, "Main", 3f, 1f, SlotW - 6f, 24f, "", 14, TextAnchor.MiddleCenter, UiKit.TextMain);
                    var sh = main.gameObject.AddComponent<Shadow>();
                    sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    sh.effectDistance = new Vector2(1f, -1f);
                    // 关键词条（数值条上方 18，11 号金色）
                    Image kwStrip = UiKit.Panel(bg.transform, "KwStrip", 0f, SlotH - 48f, SlotW, 18f, new Color(0.04f, 0.05f, 0.07f, 0.62f));
                    kwStrip.raycastTarget = false;
                    Text kw = UiKit.Label(bg.transform, "Kw", 3f, SlotH - 48f, SlotW - 6f, 18f, "", 11, TextAnchor.MiddleCenter, UiKit.Selected);
                    // 数值条（底部 30，16 号）：攻/血独立、够大
                    Image botStrip = UiKit.Panel(bg.transform, "BotStrip", 0f, SlotH - 30f, SlotW, 30f, new Color(0.04f, 0.05f, 0.07f, 0.86f));
                    botStrip.raycastTarget = false;
                    Text sub = UiKit.Label(bg.transform, "Sub", 3f, SlotH - 30f, SlotW - 6f, 30f, "", 16, TextAnchor.MiddleCenter, UiKit.TextMain);
                    var btn = bg.gameObject.AddComponent<Button>();
                    int campCopy = camp;
                    int slotCopy = s;
                    btn.onClick.AddListener(delegate { OnSlotClicked(campCopy, slotCopy); });
                    AttachRightClick(bg.gameObject, "Slot" + idx);
                    _slotBg[idx] = bg;
                    _slotMain[idx] = main;
                    _slotSub[idx] = sub;
                    _slotKw[idx] = kw;
                    _slotBtn[idx] = btn;
                    _slotFrame[idx] = AddSlotFrame(bg);
                }
                UiKit.Label(_root, "RowLabel" + camp, 496f, camp == 1 ? EnemyBackY + 40f : MineFrontY + 40f,
                    110f, 40f, camp == 1 ? "对手" : "我方", 17, TextAnchor.MiddleRight, UiKit.Selected);
            }

            // ── 中央行动条（两排之间那条空白带）：谁在行动 / 剩余费用与主行动 / 下一步提示 ──
            _statusText = UiKit.Label(_root, "Status", 300f, 348f, 920f, 24f, "", 17, TextAnchor.MiddleCenter, UiKit.TextMain);
            _hintText = UiKit.Label(_root, "Hint", 300f, 372f, 920f, 22f, "", 15, TextAnchor.MiddleCenter, UiKit.Selected);
            // ── 左栏：预备 / 已选中 / 战报 ──
            _detailText = UiKit.Label(_root, "Detail", 306f, 420f, 294f, 126f, "", 14, TextAnchor.UpperLeft, UiKit.TextDim);
            _logText = UiKit.Label(_root, "Log", 306f, 574f, 294f, 122f, "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            UiKit.Label(_root, "DetailTitle", 306f, 396f, 294f, 20f, "已选中", 13, TextAnchor.MiddleLeft, UiKit.Selected);
            UiKit.Label(_root, "LogTitle", 306f, 552f, 294f, 20f, "战报（最近 8 条）", 13, TextAnchor.MiddleLeft, UiKit.Selected);

            // 手牌 9 张
            for (int i = 0; i < 9; i++)
            {
                float x = HandX0 + i * HandStep;
                Image bg = UiKit.Panel(_root, "Hand" + i, x, HandY, HandW, HandH, UiKit.CardBg);
                bg.gameObject.AddComponent<RectMask2D>();
                var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(bg.transform, false);
                _handArt[i] = artGo.GetComponent<Image>();
                // 底部信息条：压暗，保证名称与数值在立绘上也读得清
                Image info = UiKit.Panel(bg.transform, "Info", 0f, HandH - 50f, HandW, 50f, new Color(0.04f, 0.05f, 0.07f, 0.84f));
                info.raycastTarget = false;
                // 费用徽记：左上独立小底，17 号数字，不和名称共行
                Image costBadge = UiKit.Panel(bg.transform, "CostBadge", 4f, 4f, 30f, 26f, new Color(0.06f, 0.07f, 0.10f, 0.86f));
                costBadge.raycastTarget = false;
                Text top = UiKit.Label(bg.transform, "Top", 4f, 4f, 30f, 26f, "", 17, TextAnchor.MiddleCenter, UiKit.Selected);
                var sh2 = top.gameObject.AddComponent<Shadow>();
                sh2.effectColor = new Color(0f, 0f, 0f, 0.9f);
                sh2.effectDistance = new Vector2(1f, -1f);
                // 名称 15 号 / 副行 13 号
                Text main = UiKit.Label(bg.transform, "Main", 2f, HandH - 50f, HandW - 4f, 26f, "", 15, TextAnchor.MiddleCenter, UiKit.TextMain);
                Text sub = UiKit.Label(bg.transform, "Sub", 2f, HandH - 24f, HandW - 4f, 22f, "", 13, TextAnchor.MiddleCenter, UiKit.TextDim);
                var btn = bg.gameObject.AddComponent<Button>();
                int iCopy = i;
                btn.onClick.AddListener(delegate { OnHandClicked(iCopy); });
                AttachRightClick(bg.gameObject, "Hand" + i);
                _handBg[i] = bg; _handTop[i] = top; _handMain[i] = main; _handSub[i] = sub; _handBtn[i] = btn;
            }

            // ── V0.7 特效层：盖在棋牌之上、右键详览之下；内部全是 raycastTarget=false，不吃点击 ──
            var fxGo = new GameObject("FxLayer", typeof(RectTransform));
            fxGo.transform.SetParent(_root, false);
            var fxRt = fxGo.GetComponent<RectTransform>();
            fxRt.anchorMin = Vector2.zero;
            fxRt.anchorMax = Vector2.one;
            fxRt.offsetMin = Vector2.zero;
            fxRt.offsetMax = Vector2.zero;
            _fx = fxGo.AddComponent<V06BattleFx>();
            _fx.Layer = fxRt;
            for (int i = 0; i < 12; i++)
            {
                _fx.Slots[i] = _slotBg[i] == null ? null : _slotBg[i].rectTransform;
                _fx.SlotArt[i] = _slotArt[i];
            }
            _fx.MineFlagFill = _mineFlagFill;
            _fx.EnemyFlagFill = _enemyFlagFill;
            _fx.MineFlagRect = _mineFlagFill == null ? null : _mineFlagFill.rectTransform;
            _fx.EnemyFlagRect = _enemyFlagFill == null ? null : _enemyFlagFill.rectTransform;

            // ── 右栏：行动按钮（加宽到 336，高度 44，手指/鼠标都好点）──
            UiKit.Label(_root, "ActTitle", 1240f, 200f, 336f, 22f, "行动", 16, TextAnchor.MiddleLeft, UiKit.Selected);
            _fuseBtn = UiKit.Button(_root, "BtnFuse", 1240f, 226f, 336f, 44f, "融合（1 行动）", UiKit.ButtonNormal, DoFuse);
            _powerBtn = UiKit.Button(_root, "BtnPower", 1240f, 274f, 336f, 44f, "强攻：关", UiKit.ButtonNormal, TogglePower);
            _swapBtn = UiKit.Button(_root, "BtnSwap", 1240f, 322f, 336f, 44f, "换位：关", UiKit.ButtonNormal, ToggleSwap);
            _passBtn = UiKit.Button(_root, "BtnPass", 1240f, 370f, 336f, 44f, "让过", UiKit.ButtonNormal, DoPass);
            _coverBtn = UiKit.Button(_root, "BtnCover", 1240f, 418f, 336f, 44f, "掩护（1 点）", UiKit.ButtonNormal, DoCover);
            _declineBtn = UiKit.Button(_root, "BtnDecline", 1240f, 466f, 336f, 44f, "放弃响应", UiKit.ButtonNormal, DoDecline);
            AttachActionTips();
            // 响应窗口的备选牌（对手正在攻击时出现，盖在同一列）
            _respondTitle = UiKit.Label(_root, "RespondTitle", 1240f, 522f, 336f, 22f,
                "对手正在攻击：选一张响应（每轮 1 张）", 13, TextAnchor.MiddleLeft, UiKit.Selected);
            _respondTitle.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                float ry = 546f + i * 48f;
                Button rb = UiKit.Button(_root, "Respond" + i, 1240f, ry, 336f, 44f, "", UiKit.ButtonNormal, null);
                int copy = i;
                rb.onClick.AddListener(delegate { OnRespondClicked(copy); });
                _respondBg[i] = rb.GetComponent<Image>();
                _respondText[i] = rb.GetComponentInChildren<Text>();
                rb.gameObject.SetActive(false);
            }
            // 关键规则常驻说明（缩短到一眼能读完）
            UiKit.Label(_root, "RulesTip", 1240f, 702f, 336f, 118f,
                "· 前排才能主动攻击；敌前排空才能打旗\n"
                + "· 强攻 攻击+1、掩护 受伤−1，每轮各 1 次\n"
                + "· 换位免费、每轮 1 次，不占主行动\n"
                + "· 第 8 轮起每轮末双方旗扣血 1/1/2/2/3\n"
                + "· 第 12 轮结束后比旗血，多者胜\n"
                + "· 右键任意卡面看完整说明与术语",
                12, TextAnchor.UpperLeft, UiKit.TextDim);
            // 底部：重开 / 返回
            _restartBtn = UiKit.Button(_root, "BtnRestart", 1240f, 828f, 162f, 42f, "重开一局", UiKit.ButtonNormal, RestartGame);
            UiKit.Button(_root, "BtnBackLobby", 1414f, 828f, 162f, 42f, "返回大厅", UiKit.MyCamp, BackToLobby);
            // 预备区：3 张晋阶 + 3 张支援（移到左栏，靠近手牌，每张每局一次）
            _reserveTitle = UiKit.Label(_root, "ReserveTitle", 24f, 396f, 276f, 22f,
                "预备 · 每张每局一次", 13, TextAnchor.MiddleLeft, UiKit.Selected);
            for (int i = 0; i < 6; i++)
            {
                float rx = 24f + (i % 3) * 92f;
                float ry = 422f + (i / 3) * 120f;
                Image bg = UiKit.Panel(_root, "Reserve" + i, rx, ry, 84f, 112f, UiKit.CardBg);
                bg.gameObject.AddComponent<RectMask2D>();
                var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(bg.transform, false);
                _reserveArt[i] = artGo.GetComponent<Image>();
                Image info = UiKit.Panel(bg.transform, "Info", 0f, 70f, 84f, 42f, new Color(0.04f, 0.05f, 0.07f, 0.84f));
                info.raycastTarget = false;
                Text main = UiKit.Label(bg.transform, "Main", 1f, 70f, 82f, 22f, "", 12, TextAnchor.MiddleCenter, UiKit.TextMain);
                Text sub = UiKit.Label(bg.transform, "Sub", 1f, 92f, 82f, 20f, "", 11, TextAnchor.MiddleCenter, UiKit.TextDim);
                var btn = bg.gameObject.AddComponent<Button>();
                int copy = i;
                btn.onClick.AddListener(delegate { OnReserveClicked(copy); });
                AttachRightClick(bg.gameObject, "Reserve" + i);
                _reserveBg[i] = bg; _reserveMain[i] = main; _reserveSub[i] = sub;
            }

            // ── 右键详览：全屏遮罩 + 居中大卡（默认隐藏）──
            var zoomGo = new GameObject("CardZoom", typeof(RectTransform), typeof(Image), typeof(Button));
            zoomGo.transform.SetParent(_root, false);
            var zoomRt = zoomGo.GetComponent<RectTransform>();
            zoomRt.anchorMin = Vector2.zero;
            zoomRt.anchorMax = Vector2.one;
            zoomRt.offsetMin = Vector2.zero;
            zoomRt.offsetMax = Vector2.zero;
            var zoomBg = zoomGo.GetComponent<Image>();
            zoomBg.color = new Color(0f, 0f, 0f, 0.72f);
            var zoomBtn = zoomGo.GetComponent<Button>();
            zoomBtn.transition = Selectable.Transition.None;
            zoomBtn.onClick.AddListener(HideZoom);
            _zoomLayer = zoomGo;

            // 大卡框 3:4
            const float ZoomW = 330f;
            const float ZoomH = 440f;
            Image zoomCard = UiKit.Panel(zoomGo.transform, "ZoomCard", (RefW - ZoomW) * 0.5f, (RefH - ZoomH) * 0.5f - 30f, ZoomW, ZoomH, new Color(0.10f, 0.11f, 0.14f, 0.98f));
            zoomCard.gameObject.AddComponent<RectMask2D>();
            var zoomArtGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            zoomArtGo.transform.SetParent(zoomCard.transform, false);
            _zoomArt = zoomArtGo.GetComponent<Image>();
            // 文字放在卡框右侧
            float infoX = (RefW + ZoomW) * 0.5f + 24f;
            _zoomTitle = UiKit.Label(zoomGo.transform, "ZoomTitle", infoX, 150f, RefW - infoX - 40f, 40f, "", 24, TextAnchor.UpperLeft, UiKit.Selected);
            _zoomBody = UiKit.Label(zoomGo.transform, "ZoomBody", infoX, 198f, RefW - infoX - 40f, 470f, "", 16, TextAnchor.UpperLeft, UiKit.TextMain);
            _zoomHint = UiKit.Label(zoomGo.transform, "ZoomHint", 40f, RefH - 60f, RefW - 80f, 30f, "右键任意卡面可放大查看　·　点击任意处关闭", 15, TextAnchor.MiddleCenter, UiKit.TextDim);
            // ── V0.7：GPT 交付的信息面板（只读展示）放在大卡右侧 ──
            var infoHost = new GameObject("CardInfoHost", typeof(RectTransform));
            infoHost.transform.SetParent(zoomGo.transform, false);
            var hostRt = infoHost.GetComponent<RectTransform>();
            hostRt.anchorMin = new Vector2(0f, 1f);
            hostRt.anchorMax = new Vector2(0f, 1f);
            hostRt.pivot = new Vector2(0f, 1f);
            hostRt.anchoredPosition = new Vector2(1000f, -180f);
            hostRt.sizeDelta = new Vector2(400f, 560f);
            _infoPanel = infoHost.AddComponent<V06CardInfoPanel>();
            _infoPanel.Build(hostRt);
            zoomGo.SetActive(false);

            // 结算浮层
            _resultPanel = new GameObject("Result", typeof(RectTransform), typeof(Image));
            _resultPanel.transform.SetParent(_root, false);
            var rrt = _resultPanel.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot = new Vector2(0f, 1f);
            rrt.anchoredPosition = new Vector2(420f, -430f);
            rrt.sizeDelta = new Vector2(720f, 150f);
            var rimg = _resultPanel.GetComponent<Image>();
            rimg.color = new Color(0.08f, 0.10f, 0.13f, 0.94f);
            _resultText = UiKit.Label(_resultPanel.transform, "Text", 16f, 10f, 688f, 130f, "", 20, TextAnchor.MiddleCenter, UiKit.TextMain);
            _resultPanel.SetActive(false);
        }

        /// <summary>脚本重载后自愈：字段被清空但界面树还在时，自动按上一局参数重开，避免界面冻住。</summary>
        private void TryRecoverAfterReload()
        {
            // 判断"之前真的在对局里"：状态行有文字 = 至少 Refresh 过一次（Text 内容活在场景里，
            // 重载不会清掉；而正式包刚启动时是大厅，状态行是空的）→ 这时才允许自己重开。
            bool wasInMatch = _everStarted || (_statusText != null && !string.IsNullOrEmpty(_statusText.text));
            if (!wasInMatch) { return; }
            bool treeOk = _root != null && _zoomLayer != null && _slotMain != null && _slotMain.Length > 0
                          && _slotMain[0] != null && _slotBg != null && _slotBg.Length > 0 && _slotBg[0] != null;
            if (!treeOk)
            {
                if (!_engineNullLogged)
                {
                    _engineNullLogged = true;
                    Debug.LogError("[V06诊断] 引擎为空且界面树不完整（instance=" + GetInstanceID() + "），请重新进入 Play 模式");
                }
                return;
            }
            if (_recoverTried >= 3) { return; }
            _recoverTried++;
            Debug.LogWarning("[V06诊断] 脚本重载清空了引擎，自动按上一局参数重开（第 " + _recoverTried + " 次）");
            NewGame(_lastSeed == 0 ? 20260101 : _lastSeed, _lastPreconA, _lastPreconB);
        }

        private void ApplyLayoutScale()
        {
            if (_layoutRoot == null) { return; }
            // 必须用 Canvas 的 rect（画布单位，已含 CanvasScaler 的缩放），
            // 不能用 Screen.width/height（像素）——否则会叠加两层缩放，
            // 界面被放大一倍、按钮跑到屏幕外（实机点击时表现为"射线未命中"）。
            var canvasRt = _layoutRoot.parent as RectTransform;
            if (canvasRt == null || canvasRt.rect.width <= 1f || canvasRt.rect.height <= 1f) { return; }
            float s = canvasRt.rect.height / RefH;
            if (s > 0f)
            {
                float widthAtThisScale = canvasRt.rect.width / s;
                if (widthAtThisScale < RefW) { s = Mathf.Min(canvasRt.rect.width / RefW, canvasRt.rect.height / RefH); }
            }
            if (s <= 0f) { s = 1f; }
            if (Mathf.Abs(_layoutRoot.localScale.x - s) >= 0.001f) { _layoutRoot.localScale = new Vector3(s, s, 1f); }
        }

        private void Update()
        {
            if (paused) { return; }
            ApplyLayoutScale();
            // ESC:取消当前选择(手牌/预备/放大层),不消耗任何资源
            if (Input.GetKeyDown(KeyCode.Escape) && _engine != null && !_engine.Finished)
            {
                if (_zoomLayer != null && _zoomLayer.activeSelf) { HideZoom(); }
                else if (_swapConfirmLayer != null) { CloseSwapConfirm(); _swapMode = false; _hint = "已取消换位。"; Refresh(); }
                else if (_selHand >= 0 || _selUid >= 0 || _selReserve >= 0 || _swapMode || _pendingTacticUid >= 0)
                {
                    ClearSelection();
                    _hint = "已取消选择。";
                    Refresh();
                }
            }
            if (_engine == null)
            {
                // 诊断 + 自愈：脚本重载会清空这个字段但把界面树留下，
                // 这时 Unity 不会重跑 Start，游戏就静默冻住（实机测试里遇到过两次）。
                TryRecoverAfterReload();
                return;
            }
            if (_engine.Finished)
            {
                _running = false;
                ShowResult();
                return;
            }
            // 对手回合：AI 每 0.35 秒走一步，让玩家看清发生了什么
            // 联机模式下对手是真人，由网络消息驱动，这里不能跑 AI
            if (_engine.Active != _playerCamp && !netMode)
            {
                _aiTimer += Time.deltaTime;
                if (_aiTimer >= 0.35f)
                {
                    _aiTimer = 0f;
                    bool fxOn = _fx != null && _fx.Ready;
                    if (fxOn) { _fx.Capture(_engine); }
                    bool ok = V06Ai.Step(_engine);
                    if (!ok)
                    {
                        _hint = "对手无法行动，自动让过";
                        V06Action pass = new V06Action();
                        pass.Kind = V06ActionKind.Pass;
                        string err;
                        _engine.Apply(pass, out err);
                    }
                    if (fxOn) { _fx.Diff(_engine); }
                    Refresh();
                }
            }
        }

        // ───────────────────────── 右键详览 ─────────────────────────

        /// <summary>右键回调入口（由 V06RightClick 组件转发）。</summary>
        public void OnCardRightClicked(string widgetName)
        {
            if (_zoomLayer == null || string.IsNullOrEmpty(widgetName)) { return; }
            if (_zoomLayer.activeSelf) { HideZoom(); return; }
            ShowZoom(widgetName);
        }

        /// <summary>给卡面挂右键探针。</summary>
        private void AttachRightClick(GameObject target, string widgetName)
        {
            if (target == null) { return; }
            V06RightClick rc = target.GetComponent<V06RightClick>();
            if (rc == null) { rc = target.AddComponent<V06RightClick>(); }
            rc.WidgetName = widgetName;
            rc.OnRightClick = OnCardRightClicked;
        }

        private string WidgetUnderMouse()
        {
            if (_raycaster == null || UnityEngine.EventSystems.EventSystem.current == null) { return null; }
            var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            ped.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            _raycaster.Raycast(ped, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                Transform tr = hits[i].gameObject.transform;
                while (tr != null)
                {
                    string n = tr.gameObject.name;
                    if (n.StartsWith("Hand") || n.StartsWith("Slot") || n.StartsWith("Reserve")) { return n; }
                    tr = tr.parent;
                }
            }
            return null;
        }

        private void HideZoom()
        {
            if (_zoomLayer != null) { _zoomLayer.SetActive(false); }
        }

        private void ShowZoom(string widgetName)
        {
            if (_engine == null || _zoomLayer == null) { return; }
            V06Player me = _engine.Players[_playerCamp];
            string cardId = null;
            V06Unit unit = null;
            string slotLabel = "";

            if (widgetName.StartsWith("Hand"))
            {
                int i;
                if (!int.TryParse(widgetName.Substring(4), out i) || i < 0 || i >= me.Hand.Count) { return; }
                cardId = me.Hand[i];
                slotLabel = "手牌第 " + (i + 1) + " 张";
            }
            else if (widgetName.StartsWith("Reserve"))
            {
                int i;
                if (!int.TryParse(widgetName.Substring(7), out i) || i < 0 || i >= me.Reserve.Count) { return; }
                cardId = me.Reserve[i];
                slotLabel = "预备区" + (me.ReserveUsed.Contains(cardId) ? "（本局已用）" : "（每张每局一次）");
            }
            else if (widgetName.StartsWith("Slot"))
            {
                int idx;
                if (!int.TryParse(widgetName.Substring(4), out idx)) { return; }
                int camp = idx / 6, slot = idx % 6;
                unit = _engine.Players[camp].At(slot);
                if (unit == null) { return; }
                cardId = unit.CardId;
                slotLabel = (camp == _playerCamp ? "我方" : "对手")
                    + (V06Slot.IsFront(slot) ? "前排" : "后排") + " 第 " + (slot % 3 + 1) + " 格";
            }
            if (cardId == null) { return; }
            V06Card c = V06Catalog.ById(cardId);
            if (c == null) { return; }

            int cost = c.Cost;
            V06Art.Apply(_zoomArt, cardId, cost, 330f, 440f, 0f);
            _zoomTitle.text = c.Name + "　" + (cost >= 0 ? cost + " 点" : "");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(slotLabel);
            sb.AppendLine();
            sb.AppendLine("类型：" + CategoryName(c.Category) + (string.IsNullOrEmpty(c.Tags) ? "" : "　标签：" + c.Tags));
            if (c.Category == V06Category.Unit || c.Category == V06Category.Token)
            {
                sb.AppendLine("攻生：基础 " + c.Atk + " / " + c.Hp + (unit != null ? ("　当前 " + unit.AtkBaseNow + " / " + unit.CurHp + "（已受伤害 " + unit.Damage + "）") : ""));
            }
            if (unit != null)
            {
                sb.AppendLine("位置：" + (V06Slot.IsFront(unit.Slot) ? "前排" : "后排")
                    + "　本轮可攻击：" + (_engine.CanAttackFrom(unit) ? "是" : "否")
                    + (unit.Upgraded ? "　已晋阶" : ""));
                if (!string.IsNullOrEmpty(unit.EquipId)) { sb.AppendLine("装备：" + CardNameOf(unit.EquipId)); }
                if (!string.IsNullOrEmpty(unit.SealId)) { sb.AppendLine("附印：" + CardNameOf(unit.SealId)); }
            }
            sb.AppendLine();
            sb.AppendLine("关键词：" + (c.Keywords == null || c.Keywords.Length == 0 ? "无" : string.Join("、", c.Keywords)));
            sb.AppendLine();
            sb.AppendLine("效果：");
            sb.AppendLine(c.Text);
            if (unit != null && unit.Keywords.Count > 0 && c.Keywords.Length != unit.Keywords.Count)
            {
                sb.AppendLine();
                sb.AppendLine("当前实际关键词：" + string.Join("、", unit.Keywords.ToArray()));
            }
            _zoomBody.text = sb.ToString();

            // ── V0.7 信息面板：把同一份数据喂给 GPT 的面板组件（只读） ──
            if (_infoPanel != null)
            {
                V06CardView view = new V06CardView();
                view.CardId = cardId;
                view.Name = c.Name;
                view.CategoryText = CategoryName(c.Category) + (string.IsNullOrEmpty(c.Tags) ? "" : "　" + c.Tags);
                view.Cost = c.Cost;
                view.Atk = c.Atk;
                view.Hp = c.Hp;
                view.CurrentAtk = unit != null ? unit.AtkBaseNow : c.Atk;
                view.CurrentHp = unit != null ? unit.CurHp : c.Hp;
                view.Damage = unit != null ? unit.Damage : 0;
                view.KeywordText = "关键词：" + (c.Keywords == null || c.Keywords.Length == 0
                    ? "无" : string.Join("、", c.Keywords));
                string effect = c.Text;
                if (unit != null && V06Expansion.GrowThreshold(unit.CardId) > 0)
                {
                    effect += "\n成长：" + unit.Growth + " / " + V06Expansion.GrowThreshold(unit.CardId);
                }
                var fuseHints = V06Expansion.FusionPartners(cardId);
                if (fuseHints.Count > 0)
                {
                    effect += "\n可融合：" + string.Join("、", fuseHints.ToArray());
                }
                view.EffectText = effect;
                view.TermText = V06Terms.Block(c);
                view.ArtPath = "";          // 大卡已在左侧显示立绘，面板不再重复
                view.CanPlayNow = CanUseThisCardNow(cardId, unit);
                _infoPanel.Show(view);
                if (_zoomTitle != null) { _zoomTitle.gameObject.SetActive(false); }
                if (_zoomBody != null) { _zoomBody.gameObject.SetActive(false); }
            }
            _zoomLayer.SetActive(true);
            // 第 2 批动画：打开详览时叠一层翻牌（末帧是无字卡面，所以用临时层，播完销毁，不动真实卡面）
            PlayOverlayOnce(_zoomArt, "anim_card_flip", 13f, 0.7f);
        }

        /// <summary>全屏叠一层一次性动画（胜利星屑 / 开场掷币 / 轮数跳动），播完销毁。</summary>
        private void PlayScreenOnce(string animName, float lifeSeconds)
        {
            if (!V06UiV07.HasAnim(animName)) { return; }
            // 同名全屏特效同一时刻只留一个：结算路径曾经每帧调一次，
            // 堆出上百张全屏 Image（各自跑协程）把渲染拖死，表现为"屏幕发白卡住"。
            if (_root != null)
            {
                Transform old = _root.Find("ScreenFx_" + animName);
                if (old != null) { Destroy(old.gameObject); }
            }
            var go = new GameObject("ScreenFx_" + animName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            V06UiV07.PlayOnce(img, animName, 14f);
            Destroy(go, lifeSeconds <= 0f ? 1f : lifeSeconds);
        }

        /// <summary>在一个 Image 上方叠一层一次性动画，播完自动销毁（不改动底图 sprite）。</summary>
        private void PlayOverlayOnce(Image host, string animName, float fps, float lifeSeconds)
        {
            if (host == null || !V06UiV07.HasAnim(animName)) { return; }
            var go = new GameObject("Fx_" + animName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            V06UiV07.PlayOnce(img, animName, fps);
            Destroy(go, lifeSeconds <= 0f ? 0.8f : lifeSeconds);
        }

        /// <summary>这张卡/这个单位当前能不能用（用于信息面板的"当前可用/不可用"）。</summary>
        private bool CanUseThisCardNow(string cardId, V06Unit unit)
        {
            if (_engine == null || _engine.Finished) { return false; }
            if (unit != null) { return _engine.CanAttackFrom(unit); }
            if (_engine.Active != _playerCamp) { return false; }
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.CardId == cardId && (a.Kind == V06ActionKind.Deploy || a.Kind == V06ActionKind.PlayCard)) { return true; }
            }
            return false;
        }

        private static string CardNameOf(string id)
        {
            V06Card c = V06Catalog.ById(id);
            return c == null ? id : c.Name;
        }

        private static string CategoryName(V06Category cat)
        {
            switch (cat)
            {
                case V06Category.Unit: return "单位";
                case V06Category.Token: return "衍生单位";
                case V06Category.Tactic: return "战术";
                case V06Category.Equipment: return "装备";
                case V06Category.Seal: return "附印";
                case V06Category.Status: return "战况";
                case V06Category.Response: return "响应";
                case V06Category.Upgrade: return "晋阶";
                case V06Category.Support: return "支援";
                case V06Category.Weather: return "环境";
                case V06Category.Encounter: return "遭遇";
                case V06Category.Event: return "事件";
            }
            return cat.ToString();
        }

        /// <summary>V0.7：在格子最上层放一张九宫格卡框（只画边框，中心镂空，不接收射线）。</summary>
        private static Image AddSlotFrame(Image cardBg)
        {
            LoadFrames();
            var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(cardBg.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = _frameNormal;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            img.pixelsPerUnitMultiplier = SlotW / 96f;
            img.raycastTarget = false;
            img.enabled = _frameNormal != null;
            return img;
        }

        private static void LoadFrames()
        {
            if (_frameNormal != null) { return; }
            _frameNormal = Resources.Load<Sprite>("Art/UI/V06/Frames/card_frame_normal");
            _frameSelected = Resources.Load<Sprite>("Art/UI/V06/Frames/card_frame_selected");
            _frameTarget = Resources.Load<Sprite>("Art/UI/V06/Frames/card_frame_target");
        }

        /// <summary>V0.7：按"选中 / 合法目标 / 普通"三态切换格子卡框。只改 sprite，不改颜色语言。</summary>
        private void RefreshSlotFrames()
        {
            if (_engine == null || _frameNormal == null) { return; }
            for (int camp = 0; camp < 2; camp++)
            {
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    int idx = Wig(camp) * 6 + s;
                    Image frame = _slotFrame[idx];
                    if (frame == null) { continue; }
                    V06Unit u = _engine.Players[camp].At(s);
                    Sprite want = _frameNormal;
                    if (u != null && camp == _playerCamp && u.Uid == _selUid) { want = _frameSelected; }
                    else if (u != null && (IsEnemyAttackTarget(u.Uid) || IsTacticTarget(u.Uid))) { want = _frameTarget; }
                    else if (u != null && _swapMode && _selUid >= 0 && camp == _playerCamp && u.Uid != _selUid
                        && FindSwapUnit(_selUid, u.Uid) != null) { want = _frameTarget; }
                    else if (u == null && IsDeployTarget(camp, s)) { want = _frameTarget; }
                    frame.sprite = want;
                    frame.enabled = want != null;
                }
            }
        }

        /// <summary>测试用：直接按控件名打开详览。</summary>
        public bool OpenZoomForTest(string widgetName)
        {
            if (_zoomLayer == null) { return false; }
            ShowZoom(widgetName);
            return _zoomLayer.activeSelf;
        }

        public string ZoomTitleForTest { get { return _zoomTitle == null ? "" : _zoomTitle.text; } }
        public string ZoomBodyForTest { get { return _zoomBody == null ? "" : _zoomBody.text; } }
        public bool IsZoomOpen { get { return _zoomLayer != null && _zoomLayer.activeSelf; } }
        /// <summary>测试用：关闭详览浮层。</summary>
        public void HideZoomForTest() { HideZoom(); }

        // ───────────────── V0.7 第 2 批动画播放器 ─────────────────

        /// <summary>在格子上叠一层一次性特效（进化环 / 融合环）。播完自己销毁。</summary>
        private void PlaySlotBurst(int idx, string animName)
        {
            if (idx < 0 || idx >= _slotBg.Length || _slotBg[idx] == null) { return; }
            if (!V06UiV07.HasAnim(animName)) { return; }
            var go = new GameObject("SlotBurst_" + animName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_slotBg[idx].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(SlotW + 30f, SlotH + 30f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            V06UiV07.PlayOnce(img, animName, 9f);
            Destroy(go, 1.1f);
        }

        // ───────────────── V0.7 第 4 批：卡面人物动作帧 ─────────────────

        /// <summary>进入对局的音乐：先进场曲，再交叉淡入对局主曲。</summary>
        private System.Collections.IEnumerator BattleMusicIntro()
        {
            V06Audio.Music(V06Audio.BattleIntroTrack);
            float t = 0f;
            while (t < V06Audio.BattleIntroSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            // 期间如果已经打完/回大厅，就别再切了
            if (_engine != null && !_engine.Finished) { V06Audio.Music(V06Audio.BattleTrack); }
        }

        /// <summary>把某张卡的人物动作（attack / hit / bloom）叠在格子上播一次。</summary>
        private void PlaySlotFrames(int idx, string cardId, string action)
        {
            if (idx < 0 || idx >= _slotBg.Length || _slotBg[idx] == null || string.IsNullOrEmpty(cardId)) { return; }
            Sprite[] frames = V06Art.CardFrames(cardId, action);
            if (frames == null)
            {
                // 没交动作帧的卡（目前还有相当一部分）→ 用代码补一个"反应"，
                // 让出手/受击在**所有 252 张卡**上都有反馈，不至于点了没感觉。
                PlaySlotReaction(idx, action);
                return;
            }
            var go = new GameObject("CardAct_" + action, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_slotBg[idx].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = false;
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            StartCoroutine(FramesRoutine(img, frames, action == "hit" ? 16f : 9f, action == "hit" ? 0.45f : 0.7f));
            Destroy(go, 0.9f);
        }

        /// <summary>没有动作帧时的兜底反应：出手＝前顶+提亮，受击＝后仰+泛红，绽放＝放大+金光。</summary>
        private void PlaySlotReaction(int idx, string action)
        {
            if (idx < 0 || idx >= _slotArt.Length || _slotArt[idx] == null) { return; }
            StartCoroutine(ReactionRoutine(_slotArt[idx], action));
        }

        private System.Collections.IEnumerator ReactionRoutine(Image art, string action)
        {
            if (art == null) { yield break; }
            RectTransform rt = art.rectTransform;
            Vector3 baseScale = rt.localScale;
            Color baseColor = art.color;
            float dur = action == "hit" ? 0.26f : 0.34f;
            float t = 0f;
            while (t < dur)
            {
                if (art == null) { yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float bump = Mathf.Sin(k * Mathf.PI);          // 0→1→0
                if (action == "hit")
                {
                    rt.localScale = new Vector3(baseScale.x * (1f - 0.05f * bump), baseScale.y * (1f - 0.05f * bump), 1f);
                    art.color = Color.Lerp(baseColor, new Color(1f, 0.55f, 0.55f, baseColor.a), bump * 0.85f);
                }
                else if (action == "bloom")
                {
                    rt.localScale = new Vector3(baseScale.x * (1f + 0.09f * bump), baseScale.y * (1f + 0.09f * bump), 1f);
                    art.color = Color.Lerp(baseColor, new Color(1f, 0.95f, 0.65f, baseColor.a), bump);
                }
                else
                {
                    rt.localScale = new Vector3(baseScale.x * (1f + 0.06f * bump), baseScale.y * (1f + 0.03f * bump), 1f);
                    art.color = Color.Lerp(baseColor, Color.white, bump * 0.6f);
                }
                yield return null;
            }
            if (art != null) { rt.localScale = baseScale; art.color = baseColor; }
        }

        private System.Collections.IEnumerator FramesRoutine(Image img, Sprite[] frames, float fps, float life)
        {
            if (frames == null || frames.Length == 0) { yield break; }
            float per = 1f / Mathf.Max(1f, fps);
            float t = 0f;
            int i = 0;
            while (t < life)
            {
                if (img == null) { yield break; }
                Sprite s = frames[Mathf.Clamp(i, 0, frames.Length - 1)];
                if (s != null) { img.sprite = s; }
                i++;
                float w = 0f;
                while (w < per) { w += Time.unscaledDeltaTime; yield return null; }
                t += per;
            }
        }

        /// <summary>旗条闪一下（九宫格横向拉伸，第 2 批九宫格 24/0/24/0）。</summary>
        private void PlayFlagFlash(float barY)
        {
            Sprite[] frames = V06UiV07.Anim("anim_flag_flash");
            if (frames == null) { return; }
            var go = new GameObject("FlagFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(1200f, 36f);
            rt.anchoredPosition = new Vector2(20f, -barY);
            var img = go.GetComponent<Image>();
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.pixelsPerUnitMultiplier = 1f;
            V06UiV07.PlayOnce(img, "anim_flag_flash", 10f);
            Destroy(go, 0.9f);
        }

        /// <summary>回合横幅（我方 / 对手回合开始各弹一次）。
        /// 以前只播一张彩带素材、上面没有字，玩家看到的是一条"不知道干什么的蓝色条带"，
        /// 现在把「轮到我方／对手行动 + 第 N 轮」直接压在横幅上；素材缺失时退化成深色字牌。</summary>
        private void PlayTurnBanner(bool mine)
        {
            if (_turnBannerImg == null)
            {
                _turnBannerImg = UiKit.Panel(_root, "TurnBanner", 544f, 372f, 512f, 102f, Color.white);
                _turnBannerImg.raycastTarget = false;
                _turnBannerImg.preserveAspect = false;
                _turnBannerText = UiKit.Label(_turnBannerImg.transform, "T", 0f, 24f, 512f, 54f, "", 28,
                    TextAnchor.MiddleCenter, Color.white);
                var shT = _turnBannerText.gameObject.AddComponent<Shadow>();
                shT.effectColor = new Color(0f, 0f, 0f, 0.95f);
                shT.effectDistance = new Vector2(2f, -2f);
            }
            bool hasArt = V06UiV07.Anim("anim_turn_banner") != null;
            if (hasArt)
            {
                _turnBannerImg.color = Color.white;
                V06UiV07.PlayOnce(_turnBannerImg, "anim_turn_banner", 12f);
            }
            else
            {
                _turnBannerImg.sprite = null;
                _turnBannerImg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);   // 无素材 → 深色字牌
            }
            if (_turnBannerText != null)
            {
                _turnBannerText.text = (mine ? "轮到我方" : "对手行动")
                    + (_engine != null ? "　第 " + _engine.Round + " 轮" : "");
                _turnBannerText.color = mine ? new Color(1f, 0.94f, 0.70f) : new Color(0.82f, 0.88f, 1f);
            }
            _turnBannerImg.gameObject.SetActive(true);
            if (_turnBannerImg.transform.childCount > 0) { _turnBannerImg.transform.GetChild(0).gameObject.SetActive(true); }
            StartCoroutine(HideAfter(_turnBannerImg.gameObject, 1.1f));
        }

        /// <summary>
        /// 战况（场地）牌生效时换战斗背景：我方战况优先，其次是外观战。
        /// 专用背景按约定放在 Assets/Resources/Art/battle_bg_f01.png … battle_bg_f06.png；
        /// 找不到就退回默认背景，只在顶部标出场地名（不留白板、也不假装换过图）。
        /// </summary>
        private void RefreshBattlefieldVisual()
        {
            if (_engine == null) { return; }
            int camp = -1;
            string fid = _engine.Players[_playerCamp].BattlefieldId;
            if (!string.IsNullOrEmpty(fid)) { camp = _playerCamp; }
            else
            {
                string foeFid = _engine.Players[1 - _playerCamp].BattlefieldId;
                if (!string.IsNullOrEmpty(foeFid)) { fid = foeFid; camp = 1 - _playerCamp; }
            }

            string want = camp < 0 ? "" : fid;
            if (want == _fieldApplied) { return; }          // 只在场地变化时换图
            _fieldApplied = want;

            if (camp < 0)
            {
                if (_battleBg != null && _battleBgDefault != null) { _battleBg.sprite = _battleBgDefault; }
                if (_battleBg != null) { _battleBg.color = new Color(0.72f, 0.76f, 0.84f, 1f); }
                if (_fieldLabel != null) { _fieldLabel.text = ""; }
                return;
            }

            V06Card fc = V06Catalog.ById(fid);
            string fname = fc != null ? fc.Name : fid;
            Sprite field = Resources.Load<Sprite>("Art/battle_bg_" + fid.ToLowerInvariant());
            if (_battleBg != null)
            {
                _battleBg.sprite = field != null ? field : _battleBgDefault;
                _battleBg.color = camp == _playerCamp
                    ? new Color(0.78f, 0.82f, 0.88f, 1f)          // 我方场地：偏亮
                    : new Color(0.86f, 0.72f, 0.74f, 1f);          // 对手场地：偏暖红，避免混淆
            }
            if (_fieldLabel != null)
            {
                _fieldLabel.text = "场地：" + fname + "（" + (camp == _playerCamp ? "我方" : "对手") + "）"
                    + (field == null ? "　· 专用背景待补" : "");
                _fieldLabel.color = camp == _playerCamp ? UiKit.Selected : new Color(1f, 0.72f, 0.68f);
            }
        }

        private System.Collections.IEnumerator HideAfter(GameObject go, float seconds)        {
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            if (go != null) { go.SetActive(false); }
        }

        /// <summary>新摸到的牌做一次"发牌"动效（手牌数量增加时触发）。</summary>
        private void PlayDealOnNewCards(int fromIndex, int count)
        {
            if (!V06UiV07.HasAnim("anim_card_deal")) { return; }
            for (int i = fromIndex; i < fromIndex + count && i < _handBg.Length; i++)
            {
                if (_handBg[i] == null) { continue; }
                Image img = _handBg[i];
                V06UiV07.Pop(img.rectTransform, 0.22f);
            }
            // 只在手牌最右一张叠一层动画帧，避免同帧建多个控件
            int idx = Mathf.Clamp(fromIndex + count - 1, 0, _handBg.Length - 1);
            if (_handBg[idx] == null) { return; }
            var go = new GameObject("DealFx", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_handBg[idx].transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var im = go.GetComponent<Image>();
            im.raycastTarget = false;
            V06UiV07.PlayOnce(im, "anim_card_deal", 14f);
            Destroy(go, 0.8f);
        }

        // ───────────────────────── 开局 ─────────────────────────

        public void NewGame(int seed, int preconA, int preconB)
        {
            _engine = new V06Engine();
            _engineNullLogged = false;
            _lastSeed = seed; _lastPreconA = preconA; _lastPreconB = preconB;
            Debug.Log("[V06诊断] NewGame instance=" + GetInstanceID() + " seed=" + seed + " 预组 " + preconA + "/" + preconB + " 帧 " + Time.frameCount);
            string err;
            if (!_engine.TryStart(seed, preconA, preconB, out err))
            {
                _hint = "开局失败：" + err;
                Debug.LogError("V06 开局失败：" + err);
                return;
            }
            V06Ai.Seed(seed + 17);
            _running = true;
            _everStarted = true;          // 之后才允许"重载自愈"自己重开
            // 对战音乐：进入对局先播进场曲（Snow Queen），几秒后交叉淡入对局主曲（Path of the Goblin King）
            StartCoroutine(BattleMusicIntro());
            _selHand = -1;
            _selUid = -1;
            _powerStrike = false;
            _swapMode = false;
            // 动画变更检测状态重置（否则跨局会误触发/漏触发）
            _lastHandCount = -1;
            _lastMineFlag = -1;
            _lastEnemyFlag = -1;
            _lastActiveCamp = -99;
            _lastRound = -1;
            _fieldApplied = "";             // 新一局要能重新应用场地背景
            _growthSeen.Clear();
            _fusedSeen.Clear();
            if (_turnBannerImg != null) { _turnBannerImg.gameObject.SetActive(false); }
            if (_pressureFx != null) { _pressureFx.gameObject.SetActive(false); }
            V06Precon pa = V06DeckLibrary.Get(preconA);
            V06Precon pb = V06DeckLibrary.Get(preconB);
            _hint = "新对局开始。你是 " + (pa == null ? "?" : pa.Name) + "，对手是 " + (pb == null ? "?" : pb.Name) + "。";
            if (_resultPanel != null) { _resultPanel.SetActive(false); }
            if (_banner != null) { _banner.gameObject.SetActive(false); }
            _resultShown = false;          // 新一局要能重新演出结算（否则第二次输赢不刷新文本）
            CloseSwapConfirm();
            PlayScreenOnce("anim_coin_toss", 1.0f);   // 第 3 批：开局掷币定先手
            Refresh();
        }

        private void RestartGame()
        {
            NewGame(20260101 + UnityEngine.Random.Range(1, 9999), 0, 1);
        }

        // ───────────────────────── 刷新 ─────────────────────────

        /// <summary>
        /// 按当前局面算出"下一步该点哪里"，永远返回非空字符串。
        /// 优先级：结算 &gt; 响应窗口 &gt; 对手回合 &gt; 我有选中 &gt; 我这回合能做什么。
        /// 判断一律读 `_actions`（引擎刚枚举出来的合法动作），不自己猜规则，
        /// 这样"提示"和"实际能点"永远一致。
        /// </summary>
        private string BuildGuidance()
        {
            if (_engine == null) { return "从大厅开始一局对战。"; }
            if (_engine.Finished) { return "对局结束：看结算面板，或点右栏『返回大厅』。"; }

            V06Player me = _engine.Players[_playerCamp];
            bool myTurn = _engine.Active == _playerCamp;
            bool pending = _engine.PendingAttack != null;

            // ① 响应窗口（对手攻击，等我回应）
            if (pending && myTurn)
            {
                bool canCover = HasActionOfKind(V06ActionKind.Cover);
                if (_respondActions.Count > 0)
                {
                    return "对手攻击中：点『响应』牌" + (canCover ? " 或『掩护』" : "") + "，也可『放弃响应』。";
                }
                return "对手攻击中，你没有可用响应" + (canCover ? "（可『掩护』减 1）" : "")
                    + "：点右下『放弃响应』继续。";
            }
            // ② 我已宣告攻击，等对手回应
            if (pending && !myTurn) { return "对手正在响应这次攻击…"; }
            // ③ 对手回合
            if (!myTurn) { return "对手行动中…（右键任意卡面看说明，ESC 取消选择）"; }

            int actsLeft = V06Engine.MainActionsPerRound - me.MainActionsUsed;

            // ④ 我方回合：已有选中 → 告诉它下一步点哪
            if (_selHand >= 0 && _selHand < me.Hand.Count)
            {
                V06Card c = V06Catalog.ById(me.Hand[_selHand]);
                string nm = c == null ? "这张牌" : "【" + c.Name + "】";
                if (_pendingTacticUid >= 0)
                {
                    V06Unit src = me.ByUid(_pendingTacticUid);
                    return "已选【" + (src != null ? src.Name : "?") + "】：点它对面一排的高亮空格作为落点；ESC 取消。";
                }
                if (HasActionOfKind(V06ActionKind.Deploy)) { return "点高亮格位部署" + nm + "；ESC 取消。"; }
                if (HasActionOfKind(V06ActionKind.PlayCard)) { return "点高亮的合法目标使用" + nm + "；ESC 取消。"; }
                return nm + "现在用不了（费用 " + me.ActionPoints + " 点／没有合法目标）；ESC 取消。";
            }
            if (_selReserve >= 0 && _selReserve < me.Reserve.Count)
            {
                V06Card rc = V06Catalog.ById(me.Reserve[_selReserve]);
                return "点高亮的己方单位调用预备【" + (rc != null ? rc.Name : "?") + "】；ESC 取消。";
            }
            if (_selUid >= 0)
            {
                if (_swapMode && HasActionOfKind(V06ActionKind.Swap))
                {
                    return "换位模式：点另一排的高亮空格或己方单位作为换位目标（会先弹确认框）；ESC 取消。";
                }
                if (HasActionOfKind(V06ActionKind.Attack)) { return "点高亮的敌方目标发动攻击；要与后排单位换位，先点『换位』；ESC 取消。"; }
                if (HasActionOfKind(V06ActionKind.Swap)) { return "这只本轮不能攻击：点『换位』后再点另一排的空格／单位换位；ESC 取消。"; }
                return "这只本轮无事可做（已行动／被护卫／射程不足）；ESC 取消。";
            }

            // ⑤ 我方回合：没选中 → 按"现在能做什么"给菜单
            bool canDeploy = HasActionOfKind(V06ActionKind.Deploy);
            bool canPlay = HasActionOfKind(V06ActionKind.PlayCard);
            bool canAttack = HasActionOfKind(V06ActionKind.Attack);
            bool canFlag = CanHitFlagNow();
            bool canSwap = HasActionOfKind(V06ActionKind.Swap);

            var tips = new List<string>();
            if (canDeploy) { tips.Add("选中手牌部署"); }
            if (canPlay) { tips.Add("使用战术／装备牌"); }
            if (canAttack) { tips.Add("点自己单位攻击"); }
            if (canFlag) { tips.Add("点敌方旗条直击"); }
            if (tips.Count == 0 && canSwap) { tips.Add("点单位后可换位"); }

            if (tips.Count == 0)
            {
                if (actsLeft <= 0) { return "本轮主行动已用完：点右侧『让过』结束回合。"; }
                if (me.ActionPoints <= 0) { return "本轮费用已用完，能做的都做完了：点『让过』结束回合。"; }
                return "现在没有能执行的操作：点『让过』结束回合。";
            }
            return "轮到你了（剩 " + actsLeft + " 次主行动 · 费用 " + me.ActionPoints + "）："
                + string.Join("、", tips.ToArray()) + "。";
        }

        private void Refresh()
        {
            if (_engine == null) { return; }
            _actions = _engine.LegalActions();
            bool myTurn = _engine.Active == _playerCamp && !_engine.Finished;

            for (int camp = 0; camp < 2; camp++)
            {
                V06Player p = _engine.Players[camp];
                for (int s = 0; s < 6; s++)
                {
                    int idx = Wig(camp) * 6 + s;
                    V06Unit u = p.At(s);
                    if (u == null)
                    {
                        _slotMain[idx].text = V06Slot.IsFront(s) ? "前排空位" : "后排空位";
                        if (_slotArt[idx] != null) { _slotArt[idx].enabled = false; }
                        _slotSub[idx].text = IsDeployTarget(camp, s) ? "点击部署" : "";
                        _slotSub[idx].color = IsDeployTarget(camp, s) ? UiKit.Selected : UiKit.TextDim;
                        if (_slotKw[idx] != null) { _slotKw[idx].text = ""; }
                        _slotBg[idx].color = IsDeployTarget(camp, s)
                            ? new Color(0.20f, 0.30f, 0.24f) : new Color(0.12f, 0.13f, 0.16f, 0.75f);
                        continue;
                    }
                    // 名称单独一行；攻/血放到数值条上，受伤时数字变红
                    _slotMain[idx].text = u.Name;
                    if (_slotArt[idx] != null) { V06Art.Apply(_slotArt[idx], u.CardId, CostOf(u.CardId), SlotW, SlotH, 0f); }
                    _slotSub[idx].text = u.AtkBaseNow + " / " + u.CurHp;
                    _slotSub[idx].color = u.Damage > 0 ? new Color(1f, 0.62f, 0.55f) : UiKit.TextMain;
                    if (_slotKw[idx] != null) { _slotKw[idx].text = KwLine(u); }
                    Color baseColor = camp == _playerCamp ? UiKit.MyUnit : UiKit.EnemyUnit;
                    if (_selUid == u.Uid && camp == _playerCamp) { baseColor = new Color(0.30f, 0.34f, 0.16f); }
                    if (IsEnemyAttackTarget(u.Uid)) { baseColor = new Color(0.16f, 0.34f, 0.30f); }
                    if (IsTacticTarget(u.Uid)) { baseColor = new Color(0.20f, 0.26f, 0.40f); }
                    // 换位模式下的己方目标（另一排的单位）也要亮出来，否则玩家不知道能点谁
                    if (_swapMode && _selUid >= 0 && camp == _playerCamp && u.Uid != _selUid
                        && FindSwapUnit(_selUid, u.Uid) != null) { baseColor = new Color(0.26f, 0.30f, 0.44f); }
                    if (u.AttackedThisRound) { baseColor *= 0.7f; }
                    _slotBg[idx].color = baseColor;

                    // 第 2 批动画：成长进阶 → 进化环；本局刚融合 → 融合环（按 uid 记忆，只播一次）
                    int seenGrowth;
                    if (!_growthSeen.TryGetValue(u.Uid, out seenGrowth)) { _growthSeen[u.Uid] = u.Growth; }
                    else if (u.Growth > seenGrowth)
                    {
                        _growthSeen[u.Uid] = u.Growth;
                        PlaySlotBurst(idx, "anim_slot_ring_evolve");
                        PlaySlotBurst(idx, "anim_evolve_burst");   // 第 3 批：进化向上爆发光柱
                        PlaySlotFrames(idx, u.CardId, "bloom");    // 第 4 批：卡面人物绽放帧
                    }
                    if (!string.IsNullOrEmpty(u.FusedFrom))
                    {
                        if (_fusedSeen.Add(u.Uid))
                        {
                            PlaySlotBurst(idx, "anim_slot_ring_fuse");
                            PlaySlotFrames(idx, u.CardId, "bloom");
                        }
                    }
                }
            }

            // 手牌
            V06Player me = _engine.Players[_playerCamp];
            // 第 2 批动画：新摸到的牌做一次发牌动效
            if (_lastHandCount >= 0 && me.Hand.Count > _lastHandCount)
            {
                PlayDealOnNewCards(_lastHandCount, me.Hand.Count - _lastHandCount);
            }
            _lastHandCount = me.Hand.Count;
            RefreshSlotFrames();            for (int i = 0; i < _handBg.Length; i++)
            {
                bool has = i < me.Hand.Count;
                if (!has)
                {
                    _handTop[i].text = ""; _handMain[i].text = ""; _handSub[i].text = ""; _handBg[i].color = UiKit.CardBg;
                    if (_handArt[i] != null) { _handArt[i].enabled = false; }
                    continue;
                }
                V06Card c = V06Catalog.ById(me.Hand[i]);
                if (c == null) { continue; }
                // 费用只留数字（放在左上角徽记里），省下的宽度给名称与数值
                _handTop[i].text = c.Cost >= 0 ? c.Cost.ToString() : "—";
                _handBg[i].rectTransform.sizeDelta = new Vector2(HandW, HandH);
                // 选中的手牌上抬 8px，作为"已选中"的第二个信号（不只靠边框颜色）
                float hx = HandX0 + i * HandStep;
                _handBg[i].rectTransform.anchoredPosition = new Vector2(hx, -(HandY - (i == _selHand ? HandLiftY : 0f)));
                if (_handArt[i] != null) { V06Art.Apply(_handArt[i], c.Id, c.Cost, HandW, HandH, 0f); }
                _handMain[i].text = c.Name;
                string sub = HandSubLine(c);
                if (c.Cost > me.ActionPoints) { sub += "　缺费" + (c.Cost - me.ActionPoints); }
                else if (!_engine.MainActionsLeft(_playerCamp)) { sub += "　行动已用完"; }
                _handSub[i].text = sub;
                Color col = UiKit.CardBg;
                if (i == _selHand) { col = new Color(0.30f, 0.32f, 0.14f); }
                else if (CanUseNow(c)) { col = new Color(0.15f, 0.26f, 0.20f); }
                else { col = new Color(0.16f, 0.16f, 0.18f); }
                _handBg[i].color = col;
                _handSub[i].color = (c.Cost > me.ActionPoints || !_engine.MainActionsLeft(_playerCamp))
                    ? new Color(1f, 0.62f, 0.55f) : UiKit.TextDim;
            }

            // 响应窗口：优先显示响应面板，预备区暂时隐藏（对手回合本来也不能调用预备）
            _respondActions.Clear();
            bool inResponse = _engine.PendingAttack != null && _engine.Active == _playerCamp;
            if (_engine.PendingAttack != null && _engine.Active != _playerCamp) { _hint = "对手正在响应这次攻击…"; }
            if (inResponse)
            {
                for (int i = 0; i < _actions.Count; i++)
                {
                    if (_actions[i].Kind == V06ActionKind.Respond) { _respondActions.Add(_actions[i]); }
                }
            }
            if (_respondTitle != null)
            {
                _respondTitle.gameObject.SetActive(inResponse);
                // 没有响应牌时必须说明白，否则玩家看到的只是"下面三个按钮没了"，
                // 加上手牌/格位都点不动，就会以为整回合卡死（实机反馈过）。
                if (inResponse)
                {
                    bool canCover = HasActionOfKind(V06ActionKind.Cover);
                    _respondTitle.text = _respondActions.Count > 0
                        ? ("对手攻击中：选一张响应（每轮 1 张）" + (canCover ? "，或点『掩护』" : ""))
                        : ("对手攻击中，你没有可用的响应牌" + (canCover ? "（可点『掩护』减 1）" : "")
                           + "：点下方『放弃响应』继续");
                }
                if (_reserveTitle != null) { _reserveTitle.gameObject.SetActive(!inResponse); }
                for (int i = 0; i < 3; i++)
                {
                    bool show = inResponse && i < _respondActions.Count;
                    _respondBg[i].gameObject.SetActive(show);
                    if (!show) { continue; }
                    V06Card rc = V06Catalog.ById(_respondActions[i].CardId);
                    _respondText[i].text = (rc == null ? _respondActions[i].CardId : rc.Name)
                        + (rc != null && rc.Cost >= 0 ? "（" + rc.Cost + " 点）" : "");
                }
                for (int i = 0; i < 6; i++)
                {
                    if (_reserveBg[i] != null) { _reserveBg[i].gameObject.SetActive(!inResponse); }
                }
            }

            // 预备区
            for (int i = 0; i < 6; i++)
            {
                if (i < me.Reserve.Count)
                {
                    V06Card rc = V06Catalog.ById(me.Reserve[i]);
                    if (rc == null) { _reserveMain[i].text = "?"; continue; }
                    bool used = me.ReserveUsed.Contains(rc.Id);
                    _reserveMain[i].text = rc.Name;
                    _reserveSub[i].text = (rc.Cost >= 0 ? rc.Cost + " 点 " : "") + (used ? "（已用）" : (CanUseNow(rc) ? "可调用" : "条件不足"));
                    if (_reserveArt[i] != null) { V06Art.Apply(_reserveArt[i], rc.Id, rc.Cost, 84f, 112f, 0f); }
                    _reserveBg[i].color = used ? new Color(0.13f, 0.13f, 0.14f)
                        : (i == _selReserve ? new Color(0.30f, 0.32f, 0.14f)
                        : (CanUseNow(rc) ? new Color(0.14f, 0.22f, 0.26f) : UiKit.CardBg));
                }
                else
                {
                    _reserveMain[i].text = "";
                    _reserveSub[i].text = "";
                    if (_reserveArt[i] != null) { _reserveArt[i].enabled = false; }
                    _reserveBg[i].color = UiKit.CardBg;
                }
            }

            // 旗
            int f0 = _engine.Players[_playerCamp].FlagHp;
            int f1 = _engine.Players[1 - _playerCamp].FlagHp;
            _mineFlagText.text = "我方远征旗  " + f0 + " / " + V06Engine.FlagMaxHp
                + "　（牌库 " + me.Deck.Count + "，弃牌 " + me.Discard.Count + "，手牌 " + me.Hand.Count + "）";
            _enemyFlagText.text = "对手远征旗  " + f1 + " / " + V06Engine.FlagMaxHp
                + (CanHitFlagNow() ? "　▼ 可直击（点这里）" : "");
            _mineFlagFill.rectTransform.sizeDelta = new Vector2(1200f * Mathf.Clamp01(f0 / (float)V06Engine.FlagMaxHp), 36f);
            _enemyFlagFill.rectTransform.sizeDelta = new Vector2(1200f * Mathf.Clamp01(f1 / (float)V06Engine.FlagMaxHp), 36f);
            if (CanHitFlagNow()) { _enemyFlagFill.color = new Color(0.85f, 0.55f, 0.20f); }
            else { _enemyFlagFill.color = UiKit.EnemyCamp; }

            // 第 2 批动画：旗血量变化 → 旗条闪；行动方切换 → 回合横幅
            if (_lastMineFlag >= 0 && f0 != _lastMineFlag) { PlayFlagFlash(MineFlagY); PlayScreenOnce("anim_flag_break", 0.7f); }
            if (_lastEnemyFlag >= 0 && f1 != _lastEnemyFlag) { PlayFlagFlash(EnemyFlagY); }
            _lastMineFlag = f0;
            _lastEnemyFlag = f1;
            // 第 3 批：轮数变化 → 计时器跳动
            if (_lastRound >= 0 && _engine.Round != _lastRound && !_engine.Finished) { PlayScreenOnce("anim_round_tick", 0.6f); V06Audio.Play("R39"); }
            _lastRound = _engine.Round;
            // 第 3 批：进入压力阶段 → 四周红雾（循环，随压力加深）
            if (_engine.Round >= V06Engine.PressureStartRound && !_engine.Finished)
            {
                if (_pressureFx == null)
                {
                    _pressureFx = UiKit.Panel(_root, "PressureFx", 0f, 0f, RefW, RefH, Color.white);
                    _pressureFx.raycastTarget = false;
                    if (V06UiV07.HasAnim("anim_pressure_warning"))
                    {
                        V06UiV07.PlayAnim(_pressureFx, "anim_pressure_warning", 3f, true);
                    }
                    else { _pressureFx.gameObject.SetActive(false); }
                }
                float p = Mathf.Clamp01((_engine.Round - V06Engine.PressureStartRound + 1) / 4f);
                _pressureFx.color = new Color(1f, 1f, 1f, 0.35f + 0.45f * p);
            }
            else if (_pressureFx != null && _pressureFx.gameObject.activeSelf) { _pressureFx.gameObject.SetActive(false); }
            if (_lastActiveCamp != -99 && _lastActiveCamp != _engine.Active && !_engine.Finished)
            {
                PlayTurnBanner(_engine.Active == _playerCamp);
            }
            _lastActiveCamp = _engine.Active;

            // 场地（战况牌）：整块换背景 + 顶部标出场地名，让玩家知道场上生效的是什么
            RefreshBattlefieldVisual();

            // 状态行（精简版：只留当下要看的四项，避免过长被截断）
            V06Player foe = _engine.Players[1 - _playerCamp];
            string turnText = _engine.Finished ? "对局结束" : (myTurn ? "▶ 你的行动" : "对手思考中…");
            int pressure = _engine.Round >= V06Engine.PressureStartRound
                ? (_engine.Round - V06Engine.PressureStartRound + 1) : 0;
            _statusText.text = turnText
                + "　第 " + _engine.Round + "/" + V06Engine.MaxRounds + " 轮"
                + "　│　费用 " + me.ActionPoints + " · 主行动 " + (V06Engine.MainActionsPerRound - me.MainActionsUsed) + "/" + V06Engine.MainActionsPerRound
                + "　│　对手 费用 " + foe.ActionPoints + " · 行动 " + (V06Engine.MainActionsPerRound - foe.MainActionsUsed) + "/" + V06Engine.MainActionsPerRound
                + (pressure > 0 ? "　│　轮末压力 " + pressure : "");
            _statusText.color = _engine.Finished ? UiKit.TextMain : (myTurn ? UiKit.TextMain : UiKit.TextDim);

            // 提示行：一次性消息优先（3 秒内），否则显示按当前局面算出的"下一步该点哪里"
            // 打旗那一击结算完之后，明确告诉玩家"汲取为什么不触发"（这行只出现一次）
            if (_drainFlagNote && _engine.PendingAttack == null)
            {
                _drainFlagNote = false;
                _hint = "攻击远征旗不触发汲取：汲取只在打敌单位造成伤害时生效。";
            }
            bool msgFresh = !string.IsNullOrEmpty(_hintMsg) && (Time.unscaledTime - _hintMsgAt) < HintMsgLife;
            _hintText.text = (msgFresh ? _hintMsg : BuildGuidance())
                + (_lastError != null ? "　［" + _lastError + "］" : "");

            // 详情：选中的牌/单位
            _detailText.text = DetailLine();
            if (_detailArt != null)
            {
                string selId = null;
                int selCost = 2;
                if (_selHand >= 0 && _selHand < me.Hand.Count) { selId = me.Hand[_selHand]; selCost = CostOf(selId); }
                else if (_selUid >= 0)
                {
                    V06Unit selU = me.ByUid(_selUid);
                    if (selU != null) { selId = selU.CardId; selCost = CostOf(selId); }
                }
                if (selId == null) { _detailArt.enabled = false; }
                else { V06Art.Apply(_detailArt, selId, selCost, 180f, 240f, 0f); }
            }

            // 日志尾巴（左栏放得下 8 条）
            var sb = new System.Text.StringBuilder();
            int from = Mathf.Max(0, _engine.Log.Count - 8);
            for (int i = from; i < _engine.Log.Count; i++) { sb.AppendLine(_engine.Log[i]); }
            _logText.text = sb.ToString();

            // 按钮可用性
            bool resp = _engine.PendingAttack != null && _engine.Active == _playerCamp;
            // 响应窗口里"，强攻/换位/融合/让过"其实都提交不了（引擎会退回"响应窗口只能响应/掩护/放弃"），
            // 以前它们仍显示为可点，玩家点了没反应 → 以为整回合卡死。这里统一禁用，只留响应／掩护／放弃。
            bool normalTurn = myTurn && !resp;
            _powerBtn.GetComponentInChildren<Text>().text = _powerStrike ? "强攻：开" : "强攻：关";
            _swapBtn.GetComponentInChildren<Text>().text = _swapMode ? "换位：开" : "换位：关";
            _powerBtn.interactable = normalTurn && HasActionOfKind(V06ActionKind.Attack);
            _swapBtn.interactable = normalTurn && HasActionOfKind(V06ActionKind.Swap);
            if (_fuseBtn != null) { _fuseBtn.interactable = normalTurn && HasActionOfKind(V06ActionKind.Fuse); }
            _passBtn.interactable = normalTurn;
            _coverBtn.interactable = resp && HasActionOfKind(V06ActionKind.Cover);
            _declineBtn.interactable = resp && HasActionOfKind(V06ActionKind.Decline);

            // 响应窗口：把"该点哪里"直接写在提示行上，并把『放弃响应』点亮（唯一必能继续的出口）
            if (resp)
            {
                bool canCover = HasActionOfKind(V06ActionKind.Cover);
                _hintText.text = _respondActions.Count > 0
                    ? ("对手攻击中：点『响应』牌" + (canCover ? " 或『掩护』" : "") + "，也可以『放弃响应』")
                    : ("对手攻击中，你没有可用响应" + (canCover ? "（可『掩护』减 1）" : "") + "：点右下角『放弃响应』继续");
                _hintText.color = new Color(1f, 0.86f, 0.45f);
                _declineBtn.GetComponent<Image>().color = new Color(0.46f, 0.36f, 0.16f);
            }
            else
            {
                _hintText.color = UiKit.Selected;
                _declineBtn.GetComponent<Image>().color = UiKit.ButtonNormal;
            }
        }

        private static int CostOf(string cardId)
        {
            V06Card c = V06Catalog.ById(cardId);
            return c == null ? 2 : c.Cost;
        }

        private string KwLine(V06Unit u)
        {
            int need = V06Expansion.GrowThreshold(u.CardId);
            string growth = need > 0 ? " 成" + u.Growth + "/" + need : "";
            if (u.Keywords.Count == 0)
            {
                string bare = (u.TagText != null ? u.TagText : "") + growth;
                return bare;
            }
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < u.Keywords.Count; i++)
            {
                if (i > 0) { sb.Append("·"); }
                sb.Append(u.Keywords[i]);
            }
            sb.Append(growth);
            if (u.Damage > 0) { sb.Append("（伤 " + u.Damage + "）"); }
            return sb.ToString();
        }

        /// <summary>
        /// 右侧行动按钮的悬停说明。文案一律按引擎实现写，改规则要同步改这里：
        /// 融合占 1 次主行动且不花费用点；强攻／掩护各花 1 点费用点；换位免费且不占总行动；
        /// 让过在"双方都让过或都用完主行动"时进入轮末；行动点最多结转 2 点。
        /// </summary>
        private void AttachActionTips()
        {
            if (_fuseBtn != null)
            {
                V06HoverTip.Attach(_fuseBtn.gameObject, "融合",
                    "两只可融合的己方单位合成一体：占 1 次主行动、不花费用点；新形态额外 +1/+1，当轮即可攻击。");
            }
            if (_powerBtn != null)
            {
                V06HoverTip.Attach(_powerBtn.gameObject, "强攻",
                    "本次主动攻击 +1。花 1 点费用点，每轮只能用一次；普通攻击本身不花费用。");
            }
            if (_swapBtn != null)
            {
                V06HoverTip.Attach(_swapBtn.gameObject, "换位",
                    "免费移到另一排空格，或与另一排的己方单位对调；不占总行动，每轮一次。");
            }
            if (_passBtn != null)
            {
                V06HoverTip.Attach(_passBtn.gameObject, "让过",
                    "结束本回合行动。双方都让过、或都用完主行动，即进入轮末结算；行动点最多结转 2 点。");
            }
            if (_coverBtn != null)
            {
                V06HoverTip.Attach(_coverBtn.gameObject, "掩护",
                    "被指定攻击时花 1 点费用点，使这次伤害 −1；每轮一次，不能用来保护远征旗。");
            }
            if (_declineBtn != null)
            {
                V06HoverTip.Attach(_declineBtn.gameObject, "放弃响应",
                    "不打出任何响应牌，本次攻击按原伤害直接结算。");
            }
        }

        /// <summary>手牌副行：按类别写实话。装备/附印/战况/响应/晋阶/支援都已接入
        /// （V06Effects2.Handles），只有天气/遭遇/事件是战役内容、当前模式用不了。</summary>
        private string HandSubLine(V06Card c)
        {
            switch (c.Category)
            {
                case V06Category.Unit: return c.Atk + "/" + c.Hp + " " + c.Tags;
                case V06Category.Tactic: return "战术";
                case V06Category.Equipment: return "装备";
                case V06Category.Seal: return "附印";
                case V06Category.Status: return "战况";
                case V06Category.Response: return "响应";
                case V06Category.Upgrade: return "晋阶";
                case V06Category.Support: return "支援";
                case V06Category.Token: return "衍生单位";
                case V06Category.Weather: return "天气·未开放";
                case V06Category.Encounter: return "遭遇·未开放";
                case V06Category.Event: return "事件·未开放";
                default: return c.Category.ToString();
            }
        }

        private static string Short(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        private string DetailLine()
        {
            if (_selHand >= 0)
            {
                V06Player me = _engine.Players[_playerCamp];
                if (_selHand < me.Hand.Count)
                {
                    V06Card c = V06Catalog.ById(me.Hand[_selHand]);
                    if (c != null)
                    {
                        return "【" + c.Name + "】" + (c.Cost >= 0 ? c.Cost + " 点　" : "")
                            + (c.Category == V06Category.Unit ? (c.Atk + "/" + c.Hp + "　" + c.Tags) : c.Category.ToString())
                            + "\n" + c.Text;
                    }
                }
            }
            if (_selUid >= 0)
            {
                V06Unit u = _engine.Players[_playerCamp].ByUid(_selUid);
                if (u != null)
                {
                    return "【" + u.Name + "】" + u.AtkBaseNow + "/" + u.CurHp + "（伤 " + u.Damage + "）　" + (u.TagText ?? "")
                        + "　关键词：" + (u.Keywords.Count == 0 ? "无" : string.Join("、", u.Keywords.ToArray()))
                        + "　位置：" + (V06Slot.IsFront(u.Slot) ? "前排" : "后排")
                        + (V06Expansion.GrowThreshold(u.CardId) > 0 ? "　成长：" + u.Growth + "/" + V06Expansion.GrowThreshold(u.CardId) : "")
                        + (string.IsNullOrEmpty(u.FusedFrom) ? "" : "　融合自：" + u.FusedFrom)
                        + (_engine.CanAttackFrom(u) ? "　可攻击" : "　本轮不可攻击");
                }
            }
            return "选一张手牌或自己单位，高亮处即为合法目标。";
        }

        // ───────────────────────── 交互 ─────────────────────────

        private void OnHandClicked(int i)
        {
            if (_engine == null || _engine.Finished) { return; }
            V06Player me = _engine.Players[_playerCamp];
            if (i >= me.Hand.Count) { return; }
            if (_engine.Active != _playerCamp) { _hint = "现在是对手回合，请稍等。"; Refresh(); return; }

            if (_selHand == i)
            {
                // 第二次点同一张：无目标的牌直接打出（非模态的"再点一次确认"）
                V06Action noTarget = FindNoTargetPlay(me.Hand[i]);
                if (noTarget != null) { Execute(noTarget); return; }
                _hint = "这张牌需要选目标：高亮处点击。";
                Refresh();
                return;
            }
            ClearSelection();              // 换选中必须互斥：手牌 / 预备 / 单位三选一
            _selHand = i;
            _lastError = null;
            V06Card c = V06Catalog.ById(me.Hand[i]);
            string name = c != null ? c.Name : "?";
            if (c != null && c.Cost > me.ActionPoints)
            {
                _hint = "【" + name + "】还需要 " + (c.Cost - me.ActionPoints) + " 点费用（当前 " + me.ActionPoints + " 点）。";
            }
            else if (!_engine.MainActionsLeft(_playerCamp))
            {
                _hint = "本轮主行动已用完，只能让过。";
            }
            else
            {
                _hint = "已选中【" + name + "】，点高亮的格位或目标执行。";
            }
            Refresh();
        }

        private void OnRespondClicked(int i)
        {
            if (_engine == null || _engine.Finished) { return; }
            if (i < 0 || i >= _respondActions.Count) { return; }
            Execute(_respondActions[i]);
        }

        private void OnReserveClicked(int i)
        {
            if (_engine == null || _engine.Finished) { return; }
            V06Player me = _engine.Players[_playerCamp];
            if (i >= me.Reserve.Count) { return; }
            if (_engine.Active != _playerCamp) { _hint = "现在是对手回合。"; Refresh(); return; }
            string id = me.Reserve[i];
            if (me.ReserveUsed.Contains(id)) { _hint = "这张预备本局已经用过了。"; Refresh(); return; }

            if (_selReserve == i)
            {
                // 再点一次：无目标的预备直接调用
                if (InvokePlay(id, -1)) { return; }
                _hint = "这张预备需要选目标：点高亮的己方单位。";
                Refresh();
                return;
            }
            ClearSelection();              // 换选中必须互斥：手牌 / 预备 / 单位三选一
            _selReserve = i;
            _lastError = null;
            V06Card rc = V06Catalog.ById(id);
            _hint = "已选中预备【" + (rc != null ? rc.Name : "?") + "】，点高亮的单位执行。";
            Refresh();
        }

        /// <summary>按卡牌 + 目标执行一次出牌（手牌与预备区共用）。</summary>
        private bool InvokePlay(string cardId, int uid)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == cardId && a.Uid == uid)
                {
                    Execute(a);
                    return true;
                }
            }
            return false;
        }

        /// <summary>V0.7：把引擎阵营映射到"界面位置"（0=下方我方区、1=上方敌方区）。
        /// 单人时玩家永远坐 camp 0；联机当客机时玩家坐 camp 1，界面要把它画在下方。</summary>
        private int Wig(int engineCamp) { return engineCamp == _playerCamp ? 0 : 1; }

        /// <summary>V0.7：反过来，把界面位置还原成引擎阵营。</summary>
        private int EngineCamp(int widgetCamp) { return widgetCamp == 0 ? _playerCamp : 1 - _playerCamp; }

        /// <summary>测试/驱动用：按引擎阵营取控件名。</summary>
        public string WidgetSlotName(int engineCamp, int slot) { return "Slot" + (Wig(engineCamp) * 6 + slot); }

        private void OnSlotClicked(int widgetCamp, int slot)
        {
            int camp = EngineCamp(widgetCamp);
            if (_engine == null || _engine.Finished) { return; }
            if (_engine.Active != _playerCamp) { _hint = "现在是对手回合。"; Refresh(); return; }
            V06Player me = _engine.Players[_playerCamp];
            V06Player foe = _engine.Players[1 - _playerCamp];
            _lastError = null;

            if (camp == 1)
            {
                V06Unit target = foe.At(slot);
                // 先看是不是"战术/支援指定敌方单位"
                if (_selHand >= 0 && target != null)
                {
                    V06Action tac = FindTacticOn(_selHand, target.Uid);
                    if (tac != null) { Execute(tac); return; }
                }
                if (_selReserve >= 0 && target != null)
                {
                    if (InvokePlay(me.Reserve[_selReserve], target.Uid)) { return; }
                }
                // 点敌方单位 → 作为攻击目标
                if (target == null) { _hint = "该格为空。"; Refresh(); return; }
                V06Action act = FindAttack(target.Uid);
                if (act == null)
                {
                    // 把"打不了"的原因说清楚,而不是一句"没有单位能攻击"
                    string why = "没有单位能攻击它。";
                    List<V06Unit> myUnits = new List<V06Unit>();
                    for (int u = 0; u < V06Slot.Total; u++) { V06Unit mu = me.At(u); if (mu != null) { myUnits.Add(mu); } }
                    bool anyReady = false, anyGuardBlocked = false;
                    for (int u = 0; u < myUnits.Count; u++)
                    {
                        V06Unit mu = myUnits[u];
                        if (mu.AttackedThisRound || (mu.DeployedThisRound && !mu.Has("突袭"))) { continue; }
                        anyReady = true;
                        List<V06Unit> legal = _engine.AttackTargets(mu);
                        for (int k = 0; k < legal.Count; k++) { if (legal[k].Uid == target.Uid) { anyGuardBlocked = true; break; } }
                    }
                    if (!anyReady) { why = "本单位都已行动过（本轮每单位只能攻击一次）。"; }
                    else if (anyGuardBlocked) { why = "被护卫挡住了：先攻击带护卫的单位。"; }
                    else { why = "射程不够：后排单位需要远射或飞行才能攻击。"; }
                    _hint = why;
                    Refresh();
                    return;
                }
                Execute(act);
                return;
            }

            // 自己这一侧
            V06Unit mine = me.At(slot);
            if (_selReserve >= 0)
            {
                if (mine != null && InvokePlay(me.Reserve[_selReserve], mine.Uid)) { return; }
                _hint = "这张预备不能指定该单位（或行动点不足）。";
                Refresh();
                return;
            }
            if (_selHand >= 0)
            {
                // ① S-V03 这类"需要落点"的战术：先选来源单位，再点对面空格
                if (_pendingTacticUid >= 0)
                {
                    V06Action atSlot = FindPlayAtSlot(_selHand, _pendingTacticUid, slot);
                    if (atSlot != null) { Execute(atSlot); return; }
                }
                // ② 战术指定己方单位
                if (mine != null)
                {
                    // 先判断"两段式"（要落点）的牌：这类必须让玩家接着点格子，不能在这里直接执行
                    if (HasPlayAtSlotForUnit(_selHand, mine.Uid))
                    {
                        _pendingTacticUid = mine.Uid;
                        _hint = "已选【" + mine.Name + "】：点它对面一排的高亮空格作为落点。";
                        Refresh();
                        return;
                    }
                    V06Action tac = FindAnyTargetedPlay(_selHand, mine.Uid);
                    if (tac != null) { Execute(tac); return; }
                }
                // ③ 部署
                V06Action dep = FindDeploy(slot);
                if (dep != null) { Execute(dep); return; }
                if (_pendingTacticUid >= 0)
                {
                    _hint = "这个格子不能作为落点：要选它对面那一排的空格。";
                }
                else
                {
                    _hint = mine != null ? "这张牌不能指定该单位（或行动点不足）。" : "这个格子不能部署这张牌。";
                }
                Refresh();
                return;
            }
            // ── 换位：必须显式确认，绝不因为"点了另一个单位"就自动换了位置 ──
            if (_selUid >= 0 && mine != null && mine.Uid != _selUid)
            {
                V06Action swUnit = FindSwapUnit(_selUid, mine.Uid);
                if (swUnit != null)
                {
                    V06Unit selU = me.ByUid(_selUid);
                    if (_swapMode)
                    {
                        AskSwapConfirm(selU, mine, swUnit, -1);
                    }
                    else
                    {
                        // 没进换位模式：只切换选中，不做换位（实机里"点到另一只就被换走"就是这么来的）
                        _hint = "已选中【" + mine.Name + "】。要与【" + (selU != null ? selU.Name : "上一只") + "】换位，请先点『换位』按钮再点目标。";
                        ClearSelection();
                        _selUid = mine.Uid;
                    }
                    Refresh();
                    return;
                }
            }
            if (_selUid >= 0 && mine == null)
            {
                V06Action swSlot = FindSwap(_selUid, slot);
                if (swSlot != null)
                {
                    V06Unit selU = me.ByUid(_selUid);
                    if (_swapMode) { AskSwapConfirm(selU, null, swSlot, slot); }
                    else
                    {
                        _hint = "要移到空位请先点『换位』按钮，再点目标格（会先确认）。";
                    }
                    Refresh();
                    return;
                }
            }
            if (mine == null) { _hint = "空格。先选一张手牌来部署。"; Refresh(); return; }
            bool sameUnit = (_selUid == mine.Uid);
            ClearSelection();              // 点单位也要把之前的手牌/预备选择清掉
            if (!sameUnit) { _selUid = mine.Uid; }
            _hint = _selUid >= 0 ? ("已选中【" + mine.Name + "】，点敌方高亮目标发起攻击。") : "已取消选择。";
            Refresh();
        }

        /// <summary>
        /// 清空所有选中态。手牌、预备、单位三种选择是互斥的：
        /// 之前只在各自入口清了一部分（点手牌不清预备），结果"先点预备再点手牌"会两个都留着，
        /// 之后点己方格位时永远先走预备分支 → 手牌怎么点都部署不出去（实机复现过）。
        /// 任何新的选中入口都必须先调它。
        /// </summary>
        private void ClearSelection()
        {
            _selHand = -1;
            _selUid = -1;
            _selReserve = -1;
            _pendingTacticUid = -1;
            _swapMode = false;
        }

        // ── 换位确认弹窗 ─────────────────────────────────────────
        /// <summary>换位是"改局面"的操作，必须先让玩家确认再执行；不经确认的自动换位已去掉。</summary>
        private void AskSwapConfirm(V06Unit a, V06Unit b, V06Action act, int slot)
        {
            CloseSwapConfirm();
            if (a == null || act == null) { return; }
            var layer = new GameObject("SwapConfirm", typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(_root, false);
            var lrt = layer.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lrt.SetAsLastSibling();
            var mask = layer.GetComponent<Image>();
            mask.color = new Color(0f, 0f, 0f, 0.62f);
            mask.raycastTarget = true;          // 挡住底下的点击，避免"确认框还在又点了别处"
            Image panel = UiKit.Panel(layer.transform, "Panel", 450f, 320f, 700f, 250f, new Color(0.11f, 0.13f, 0.17f, 0.99f));
            UiKit.Label(panel.transform, "T", 24f, 18f, 652f, 34f, "确认换位", 20, TextAnchor.MiddleLeft, UiKit.Selected);
            string body = slot >= 0
                ? "把【" + a.Name + "】移到对面那一排的空格（第 " + (slot % 3 + 1) + " 格）。"
                : "把【" + a.Name + "】与【" + (b != null ? b.Name : "?") + "】互换前后排位置。";
            UiKit.Label(panel.transform, "B", 24f, 58f, 652f, 76f,
                body + "\n换位每轮 1 次：不消耗费用点，也不占主行动。", 16, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Button(panel.transform, "Ok", 220f, 178f, 180f, 46f, "确定换位", UiKit.MyCamp, delegate
            {
                CloseSwapConfirm();
                _swapMode = false;
                Execute(act);
            });
            UiKit.Button(panel.transform, "Cancel", 420f, 178f, 180f, 46f, "取消", UiKit.ButtonNormal, delegate
            {
                CloseSwapConfirm();
                _swapMode = false;
                _hint = "已取消换位。";
                Refresh();
            });
            _swapConfirmLayer = layer;
        }

        private void CloseSwapConfirm()
        {
            if (_swapConfirmLayer != null) { Destroy(_swapConfirmLayer); _swapConfirmLayer = null; }
        }

        /// <summary>某张手牌能不能"指定某个单位 + 某个空格"（S-V03 红雾换位这种两段式）。</summary>
        private V06Action FindPlayAtSlot(int handIndex, int uid, int slot)
        {
            V06Player me = _engine.Players[_playerCamp];
            if (handIndex < 0 || handIndex >= me.Hand.Count) { return null; }
            string id = me.Hand[handIndex];
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == id && a.Uid == uid && a.TargetSlot == slot) { return a; }
            }
            return null;
        }

        /// <summary>这张牌是否存在"指定该单位 + 某空格"的用法（决定点单位后是否进入选落点状态）。</summary>
        private bool HasPlayAtSlotForUnit(int handIndex, int uid)
        {
            V06Player me = _engine.Players[_playerCamp];
            if (handIndex < 0 || handIndex >= me.Hand.Count) { return false; }
            string id = me.Hand[handIndex];
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == id && a.Uid == uid && a.TargetSlot >= 0) { return true; }
            }
            return false;
        }

        private void OnEnemyFlagClicked()
        {
            if (_engine == null || _engine.Finished || _engine.Active != _playerCamp) { Refresh(); return; }
            V06Action act = FindFlagAttack();
            if (act == null) { _hint = "敌方前排还有单位，不能直接打旗。"; Refresh(); return; }
            Execute(act);
        }

        private void TogglePower() { SetPowerStrike(!_powerStrike); }
        private void ToggleSwap() { SetSwapMode(!_swapMode); }

        /// <summary>强攻开关（绝对值设置，供界面按钮与自动化共用）。</summary>
        public void SetPowerStrike(bool v)
        {
            _powerStrike = v;
            _hint = v ? "强攻开启：下次攻击 +1（消耗 1 点，每轮 1 次）。" : "强攻关闭。";
            Refresh();
        }

        /// <summary>换位模式（绝对值设置）。其实换位不依赖这个开关，按钮只是提示。</summary>
        public void SetSwapMode(bool v)
        {
            _swapMode = v;
            _hint = v ? "换位模式：先点己方单位，再点另一排空格或单位。" : "换位模式关闭。";
            Refresh();
        }

        private void DoPass()
        {
            Execute(new V06Action { Kind = V06ActionKind.Pass });
        }

        private void DoCover()
        {
            Execute(new V06Action { Kind = V06ActionKind.Cover });
        }

        private void DoDecline()
        {
            Execute(new V06Action { Kind = V06ActionKind.Decline });
        }

        /// <summary>V0.7 融合：按"结果身材最优"挑一组合法组合执行（界面只有一个按钮）。</summary>
        private void DoFuse()
        {
            V06Action best = BestFuseAction();
            if (best == null) { _hint = "现在没有可融合的两只兽（需要指定组合同时在场上）。"; Refresh(); return; }
            Execute(best);
        }

        /// <summary>挑一组可融合的组合：优先融合体身材与关键词更强的那组。</summary>
        public V06Action BestFuseAction()
        {
            V06Action best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind != V06ActionKind.Fuse) { continue; }
                V06Card rc = V06Catalog.ById(a.CardId);
                int sc = rc == null ? 0 : rc.Atk * 3 + rc.Hp * 2 + rc.Keywords.Length * 2;
                if (sc > bestScore) { bestScore = sc; best = a; }
            }
            return best;
        }

        // ───────────────────────── 行动查找与执行 ─────────────────────────

        private bool HasActionOfKind(V06ActionKind kind)
        {
            for (int i = 0; i < _actions.Count; i++) { if (_actions[i].Kind == kind) { return true; } }
            return false;
        }

        private bool IsDeployTarget(int camp, int slot)
        {
            if (camp != _playerCamp) { return false; }
            // 两段式战术的落点（如红雾换位：先单位后空格）
            if (_pendingTacticUid >= 0 && FindPlayAtSlot(_selHand, _pendingTacticUid, slot) != null) { return true; }
            if (_swapMode && _selUid >= 0 && FindSwap(_selUid, slot) != null) { return true; }
            if (_selHand < 0) { return false; }
            return FindDeploy(slot) != null;
        }

        private V06Action FindDeploy(int slot)
        {
            V06Player me = _engine.Players[_playerCamp];
            if (_selHand < 0 || _selHand >= me.Hand.Count) { return null; }
            string id = me.Hand[_selHand];
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.Deploy && a.CardId == id && a.Slot == slot) { return a; }
            }
            return null;
        }

        /// <summary>选中的手牌能否指定某个单位（战术目标）。</summary>
        private V06Action FindTacticOn(int handIndex, int uid)
        {
            V06Player me = _engine.Players[_playerCamp];
            if (handIndex < 0 || handIndex >= me.Hand.Count) { return null; }
            string id = me.Hand[handIndex];
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == id && a.Uid == uid) { return a; }
            }
            return null;
        }

        private bool IsTacticTarget(int uid)
        {
            if (_selHand >= 0 && FindTacticOn(_selHand, uid) != null) { return true; }
            if (_selReserve >= 0)
            {
                V06Player me = _engine.Players[_playerCamp];
                if (_selReserve < me.Reserve.Count) { return FindPlayAction(me.Reserve[_selReserve], uid) != null; }
            }
            return false;
        }

        /// <summary>手牌里任意一类"指定单位"的牌（战术/装备/附印/晋阶/支援）。</summary>
        private V06Action FindAnyTargetedPlay(int handIndex, int uid)
        {
            V06Player me = _engine.Players[_playerCamp];
            if (handIndex < 0 || handIndex >= me.Hand.Count) { return null; }
            return FindPlayAction(me.Hand[handIndex], uid);
        }

        private V06Action FindPlayAction(string cardId, int uid)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                // 必须排除"还带落点"的方案：那种牌要玩家先选单位、再选格子两步走。
                // 只按 uid 命中就执行，等于引擎替玩家挑落点（红雾换位把单位自己挪走就是这么来的）。
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == cardId && a.Uid == uid && a.TargetSlot < 0) { return a; }
            }
            return null;
        }

        private V06Action FindSwapUnit(int uid, int otherUid)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.Swap && a.Uid == uid && a.TargetUid == otherUid) { return a; }
            }
            return null;
        }

        private V06Action FindNoTargetPlay(string cardId)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == cardId && a.Uid < 0) { return a; }
            }
            return null;
        }

        private V06Action FindAttack(int targetUid)
        {
            if (_selUid < 0) { return null; }
            V06Action fallback = null;
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind != V06ActionKind.Attack || a.Uid != _selUid || a.TargetUid != targetUid) { continue; }
                if (a.PowerStrike == _powerStrike) { return a; }
                fallback = a;
            }
            return fallback;
        }

        private V06Action FindFlagAttack()
        {
            if (_selUid < 0) { return null; }
            V06Action fallback = null;
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind != V06ActionKind.Attack || a.Uid != _selUid || !a.ToFlag) { continue; }
                if (a.PowerStrike == _powerStrike) { return a; }
                fallback = a;
            }
            return fallback;
        }

        private V06Action FindSwap(int uid, int slot)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.Swap && a.Uid == uid && a.TargetSlot == slot) { return a; }
            }
            return null;
        }

        private bool IsEnemyAttackTarget(int uid)
        {
            if (_selUid < 0) { return false; }
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.Attack && a.Uid == _selUid && a.TargetUid == uid) { return true; }
            }
            return false;
        }

        private bool CanHitFlagNow()
        {
            if (_selUid < 0) { return false; }
            return FindFlagAttack() != null;
        }

        private bool CanUseNow(V06Card c)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                V06Action a = _actions[i];
                if (a.Kind == V06ActionKind.Deploy && a.CardId == c.Id) { return true; }
                if (a.Kind == V06ActionKind.PlayCard && a.CardId == c.Id) { return true; }
            }
            return false;
        }

        private void Execute(V06Action a)
        {
            string err;
            // V0.7 特效：Apply 前后各取一次快照，只用来播表现，不参与规则
            bool fxOn = _fx != null && _fx.Ready;
            int guardCamp = -1, guardSlot = -1, guardHp = -1;
            // 第 3 批：出手方格子（攻击前扑）与场上单位 Uid 快照（阵亡消散）
            int atkIdx = -1;
            string atkCardId = null, tgtCardId = null;
            int tgtIdx = -1;
            System.Collections.Generic.Dictionary<int, int> slotOfUid = new System.Collections.Generic.Dictionary<int, int>();
            if (_engine != null)
            {
                for (int camp = 0; camp < 2; camp++)
                {
                    for (int s = 0; s < 6; s++)
                    {
                        V06Unit uu = _engine.Players[camp].At(s);
                        if (uu != null) { slotOfUid[uu.Uid] = Wig(camp) * 6 + s; }
                    }
                }
            }
            if (a != null && a.Kind == V06ActionKind.Attack && _engine != null)
            {
                int attackerUid = a.Uid;
                int si;
                if (slotOfUid.TryGetValue(attackerUid, out si))
                {
                    atkIdx = si;
                    // 出手帧用出手方自己的卡
                    int cA = si / 6;
                    V06Unit uAtk = _engine.Players[EngineCamp(cA)].At(si % 6);
                    if (uAtk != null) { atkCardId = uAtk.CardId; }
                }
                if (!a.ToFlag)
                {
                    V06Unit uTgt = _engine.Players[1 - _engine.Active].ByUid(a.TargetUid);
                    if (uTgt != null)
                    {
                        int ti;
                        if (slotOfUid.TryGetValue(uTgt.Uid, out ti)) { tgtIdx = ti; }
                        tgtCardId = uTgt.CardId;
                    }
                }
            }
            if (a != null && a.Kind == V06ActionKind.Attack && !a.ToFlag && _engine != null)
            {
                V06Unit t0 = _engine.Players[1 - _engine.Active].ByUid(a.TargetUid);
                if (t0 != null) { guardCamp = t0.Camp; guardSlot = t0.Slot; guardHp = t0.CurHp; }
            }
            if (fxOn) { _fx.Capture(_engine); }
            int logBefore = _engine.Log.Count;
            bool ok = _engine.Apply(a, out err);
            if (!ok)
            {
                _lastError = "非法操作：" + err;
                _hint = _lastError;
                Refresh();
                return;
            }
            // ── 汲取反馈：引擎只在战斗日志里写一行，玩家几乎注意不到 ──
            // 触发时把结果顶到提示行；打旗时明确说明"不触发"，免得以为效果坏了。
            bool drainFired = false;
            for (int li = logBefore; li < _engine.Log.Count; li++)
            {
                if (_engine.Log[li] != null && _engine.Log[li].Contains("汲取")) { _hint = _engine.Log[li]; drainFired = true; break; }
            }
            if (!drainFired && a != null && a.Kind == V06ActionKind.Attack && a.ToFlag && _engine != null)
            {
                V06Unit flagAtk = null;
                for (int c2 = 0; c2 < 2 && flagAtk == null; c2++) { flagAtk = _engine.Players[c2].ByUid(a.Uid); }
                // 响应窗口期间提示行归"对手正在响应…"，所以先记一笔，等结算完再说明
                if (flagAtk != null && flagAtk.Has("汲取")) { _drainFlagNote = true; }
            }
            // ── 第 3 批动画：出手前扑 / 格挡涟漪 / 阵亡消散 / 部署法阵 / 融合连线 ──
            if (atkIdx >= 0)
            {
                PlaySlotBurst(atkIdx, "anim_attack_lunge");
                // 第 4 批：出击者自己的卡面人物做"出手"动作
                if (!string.IsNullOrEmpty(atkCardId)) { PlaySlotFrames(atkIdx, atkCardId, "attack"); }
            }
            // 第 4 批：被打到的目标做"受击"动作
            if (tgtIdx >= 0 && !string.IsNullOrEmpty(tgtCardId)) { PlaySlotFrames(tgtIdx, tgtCardId, "hit"); }
            foreach (int uid in slotOfUid.Keys)
            {
                bool alive = false;
                for (int camp = 0; camp < 2 && !alive; camp++)
                {
                    if (_engine.Players[camp].ByUid(uid) != null) { alive = true; }
                }
                if (!alive) { PlaySlotBurst(slotOfUid[uid], "anim_death_dissolve"); }
            }
            if (a != null && a.Kind == V06ActionKind.Deploy && a.Slot >= 0)
            {
                PlaySlotBurst(Wig(_playerCamp) * 6 + a.Slot, "anim_deploy_summon");
            }
            if (a != null && a.Kind == V06ActionKind.Fuse) { PlayScreenOnce("anim_fuse_link", 0.9f); }
            if (fxOn)
            {
                // 特效永远不能打断动作流程：这里出任何异常都只警告一次，规则照常结算
                try
                {
                    _fx.Diff(_engine, a);
                    if (guardCamp >= 0)
                    {
                        V06Unit t1 = _engine.Players[guardCamp].ByUid(a.TargetUid);
                        if (t1 != null && t1.CurHp == guardHp)
                        {
                            _fx.PlayGuard(guardCamp, guardSlot);
                            PlaySlotBurst(Wig(guardCamp) * 6 + guardSlot, "anim_guard_ripple");
                        }
                    }
                }
                catch (System.Exception fxEx)
                {
                    if (!_fxErrorLogged)
                    {
                        _fxErrorLogged = true;
                        Debug.LogWarning("[V06特效] 播放异常（已忽略，规则不受影响）：" + fxEx.Message);
                    }
                }
            }
            if (netMode) { V06Net.SendAct(a); }
            _lastError = null;
            _selHand = -1;
            _selUid = -1;
            _selReserve = -1;
            _powerStrike = false;
            _swapMode = false;
            if (_engine.Finished) { ShowResult(); }
            Refresh();
        }

        private void ShowResult()
        {
            if (_resultPanel == null) { return; }
            _resultPanel.SetActive(true);
            // 下面全是"演一次"的东西（全屏特效、音效、弹出补间、写存档）：
            // Update 每帧都会进来，所以必须先挡住重复执行。
            if (_resultShown) { return; }
            _resultShown = true;
            Debug.Log("[V06结算] 演出一次（每局只应出现一次）：胜方=" + _engine.Winner
                + " 原因=" + _engine.FinishReason + " 轮=" + _engine.Round);
            string who;
            if (_engine.Winner == _playerCamp) { who = "你赢了"; }
            else if (_engine.Winner == -2) { who = "平局"; }
            else if (_engine.Winner < 0) { who = "未分胜负"; }
            else { who = "对手获胜"; }
            _resultText.text = "对局结束：" + who + "\n" + _engine.FinishReason
                + "\n旗 " + _engine.Players[_playerCamp].FlagHp + " : " + _engine.Players[1 - _playerCamp].FlagHp
                + "　共 " + _engine.Round + " 轮"
                + (netMode ? "　（联机）" : "");
            // V0.7：结算横幅（有关键帧就用交叉淡入补间）
            string bannerSkin = _engine.Winner == _playerCamp ? "banner_victory"
                : (_engine.Winner == -2 ? "banner_draw" : "banner_defeat");
            string bannerAnim = _engine.Winner == _playerCamp ? "anim_banner_victory"
                : (_engine.Winner == -2 ? "anim_banner_draw" : "anim_banner_defeat");
            if (_banner == null)
            {
                _banner = V06UiV07.AnimatedSprite(_root, "ResultBanner", 200f, 300f, 1200f, 260f, bannerSkin, bannerAnim, 0.34f);
            }
            else
            {
                Sprite bs = V06UiV07.Skin(bannerSkin);
                if (bs != null) { _banner.sprite = bs; }
                _banner.gameObject.SetActive(true);
                V06UiV07.Pop(_banner.rectTransform, 0.2f);
            }
            V06Audio.Play(_engine.Winner == _playerCamp ? "R41" : "R42");
            // 结算全屏特效：胜→星屑／平→柔光／负→暗角。
            // 平局以前借用失败暗角（看着像输了），现在单独给 anim_draw_veil，没素材才退回暗角。
            if (_engine.Winner == _playerCamp) { PlayScreenOnce("anim_victory_stars", 2.2f); }
            else if (_engine.Winner == -2)
            {
                if (V06UiV07.HasAnim("anim_draw_veil")) { PlayScreenOnce("anim_draw_veil", 2.2f); }
                else { PlayScreenOnce("anim_defeat_vignette", 2.2f); }
            }
            else { PlayScreenOnce("anim_defeat_vignette", 2.6f); }
            // V0.7：结算写进存档（每局只记一次）
            if (!_resultRecorded && !netMode)
            {
                _resultRecorded = true;
                V06Save.RecordMatch(_engine, _playerCamp, _myDeckName, _foeDeckName, "单人");
            }
            else if (!_resultRecorded && netMode)
            {
                _resultRecorded = true;
                V06Save.RecordMatch(_engine, _playerCamp, _myDeckName, _foeDeckName, "联机");
            }
        }

        // ───────────────────────── 供自动化测试读取 ─────────────────────────

        public V06Engine Engine { get { return _engine; } }
        public List<V06Action> CurrentActions { get { return _actions; } }

        // ───────────────────── V0.7：大厅/联机入口 ─────────────────────

        /// <summary>用自定义卡组开局（大厅/组卡/联机共用）。myCamp 决定我坐哪一方。</summary>
        public bool StartMatchDeck(V06Precon deckA, V06Precon deckB, int seed, bool net, int myCamp, out string error)
        {
            error = "";
            _engine = new V06Engine();
            _engineNullLogged = false;
            if (!_engine.TryStartDeck(seed, deckA, deckB, out error))
            {
                _hint = "开局失败：" + error;
                Refresh();
                return false;
            }
            _playerCamp = myCamp;
            netMode = net;
            netIsHost = myCamp == 0;
            _myDeckName = deckA == null ? "" : deckA.Name;
            _foeDeckName = deckB == null ? "" : deckB.Name;
            _resultRecorded = false;
            _resultShown = false;
            CloseSwapConfirm();
            _selHand = -1; _selUid = -1; _selReserve = -1;
            _powerStrike = false; _swapMode = false;
            _aiTimer = 0f;
            if (_resultPanel != null) { _resultPanel.SetActive(false); }
            if (_banner != null) { _banner.gameObject.SetActive(false); }
            V06Ai.Seed(seed + 17);
            Refresh();
            // 大厅/组卡/联机都走这条开局路径，所以对战音乐必须在这里起（之前只挂在 NewGame 上，正式玩家永远听不到）
            StartCoroutine(BattleMusicIntro());
            return true;
        }

        /// <summary>联机：把对手发来的行动应用到本地引擎（双方锁步）。</summary>
        public bool ApplyRemoteAction(V06NetMsg m)
        {
            if (_engine == null || _engine.Finished) { return false; }
            V06Action a = V06Net.ToAction(m);
            if (a == null) { return false; }
            string err;
            bool fxOn = _fx != null && _fx.Ready;
            if (fxOn) { _fx.Capture(_engine); }
            bool ok = _engine.Apply(a, out err);
            if (!ok) { Debug.LogWarning("[V06Net] 远端行动被拒绝：" + a + " — " + err); return false; }
            if (fxOn) { _fx.Diff(_engine, a); }
            if (_engine.Finished) { ShowResult(); }
            Refresh();
            return true;
        }

        /// <summary>显示/隐藏战斗画布（回大厅时整块藏起来，避免大厅下面透出棋盘）。</summary>
        public void SetVisualVisible(bool visible)
        {
            if (_canvas == null) { return; }
            if (_canvas.gameObject.activeSelf != visible) { _canvas.gameObject.SetActive(visible); }
        }

        /// <summary>回大厅（结算面板与联机都用到）。</summary>
        public void BackToLobby()
        {
            V06Audio.Music("menu");
            SetVisualVisible(false);
            if (V06ShellUI.Instance != null) { V06ShellUI.Instance.Show(); }
        }

        /// <summary>结算面板是否显示（自动化巡检用）。</summary>
        public bool ResultPanelVisible { get { return _resultPanel != null && _resultPanel.activeSelf; } }

        /// <summary>当前是否在等我方做出响应（响应牌／掩护／放弃）。自动化巡检据此点"放弃响应"，
        /// 否则对手一攻击就卡在响应窗口里，整局永远打不完。</summary>
        public bool IsResponding
        {
            get { return _engine != null && !_engine.Finished && _engine.PendingAttack != null && _engine.Active == _playerCamp; }
        }

        /// <summary>当前存活的全屏一次性特效层数量（自动化巡检用）：
        /// 结算演出若每帧重放，这里会堆到上百层，正是"屏幕发白卡死"的根因，所以要能读出来。</summary>
        public int ScreenFxCount
        {
            get
            {
                if (_root == null) { return 0; }
                int n = 0;
                for (int i = 0; i < _root.childCount; i++)
                {
                    if (_root.GetChild(i).gameObject.name.StartsWith("ScreenFx_")) { n++; }
                }
                return n;
            }
        }
        /// <summary>V0.7：已触发的特效次数（自动化测试用来确认手感层真的在跑）。</summary>
        public int FxCount { get { return _fx == null ? 0 : _fx.FxCount; } }
        /// <summary>V0.7：特效组件的可用状态与素材加载情况。</summary>
        public string FxStatusForTest
        {
            get
            {
                if (_fx == null) { return "fx=null"; }
                return "ready=" + _fx.Ready + " seqs=" + _fx.LoadedSequences + " [" + _fx.LoadedSequenceNames + "]"
                       + " count=" + _fx.FxCount + " seqPlays=" + _fx.SequenceCount;
            }
        }
        /// <summary>V0.7：信息面板是否已建好（供自动化验收）。</summary>
        public bool InfoPanelBuilt { get { return _infoPanel != null; } }
        /// <summary>测试用：外部改过引擎状态（如注入素材）后强制刷一次界面。</summary>
        public void ForceRefresh() { Refresh(); }

        /// <summary>测试用：换位确认框是否正开着（自动化要确认"点另一只不会直接换位"）。</summary>
        public bool SwapConfirmVisible { get { return _swapConfirmLayer != null; } }

        /// <summary>测试用：当前是否处于"已选来源单位、等玩家点落点"的两段式战术状态（S-V03 等）。</summary>
        public bool PendingTactic { get { return _pendingTacticUid >= 0; } }
        public int PendingTacticUid { get { return _pendingTacticUid; } }

        /// <summary>测试用：点换位确认框里的"确定/取消"。</summary>
        public bool ClickSwapConfirm(bool ok)
        {
            if (_swapConfirmLayer == null) { return false; }
            Transform panel = _swapConfirmLayer.transform.Find("Panel");
            if (panel == null) { return false; }
            Transform btn = panel.Find(ok ? "Ok" : "Cancel");
            if (btn == null) { return false; }
            Button b = btn.GetComponent<Button>();
            if (b == null) { return false; }
            b.onClick.Invoke();
            return true;
        }
        public int PlayerCamp { get { return _playerCamp; } }
        public bool IsPlayerTurn { get { return _engine != null && _engine.Active == _playerCamp && !_engine.Finished; } }

        /// <summary>测试用：按 UI 的点击路径执行一个行动（选牌 → 点格子）。</summary>
        /// <summary>测试用：按界面路径点击一张预备牌（可带目标单位）。</summary>
    public bool ClickReserve(V06Action a)
    {
        V06Player me = _engine.Players[_playerCamp];
        int ri = me.Reserve.IndexOf(a.CardId);
        if (ri < 0) { return false; }
        OnReserveClicked(ri);
        if (a.Uid < 0) { OnReserveClicked(ri); return true; }
        V06Unit mine = me.ByUid(a.Uid);
        if (mine != null) { OnSlotClicked(_playerCamp, mine.Slot); return true; }
        V06Unit foe = _engine.Players[1 - _playerCamp].ByUid(a.Uid);
        if (foe != null) { OnSlotClicked(1 - _playerCamp, foe.Slot); return true; }
        return false;
    }

    /// <summary>测试用：按界面路径点击第 i 个响应按钮。</summary>
    public bool ClickRespondByIndex(int i)
    {
        OnRespondClicked(i);
        return true;
    }

    /// <summary>当前可用的响应行动（供自动化读取）。</summary>
    public List<V06Action> RespondActions { get { return _respondActions; } }

    public bool ClickAction(V06Action a)
        {
            if (a == null || _engine == null) { return false; }
            // 自动化点击必须从"干净选中态"开始：手牌/预备/单位是开关式选中，
            // 上一次点击留下的选中会让这一次变成"再点一次"（表现为行动被吞、测试卡住）。
            ClearSelection();
            if (a.Kind == V06ActionKind.Pass) { DoPass(); return true; }
            if (a.Kind == V06ActionKind.Cover) { DoCover(); return true; }
            if (a.Kind == V06ActionKind.Decline) { DoDecline(); return true; }
            if (a.Kind == V06ActionKind.Deploy)
            {
                V06Player me = _engine.Players[_playerCamp];
                int hi = me.Hand.IndexOf(a.CardId);
                if (hi < 0) { return false; }
                OnHandClicked(hi);
                OnSlotClicked(_playerCamp, a.Slot);
                return true;
            }
            if (a.Kind == V06ActionKind.PlayCard)
            {
                V06Player me = _engine.Players[_playerCamp];
                int hi = me.Hand.IndexOf(a.CardId);
                if (hi < 0) { return false; }
                OnHandClicked(hi);
                if (a.Uid < 0) { OnHandClicked(hi); return true; }
                // 两段式战术（S-V03 红雾换位这类）：点来源单位 → 再点玩家自己选的落点
                if (a.TargetSlot >= 0)
                {
                    V06Unit src = me.ByUid(a.Uid);
                    if (src == null) { return false; }
                    OnSlotClicked(_playerCamp, src.Slot);
                    OnSlotClicked(_playerCamp, a.TargetSlot);
                    return true;
                }
                string err2;
                return _engine.Apply(a, out err2);
            }
            if (a.Kind == V06ActionKind.Attack)
            {
                V06Player me = _engine.Players[_playerCamp];
                V06Unit u = me.ByUid(a.Uid);
                if (u == null) { return false; }
                OnSlotClicked(_playerCamp, u.Slot);
                _powerStrike = a.PowerStrike;
                Refresh();
                if (a.ToFlag) { OnEnemyFlagClicked(); return true; }
                V06Unit t = _engine.Players[1 - _playerCamp].ByUid(a.TargetUid);
                if (t == null) { return false; }
                OnSlotClicked(1 - _playerCamp, t.Slot);
                return true;
            }
            if (a.Kind == V06ActionKind.Fuse)
            {
                // 融合在引擎里是一次原子行动，界面只需要走"点按钮"这一条路径
                Execute(a);
                return true;
            }
            if (a.Kind == V06ActionKind.Swap)
            {
                V06Player me = _engine.Players[_playerCamp];
                V06Unit u = me.ByUid(a.Uid);
                if (u == null) { return false; }
                _swapMode = true;
                OnSlotClicked(_playerCamp, u.Slot);
                if (a.TargetUid >= 0)
                {
                    V06Unit other = me.ByUid(a.TargetUid);
                    if (other == null) { return false; }
                    OnSlotClicked(_playerCamp, other.Slot);
                }
                else
                {
                    OnSlotClicked(_playerCamp, a.TargetSlot);
                }
                // 换位现在必须玩家确认；自动化走"点确定"这条真实路径
                if (_swapConfirmLayer != null) { ClickSwapConfirm(true); }
                return true;
            }
            return false;
        }
    }
}
