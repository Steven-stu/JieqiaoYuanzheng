using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Jieqiao.UI
{
    /// <summary>
    /// 大厅与系统界面（V0.7 第一版：纯文字骨架 + 本地联机）。
    ///
    /// 已实现页面：
    ///   大厅 Home / 卡牌图鉴 Codex / 组卡组 Deck / 个人资料 Profile / 联机 Net / 每日签到 CheckIn
    /// 占位页面：
    ///   商店 Shop / 交换卡牌 Trade（等美术与数值，先留入口）
    ///
    /// 设计约束与战斗界面一致：全部代码构建 uGUI、坐标用 1600×900 设计空间、
    /// 布局根节点按画布高度等比缩放（不能用 Screen 尺寸，会双层缩放）。
    /// </summary>
    public sealed class V06ShellUI : MonoBehaviour
    {
        public static V06ShellUI Instance;
        private string _lastPageSound = "";
        private Button _devBtn;

        private const float RefW = UiKit.RefW;
        private const float RefH = UiKit.RefH;

        private V06BattleUI _battle;
        private Canvas _canvas;
        private RectTransform _root;
        private string _page = "home";
        private bool _visible = true;

        // 顶栏
        private Text _nameText, _statText, _coinText;
        private Image _avatar;
        private readonly System.Collections.Generic.Dictionary<string, Image> _pageBg = new System.Collections.Generic.Dictionary<string, Image>();
        // 页面根
        private RectTransform _pgHome, _pgCodex, _pgDeck, _pgProfile, _pgNet, _pgShop, _pgCheckIn, _pgTrade;
        private readonly List<Button> _navButtons = new List<Button>();
        private readonly List<string> _navIds = new List<string>();

        // 大厅
        private Text _homeInfo, _homeDeck;
        // 图鉴
        private readonly List<Image> _codexCellBg = new List<Image>();
        private readonly List<Text> _codexCellText = new List<Text>();
        private readonly List<Button> _codexCellBtn = new List<Button>();
        private readonly List<string> _codexIds = new List<string>();
        private int _codexPage;
        private string _codexFilter = "all";
        private V06CardInfoPanel _codexDetail;
        private Text _codexPageText;
        // 组卡
        private readonly List<Button> _deckPickBtn = new List<Button>();
        private readonly List<Text> _deckPickText = new List<Text>();
        private readonly List<string> _deckPickIds = new List<string>();
        private int _deckPickPage;
        private string _deckTab = "unit";
        private Text _deckSummary, _deckPageText;
        // 资料
        private Text _profileStats;
        private readonly List<Text> _recordTexts = new List<Text>();
        // 联机
        private InputField _roomInput, _pwInput, _nameInput, _hostIpInput;
        private Text _netStatus, _netDeckText;
        private Button _netStartBtn;
        // 签到
        private readonly List<Image> _checkCells = new List<Image>();
        private readonly List<Text> _checkLabels = new List<Text>();
        private Text _checkInfo;

        // ── V0.7 第 2 批动画接线 ──
        private Image _dustOverlay;          // 大厅浮尘氛围层（1600×900 循环）
        private Image _avatarGlow;           // 头像悬停/呼吸光圈
        private Image _deckCardbackGlow;     // 组卡页卡背光
        private Image _winrateGlow;          // 资料页胜率光晕
        private Image _netSpinner;           // 联机连接中旋转
        private Image _toastBg;              // 提示条（notify_toast 皮 + anim_toast_wipe 扫光）
        private Text _toastText;
        private float _toastHideAt;
        private Image _deckSaveBtnImg;       // 保存卡组按钮（播 anim_save_confirm）
        // 图鉴：传说级（进化线终态）格子的循环光
        private readonly Dictionary<int, Image> _codexGlow = new Dictionary<int, Image>();
        private readonly Dictionary<int, Image> _deckRowGlow = new Dictionary<int, Image>();
        // 新组卡页:图鉴式卡片墙
        private readonly List<Image> _deckCellBg = new List<Image>();
        private readonly List<Image> _deckCellArt = new List<Image>();
        private readonly List<Text> _deckCellText = new List<Text>();
        private readonly List<Text> _deckCellBadge = new List<Text>();
        private string _deckSelId;
        private V06CardInfoPanel _deckDetail;
        private Text _deckDetailHint;
        private readonly List<Text> _deckListText = new List<Text>();
        private readonly List<Image> _deckListBg = new List<Image>();
        /// <summary>三个页签的文字（开发者模式下要改文案，说明卡池已放开）。</summary>
        private readonly Text[] _deckTabTexts = new Text[3];
        private RectTransform _nameLayer;
        private InputField _deckNameInput;
        private int _deckListPage;
        private Text _deckListPageText;
        private const int DeckRowsPerPage = 4;

        // 当前选择
        private int _deckIndex;
        private int _matchSeed;
        private bool _matchRunning;

        private void Start()
        {
            Instance = this;
            V06Save.Load();
            _battle = FindObjectOfType<V06BattleUI>();
            Build();
            ShowPage("home");
            V06Audio.Music("menu");
            // 进游戏先停在主页面：战斗画布整块隐藏，避免棋盘透出来
            _matchRunning = false;
            if (_battle != null) { _battle.paused = true; _battle.SetVisualVisible(false); }
            Debug.Log("[V06启动] 大厅已建：page=" + _page + " visible=" + _visible
                + " canvas=" + (_canvas == null ? "null" : ("order=" + _canvas.sortingOrder + " active=" + _canvas.gameObject.activeSelf))
                + " 页节点=" + (_root == null ? 0 : _root.childCount)
                + " battle=" + (_battle == null ? "null" : ("engine=" + (_battle.Engine == null ? "null" : "有") + " paused=" + _battle.paused)));
        }

        // ───────────────────────── 构建 ─────────────────────────

        private void Build()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
            var canvasGo = new GameObject("ShellCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("ShellBg", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.055f, 0.065f, 0.09f, 1f);
            bgImg.raycastTarget = true;   // 挡住底下的战斗界面点击

            // 第 2 批动画：大厅浮尘（3 帧 1600×900 透明粒子层，纯装饰、不吃射线，压在内容之下）
            var dust = new GameObject("ShellDust", typeof(RectTransform), typeof(Image));
            dust.transform.SetParent(canvasGo.transform, false);
            var dustRt = dust.GetComponent<RectTransform>();
            dustRt.anchorMin = Vector2.zero; dustRt.anchorMax = Vector2.one;
            dustRt.offsetMin = Vector2.zero; dustRt.offsetMax = Vector2.zero;
            _dustOverlay = dust.GetComponent<Image>();
            _dustOverlay.raycastTarget = false;
            _dustOverlay.color = new Color(1f, 1f, 1f, 0.33f);
            if (V06UiV07.HasAnim("anim_bg_dust")) { V06UiV07.PlayAnim(_dustOverlay, "anim_bg_dust", 4f, true); }
            else { dust.SetActive(false); }

            var layoutGo = new GameObject("ShellRoot", typeof(RectTransform));
            layoutGo.transform.SetParent(canvasGo.transform, false);
            _root = layoutGo.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(RefW, RefH);
            _root.anchoredPosition = Vector2.zero;

            BuildHeader();
            BuildNav();
            _pgHome = NewPage("PageHome");
            _pgCodex = NewPage("PageCodex");
            _pgDeck = NewPage("PageDeck");
            _pgProfile = NewPage("PageProfile");
            _pgNet = NewPage("PageNet");
            _pgShop = NewPage("PageShop");
            _pgCheckIn = NewPage("PageCheckIn");
            _pgTrade = NewPage("PageTrade");
            AddPageBackground("home", "bg_home");
            AddPageBackground("codex", "bg_codex");
            AddPageBackground("deck", "bg_deckbuilder");
            AddPageBackground("profile", "bg_profile");
            AddPageBackground("net", "bg_home");
            AddPageBackground("shop", "bg_shop");
            AddPageBackground("checkin", "bg_checkin");
            AddPageBackground("trade", "bg_trade");
            BuildHome();
            BuildCodex();
            BuildDeck();
            BuildProfile();
            BuildNet();
            BuildShopPlaceholder();
            BuildCheckIn();
            BuildTradePlaceholder();
            ApplyScale();
        }

        private void Update()
        {
            ApplyScale();
            PumpToast();
            // 开发者模式快捷键:F9 开关(全部卡牌可用 / 货币充足 / 商店免费)
            if (Input.GetKeyDown(KeyCode.F9)) { ToggleDevMode(); }
            if (_nameLayer != null && Input.GetKeyDown(KeyCode.Escape)) { CloseNameDialog(); }
            if (V06Net.MyRole != V06Net.Role.None) { PumpNet(); }
            if (_visible && _page == "net") { RefreshNetStatus(); }
            if (_visible && _page == "home") { RefreshHome(); }
        }

        /// <summary>开发者模式开关入口(资料页按钮与 F9 共用)。</summary>
        public void ToggleDevMode()
        {
            string msg = V06Save.SetDevMode(!V06Save.DevMode);
            Toast(msg + "   [F9 切换]");
            RefreshHome();
            RefreshShop();
            RefreshDeck();
            RefreshCodex();
            RefreshCheckIn();
            RefreshDevBtn();
        }

        private void RefreshDevBtn()
        {
            if (_devBtn == null) { return; }
            Text t = _devBtn.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.text = "开发者模式：" + (V06Save.DevMode ? "开（全卡+无限货币）" : "关") + " [F9]";
                t.color = V06Save.DevMode ? new Color(0.55f, 0.95f, 0.55f) : UiKit.TextMain;
            }
        }

        private void ApplyScale()
        {
            if (_root == null) { return; }
            var parent = _root.parent as RectTransform;
            if (parent == null || parent.rect.height <= 1f) { return; }
            // 必须按"装得下"缩放：窗口不是 16:9 时，只按高度缩放会把左右两边挤出屏幕
            // （实机 exe 在 1560×1040 下 Nav_home / 图鉴格子就整个跑到屏幕外，点不到）
            float s = Mathf.Min(parent.rect.width / RefW, parent.rect.height / RefH);
            if (s > 0f && Mathf.Abs(_root.localScale.x - s) > 0.001f)
            {
                _root.localScale = new Vector3(s, s, 1f);
            }
        }

        private void BuildHeader()
        {
            V06UiV07.Panel(_root, "HeaderBg", 0f, 0f, RefW, 84f, "home_bar_top", new Color(0.10f, 0.115f, 0.15f, 0.98f));
            _avatar = V06UiV07.Icon(_root, "Avatar", 16f, 8f, 68f, "avatar_01", Color.white);
            if (_avatar != null)
            {
                var slot = V06UiV07.Icon(_root, "AvatarSlot", 10f, 2f, 80f, "profile_avatar_slot", Color.white);
                if (slot != null) { slot.transform.SetSiblingIndex(_avatar.transform.GetSiblingIndex()); }
                // 第 2 批动画：头像呼吸光圈（2 帧循环，叠在头像上方）
                _avatarGlow = V06UiV07.Icon(_root, "AvatarGlow", 8f, 0f, 84f, "avatar_01", new Color(1f, 0.95f, 0.7f, 0.55f));
                if (_avatarGlow != null)
                {
                    _avatarGlow.transform.SetSiblingIndex(_avatar.transform.GetSiblingIndex() + 1);
                    if (V06UiV07.HasAnim("anim_avatar_hover")) { V06UiV07.PlayAnim(_avatarGlow, "anim_avatar_hover", 2.5f, true); }
                    else { _avatarGlow.gameObject.SetActive(false); }
                }
            }
            string[] hdrIcons = new string[] { "icon_coin", "icon_gem", "icon_pack" };
            for (int hi = 0; hi < hdrIcons.Length; hi++)
            {
                V06UiV07.Icon(_root, "HdrIcon" + hi, 1000f + hi * 190f, 26f, 30f, hdrIcons[hi], Color.white);
            }
            _nameText = UiKit.Label(_root, "Name", 24f, 10f, 520f, 30f, "", 22, TextAnchor.MiddleLeft, UiKit.Selected);
            _statText = UiKit.Label(_root, "Stat", 24f, 44f, 720f, 26f, "", 15, TextAnchor.MiddleLeft, UiKit.TextDim);
            _coinText = UiKit.Label(_root, "Coins", 980f, 24f, 600f, 34f, "", 18, TextAnchor.MiddleRight, UiKit.TextMain);
        }

        private void BuildNav()
        {
            string[] ids = new string[] { "home", "codex", "deck", "profile", "net", "checkin", "shop", "trade" };
            string[] labels = new string[] { "大厅", "图鉴", "组卡组", "个人资料", "联机对战", "每日签到", "商店", "交换" };
            for (int i = 0; i < ids.Length; i++)
            {
                Button b = V06UiV07.IconButton(_root, "Nav_" + ids[i], 24f + i * 176f, 96f, 168f, 44f,
                    "tab_normal", "icon_nav_" + NavIconKey(ids[i]), labels[i], null);
                string id = ids[i];
                b.onClick.AddListener(delegate { ShowPage(id); });
                _navButtons.Add(b);
                _navIds.Add(id);
            }
            UiKit.Panel(_root, "NavLine", 0f, 148f, RefW, 2f, new Color(0.24f, 0.28f, 0.34f, 1f));
        }

        private RectTransform NewPage(string name)
        {
            RectTransform rt = UiKit.Rect(_root, name, 0f, 150f, RefW, RefH - 150f);
            rt.gameObject.SetActive(false);
            return rt;
        }

        // ───────────────── V0.7 第 2 批动画：通用提示条 ─────────────────

        /// <summary>弹一条提示（notify_toast 底皮 + anim_toast_wipe 扫光）。1.8 秒后自动收起。</summary>
        public void Toast(string msg)
        {
            if (_root == null || string.IsNullOrEmpty(msg)) { return; }
            if (_toastBg == null)
            {
                _toastBg = V06UiV07.Panel(_root, "Toast", 500f, 500f, 600f, 68f, "notify_toast", new Color(0.10f, 0.12f, 0.16f, 0.96f));
                if (_toastBg == null) { return; }
                _toastBg.raycastTarget = false;
                _toastText = UiKit.Label(_toastBg.transform, "T", 20f, 0f, 560f, 68f, "", 18, TextAnchor.MiddleCenter, UiKit.TextMain);
                _toastText.raycastTarget = false;
                _toastBg.gameObject.SetActive(false);
            }
            _toastText.text = msg;
            _toastBg.gameObject.SetActive(true);
            _toastBg.transform.SetAsLastSibling();
            V06UiV07.Pop(_toastBg.rectTransform, 0.18f);
            if (V06UiV07.HasAnim("anim_toast_wipe"))
            {
                var go = new GameObject("ToastWipe", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_toastBg.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var im = go.GetComponent<Image>();
                im.raycastTarget = false;
                im.type = Image.Type.Sliced;      // 第 2 批九宫格 16/0/16/0
                V06UiV07.PlayOnce(im, "anim_toast_wipe", 14f);
                Destroy(go, 0.7f);
            }
            _toastHideAt = Time.unscaledTime + 1.8f;
        }

        private void PumpToast()
        {
            if (_toastBg != null && _toastBg.gameObject.activeSelf && Time.unscaledTime > _toastHideAt)
            {
                _toastBg.gameObject.SetActive(false);
            }
        }

        /// <summary>在一个 Image 上叠一层一次性动画（播完销毁，不改底图）。</summary>
        public static void OverlayOnce(Image host, string animName, float fps, float life)
        {
            if (host == null || !V06UiV07.HasAnim(animName)) { return; }
            var go = new GameObject("Fx_" + animName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var im = go.GetComponent<Image>();
            im.raycastTarget = false;
            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
            V06UiV07.PlayOnce(im, animName, fps);
            Object.Destroy(go, life <= 0f ? 0.8f : life);
        }

        // ───────────────────────── V0.7 皮肤辅助 ─────────────────────────

        /// <summary>给某个页面挂全屏背景（素材缺失则不加，页面仍是深色底）。</summary>
        private void AddPageBackground(string pageId, string skinName)
        {
            RectTransform page = PageOf(pageId);
            if (page == null) { return; }
            Sprite s = V06UiV07.Skin(skinName);
            if (s == null) { return; }
            var go = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(page, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling();
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.preserveAspect = false;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.92f);
            _pageBg[pageId] = img;
        }

        private RectTransform PageOf(string id)
        {
            switch (id)
            {
                case "home": return _pgHome;
                case "codex": return _pgCodex;
                case "deck": return _pgDeck;
                case "profile": return _pgProfile;
                case "net": return _pgNet;
                case "shop": return _pgShop;
                case "checkin": return _pgCheckIn;
                case "trade": return _pgTrade;
            }
            return null;
        }

        /// <summary>导航图标名（GPT 交的是 icon_nav_*，没有 home 就借星标）。</summary>
        private static string NavIconKey(string id)
        {
            switch (id)
            {
                case "home": return "star";
                case "codex": return "codex";
                case "deck": return "deck";
                case "profile": return "profile";
                case "net": return "battle";
                case "checkin": return "checkin";
                case "shop": return "shop";
                case "trade": return "trade";
            }
            return "star";
        }
        // ───────────────────────── 大厅（主页面） ─────────────────────────

        private Text _homeLevelText, _homeTitleText, _homeStatLine, _homeCollectLine, _homeRecent;
        private Image _homeExpFill, _homeCollectFill;
        private readonly List<Image> _currChips = new List<Image>();
        private readonly List<Text> _currTexts = new List<Text>();
        private readonly List<Image> _entryArt = new List<Image>();
        private readonly List<Button> _entryBtn = new List<Button>();
        private Image _homeAvatarImg;
        private Text _homeNoticeText;

        private void BuildHome()
        {
            // ① 左下：远征者档案卡（头像 + 等级条 + 称号缎带 + 战绩 + 收集进度）
            Image profBg = V06UiV07.Panel(_pgHome, "HomeProfile", 40f, 16f, 470f, 330f, "deck_panel_left", new Color(0.11f, 0.13f, 0.17f, 0.96f));
            profBg.raycastTarget = false;   // 纯装饰底，不能吃点击
            V06UiV07.Icon(_pgHome, "HomeAvatarSlot", 62f, 34f, 128f, "profile_avatar_slot", Color.white);
            _homeAvatarImg = V06UiV07.Icon(_pgHome, "HomeAvatar", 74f, 46f, 104f, "avatar_01", Color.white);
            _nameText = UiKit.Label(_pgHome, "HomeName", 206f, 40f, 290f, 34f, "", 24, TextAnchor.MiddleLeft, UiKit.Selected);
            _homeTitleText = UiKit.Label(_pgHome, "HomeTitleTxt", 206f, 76f, 290f, 28f, "", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            _homeLevelText = UiKit.Label(_pgHome, "HomeLevel", 206f, 106f, 290f, 28f, "", 16, TextAnchor.MiddleLeft, UiKit.TextMain);
            // 等级条
            V06UiV07.Panel(_pgHome, "HomeExpBg", 60f, 172f, 420f, 24f, "codex_progress_bar", new Color(0.10f, 0.11f, 0.14f, 0.9f)).raycastTarget = false;
            _homeExpFill = UiKit.Panel(_pgHome, "HomeExpFill", 62f, 174f, 0f, 20f, new Color(0.85f, 0.68f, 0.32f, 0.95f));
            _homeExpFill.raycastTarget = false;
            _homeStatLine = UiKit.Label(_pgHome, "HomeStat", 60f, 202f, 420f, 26f, "", 15, TextAnchor.MiddleLeft, UiKit.TextDim);
            V06UiV07.Icon(_pgHome, "HomeCollectIcon", 60f, 232f, 26f, "icon_star", Color.white);
            _homeCollectLine = UiKit.Label(_pgHome, "HomeCollect", 92f, 232f, 390f, 26f, "", 15, TextAnchor.MiddleLeft, UiKit.TextMain);
            V06UiV07.Panel(_pgHome, "HomeCollectBg", 60f, 262f, 420f, 18f, "codex_progress_bar", new Color(0.10f, 0.11f, 0.14f, 0.9f)).raycastTarget = false;
            _homeCollectFill = UiKit.Panel(_pgHome, "HomeCollectFill", 62f, 264f, 0f, 14f, new Color(0.45f, 0.72f, 0.55f, 0.95f));
            _homeCollectFill.raycastTarget = false;
            UiKit.Button(_pgHome, "BtnGoProfile2", 60f, 288f, 200f, 40f, "个人资料", UiKit.ButtonNormal,
                delegate { ShowPage("profile"); });

            // ② 货币栏（金币 / 宝石 / 卡包，各带图标 + 数量，可点进货架）
            BuildCurrencyRow(_pgHome, 60f, 340f);

            // ③ 右上 8 个入口大图（对战 / 联机 / 图鉴 / 组卡 / 签到 / 商店 / 资料 / 交换）
            BuildEntryGrid();
        }

        /// <summary>货币栏：三个带图标的胶囊，点一下就跳到商店。</summary>
        private void BuildCurrencyRow(RectTransform page, float x, float y)
        {
            string[] icons = new string[] { "icon_coin", "icon_gem", "icon_pack" };
            string[] labels = new string[] { "金币", "宝石", "卡包" };
            for (int i = 0; i < 3; i++)
            {
                float cx = x + i * 128f;
                Image chip = V06UiV07.Panel(page, "CurrChip" + i, cx, y, 120f, 44f, "tab_normal", new Color(0.13f, 0.15f, 0.2f, 0.95f));
                chip.raycastTarget = true;
                var btn = chip.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(delegate { ShowPage("shop"); });
                V06Audio.ClickOn(btn);
                V06UiV07.Icon(chip.transform, "Ic", 6f, 7f, 30f, icons[i], Color.white);
                Text t = UiKit.Label(chip.transform, "N", 38f, 0f, 76f, 44f, "0", 17, TextAnchor.MiddleLeft, UiKit.TextMain);
                if (i == 0) { t.color = new Color(1f, 0.88f, 0.55f); }
                _currChips.Add(chip);
                _currTexts.Add(t);
                if (labels == null) { break; }
            }
        }

        /// <summary>8 个入口大图（GPT 交的 home_entry_* 优先，没有就用 icon_nav_* 兜底）。</summary>
        private void BuildEntryGrid()
        {
            string[] ids = new string[] { "battle", "net", "codex", "deck", "checkin", "shop", "profile", "trade" };
            string[] names = new string[] { "对战", "联机对战", "卡牌图鉴", "组卡组", "每日签到", "商店", "个人资料", "交换卡牌" };
            string[] subs = new string[] { "挑战 AI", "房间号联机", "252 张卡", "24 张主牌", "领奖励", "买卡包", "战绩胜率", "与玩家换卡" };
            string[] arts = new string[] { "home_entry_battle", "icon_nav_battle", "home_entry_codex", "icon_nav_deck",
                "home_entry_checkin", "home_entry_shop", "home_entry_profile", "icon_nav_trade" };
            string[] anims = new string[] { "anim_entry_battle", "", "anim_entry_codex", "",
                "anim_entry_checkin", "anim_entry_shop", "anim_entry_profile", "" };
            const float x0 = 540f, y0 = 16f, tw = 250f, th = 168f, gapX = 5f, gapY = 6f;
            for (int i = 0; i < ids.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float x = x0 + col * (tw + gapX);
                float y = y0 + row * (th + gapY);
                Image tile = V06UiV07.Panel(_pgHome, "Entry" + ids[i], x, y, tw, th,
                    "panel_sub", new Color(0.12f, 0.14f, 0.19f, 0.96f));
                tile.raycastTarget = true;
                var btn = tile.gameObject.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.97f, 0.86f);
                colors.pressedColor = new Color(0.84f, 0.87f, 0.94f);
                btn.colors = colors;
                string pageId = ids[i];
                btn.onClick.AddListener(delegate { OnEntryClicked(pageId); });
                V06Audio.ClickOn(btn);
                // 图：大的入口图铺右半边，图标兜底就更小
                bool big = arts[i].StartsWith("home_entry");
                Image art = big
                    ? V06UiV07.Icon(tile.transform, "Art", 130f, 10f, 112f, arts[i], Color.white)
                    : V06UiV07.Icon(tile.transform, "Art", 158f, 34f, 66f, arts[i], Color.white);
                if (art != null && !string.IsNullOrEmpty(anims[i])) { V06UiV07.PlayAnim(art, anims[i], 4f, true); }
                UiKit.Label(tile.transform, "T", 12f, 34f, 132f, 30f, names[i], 19, TextAnchor.MiddleLeft, UiKit.Selected);
                UiKit.Label(tile.transform, "S", 12f, 66f, 132f, 40f, subs[i], 14, TextAnchor.UpperLeft, UiKit.TextDim);
                _entryArt.Add(art);
                _entryBtn.Add(btn);
            }

            // ④ 卡组栏（当前卡组 + 换组 + 编辑）
            V06UiV07.Panel(_pgHome, "HomeDeckPanel", 540f, 366f, 500f, 168f, "deck_panel_right", new Color(0.11f, 0.13f, 0.17f, 0.96f)).raycastTarget = false;
            UiKit.Label(_pgHome, "HomeDeckTitle", 560f, 380f, 300f, 28f, "当前卡组", 18, TextAnchor.MiddleLeft, UiKit.Selected);
            _homeDeck = UiKit.Label(_pgHome, "HomeDeck", 560f, 410f, 460f, 60f, "", 16, TextAnchor.UpperLeft, UiKit.TextMain);
            UiKit.Button(_pgHome, "BtnDeckPrev", 560f, 480f, 140f, 40f, "上一套", UiKit.ButtonNormal, delegate { CycleDeck(-1); });
            UiKit.Button(_pgHome, "BtnDeckNext", 712f, 480f, 140f, 40f, "下一套", UiKit.ButtonNormal, delegate { CycleDeck(1); });
            UiKit.Button(_pgHome, "BtnGoDeck", 864f, 480f, 160f, 40f, "编辑卡组", UiKit.ButtonNormal, delegate { ShowPage("deck"); });

            // ⑤ 开始对战（大按钮）
            Button solo = UiKit.Button(_pgHome, "BtnSolo", 540f, 540f, 500f, 76f, "开始对战（挑战 AI）", UiKit.MyCamp, delegate { StartSoloMatch(); });
            try { solo.GetComponentInChildren<Text>().fontSize = 24; } catch (System.Exception) { }

            // ⑥ 公告条（GPT 的 home_notice_ribbon + 循环扫光）
            if (V06UiV07.Skin("home_notice_ribbon") != null)
            {
                Image ribbon = V06UiV07.Panel(_pgHome, "HomeNotice", 1060f, 366f, 500f, 60f, "home_notice_ribbon", new Color(0.13f, 0.15f, 0.2f, 0.95f));
                ribbon.raycastTarget = false;
                if (V06UiV07.HasAnim("anim_notice_scroll")) { V06UiV07.PlayAnim(ribbon, "anim_notice_scroll", 3f, true); }
            }
            _homeNoticeText = UiKit.Label(_pgHome, "HomeNoticeTxt", 1080f, 368f, 460f, 56f, "", 15, TextAnchor.MiddleLeft, UiKit.TextMain);

            // ⑦ 今日提示（原文字说明保留一小块，避免玩家不知道联机怎么用）
            UiKit.Label(_pgHome, "HomeTip", 1060f, 436f, 500f, 74f,
                "联机：点「联机对战」→ 房主建房间（房间号 1–999 + 密码）→ 另一台机器输入同一房间号加入；"
                + "同一台电脑也能测：开第二个游戏窗口即可。",
                14, TextAnchor.UpperLeft, UiKit.TextDim);

            // ⑦.5 最近对局（右侧下半区，填满视觉）
            _homeRecent = UiKit.Label(_pgHome, "HomeRecent", 1060f, 516f, 500f, 96f, "", 14, TextAnchor.UpperLeft, UiKit.TextMain);

            // ⑧ 收藏进度条（整页底部）
            UiKit.Label(_pgHome, "HomeFoot", 60f, 620f, 1500f, 40f,
                "V0.7：252 张卡（192 基础 + 60 扩展）· 兽群 44 + 少女 16 · 进化线 14 条 · 融合对 8 组",
                14, TextAnchor.MiddleLeft, UiKit.TextDim);
        }

        /// <summary>V0.7：大厅 5 个入口大图（200×200，带悬停动画）。</summary>
        private void BuildHomeEntries()
        {
            string[] skins = new string[] { "home_entry_battle", "home_entry_shop", "home_entry_checkin", "home_entry_codex", "home_entry_profile" };
            string[] anims = new string[] { "anim_entry_battle", "anim_entry_shop", "anim_entry_checkin", "anim_entry_codex", "anim_entry_profile" };
            string[] pages = new string[] { "battle", "shop", "checkin", "codex", "profile" };
            string[] labels = new string[] { "对战", "商店", "签到", "图鉴", "资料" };
            for (int i = 0; i < skins.Length; i++)
            {
                Sprite sp = V06UiV07.Skin(skins[i]);
                if (sp == null) { continue; }
                float x = 40f + i * 232f;
                Image img = UiKit.Panel(_pgHome, "EntryMain" + i, x, 452f, 200f, 200f, Color.white);
                img.sprite = sp;
                img.preserveAspect = true;
                img.raycastTarget = true;
                var btn = img.gameObject.AddComponent<Button>();
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.97f, 0.88f);
                colors.pressedColor = new Color(0.85f, 0.88f, 0.95f);
                btn.colors = colors;
                string pageId = pages[i];
                btn.onClick.AddListener(delegate { OnEntryClicked(pageId); });
                V06Audio.ClickOn(btn);
                V06UiV07.PlayAnim(img, anims[i], 4f, true);
                UiKit.Label(_pgHome, "EntryLabel" + i, x, 658f, 200f, 28f, labels[i], 16, TextAnchor.MiddleCenter, UiKit.Selected);
            }
        }

        private void OnEntryClicked(string pageId)
        {
            if (pageId == "battle") { StartSoloMatch(); return; }
            ShowPage(pageId);
        }

        private void RefreshHome()
        {
            if (V06Save.Data == null) { return; }
            V06SaveData d = V06Save.Data;
            int distinct = V06Save.DistinctOwned();
            int all = V06Catalog.All.Count;
            _nameText.text = d.playerName;
            _statText.text = "Lv." + d.level + " · " + d.title
                + "　·　对局 " + d.matches + "　胜率 " + d.WinRate + "%　收集 " + distinct + "/" + all;
            _coinText.text = "金币 " + d.coins + "　宝石 " + d.gems + "　卡包 " + d.packs;
            if (_currTexts.Count >= 3)
            {
                _currTexts[0].text = d.coins.ToString();
                _currTexts[1].text = d.gems.ToString();
                _currTexts[2].text = d.packs.ToString();
            }
            if (_homeTitleText != null) { _homeTitleText.text = "称号：" + d.title; }
            if (_homeLevelText != null) { _homeLevelText.text = "Lv." + d.level + "　经验 " + d.exp + "/100"; }
            if (_homeStatLine != null)
            {
                _homeStatLine.text = "对局 " + d.matches + "　胜 " + d.wins + "　负 " + d.losses + "　平 " + d.draws
                    + "　胜率 " + d.WinRate + "%";
            }
            if (_homeExpFill != null)
            {
                // 经验为 0 时留 4px 的"空条"，否则控件宽度为 0，看起来像坏掉了
                float p = Mathf.Clamp01(d.exp / 100f);
                _homeExpFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, 416f * p), 20f);
            }
            if (_homeCollectLine != null)
            {
                _homeCollectLine.text = "收藏 " + distinct + "/" + all + " 种 · 共 " + V06Save.TotalOwned() + " 张";
            }
            if (_homeCollectFill != null)
            {
                float p = all <= 0 ? 0f : Mathf.Clamp01(distinct / (float)all);
                _homeCollectFill.rectTransform.sizeDelta = new Vector2(Mathf.Max(4f, 416f * p), 14f);
            }
            if (_homeNoticeText != null)
            {
                _homeNoticeText.text = V06Save.CanCheckIn()
                    ? "公告：今天还没签到，点「每日签到」领 " + V06Save.CheckInRewardText(((d.streak) % 7) + 1) + "。"
                    : "公告：今日已签到（连续 " + d.streak + " 天）。商店每日特惠" + (V06Shop.DailyAvailable ? "可买。" : "已售完。");
            }
            V06DeckSave deck = CurrentDeck();
            if (_homeRecent != null)
            {
                System.Text.StringBuilder rb = new System.Text.StringBuilder();
                rb.AppendLine("最近对局");
                int shown = 0;
                for (int i = 0; i < d.records.Count && shown < 3; i++)
                {
                    V06RecordEntry r = d.records[i];
                    string mark = r.result > 0 ? "胜" : (r.result == 0 ? "平" : "负");
                    rb.AppendLine(mark + "　" + r.myDeck + " vs " + r.foeDeck + "　" + r.rounds + " 轮　旗 " + r.myFlag + ":" + r.foeFlag);
                    shown++;
                }
                if (shown == 0) { rb.Append("还没有对局记录，点「开始对战」打一局。"); }
                _homeRecent.text = rb.ToString();
            }
            if (_homeDeck != null)
            {
                _homeDeck.text = (deck == null ? "（无卡组）" : deck.name + "\n" + deck.MainCount + "/24 主牌 · "
                    + (deck.IsLegal() ? "可开局 ✔" : "还不合法 ✘"))
                    + (V06Save.Data.decks.Count > 1 ? "\n（" + (_deckIndex + 1) + "/" + V06Save.Data.decks.Count + " 套）" : "");
            }
        }

        private V06DeckSave CurrentDeck()
        {
            if (V06Save.Data == null || V06Save.Data.decks.Count == 0) { return null; }
            _deckIndex = Mathf.Clamp(_deckIndex, 0, V06Save.Data.decks.Count - 1);
            return V06Save.Data.decks[_deckIndex];
        }

        private void CycleDeck(int delta)
        {
            if (V06Save.Data == null || V06Save.Data.decks.Count == 0) { return; }
            _deckIndex = (_deckIndex + delta + V06Save.Data.decks.Count) % V06Save.Data.decks.Count;
            RefreshHome();
        }

        // ───────────────────────── 单人开局 ─────────────────────────

        private void StartSoloMatch()
        {
            V06DeckSave mine = CurrentDeck();
            if (mine == null) { Toast("还没有卡组，先去「组卡组」建一套。"); ShowPage("deck"); return; }
            if (!mine.IsLegal())
            {
                // 玩家视角的兜底：当前卡组不合法时不要"点了没反应"，自动换成合法的那套并说明
                int legalIdx = -1;
                for (int i = 0; i < V06Save.Data.decks.Count; i++)
                {
                    if (V06Save.Data.decks[i] != null && V06Save.Data.decks[i].IsLegal()) { legalIdx = i; break; }
                }
                if (legalIdx >= 0 && legalIdx != _deckIndex)
                {
                    _deckIndex = legalIdx;
                    mine = V06Save.Data.decks[legalIdx];
                    RefreshHome();
                    Toast("「" + (string.IsNullOrEmpty(mine.name) ? "另一套" : mine.name) + "」才合法，已自动切换。");
                }
                else
                {
                    Toast("当前卡组 " + mine.MainCount + "/24 主牌，还不能开局——去「组卡组」补满 24 张。");
                    ShowPage("deck");
                    return;
                }
            }
            if (_battle == null) { _battle = FindObjectOfType<V06BattleUI>(); }
            if (_battle == null) { Toast("战斗界面没找到，请重启游戏。"); return; }
            int seed = (int)(System.DateTime.Now.Ticks % 1000000);
            _matchSeed = seed;
            string err;
            V06Precon foe = V06DeckLibrary.Get(Random.Range(0, 8));
            if (!_battle.StartMatchDeck(mine.ToPrecon(), foe, seed, false, 0, out err))
            {
                Debug.LogWarning("[V06Shell] 开局失败：" + err);
                Toast("开局失败：" + err);
                return;
            }
            _battle.paused = false;
            _matchRunning = true;
            Hide();
        }

        // ───────────────────────── 图鉴 ─────────────────────────

        private void BuildCodex()
        {
            string[] ids = new string[] { "all", "unit", "tactic", "equip", "other", "exp" };
            string[] labels = new string[] { "全部", "单位", "战术", "装备/附印/战况", "其他", "V0.7 扩展" };
            for (int i = 0; i < ids.Length; i++)
            {
                Button b = UiKit.Button(_pgCodex, "CodexTab_" + ids[i], 40f + i * 186f, 8f, 176f, 40f, labels[i], UiKit.ButtonNormal, null);
                string id = ids[i];
                b.onClick.AddListener(delegate { _codexFilter = id; _codexPage = 0; RefreshCodex(); });
            }
            for (int i = 0; i < 28; i++)
            {
                int col = i % 7;
                int row = i / 7;
                float x = 40f + col * 160f;
                float y = 56f + row * 160f;
                Image bg = V06UiV07.Panel(_pgCodex, "CodexCell" + i, x, y, 150f, 152f, "codex_cell", UiKit.CardBg);
                Text t = UiKit.Label(bg.transform, "T", 4f, 4f, 142f, 150f, "", 14, TextAnchor.MiddleCenter, UiKit.TextMain);
                Button b = bg.gameObject.AddComponent<Button>();
                int copy = i;
                b.onClick.AddListener(delegate { OnCodexCell(copy); });
                _codexCellBg.Add(bg);
                _codexCellText.Add(t);
                _codexCellBtn.Add(b);
            }
            _codexPageText = UiKit.Label(_pgCodex, "CodexPage", 40f, 700f, 400f, 30f, "", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Button(_pgCodex, "CodexPrev", 40f, 700f, 120f, 40f, "上一页", UiKit.ButtonNormal, delegate { _codexPage--; RefreshCodex(); });
            UiKit.Button(_pgCodex, "CodexNext", 170f, 700f, 120f, 40f, "下一页", UiKit.ButtonNormal, delegate { _codexPage++; RefreshCodex(); });
            var host = new GameObject("CodexDetailHost", typeof(RectTransform));
            host.transform.SetParent(_pgCodex, false);
            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(1160f, -60f);
            rt.sizeDelta = new Vector2(400f, 560f);
            _codexDetail = host.AddComponent<V06CardInfoPanel>();
            _codexDetail.Build(rt);
        }

        private bool CodexFilterMatch(V06Card c)
        {
            switch (_codexFilter)
            {
                case "unit": return c.Category == V06Category.Unit || c.Category == V06Category.Token;
                case "tactic": return c.Category == V06Category.Tactic;
                case "equip": return c.Category == V06Category.Equipment || c.Category == V06Category.Seal
                                      || c.Category == V06Category.Status;
                case "other": return c.Category != V06Category.Unit && c.Category != V06Category.Token
                                      && c.Category != V06Category.Tactic && c.Category != V06Category.Equipment
                                      && c.Category != V06Category.Seal && c.Category != V06Category.Status && !c.Exp;
                case "exp": return c.Exp;
                default: return !c.Exp || true;
            }
        }

        private void RefreshCodex()
        {
            _codexIds.Clear();
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (CodexFilterMatch(c)) { _codexIds.Add(c.Id); }
            }
            int perPage = _codexCellBg.Count;
            if (perPage <= 0) { return; }   // 脚本重载会把列表字段清空但界面还在，这里兜一下
            int maxPage = Mathf.Max(0, (_codexIds.Count - 1) / perPage);
            _codexPage = Mathf.Clamp(_codexPage, 0, maxPage);
            for (int i = 0; i < perPage; i++)
            {
                int idx = _codexPage * perPage + i;
                bool has = idx < _codexIds.Count;
                _codexCellBg[i].gameObject.SetActive(has);
                if (!has) { continue; }
                V06Card c = V06Catalog.ById(_codexIds[idx]);
                int owned = V06Save.OwnedOf(_codexIds[idx]);
                if (owned > 0)
                {
                    _codexCellText[i].text = c.Name + "\n" + (c.Cost >= 0 ? c.Cost + " 费" : "—")
                        + (c.Category == V06Category.Unit || c.Category == V06Category.Token ? "\n" + c.Atk + "/" + c.Hp : "")
                        + "\n×" + owned;
                    _codexCellBg[i].color = c.Exp ? new Color(0.20f, 0.16f, 0.24f) : new Color(0.16f, 0.20f, 0.26f);
                    _codexCellText[i].color = UiKit.TextMain;
                }
                else
                {
                    _codexCellText[i].text = "? ? ?\n未获得\n" + c.Id;
                    _codexCellBg[i].color = new Color(0.10f, 0.11f, 0.13f);
                    _codexCellText[i].color = UiKit.TextDim;
                }
                // 第 2 批动画：进化线终态（"传说级"）格子挂一层循环光辉
                bool legend = owned > 0 && V06Expansion.LineOf(c.Id) > 0 && V06Expansion.EvolutionOf(c.Id) == null;
                Image glow;
                if (!_codexGlow.TryGetValue(i, out glow))
                {
                    glow = null;
                    if (V06UiV07.HasAnim("anim_rarity_glow_5"))
                    {
                        glow = UiKit.Panel(_codexCellBg[i].transform, "LegendGlow", 5f, 6f, 140f, 140f, new Color(1f, 1f, 1f, 0.7f));
                        glow.raycastTarget = false;
                        glow.transform.SetSiblingIndex(0);      // 光在文字下面
                        V06UiV07.PlayAnim(glow, "anim_rarity_glow_5", 2.2f, true);
                    }
                    if (glow != null) { _codexGlow[i] = glow; }
                }
                if (glow != null && glow.gameObject.activeSelf != legend) { glow.gameObject.SetActive(legend); }
            }
            _codexPageText.text = "第 " + (_codexPage + 1) + "/" + (maxPage + 1) + " 页　共 " + _codexIds.Count + " 张";
            if (_codexIds.Count > 0 && _codexDetail != null)
            {
                ShowCodexDetail(_codexIds[Mathf.Min(_codexPage * perPage, _codexIds.Count - 1)]);
            }
        }

        private void OnCodexCell(int i)
        {
            int perPage = _codexCellBg.Count;
            int idx = _codexPage * perPage + i;
            if (idx < 0 || idx >= _codexIds.Count) { return; }
            ShowCodexDetail(_codexIds[idx]);
        }

        private void ShowCodexDetail(string cardId)
        {
            if (_codexDetail == null) { return; }
            V06Card c = V06Catalog.ById(cardId);
            if (c == null) { return; }
            V06CardView v = new V06CardView();
            v.CardId = c.Id;
            v.Name = c.Name;
            v.CategoryText = CategoryName(c.Category) + (string.IsNullOrEmpty(c.Tags) ? "" : "　" + c.Tags);
            v.Cost = c.Cost;
            v.Atk = c.Atk;
            v.Hp = c.Hp;
            v.CurrentAtk = c.Atk;
            v.CurrentHp = c.Hp;
            v.Damage = 0;
            v.KeywordText = "关键词：" + (c.Keywords == null || c.Keywords.Length == 0 ? "无" : string.Join("、", c.Keywords))
                + "　持有：" + V06Save.OwnedOf(c.Id) + " 张";
            string effect = c.Text;
            List<string> fuse = V06Expansion.FusionPartners(c.Id);
            if (fuse.Count > 0) { effect += "\n可融合：" + string.Join("、", fuse.ToArray()); }
            if (V06Expansion.LineOf(c.Id) > 0)
            {
                int line = V06Expansion.LineOf(c.Id);
                effect += "\n进化线：" + V06Expansion.LineName(line)
                    + (V06Expansion.EvolutionOf(c.Id) == null ? "（终态）" : "→ " + CardNameOf(V06Expansion.EvolutionOf(c.Id)));
            }
            v.EffectText = effect;
            v.TermText = V06Terms.Block(c);
            v.ArtPath = "Art/units/" + c.Id;
            v.CanPlayNow = V06Save.OwnedOf(c.Id) > 0;
            _codexDetail.Show(v);
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

        private static string CardNameOf(string id)
        {
            V06Card c = V06Catalog.ById(id);
            return c == null ? id : c.Name;
        }

        // ───────────────────────── 组卡 ─────────────────────────

        private void BuildDeck()
        {
            string[] tabs = new string[] { "unit", "tactic", "extra" };
            string[] labels = new string[] { "单位（8 种 ×2）", "战术（2 种 ×2）", "额外牌（至多 4 张）" };
            for (int i = 0; i < tabs.Length; i++)
            {
                Button b = UiKit.Button(_pgDeck, "DeckTab_" + tabs[i], 40f + i * 226f, 8f, 214f, 40f, labels[i], UiKit.ButtonNormal, null);
                string id = tabs[i];
                b.onClick.AddListener(delegate { _deckTab = id; _deckPickPage = 0; _deckSelId = null; RefreshDeck(); });
                _deckTabTexts[i] = b.GetComponentInChildren<Text>();
            }

            // 卡片墙:8 列 × 3 行 = 24 张/页;立绘用 3:4(与战斗手牌同比例,等比放大到 124×165)
            // 格子高度 192 → 198：底部文字是两行(名称 + “2费 3/3”)，原来只留 24px，
            // 实测两行 11 号需要 25px，会被 Truncate 切掉最后一行（大厅巡检报的"纵向溢出 24"就是这个）。
            for (int i = 0; i < 24; i++)
            {
                int col = i % 8;
                int row = i / 8;
                float x = 40f + col * 133f;
                float y = 56f + row * 204f;
                Image bg = UiKit.Panel(_pgDeck, "DeckCell" + i, x, y, 126f, 198f, UiKit.CardBg);
                Image art = UiKit.Panel(bg.transform, "Art", 1f, 1f, 124f, 165f, new Color(0.10f, 0.11f, 0.14f, 1f));
                art.raycastTarget = false;
                art.preserveAspect = true;
                Text t = UiKit.Label(bg.transform, "T", 1f, 167f, 124f, 30f, "", 11, TextAnchor.MiddleCenter, UiKit.TextMain);
                t.raycastTarget = false;
                Text badge = UiKit.Label(bg.transform, "Badge", 1f, 1f, 122f, 20f, "", 12, TextAnchor.UpperRight, new Color(0.55f, 1f, 0.6f));
                badge.raycastTarget = false;
                Button b = bg.gameObject.AddComponent<Button>();
                int copy = i;
                b.onClick.AddListener(delegate { OnDeckCell(copy); });
                _deckCellBg.Add(bg);
                _deckCellArt.Add(art);
                _deckCellText.Add(t);
                _deckCellBadge.Add(badge);
            }

            _deckPageText = UiKit.Label(_pgDeck, "DeckPage", 380f, 676f, 420f, 30f, "", 15, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Button(_pgDeck, "DeckPrev", 40f, 672f, 120f, 38f, "上一页", UiKit.ButtonNormal, delegate { _deckPickPage--; RefreshDeck(); });
            UiKit.Button(_pgDeck, "DeckNext", 170f, 672f, 120f, 38f, "下一页", UiKit.ButtonNormal, delegate { _deckPickPage++; RefreshDeck(); });
            UiKit.Button(_pgDeck, "DeckClear2", 810f, 672f, 130f, 38f, "清空", UiKit.ButtonNormal, delegate { ClearDeck(); });
            UiKit.Button(_pgDeck, "DeckNew2", 950f, 672f, 170f, 38f, "新建(复制)", UiKit.ButtonNormal, delegate { NewDeck(); });

            // 左侧底部:卡组清单
            _deckSummary = UiKit.Label(_pgDeck, "DeckSummary", 40f, 716f, 1060f, 168f, "", 14, TextAnchor.UpperLeft, UiKit.TextMain);

            // 右侧:卡牌详情(复用到图鉴同一个面板)
            var host = new GameObject("DeckDetailHost", typeof(RectTransform));
            host.transform.SetParent(_pgDeck, false);
            var rt = host.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(1128f, -14f);
            rt.sizeDelta = new Vector2(430f, 470f);
            _deckDetail = host.AddComponent<V06CardInfoPanel>();
            _deckDetail.Build(rt);
            _deckDetail.Hide();

            _deckDetailHint = UiKit.Label(_pgDeck, "DeckHint", 1128f, 462f, 430f, 30f, "点左边任意一张卡 → 在这里看详情,再决定加入", 14, TextAnchor.MiddleLeft, UiKit.TextDim);

            Button addBtn = UiKit.Button(_pgDeck, "DeckAdd", 1128f, 496f, 210f, 46f, "加入卡组", new Color(0.24f, 0.36f, 0.22f), delegate { DeckAddSelected(); });
            UiKit.Button(_pgDeck, "DeckDel", 1348f, 496f, 210f, 46f, "取消选择", new Color(0.36f, 0.24f, 0.22f), delegate { DeckRemoveSelected(); });
            if (addBtn == null) { Debug.LogWarning("[Deck] 加入按钮创建失败"); }

            Button saveBtn = UiKit.Button(_pgDeck, "DeckSave", 1128f, 554f, 430f, 44f, "保存卡组", UiKit.MyCamp, delegate { SaveDeck(); });
            _deckSaveBtnImg = saveBtn == null ? null : saveBtn.GetComponent<Image>();
            UiKit.Button(_pgDeck, "DeckFill", 1128f, 604f, 430f, 44f, "一键填充到合法（8+2+4）", new Color(0.24f, 0.34f, 0.22f), delegate { FillDeck(); });
            UiKit.Button(_pgDeck, "DeckUse", 1128f, 654f, 430f, 52f, "用这套打一局", UiKit.MyCamp, delegate { StartSoloMatch(); });
            // ---- 我的卡组列表(每页 4 套,带翻页;按钮单独一行,不重叠)----
            UiKit.Label(_pgDeck, "DeckListTitle", 1128f, 692f, 200f, 22f, "我的卡组", 15, TextAnchor.MiddleLeft, UiKit.Selected);
            // 提示：双击改名（下面三个按钮之外的第二条入口，鼠标用户更顺手）
            UiKit.Label(_pgDeck, "DeckListHint", 1264f, 692f, 294f, 22f, "双击任意卡组名可改名", 12,
                TextAnchor.MiddleRight, UiKit.TextDim);
            for (int i = 0; i < DeckRowsPerPage; i++)
            {
                Image bg = UiKit.Panel(_pgDeck, "DeckRow" + i, 1128f, 716f + i * 28f, 430f, 26f, UiKit.CardBg);
                Text t = UiKit.Label(bg.transform, "T", 6f, 0f, 418f, 26f, "", 13, TextAnchor.MiddleLeft, UiKit.TextMain);
                Button b = bg.gameObject.AddComponent<Button>();
                int copy = i;
                b.onClick.AddListener(delegate { OnDeckRowClicked(copy); });
                _deckListBg.Add(bg);
                _deckListText.Add(t);
            }
            UiKit.Button(_pgDeck, "DeckListPrev", 1128f, 832f, 130f, 26f, "▲ 上一页", UiKit.ButtonNormal, delegate { _deckListPage--; RefreshDeck(); });
            _deckListPageText = UiKit.Label(_pgDeck, "DeckListPage", 1264f, 832f, 158f, 26f, "", 13, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Button(_pgDeck, "DeckListNext", 1428f, 832f, 130f, 26f, "下一页 ▼", UiKit.ButtonNormal, delegate { _deckListPage++; RefreshDeck(); });
            UiKit.Button(_pgDeck, "DeckNewEmpty", 1128f, 862f, 138f, 30f, "新建空卡组", new Color(0.24f, 0.34f, 0.22f), delegate { NewDeck(); });
            UiKit.Button(_pgDeck, "DeckRename", 1272f, 862f, 138f, 30f, "重命名", UiKit.ButtonNormal, delegate { RenameDeck(); });
            UiKit.Button(_pgDeck, "DeckDelete", 1416f, 862f, 138f, 30f, "删除", new Color(0.38f, 0.24f, 0.22f), delegate { DeleteDeck(); });
        }

        private void UnitPicker(RectTransform page)
        {
            // 占位：真正的列表在 RefreshDeck 里填充
        }

        private List<string> DeckPickPool()
        {
            List<string> pool = new List<string>();
            // 开发者模式：卡池放开成"全目录 252 张"，不按持有量／类别／费用过滤，
            // 也不再排除未接入的响应卡，方便直接组出任意组合验证规则。
            // 注意：组出来的卡组仍要过 IsLegal（24 张、同名≤2、预备 3+3）才能开局。
            if (V06Save.DevMode)
            {
                for (int i = 0; i < V06Catalog.All.Count; i++) { pool.Add(V06Catalog.All[i].Id); }
                return pool;
            }
            for (int i = 0; i < V06Catalog.All.Count; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (V06Save.OwnedOf(c.Id) <= 0) { continue; }
                // 未接入的响应窗口卡(X02/X04/X06)不进入普通主牌
                if (c.Id == "X02" || c.Id == "X04" || c.Id == "X06") { continue; }
                if (_deckTab == "unit")
                {
                    if (c.Category != V06Category.Unit) { continue; }
                    if (c.Cost < 1 || c.Cost > 4) { continue; }   // 进化形态不可部署
                }
                else if (_deckTab == "tactic")
                {
                    if (c.Category != V06Category.Tactic) { continue; }
                }
                else
                {
                    if (c.Category == V06Category.Unit || c.Category == V06Category.Tactic
                        || c.Category == V06Category.Token || c.Category == V06Category.Weather
                        || c.Category == V06Category.Encounter || c.Category == V06Category.Event) { continue; }
                    if (c.Cost < 0) { continue; }
                }
                pool.Add(c.Id);
            }
            return pool;
        }

        private void RefreshDeck()
        {
            V06DeckSave deck = CurrentDeck();
            List<string> pool = DeckPickPool();
            int perPage = _deckCellBg.Count;
            if (perPage <= 0) { return; }
            int maxPage = Mathf.Max(0, (pool.Count - 1) / perPage);
            _deckPickPage = Mathf.Clamp(_deckPickPage, 0, maxPage);
            // 页签文案随模式变：开发者模式下明确写出"全卡池、上限已放开"，免得看着像 bug
            if (_deckTabTexts[0] != null)
            {
                bool dev = V06Save.DevMode;
                _deckTabTexts[0].text = dev ? "单位（全部·至多 12 种）" : "单位（8 种 ×2）";
                _deckTabTexts[1].text = dev ? "战术（全部·至多 12 种）" : "战术（2 种 ×2）";
                _deckTabTexts[2].text = dev ? "额外牌（全部·至多 24 张）" : "额外牌（至多 4 张）";
            }

            for (int i = 0; i < perPage; i++)
            {
                int idx = _deckPickPage * perPage + i;
                bool has = idx < pool.Count;
                _deckCellBg[i].gameObject.SetActive(has);
                if (!has) { continue; }
                string id = pool[idx];
                V06Card c = V06Catalog.ById(id);
                if (c == null) { continue; }

                bool picked = deck != null && (Contains(deck.units, id) || Contains(deck.tactics, id) || Contains(deck.extras, id));
                bool isSelected = _deckSelId == id;

                Sprite sp = Resources.Load<Sprite>("Art/units/" + id);
                _deckCellArt[i].sprite = sp;
                _deckCellArt[i].color = sp == null ? new Color(0.10f, 0.11f, 0.14f, 1f) : Color.white;
                _deckCellText[i].text = c.Name + "\n" + (c.Cost >= 0 ? c.Cost + "费" : "")
                    + (c.Category == V06Category.Unit ? " " + c.Atk + "/" + c.Hp : "");
                _deckCellText[i].color = picked ? new Color(0.62f, 1f, 0.62f) : UiKit.TextMain;
                _deckCellBadge[i].text = picked ? "✔ 已选" : "";
                _deckCellBg[i].color = isSelected ? new Color(0.30f, 0.34f, 0.20f)
                    : (picked ? new Color(0.20f, 0.30f, 0.20f) : UiKit.CardBg);
            }

            _deckPageText.text = "第 " + (_deckPickPage + 1) + "/" + (maxPage + 1) + " 页　共 " + pool.Count + " 张可选（点卡片看详情）";

            int totalDecks = V06Save.Data == null ? 0 : V06Save.Data.decks.Count;
            int maxDeckPage = Mathf.Max(0, (totalDecks - 1) / DeckRowsPerPage);
            _deckListPage = Mathf.Clamp(_deckListPage, 0, maxDeckPage);
            if (_deckListPageText != null)
            {
                _deckListPageText.text = "第 " + (_deckListPage + 1) + "/" + (maxDeckPage + 1) + " 页　共 " + totalDecks + " 套";
            }
            for (int i = 0; i < _deckListText.Count; i++)
            {
                int di = _deckListPage * DeckRowsPerPage + i;
                bool has = V06Save.Data != null && di < V06Save.Data.decks.Count;
                _deckListBg[i].gameObject.SetActive(has);
                if (!has) { continue; }
                V06DeckSave d = V06Save.Data.decks[di];
                bool cur = di == _deckIndex;
                _deckListText[i].text = (cur ? "▶ " : "　") + (string.IsNullOrEmpty(d.name) ? "未命名" : d.name)
                    + "　" + d.MainCount + "/24" + (d.IsLegal() ? " ✔" : "")
                    + "　" + d.units.Length + "单位 " + d.tactics.Length + "战术";
                _deckListText[i].color = cur ? new Color(0.62f, 1f, 0.62f) : UiKit.TextMain;
                _deckListBg[i].color = cur ? new Color(0.20f, 0.30f, 0.20f) : UiKit.CardBg;
            }
            // 当前套不在本页时,自动翻到它所在页
            if (V06Save.Data != null && totalDecks > 0)
            {
                int curPage = _deckIndex / DeckRowsPerPage;
                if (curPage != _deckListPage) { }
            }

            if (deck == null) { _deckSummary.text = "（没有卡组）"; return; }
            int uN = deck.units.Length, tN = deck.tactics.Length;
            int eN = deck.extras == null ? 0 : deck.extras.Length;
            int gN = deck.upgrades == null ? 0 : deck.upgrades.Length;
            int pN = deck.supports == null ? 0 : deck.supports.Length;
            bool okMain = deck.MainCount == 24;
            _deckSummary.text = "【" + deck.name + "】主牌 " + deck.MainCount + "/24 " + (okMain ? "✔" : "✘ 还差 " + (24 - deck.MainCount) + " 张")
                + "　单位 " + uN + " 种(" + (uN * 2) + "张)　战术 " + tN + " 种(" + (tN * 2) + "张)　额外 " + eN + " 张"
                + "　预备 " + gN + "+" + pN + "/6"
                + "　" + (deck.IsLegal() ? "【可以开局】" : "【还不能开局：" + deck.LegalProblems() + "】")
                + "\n单位：" + JoinNames(deck.units)
                + "\n战术：" + JoinNames(deck.tactics)
                + "\n额外：" + JoinNames(deck.extras);
        }

        private static bool Contains(string[] arr, string id)
        {
            if (arr == null) { return false; }
            for (int i = 0; i < arr.Length; i++) { if (arr[i] == id) { return true; } }
            return false;
        }

        private static string JoinNames(string[] arr)
        {
            if (arr == null || arr.Length == 0) { return "（空）"; }
            List<string> names = new List<string>();
            for (int i = 0; i < arr.Length; i++) { names.Add(CardNameOf(arr[i])); }
            return string.Join("、", names.ToArray());
        }

        /// <summary>点卡片墙:只在右侧显示详情,不改卡组。</summary>
        private void OnDeckCell(int i)
        {
            List<string> pool = DeckPickPool();
            int idx = _deckPickPage * _deckCellBg.Count + i;
            if (idx < 0 || idx >= pool.Count) { return; }
            _deckSelId = pool[idx];
            ShowDeckDetail(_deckSelId);
            RefreshDeck();
        }

        /// <summary>把当前选中的卡显示到右侧详情面板。</summary>
        private void ShowDeckDetail(string id)
        {
            V06Card c = V06Catalog.ById(id);
            if (c == null || _deckDetail == null) { return; }
            V06CardView v = new V06CardView();
            v.CardId = c.Id;
            v.Name = c.Name;
            v.CategoryText = CategoryName(c.Category) + (string.IsNullOrEmpty(c.Tags) ? "" : "　" + c.Tags);
            v.Cost = c.Cost;
            v.Atk = c.Atk;
            v.Hp = c.Hp;
            v.CurrentAtk = c.Atk;
            v.CurrentHp = c.Hp;
            v.KeywordText = c.Keywords == null ? "" : string.Join("、", c.Keywords);
            v.EffectText = c.Text;
            v.TermText = V06Terms.Block(c);
            v.ArtPath = "Art/units/" + c.Id;
            v.CanPlayNow = V06Save.OwnedOf(c.Id) > 0;
            _deckDetail.Show(v);
            if (_deckDetailHint != null)
            {
                V06DeckSave deck = CurrentDeck();
                bool inDeck = deck != null && (Contains(deck.units, id) || Contains(deck.tactics, id) || Contains(deck.extras, id));
                string howMany = c.Category == V06Category.Unit ? "（这种会占 2 张）"
                    : (c.Category == V06Category.Tactic ? "（这种会占 2 张）" : "（占 1 张）");
                _deckDetailHint.text = (inDeck ? "已在卡组中 " : "未加入 ") + howMany + "　持有 " + V06Save.OwnedOf(id);
            }
        }

        /// <summary>把选中的卡加进卡组。</summary>
        private void DeckAddSelected()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null || string.IsNullOrEmpty(_deckSelId)) { Toast("先在左边点一张卡"); return; }
            V06Card c = V06Catalog.ById(_deckSelId);
            if (c == null) { return; }
            // 归入哪一栏：正式模式按当前页签；开发者模式按卡的类别自动归位，
            // 因为开发者模式的卡池是全目录 252 张，页签里什么都有，
            // 按页签硬塞会把"天气牌"写进 units 数组，卡组结构就错了。
            string bucket = _deckTab;
            if (V06Save.DevMode)
            {
                if (c.Category == V06Category.Unit) { bucket = "unit"; }
                else if (c.Category == V06Category.Tactic) { bucket = "tactic"; }
                else { bucket = "extra"; }
            }
            List<string> list = new List<string>();
            if (bucket == "unit") { list.AddRange(deck.units); }
            else if (bucket == "tactic") { list.AddRange(deck.tactics); }
            else if (deck.extras != null) { list.AddRange(deck.extras); }
            if (list.Contains(_deckSelId)) { Toast("这张已经在卡组里了"); return; }
            // 上限随模式变：正式模式 8 种单位 / 2 种战术 / 4 张额外牌；
            // 开发者模式放开到 IsLegal 允许的上限（单位 12 种、战术 12 种、额外 24 张），
            // 否则"卡池里有全部卡牌"也没法真的组进卡组去验证。
            int maxUnitTypes = V06Save.DevMode ? 12 : 8;
            int maxTacticTypes = V06Save.DevMode ? 12 : 2;
            int maxExtras = V06Save.DevMode ? 24 : 4;
            if (bucket == "unit" && list.Count >= maxUnitTypes) { Toast("单位最多 " + maxUnitTypes + " 种，先取消一张"); return; }
            if (bucket == "tactic" && list.Count >= maxTacticTypes) { Toast("战术最多 " + maxTacticTypes + " 种，先取消一张"); return; }
            if (bucket == "extra" && list.Count >= maxExtras) { Toast("额外牌最多 " + maxExtras + " 张，先取消一张"); return; }
            list.Add(_deckSelId);
            if (bucket == "unit") { deck.units = list.ToArray(); }
            else if (bucket == "tactic") { deck.tactics = list.ToArray(); }
            else { deck.extras = list.ToArray(); }
            if (string.IsNullOrEmpty(deck.vanguard) || !Contains(deck.units, deck.vanguard))
            {
                deck.vanguard = deck.units.Length > 0 ? deck.units[0] : null;
            }
            Toast("已加入：" + c.Name + "　主牌 " + deck.MainCount + "/24");
            ShowDeckDetail(_deckSelId);
            RefreshDeck();
        }

        /// <summary>把选中的卡从卡组移除。</summary>
        private void DeckRemoveSelected()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null || string.IsNullOrEmpty(_deckSelId)) { Toast("先在左边点一张卡"); return; }
            V06Card c = V06Catalog.ById(_deckSelId);
            bool removed = false;
            List<string> u = new List<string>(deck.units);
            if (u.Remove(_deckSelId)) { deck.units = u.ToArray(); removed = true; }
            List<string> t = new List<string>(deck.tactics);
            if (t.Remove(_deckSelId)) { deck.tactics = t.ToArray(); removed = true; }
            if (deck.extras != null)
            {
                List<string> e = new List<string>(deck.extras);
                if (e.Remove(_deckSelId)) { deck.extras = e.ToArray(); removed = true; }
            }
            if (removed)
            {
                if (deck.units.Length == 0) { deck.vanguard = null; }
                else if (!Contains(deck.units, deck.vanguard)) { deck.vanguard = deck.units[0]; }
                Toast("已移出：" + (c == null ? _deckSelId : c.Name) + "　主牌 " + deck.MainCount + "/24");
            }
            else { Toast("这张不在卡组里"); }
            ShowDeckDetail(_deckSelId);
            RefreshDeck();
        }

        private void SaveDeck()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            if (deck.upgrades == null || deck.upgrades.Length != 3)
            {
                deck.upgrades = PickReserve(V06Category.Upgrade, 3);
            }
            if (deck.supports == null || deck.supports.Length != 3)
            {
                deck.supports = PickReserve(V06Category.Support, 3);
            }
            V06Save.Save();
            RefreshDeck();
            RefreshHome();
            // 第 2 批动画：保存确认（扫光 + 提示条）
            if (_deckSaveBtnImg != null) { OverlayOnce(_deckSaveBtnImg, "anim_save_confirm", 12f, 0.8f); }
            Toast(deck.IsLegal() ? "卡组已保存（合法，可开局）" : "卡组已保存，但还不合法：需要 8 单位 + 2 战术 + 24 张主牌");
        }

        private static string[] PickReserve(V06Category cat, int n)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < V06Catalog.All.Count && r.Count < n; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (c.Category != cat) { continue; }
                if (V06Save.OwnedOf(c.Id) <= 0) { continue; }
                r.Add(c.Id);
            }
            return r.ToArray();
        }

        /// <summary>一键填充成合法卡组:8 单位 + 2 战术 + 4 额外,并补 3 晋阶 + 3 支援。</summary>
        private void FillDeck()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            List<string> units = PickByCategory(V06Category.Unit, 8, true);
            List<string> tactics = PickByCategory(V06Category.Tactic, 2, false);
            List<string> extras = PickExtras(4);
            if (units.Count < 8 || tactics.Count < 2)
            {
                Toast("可用卡不够:单位 " + units.Count + "/8、战术 " + tactics.Count + "/2");
                return;
            }
            deck.units = units.ToArray();
            deck.tactics = tactics.ToArray();
            deck.extras = extras.ToArray();
            deck.vanguard = deck.units.Length > 0 ? deck.units[0] : null;
            deck.upgrades = PickReserve(V06Category.Upgrade, 3);
            deck.supports = PickReserve(V06Category.Support, 3);
            V06Save.Save();
            RefreshDeck();
            Toast(deck.IsLegal()
                ? "已填充合法卡组：主牌 " + deck.MainCount + "/24，可以开局"
                : "已填充，但主牌只有 " + deck.MainCount + "/24，请手动补额外牌");
        }

        /// <summary>按类型挑前 n 张持有卡(单位只收费用 1–4 的可部署形态)。</summary>
        private static List<string> PickByCategory(V06Category cat, int n, bool costLimit)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < V06Catalog.All.Count && r.Count < n; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (c.Category != cat) { continue; }
                if (costLimit && (c.Cost < 1 || c.Cost > 4)) { continue; }
                if (V06Save.OwnedOf(c.Id) <= 0) { continue; }
                r.Add(c.Id);
            }
            return r;
        }

        /// <summary>额外牌池:装备/附印/支援/晋阶/状态/响应等非单位非战术牌。</summary>
        private static List<string> PickExtras(int n)
        {
            List<string> r = new List<string>();
            for (int i = 0; i < V06Catalog.All.Count && r.Count < n; i++)
            {
                V06Card c = V06Catalog.All[i];
                if (c.Category == V06Category.Unit || c.Category == V06Category.Tactic
                    || c.Category == V06Category.Token || c.Category == V06Category.Weather
                    || c.Category == V06Category.Encounter || c.Category == V06Category.Event) { continue; }
                if (c.Cost < 0) { continue; }
                if (V06Save.OwnedOf(c.Id) <= 0) { continue; }
                r.Add(c.Id);
            }
            return r;
        }

        private void ClearDeck()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            deck.units = new string[0];
            deck.tactics = new string[0];
            deck.extras = new string[0];
            deck.vanguard = null;
            RefreshDeck();
        }

        /// <summary>新建一套空卡组(不复制当前),并弹出命名框。</summary>
        private void NewDeck()
        {
            V06DeckSave d = new V06DeckSave();
            d.name = "我的卡组 " + (V06Save.Data.decks.Count + 1);
            d.vanguard = null;
            d.units = new string[0];
            d.tactics = new string[0];
            d.extras = new string[0];
            d.upgrades = new string[0];
            d.supports = new string[0];
            V06Save.Data.decks.Add(d);
            _deckIndex = V06Save.Data.decks.Count - 1;
            _deckListPage = _deckIndex / DeckRowsPerPage;
            _deckSelId = null;
            V06Save.Save();
            RefreshDeck();
            Toast("已新建空卡组,先给它起个名字");
            ShowNameDialog(d.name);
        }

        /// <summary>点列表第 row 行(按当前页换算成真实下标)。</summary>
        private void SelectDeckRow(int row)
        {
            int i = _deckListPage * DeckRowsPerPage + row;
            SelectDeck(i);
        }

        // 双击改名：记录上一次点的行与时间，同一行在 0.38 秒内再点一次就算双击
        private int _deckRowLastRow = -2;
        private float _deckRowLastTime = -10f;
        private const float DeckRowDoubleClickWindow = 0.38f;

        /// <summary>
        /// 卡组列表行点击：单击 = 切到这套，双击 = 弹出改名框（改的就是双击的那一套）。
        /// 注意先切换再弹框：ApplyDeckName 改的是"当前卡组"，不先切过去就会改错套。
        /// </summary>
        private void OnDeckRowClicked(int row)
        {
            int abs = _deckListPage * DeckRowsPerPage + row;
            float now = Time.unscaledTime;
            bool dbl = (_deckRowLastRow == abs) && (now - _deckRowLastTime <= DeckRowDoubleClickWindow);
            _deckRowLastRow = abs;
            _deckRowLastTime = now;

            SelectDeck(abs);
            if (!dbl) { return; }

            _deckRowLastRow = -2;                      // 用掉这次双击，避免三连击重复弹
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            ShowNameDialog(deck.name);
        }

        /// <summary>切换到第 i 套卡组。</summary>
        private void SelectDeck(int i)
        {
            if (V06Save.Data == null || i < 0 || i >= V06Save.Data.decks.Count) { return; }
            if (_deckIndex == i) { return; }
            _deckIndex = i;
            _deckSelId = null;
            _deckListPage = i / DeckRowsPerPage;
            RefreshDeck();
            Toast("当前卡组：" + V06Save.Data.decks[i].name);
        }

        /// <summary>重命名当前卡组。</summary>
        private void RenameDeck()
        {
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            ShowNameDialog(deck.name);
        }

        /// <summary>删除当前卡组(至少保留一套)。</summary>
        private void DeleteDeck()
        {
            if (V06Save.Data == null || V06Save.Data.decks.Count <= 1) { Toast("至少要保留一套卡组"); return; }
            V06DeckSave deck = CurrentDeck();
            if (deck == null) { return; }
            string name = deck.name;
            V06Save.Data.decks.RemoveAt(_deckIndex);
            _deckIndex = Mathf.Clamp(_deckIndex, 0, V06Save.Data.decks.Count - 1);
            _deckListPage = _deckIndex / DeckRowsPerPage;
            _deckSelId = null;
            V06Save.Save();
            RefreshDeck();
            Toast("已删除卡组：" + name);
        }

        /// <summary>卡组命名弹窗。</summary>
        public void ShowNameDialog(string initial)
        {
            if (_nameLayer != null) { Destroy(_nameLayer.gameObject); _nameLayer = null; }
            var layer = new GameObject("NameLayer", typeof(RectTransform));
            layer.transform.SetParent(_root, false);
            var lrt = layer.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lrt.SetAsLastSibling();
            _nameLayer = lrt;

            Image mask = UiKit.Panel(lrt, "Mask", 0f, 0f, RefW, RefH, new Color(0f, 0f, 0f, 0.62f));
            mask.raycastTarget = true;
            Image panel = UiKit.Panel(lrt, "NamePanel", 500f, 330f, 600f, 230f, new Color(0.11f, 0.13f, 0.17f, 0.99f));
            UiKit.Label(panel.transform, "Title", 24f, 16f, 552f, 32f, "给卡组起个名字", 20, TextAnchor.MiddleLeft, UiKit.Selected);

            var inputGo = new GameObject("NameInput", typeof(RectTransform));
            inputGo.transform.SetParent(panel.transform, false);
            var irt = inputGo.GetComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0f, 1f);
            irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(24f, -60f);
            irt.sizeDelta = new Vector2(552f, 50f);
            Image inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            Text inText = UiKit.Label(inputGo.transform, "Text", 12f, 4f, 528f, 42f, "", 18, TextAnchor.MiddleLeft, UiKit.TextMain);
            inText.supportRichText = false;
            var input = inputGo.AddComponent<InputField>();
            input.textComponent = inText;
            input.targetGraphic = inputBg;
            input.characterLimit = 12;
            input.text = string.IsNullOrEmpty(initial) ? "" : initial;
            _deckNameInput = input;

            UiKit.Button(panel.transform, "Ok", 210f, 154f, 170f, 46f, "确定", UiKit.MyCamp, delegate
            {
                string v = _deckNameInput == null ? null : _deckNameInput.text;
                ApplyDeckName(v);
            });
            UiKit.Button(panel.transform, "Cancel", 400f, 154f, 170f, 46f, "取消", UiKit.ButtonNormal, delegate { CloseNameDialog(); });
            // 回车确认
            input.onEndEdit.AddListener(delegate (string v)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { ApplyDeckName(v); }
            });
            // 抢键盘焦点:同一帧 ActivateInputField 常常无效,下一帧再选中一次
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }
            input.ActivateInputField();
            input.Select();
            StartCoroutine(FocusNameInputNextFrame(input));
        }

        /// <summary>下一帧再抢一次焦点,保证能直接打字。</summary>
        private System.Collections.IEnumerator FocusNameInputNextFrame(InputField input)
        {
            yield return null;
            yield return null;
            if (input == null || _nameLayer == null) { yield break; }
            if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(input.gameObject); }
            input.ActivateInputField();
            input.MoveTextEnd(false);
        }

        private void ApplyDeckName(string name)
        {
            V06DeckSave deck = CurrentDeck();
            if (deck != null)
            {
                string n = string.IsNullOrEmpty(name) ? deck.name : name.Trim();
                if (string.IsNullOrEmpty(n)) { n = "未命名卡组"; }
                if (n.Length > 12) { n = n.Substring(0, 12); }
                deck.name = n;
                V06Save.Save();
                Toast("已命名为：" + n);
            }
            CloseNameDialog();
            RefreshDeck();
        }

        private void CloseNameDialog()
        {
            if (_nameLayer != null) { Destroy(_nameLayer.gameObject); _nameLayer = null; }
            _deckNameInput = null;
        }

        // ───────────────────────── 资料 ─────────────────────────

        private void BuildProfile()
        {
            V06UiV07.Panel(_pgProfile, "ProfLeft", 40f, 8f, 620f, 700f, "panel_main", new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgProfile, "ProfTitle", 64f, 28f, 560f, 32f, "战绩总览", 22, TextAnchor.MiddleLeft, UiKit.Selected);
            // 第 2 批动画：胜率光晕（垫在战绩文字下面）
            _winrateGlow = UiKit.Panel(_pgProfile, "WinrateGlow", 60f, 62f, 260f, 130f, new Color(1f, 1f, 1f, 0.42f));
            _winrateGlow.raycastTarget = false;
            if (V06UiV07.HasAnim("anim_winrate_glow")) { V06UiV07.PlayAnim(_winrateGlow, "anim_winrate_glow", 2.4f, true); }
            else { _winrateGlow.gameObject.SetActive(false); }
            _profileStats = UiKit.Label(_pgProfile, "ProfStats", 64f, 76f, 560f, 600f, "", 17, TextAnchor.UpperLeft, UiKit.TextMain);
            V06UiV07.Panel(_pgProfile, "ProfRight", 700f, 8f, 860f, 700f, "panel_main", new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgProfile, "ProfRecords", 724f, 28f, 800f, 32f, "最近对局", 22, TextAnchor.MiddleLeft, UiKit.Selected);
            for (int i = 0; i < 14; i++)
            {
                Text t = UiKit.Label(_pgProfile, "Record" + i, 724f, 72f + i * 42f, 812f, 38f, "", 15, TextAnchor.MiddleLeft, UiKit.TextDim);
                _recordTexts.Add(t);
            }
            _devBtn = UiKit.Button(_pgProfile, "ProfDev", 336f, 640f, 300f, 44f, "开发者模式：关 [F9]", UiKit.ButtonNormal, delegate
            {
                ToggleDevMode();
                RefreshDevBtn();
            });
            UiKit.Button(_pgProfile, "ProfReset", 64f, 640f, 260f, 44f, "开发:清空战绩", UiKit.ButtonNormal, delegate
            {
                V06Save.Data.records.Clear();
                V06Save.Data.matches = 0; V06Save.Data.wins = 0; V06Save.Data.losses = 0; V06Save.Data.draws = 0;
                V06Save.Data.roundsSum = 0; V06Save.Data.flagDiffSum = 0;
                V06Save.Save();
                RefreshProfile();
            });
        }

        private void RefreshProfile()
        {
            V06SaveData d = V06Save.Data;
            if (d == null) { return; }
            if (d.matches <= 0)
            {
                _profileStats.text = "还没有战绩。\n完成第一局后，这里会显示胜负、胜率与平均轮数。\n\n"
                    + "收集：" + V06Save.DistinctOwned() + " 种 / " + V06Save.TotalOwned() + " 张\n"
                    + "等级：" + d.level + "（" + d.exp + "/100）　称号：" + d.title + "\n"
                    + "连续签到：" + d.streak + " 天";
                for (int i = 0; i < _recordTexts.Count; i++) { _recordTexts[i].text = i == 0 ? "（暂无对局记录）" : ""; }
                return;
            }
            _profileStats.text = "对局：" + d.matches + "\n胜：" + d.wins + "　负：" + d.losses + "　平：" + d.draws
                + "\n胜率：" + d.WinRate + "%\n平均轮数：" + d.AvgRounds + "\n累计旗差：" + d.flagDiffSum
                + "\n等级：" + d.level + "（" + d.exp + "/100）\n称号：" + d.title
                + "\n收集：" + V06Save.DistinctOwned() + " 种 / " + V06Save.TotalOwned() + " 张"
                + "\n连续签到：" + d.streak + " 天";
            for (int i = 0; i < _recordTexts.Count; i++)
            {
                if (i >= d.records.Count) { _recordTexts[i].text = ""; continue; }
                V06RecordEntry r = d.records[i];
                string mark = r.result > 0 ? "胜" : (r.result == 0 ? "平" : "负");
                _recordTexts[i].text = r.time + "　" + mark + "　" + r.myDeck + " vs " + r.foeDeck
                    + "　" + r.rounds + " 轮　旗 " + r.myFlag + ":" + r.foeFlag + "　" + r.mode;
                _recordTexts[i].color = r.result > 0 ? new Color(0.55f, 0.9f, 0.65f)
                    : (r.result == 0 ? UiKit.TextDim : new Color(0.9f, 0.55f, 0.55f));
            }
        }

        // ───────────────────────── 联机 ─────────────────────────

        private void BuildNet()
        {
            UiKit.Panel(_pgNet, "NetPanel", 40f, 8f, 760f, 660f, new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgNet, "NetTitle", 64f, 28f, 700f, 32f, "局域网联机（房间号 + 密码）", 22, TextAnchor.MiddleLeft, UiKit.Selected);
            UiKit.Label(_pgNet, "NetNameL", 64f, 84f, 120f, 40f, "玩家名", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            _nameInput = MakeInput(_pgNet, "NetNameIn", 200f, 84f, 520f, 40f, "远征者", false);
            UiKit.Label(_pgNet, "NetRoomL", 64f, 140f, 120f, 40f, "房间号", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            _roomInput = MakeInput(_pgNet, "NetRoomIn", 200f, 140f, 200f, 40f, "1", false);
            _roomInput.characterLimit = 3;
            UiKit.Label(_pgNet, "NetRoomTip", 420f, 140f, 360f, 40f, "1–999", 14, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Label(_pgNet, "NetPwL", 64f, 196f, 120f, 40f, "密码", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            _pwInput = MakeInput(_pgNet, "NetPwIn", 200f, 196f, 300f, 40f, "", true);
            UiKit.Label(_pgNet, "NetPwTip", 520f, 196f, 260f, 40f, "可留空", 14, TextAnchor.MiddleLeft, UiKit.TextDim);
            UiKit.Label(_pgNet, "NetIpL", 64f, 224f, 120f, 30f, "房主 IP", 14, TextAnchor.MiddleLeft, UiKit.TextDim);
            _hostIpInput = MakeInput(_pgNet, "NetIpIn", 200f, 224f, 300f, 34f, "", false);
            UiKit.Label(_pgNet, "NetIpTip", 520f, 224f, 260f, 30f, "跨网段/VPN 才需要填", 13, TextAnchor.MiddleLeft, UiKit.TextDim);

            UiKit.Button(_pgNet, "NetHost", 64f, 262f, 240f, 56f, "建房间（我当房主）", UiKit.MyCamp, delegate { DoHost(); });
            UiKit.Button(_pgNet, "NetJoin", 320f, 262f, 240f, 56f, "加入房间", UiKit.ButtonNormal, delegate { DoJoin(); });
            UiKit.Button(_pgNet, "NetClose", 576f, 262f, 160f, 56f, "断开", UiKit.ButtonNormal, delegate { V06Net.Close(); RefreshNetStatus(); });

            _netStatus = UiKit.Label(_pgNet, "NetStatus", 64f, 336f, 700f, 160f, "", 16, TextAnchor.UpperLeft, UiKit.TextMain);
            // 第 2 批动画：连接中旋转指示（未连接/已连接都收起，只有"半连接"时转）
            _netSpinner = V06UiV07.Icon(_pgNet, "NetSpinner", 690f, 340f, 56f, "loading_spin", Color.white);
            if (_netSpinner == null)
            {
                _netSpinner = UiKit.Panel(_pgNet, "NetSpinner", 690f, 340f, 56f, 56f, new Color(0.8f, 0.85f, 0.95f, 0.8f));
            }
            _netSpinner.raycastTarget = false;
            if (V06UiV07.HasAnim("anim_loading_spin")) { V06UiV07.PlayAnim(_netSpinner, "anim_loading_spin", 12f, true); }
            _netSpinner.gameObject.SetActive(false);
            _netDeckText = UiKit.Label(_pgNet, "NetDeck", 64f, 500f, 700f, 40f, "", 16, TextAnchor.MiddleLeft, UiKit.TextDim);
            _netStartBtn = UiKit.Button(_pgNet, "NetStart", 64f, 556f, 300f, 56f, "开始联机对局", UiKit.Selected, delegate { HostBeginMatch(); });
            UiKit.Button(_pgNet, "NetDeckCycle", 380f, 556f, 220f, 56f, "换卡组", UiKit.ButtonNormal, delegate { CycleDeck(1); RefreshNetStatus(); });

            UiKit.Panel(_pgNet, "NetHelp", 840f, 8f, 720f, 660f, new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgNet, "NetHelpT", 864f, 28f, 660f, 32f, "怎么连（PC 端）", 20, TextAnchor.MiddleLeft, UiKit.Selected);
            UiKit.Label(_pgNet, "NetHelpBody", 864f, 72f, 660f, 560f,
                "① 房主点「建房间」，输入房间号（1–999）与密码，其他人用同样的房间号+密码点「加入房间」。\n\n"
                + "② 同一台机器测试：开第二个游戏窗口（或编辑器再进一次 Play），一个建房、一个加入；"
                + "房间号一致即可，客户端会用 UDP 广播自动找到房主。\n\n"
                + "③ 同一局域网：两台 PC 都连同一个路由器即可，不需要填 IP；"
                + "如果 Windows 防火墙弹窗，请允许程序访问专用网络。\n\n"
                + "④ 跨互联网：目前是局域网直连方案。要跨网需要端口映射或用转发服务器（后续再做）。\n\n"
                + "⑤ 房主必须先「开始联机对局」，双方才会同时进入战斗；进入后由当前行动方出牌，"
                + "另一方只收到结果（锁步，两边用同一套确定性引擎）。",
                14, TextAnchor.UpperLeft, UiKit.TextDim);
        }

        private InputField MakeInput(RectTransform parent, string name, float x, float y, float w, float h,
            string initial, bool password)
        {
            Image bg = UiKit.Panel(parent, name, x, y, w, h, new Color(0.07f, 0.09f, 0.12f, 1f));
            var input = bg.gameObject.AddComponent<InputField>();
            Text text = UiKit.Label(bg.transform, "Text", 10f, 0f, w - 20f, h, initial, 16, TextAnchor.MiddleLeft, UiKit.TextMain);
            text.supportRichText = false;
            input.textComponent = text;
            input.text = initial;
            input.targetGraphic = bg;
            if (password) { input.contentType = InputField.ContentType.Password; }
            return input;
        }

        private void DoHost()
        {
            string err;
            int room = ParseRoom();
            string pw = _pwInput == null ? "" : _pwInput.text;
            string name = _nameInput == null ? "远征者" : _nameInput.text;
            if (!V06Net.StartHost(room, pw, name, out err)) { _netStatus.text = err; return; }
            _battle = FindObjectOfType<V06BattleUI>();
            RefreshNetStatus();
        }

        private void DoJoin()
        {
            string err;
            int room = ParseRoom();
            string pw = _pwInput == null ? "" : _pwInput.text;
            string name = _nameInput == null ? "远征者" : _nameInput.text;
            string ip = _hostIpInput == null ? "" : _hostIpInput.text;
            if (!V06Net.StartJoin(room, pw, name, ip, out err)) { _netStatus.text = err; return; }
            V06NetMsg deckMsg = new V06NetMsg();
            deckMsg.t = "deck";
            V06DeckSave mine = CurrentDeck();
            deckMsg.payload = mine == null ? "" : JsonUtility.ToJson(mine);
            V06Net.Send(deckMsg);
            RefreshNetStatus();
        }

        private int ParseRoom()
        {
            int room = 1;
            if (_roomInput != null) { int.TryParse(_roomInput.text, out room); }
            return Mathf.Clamp(room, V06Net.MinRoom, V06Net.MaxRoom);
        }

        private void RefreshNetStatus()
        {
            if (_netStatus == null) { return; }
            V06DeckSave deck = CurrentDeck();
            _netDeckText.text = "本局卡组：" + (deck == null ? "（无）" : deck.name + "　" + deck.MainCount + "/24");
            string myIp = V06Net.MyRole == V06Net.Role.Host ? "　本机 IP：" + V06Net.LocalIp : "";
            _netStatus.text = "角色：" + (V06Net.MyRole == V06Net.Role.Host ? "房主" :
                (V06Net.MyRole == V06Net.Role.Client ? "客机" : "未连接"))
                + "　房间：" + (V06Net.Room > 0 ? V06Net.Room.ToString() : "—")
                + "　状态：" + (V06Net.Connected ? "已连接" : "未连接") + myIp
                + "\n对手：" + (V06Net.PeerName ?? "—")
                + "\n\n消息：" + V06Net.Status;
            if (_netStartBtn != null)
            {
                bool hostReady = V06Net.MyRole == V06Net.Role.Host && V06Net.Connected && !V06Net.MatchStarted;
                _netStartBtn.GetComponent<Image>().color = hostReady ? UiKit.Selected : new Color(0.2f, 0.22f, 0.26f);
            }
            if (_netSpinner != null)
            {
                // 旋转指示：已建/已加房间但还没连上对手时转圈
                bool spinning = V06Net.MyRole != V06Net.Role.None && !V06Net.Connected;
                if (_netSpinner.gameObject.activeSelf != spinning) { _netSpinner.gameObject.SetActive(spinning); }
            }
        }

        private string _guestDeckJson = "";

        /// <summary>房主按下「开始联机对局」：把种子与双方卡组发给对手，然后双方一起开局。</summary>
        private void HostBeginMatch()
        {
            if (V06Net.MyRole != V06Net.Role.Host || !V06Net.Connected) { return; }
            V06DeckSave mine = CurrentDeck();
            if (mine == null || !mine.IsLegal()) { V06Net.Status = "自己的卡组还不合法（要 8 单位 + 2 战术 + 24 主牌）"; return; }
            V06DeckSave guest = null;
            if (!string.IsNullOrEmpty(_guestDeckJson))
            {
                try { guest = JsonUtility.FromJson<V06DeckSave>(_guestDeckJson); } catch (System.Exception) { }
            }
            if (guest == null) { V06Net.Status = "还没收到对手的卡组（请对手重新点一次加入）"; return; }
            int seed = (int)(System.DateTime.Now.Ticks % 1000000);
            V06NetMsg start = new V06NetMsg();
            start.t = "start";
            start.seed = seed;
            start.payload = JsonUtility.ToJson(mine);
            start.payload2 = JsonUtility.ToJson(guest);
            start.camp = 1;
            V06Net.Send(start);
            V06Net.MatchStarted = true;
            BeginNetBattle(mine, guest, seed, 0);
        }

        private void BeginNetBattle(V06DeckSave a, V06DeckSave b, int seed, int myCamp)
        {
            if (_battle == null) { _battle = FindObjectOfType<V06BattleUI>(); }
            if (_battle == null) { return; }
            string err;
            if (!_battle.StartMatchDeck(a.ToPrecon(), b.ToPrecon(), seed, true, myCamp, out err))
            {
                V06Net.Status = "开局失败：" + err;
                return;
            }
            _battle.paused = false;
            _matchRunning = true;
            _matchSeed = seed;
            Hide();
        }

        private void PumpNet()
        {
            List<V06NetMsg> msgs = V06Net.Drain();
            for (int i = 0; i < msgs.Count; i++)
            {
                V06NetMsg m = msgs[i];
                if (m.t == "deck")
                {
                    _guestDeckJson = m.payload;
                    V06Net.Status = (V06Net.PeerName ?? "对手") + " 的卡组已收到，点「开始联机对局」";
                }
                else if (m.t == "start")
                {
                    V06DeckSave hostDeck = null, guestDeck = null;
                    try { hostDeck = JsonUtility.FromJson<V06DeckSave>(m.payload); } catch (System.Exception) { }
                    try { guestDeck = JsonUtility.FromJson<V06DeckSave>(m.payload2); } catch (System.Exception) { }
                    if (hostDeck == null || guestDeck == null) { continue; }
                    V06Net.MatchStarted = true;
                    V06Net.Status = "对局开始";
                    BeginNetBattle(hostDeck, guestDeck, m.seed, 1);
                }
                else if (m.t == "act")
                {
                    if (_battle == null) { _battle = FindObjectOfType<V06BattleUI>(); }
                    if (_battle != null) { _battle.ApplyRemoteAction(m); }
                }
                else if (m.t == "bye")
                {
                    V06Net.Close();
                    V06Net.Status = "对手离开了房间";
                    RefreshNetStatus();
                }
            }
        }

        // ───────────────────────── 商店（真的能买） ─────────────────────────

        private readonly List<Image> _shopShelf = new List<Image>();
        private readonly List<Text> _shopPriceText = new List<Text>();
        private readonly List<Button> _shopBuyBtn = new List<Button>();
        private readonly List<Image> _shopSoldOut = new List<Image>();
        private readonly List<Text> _shopCountText = new List<Text>();
        private Text _shopCurrencyText, _shopLogText;
        private Image _shopPackOwnedImg;

        private void BuildShopPlaceholder()
        {
            // 标题 + 货币
            UiKit.Label(_pgShop, "ShopT", 40f, 4f, 500f, 40f, "商店", 26, TextAnchor.MiddleLeft, UiKit.Selected);
            _shopCurrencyText = UiKit.Label(_pgShop, "ShopCurr", 560f, 8f, 700f, 34f, "", 18, TextAnchor.MiddleRight, UiKit.TextMain);
            UiKit.Label(_pgShop, "ShopHint", 40f, 44f, 1500f, 26f,
                "买卡包 → 开包得卡（卡池只含可部署牌，费用 1–4）。进化形态与融合体仍然只能靠进化/融合获得。",
                14, TextAnchor.MiddleLeft, UiKit.TextDim);

            // 5 个货架
            for (int i = 0; i < V06Shop.Defs.Length; i++)
            {
                V06Shop.Def d = V06Shop.Defs[i];
                float x = 40f + i * 300f;
                Image shelf = V06UiV07.Panel(_pgShop, "ShopShelf" + i, x, 84f, 280f, 470f,
                    "shop_shelf_frame", new Color(0.12f, 0.14f, 0.19f, 0.96f));
                if (V06UiV07.HasAnim("anim_shop_shelf_pulse")) { V06UiV07.PlayAnim(shelf, "anim_shop_shelf_pulse", 2f, true); }
                V06UiV07.Icon(shelf.transform, "Pack", 44f, 24f, 192f, d.Skin, Color.white);
                UiKit.Label(shelf.transform, "Name", 12f, 222f, 256f, 32f, d.Name, 22, TextAnchor.MiddleCenter, UiKit.Selected);
                UiKit.Label(shelf.transform, "Desc", 16f, 258f, 248f, 56f, d.Desc, 15, TextAnchor.UpperLeft, UiKit.TextDim);

                // 价格牌
                Image tag = V06UiV07.Panel(shelf.transform, "PriceTag", 56f, 320f, 168f, 52f,
                    "shop_price_tag", new Color(0.16f, 0.17f, 0.22f, 0.98f));
                string unit = d.Pay == V06Shop.Currency.Gems ? "宝石" : (d.Pay == V06Shop.Currency.Packs ? "卡包" : "金币");
                Text pt = UiKit.Label(tag.transform, "P", 0f, 0f, 168f, 52f, d.Price + " " + unit, 22,
                    TextAnchor.MiddleCenter, UiKit.TextMain);
                _shopPriceText.Add(pt);
                // 库存/限购
                Text ct = UiKit.Label(shelf.transform, "Count", 16f, 378f, 248f, 26f, "", 14, TextAnchor.MiddleCenter, UiKit.TextDim);
                _shopCountText.Add(ct);
                // 购买按钮
                Button buy = UiKit.Button(shelf.transform, "ShopBuy" + i, 24f, 408f, 232f, 48f, "购买", UiKit.MyCamp, null);
                int copy = i;
                buy.onClick.AddListener(delegate { DoBuy(copy); });
                _shopBuyBtn.Add(buy);
                // 售罄章（默认隐藏）
                Image sold = V06UiV07.Icon(shelf.transform, "SoldOut", 60f, 96f, 160f, "shop_soldout_stamp", Color.white);
                if (sold != null) { sold.gameObject.SetActive(false); _shopSoldOut.Add(sold); }
                else { _shopSoldOut.Add(null); }
                _shopShelf.Add(shelf);
            }

            // 右侧：开包 + 日志
            if (_shopShelf.Count == 0) { return; }
            UiKit.Label(_pgShop, "ShopLogTitle", 40f, 566f, 600f, 28f, "开包记录", 18, TextAnchor.MiddleLeft, UiKit.Selected);
            _shopLogText = UiKit.Label(_pgShop, "ShopLog", 40f, 596f, 1100f, 130f, "", 15, TextAnchor.UpperLeft, UiKit.TextMain);
            Button open = UiKit.Button(_pgShop, "ShopOpenPack", 1180f, 590f, 360f, 56f, "打开库存卡包（0）", new Color(0.22f, 0.26f, 0.32f),
                delegate { DoOpenPack(); });
            open.name = "ShopOpenPack";
            UiKit.Label(_pgShop, "ShopRule", 1180f, 654f, 360f, 76f,
                "对局与签到都会给卡包。商店「购买」= 立刻开卡入库;下方「打开库存卡包」消耗你已有的卡包。", 15, TextAnchor.UpperLeft, UiKit.TextDim);
        }

        private void DoBuy(int i)
        {
            if (i < 0 || i >= V06Shop.Defs.Length) { return; }
            string err;
            List<string> ids;
            List<string> names = V06Shop.Buy(V06Shop.Defs[i], out err, out ids);
            if (names == null)
            {
                if (_shopLogText != null) { _shopLogText.text = "购买失败：" + err; }
                bool soldOut = err != null && (err.Contains("售罄") || err.Contains("限购") || err.Contains("上限"));
                V06Audio.Play(soldOut ? "R20" : "R17");
                Toast("购买失败：" + err);
                return;
            }
            V06Audio.Play("R19");
            if (_shopBuyBtn[i] != null) { OverlayOnce(_shopBuyBtn[i].GetComponent<Image>(), "anim_buy_burst", 12f, 0.8f); }
            if (_shopShelf[i] != null && V06UiV07.HasAnim("anim_price_pop")) { OverlayOnce(_shopShelf[i], "anim_price_pop", 12f, 0.7f); }
            if (_shopLogText != null)
            {
                _shopLogText.text = "开出：" + string.Join("、", names.ToArray());
            }
            Toast("开出 " + names.Count + " 张：" + string.Join("、", names.ToArray()));
            RefreshShop();
            RefreshHome();
        }

        private void DoOpenPack()
        {
            string err;
            List<string> ids;
            List<string> names = V06Shop.OpenPack(out err, out ids);
            if (names == null)
            {
                if (_shopLogText != null) { _shopLogText.text = "开包失败：" + err; }
                Toast("开包失败：" + err);
                return;
            }
            V06Audio.Play("R14");
            if (_shopLogText != null) { _shopLogText.text = "开包（用掉 1 个卡包）：" + string.Join("、", names.ToArray()); }
            Toast("开包：" + string.Join("、", names.ToArray()));
            RefreshShop();
            RefreshHome();
        }

        private void RefreshShop()
        {
            if (V06Save.Data == null) { return; }
            V06SaveData d = V06Save.Data;
            if (_shopCurrencyText != null)
            {
                _shopCurrencyText.text = "金币 " + d.coins + "　宝石 " + d.gems + "　卡包 " + d.packs;
            }
            for (int i = 0; i < V06Shop.Defs.Length && i < _shopBuyBtn.Count; i++)
            {
                V06Shop.Def d2 = V06Shop.Defs[i];
                string why;
                bool can = V06Shop.CanBuy(d2, out why);
                if (_shopBuyBtn[i] != null)
                {
                    _shopBuyBtn[i].GetComponent<Image>().color = can ? UiKit.MyCamp : new Color(0.20f, 0.21f, 0.25f);
                    var txt = _shopBuyBtn[i].GetComponentInChildren<Text>();
                    if (txt != null)
                    {
                        string cur = d2.Pay == V06Shop.Currency.Gems ? "宝石" : (d2.Pay == V06Shop.Currency.Packs ? "卡包" : "金币");
                        // 可买:金色底用深色字;不可买:写明原因,不要只说"买不了"
                        txt.text = can ? (d2.Price + " " + cur + " · 购买 " + d2.Count + " 张") : ("买不了：" + why);
                        txt.color = can ? new Color(0.09f, 0.13f, 0.18f) : new Color(0.85f, 0.70f, 0.66f);
                        txt.fontSize = can ? 17 : 15;
                    }
                }
                if (i < _shopCountText.Count && _shopCountText[i] != null)
                {
                    if (d2.Id == "daily")
                    {
                        _shopCountText[i].text = V06Shop.DailyAvailable ? "今日未购买（每天 1 次）" : "今日已购买";
                    }
                    else { _shopCountText[i].text = "不限次 · 共 " + d2.Count + " 张"; }
                    _shopCountText[i].color = can ? UiKit.TextDim : new Color(0.85f, 0.55f, 0.5f);
                }
                if (i < _shopSoldOut.Count && _shopSoldOut[i] != null)
                {
                    bool sold = d2.Id == "daily" && !V06Shop.DailyAvailable;
                    if (_shopSoldOut[i].gameObject.activeSelf != sold) { _shopSoldOut[i].gameObject.SetActive(sold); }
                }
            }
            // 开包按钮文案
            Transform op = _pgShop.Find("ShopOpenPack");
            if (op != null)
            {
                var t = op.GetComponentInChildren<Text>();
                if (t != null) { t.text = "开一个已购卡包（" + d.packs + "）"; }
                var img = op.GetComponent<Image>();
                if (img != null) { img.color = d.packs > 0 ? UiKit.Selected : new Color(0.20f, 0.21f, 0.25f); }
            }
        }

        private void BuildTradePlaceholder()
        {
            UiKit.Panel(_pgTrade, "TradePanel", 40f, 8f, 1520f, 660f, new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgTrade, "TradeT", 64f, 28f, 900f, 40f, "交换卡牌（待开放）", 24, TextAnchor.MiddleLeft, UiKit.Selected);
            UiKit.Label(_pgTrade, "TradeBody", 64f, 84f, 1460f, 500f,
                "已经设计好但还没做：双方展示位、信封/火漆、在线玩家列表、发起与接受流程。\n\n"
                + "素材清单见同一份文档第 7 节（9 个文件）。\n\n"
                + "实现方式建议：不做真服务端，先做「本地模拟 + 信箱式异步」——"
                + "把自己的 3 张卡装进信封发给某个玩家档案，对方接受后双方库存互换；"
                + "等有服务器时只换传输层，界面与美术完全复用。\n\n"
                + "如果要现在就做，我可以先做本地模拟版（单存档内两个档案互换）。",
                17, TextAnchor.UpperLeft, UiKit.TextDim);
        }

        // ───────────────────────── 签到 ─────────────────────────

        private void BuildCheckIn()
        {
            UiKit.Panel(_pgCheckIn, "CheckPanel", 40f, 8f, 1520f, 660f, new Color(0.11f, 0.13f, 0.17f, 0.96f));
            UiKit.Label(_pgCheckIn, "CheckT", 64f, 28f, 900f, 40f, "每日签到", 24, TextAnchor.MiddleLeft, UiKit.Selected);
            _checkInfo = UiKit.Label(_pgCheckIn, "CheckInfo", 64f, 78f, 900f, 40f, "", 17, TextAnchor.MiddleLeft, UiKit.TextMain);
            for (int i = 0; i < 7; i++)
            {
                Image cell = V06UiV07.Panel(_pgCheckIn, "CheckCell" + i, 64f + i * 200f, 140f, 180f, 220f, "checkin_cell_normal", UiKit.CardBg);
                Text t = UiKit.Label(cell.transform, "T", 8f, 8f, 164f, 204f, "", 16, TextAnchor.MiddleCenter, UiKit.TextMain);
                _checkCells.Add(cell);
                _checkLabels.Add(t);
            }
            UiKit.Button(_pgCheckIn, "CheckBtn", 64f, 400f, 320f, 60f, "领取今日奖励", UiKit.MyCamp, delegate
            {
                string reward = V06Save.DoCheckIn();
                RefreshCheckIn();
                // 领取时盖个章（有关键帧就播帧，没有就用缩放弹一下）
                for (int ci = 0; ci < _checkCells.Count; ci++) { V06UiV07.StopPulse(_checkCells[ci].rectTransform); }
                V06UiV07.PlayOnce(_checkCells[System.Math.Max(0, ((V06Save.Data.streak - 1) % 7))], "anim_checkin_stamp", 20f);
                V06Audio.Play("R22");
                if (reward != null) { _checkInfo.text = "签到成功！" + reward; }
            });
            UiKit.Button(_pgCheckIn, "CheckBack", 400f, 400f, 240f, 60f, "回大厅", UiKit.ButtonNormal, delegate { ShowPage("home"); });
            RefreshCheckIn();
        }

        private void RefreshCheckIn()
        {
            if (V06Save.Data == null) { return; }
            int day = ((V06Save.Data.streak - 1) % 7) + 1;
            if (V06Save.Data.streak <= 0) { day = 1; }
            bool todayDone = !V06Save.CanCheckIn();
            for (int i = 0; i < _checkCells.Count; i++)
            {
                int d = i + 1;
                bool claimed = d < day || (todayDone && d == day);
                bool ready = !todayDone && d == day;
                _checkLabels[i].text = (ready ? "【今日】" : "第 " + d + " 天") + "\n"
                    + V06Save.CheckInRewardText(d) + "\n"
                    + (claimed ? "✔ 已领取" : (ready ? "今天可领" : "未解锁"));
                _checkCells[i].color = claimed ? new Color(0.14f, 0.20f, 0.16f)
                    : (ready ? new Color(0.24f, 0.22f, 0.12f) : UiKit.CardBg);
                _checkLabels[i].color = claimed ? new Color(0.6f, 0.85f, 0.65f)
                    : (ready ? new Color(1f, 0.92f, 0.55f) : new Color(0.62f, 0.64f, 0.70f));
                // V0.7：今天可领的格子呼吸 + 若有关键帧就播帧
                if (ready)
                {
                    V06UiV07.Pulse(_checkCells[i].rectTransform, 2.4f);
                    V06UiV07.PlayAnim(_checkCells[i], "anim_checkin_ready", 20f, true);
                }
                else
                {
                    V06UiV07.StopPulse(_checkCells[i].rectTransform);
                }
            }
            _checkInfo.text = "连续签到 " + V06Save.Data.streak + " 天　本月已签 " + V06Save.Data.monthDays + " 天　"
                + (todayDone ? "今天已签到（明天再来）" : "今天还没签到");
            // 今日已领:主按钮禁用并明确文案,避免玩家反复点
            Transform cb = _pgCheckIn.Find("CheckBtn");
            if (cb != null)
            {
                Button b = cb.GetComponent<Button>();
                Text t = cb.GetComponentInChildren<Text>();
                if (b != null) { b.interactable = !todayDone; }
                if (t != null)
                {
                    t.text = todayDone ? "今日已领取" : "领取今日奖励";
                    t.color = todayDone ? new Color(0.62f, 0.66f, 0.72f) : UiKit.TextMain;
                }
                Image img = cb.GetComponent<Image>();
                if (img != null) { img.color = todayDone ? new Color(0.20f, 0.22f, 0.26f) : UiKit.MyCamp; }
            }
        }

        // ───────────────────────── 页面切换 / 显隐 ─────────────────────────

        public void ShowPage(string id)
        {
            _page = id;
            _pgHome.gameObject.SetActive(id == "home");
            _pgCodex.gameObject.SetActive(id == "codex");
            _pgDeck.gameObject.SetActive(id == "deck");
            _pgProfile.gameObject.SetActive(id == "profile");
            _pgNet.gameObject.SetActive(id == "net");
            _pgShop.gameObject.SetActive(id == "shop");
            _pgCheckIn.gameObject.SetActive(id == "checkin");
            _pgTrade.gameObject.SetActive(id == "trade");
            for (int i = 0; i < _navButtons.Count; i++)
            {
                bool active = _navIds[i] == id;
                _navButtons[i].GetComponent<Image>().color =
                    active ? new Color(0.26f, 0.30f, 0.24f) : UiKit.ButtonNormal;
            }
            // 切页音:回大厅一记收束,其余页签一声轻挑
            if (id == "home" && _lastPageSound != "home") { V06Audio.Play("R05"); }
            else if (id != "home") { V06Audio.Play("R03"); }
            _lastPageSound = id;
            V06UiV07.FadeIn(PageOf(id), 0.16f);
            V06UiV07.PageWipe(_root, null);
            // 大厅用主题曲，其他次级页面切到更安静的民谣管弦（双源交叉淡入，切页不断音）
            V06Audio.Music(id == "home" ? "menu" : "lobby_soft");
            // 商店页每次进入都刷新一次货币与可买状态
            if (id == "shop") { RefreshShop(); }
            if (id == "home") { RefreshHome(); }
            else if (id == "codex") { RefreshCodex(); }
            else if (id == "deck") { RefreshDeck(); }
            else if (id == "profile") { RefreshProfile(); }
            else if (id == "net") { RefreshNetStatus(); }
            else if (id == "checkin") { RefreshCheckIn(); }
        }

        public void Show()
        {
            _visible = true;
            _matchRunning = false;
            if (_canvas != null) { _canvas.gameObject.SetActive(true); }
            if (_battle != null)
            {
                _battle.paused = true;
                // 回大厅时把战斗画布整块藏起来：否则大厅下面会透出棋盘与旗条，看着像"还停在战斗界面"
                _battle.SetVisualVisible(false);
            }
            ShowPage(_page);
            RefreshHome();
        }

        public void Hide()
        {
            _visible = false;
            if (_canvas != null) { _canvas.gameObject.SetActive(false); }
            if (_battle != null) { _battle.SetVisualVisible(true); }
        }

        public bool Visible { get { return _visible; } }
        public string CurrentPage { get { return _page; } }

        /// <summary>测试用：直接切页并回报当前页。</summary>
        public string GoPageForTest(string id)
        {
            if (_root == null) { return "界面已被脚本重载清空，请重新进入 Play"; }
            Show(); ShowPage(id); return _page;
        }
        /// <summary>测试用：显示大厅并切到指定页，返回页面 id。</summary>
        public string ShowPageForTest(string id)
        {
            if (_root == null) { return "界面已被脚本重载清空"; }
            Show(); ShowPage(id); return _page;
        }
        /// <summary>测试用：取某个页面的根节点。</summary>
        public RectTransform PageForTest(string id) { return PageOf(id); }
        /// <summary>测试用：布局根节点。</summary>
        public RectTransform RootForTest { get { return _root; } }
        /// <summary>测试用：只开一张卡的图鉴详情。</summary>
        public void ShowCodexForTest(string cardId) { ShowPage("codex"); ShowCodexDetail(cardId); }
        /// <summary>测试用：读一张卡的持有数。</summary>
        public int OwnedForTest(string cardId) { return V06Save.OwnedOf(cardId); }
        /// <summary>测试用：启用单人对局（返回是否成功）。</summary>
        public bool StartSoloForTest()
        {
            if (V06Save.Data.decks.Count == 0) { return false; }
            V06DeckSave mine = V06Save.Data.decks[0];
            if (mine == null || !mine.IsLegal()) { return false; }
            if (_battle == null) { _battle = FindObjectOfType<V06BattleUI>(); }
            if (_battle == null) { return false; }
            string err;
            return _battle.StartMatchDeck(mine.ToPrecon(), V06DeckLibrary.Get(1), 424242, false, 0, out err);
        }
    }
}
