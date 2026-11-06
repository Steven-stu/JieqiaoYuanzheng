using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 鼠标悬停说明（工具提示）。
    ///
    /// 用 `V06HoverTip.Attach(控件, "标题", "说明")` 挂到任意带 Image 射线的控件上，
    /// 指针进入时在指针旁显示一块说明，离开即隐藏。说明文字必须与引擎实现一致，
    /// 改规则时同步改调用处（战斗按钮的文案集中在 V06BattleUI.AttachActionTips）。
    ///
    /// 实现要点：
    ///   · 提示面板自建在**悬停控件所在的 Canvas** 上，并置为最后一个兄弟节点，
    ///     保证画在同 Canvas 的按钮之上，且跟着 Canvas 的缩放走；
    ///   · 面板与文字全部 raycastTarget=false，不会挡住按钮本身；
    ///   · 面板位置按指针实时跟随，靠近屏幕右/下边缘时自动翻到另一侧并夹紧。
    /// </summary>
    public sealed class V06HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float TipW = 470f;
        private const float TipH = 96f;
        private const float CursorGap = 22f;

        public string Title = "";
        public string Body = "";

        private static GameObject _panel;
        private static RectTransform _panelRt;
        private static Text _titleText;
        private static Text _bodyText;
        private static Canvas _canvas;
        private static V06HoverTip _shown;

        /// <summary>给目标控件挂一条悬停说明（重复调用会覆盖文案）。</summary>
        public static V06HoverTip Attach(GameObject target, string title, string body)
        {
            if (target == null) { return null; }
            V06HoverTip tip = target.GetComponent<V06HoverTip>();
            if (tip == null) { tip = target.AddComponent<V06HoverTip>(); }
            tip.Title = title ?? "";
            tip.Body = body ?? "";
            return tip;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(Body)) { return; }
            Canvas c = GetComponentInParent<Canvas>();
            if (c == null) { return; }
            Show(c);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_shown == this) { Hide(); }
        }

        private void OnDisable()
        {
            if (_shown == this) { Hide(); }
        }

        private void Update()
        {
            if (_shown == this && _panel != null && _panel.activeSelf) { Follow(); }
        }

        // ───────────────────────── 面板 ─────────────────────────

        private static void EnsurePanel(Canvas c)
        {
            if (_panel != null && _canvas == c) { return; }
            if (_panel != null) { Object.Destroy(_panel); _panel = null; }
            _canvas = c;

            RectTransform host = c.transform as RectTransform;
            if (host == null) { return; }

            var go = new GameObject("HoverTip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host, false);
            _panelRt = (RectTransform)go.transform;
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0f, 0.5f);
            _panelRt.sizeDelta = new Vector2(TipW, TipH);
            _panelRt.anchoredPosition = Vector2.zero;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.05f, 0.07f, 0.10f, 0.97f);
            bg.raycastTarget = false;

            // 顶部一条金线，和大厅/面板的描边风格一致
            Image rule = UiKit.Panel(go.transform, "Rule", 0f, 0f, TipW, 2f, new Color(0.92f, 0.79f, 0.54f, 0.9f));
            rule.raycastTarget = false;

            _titleText = UiKit.Label(go.transform, "T", 12f, 8f, TipW - 24f, 24f, "", 16,
                TextAnchor.MiddleLeft, UiKit.Selected);
            _titleText.raycastTarget = false;

            _bodyText = UiKit.Label(go.transform, "B", 12f, 34f, TipW - 24f, TipH - 42f, "", 14,
                TextAnchor.UpperLeft, UiKit.TextMain);
            _bodyText.raycastTarget = false;
            _bodyText.lineSpacing = 1.15f;

            go.SetActive(false);
            _panel = go;
        }

        private void Show(Canvas c)
        {
            EnsurePanel(c);
            if (_panel == null) { return; }
            if (_titleText != null) { _titleText.text = Title; }
            if (_bodyText != null) { _bodyText.text = Body; }
            _panel.transform.SetAsLastSibling();
            _panel.SetActive(true);
            _shown = this;
            Follow();
        }

        private static void Hide()
        {
            _shown = null;
            if (_panel != null) { _panel.SetActive(false); }
        }

        private void Follow()
        {
            if (_panelRt == null || _canvas == null) { return; }
            RectTransform host = _canvas.transform as RectTransform;
            if (host == null) { return; }

            Vector2 screen = Input.mousePosition;
            Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(host, screen, cam, out local)) { return; }

            float halfW = host.rect.width * 0.5f;
            float halfH = host.rect.height * 0.5f;

            // 默认显示在指针右侧偏下；右边放不下就翻到左侧
            float x = local.x + CursorGap;
            if (x + TipW > halfW - 8f) { x = local.x - CursorGap - TipW; }
            if (x < -halfW + 8f) { x = -halfW + 8f; }

            float y = local.y;
            float halfTip = TipH * 0.5f;
            y = Mathf.Clamp(y, -halfH + halfTip + 8f, halfH - halfTip - 8f);

            _panelRt.anchoredPosition = new Vector2(x, y);
        }
    }
}
