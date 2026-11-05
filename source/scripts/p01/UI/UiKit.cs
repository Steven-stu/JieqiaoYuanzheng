using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 灰模界面的基础件：字体、矩形、文字、色板。
    /// 全部用纯色块 + 文字构成，不含任何贴图资源。
    /// </summary>
    public static class UiKit
    {
        // 参考分辨率，与 CanvasScaler 一致；所有坐标按这个空间给
        public const float RefW = 1600f;
        public const float RefH = 900f;

        public static readonly Color Bg = new Color(0.07f, 0.08f, 0.10f);
        public static readonly Color PanelBg = new Color(0.12f, 0.13f, 0.16f);
        public static readonly Color CardBg = new Color(0.16f, 0.18f, 0.22f);
        public static readonly Color MyUnit = new Color(0.13f, 0.22f, 0.31f);
        public static readonly Color EnemyUnit = new Color(0.29f, 0.14f, 0.17f);
        public static readonly Color Border = new Color(0.29f, 0.33f, 0.40f);
        public static readonly Color Selected = new Color(0.96f, 0.79f, 0.27f);
        public static readonly Color Target = new Color(0.31f, 0.82f, 0.77f);
        public static readonly Color Threat = new Color(0.96f, 0.40f, 0.40f);
        public static readonly Color TextMain = new Color(0.90f, 0.92f, 0.95f);
        public static readonly Color TextDim = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color MyCamp = new Color(0.18f, 0.45f, 0.32f);
        public static readonly Color EnemyCamp = new Color(0.50f, 0.18f, 0.22f);
        public static readonly Color ButtonNormal = new Color(0.19f, 0.23f, 0.30f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun", "Noto Sans CJK SC" };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        Font f = UnityEngine.Font.CreateDynamicFontFromOSFont(candidates[i], 16);
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

        /// <summary>以左上角为原点建一个矩形（x 向右、y 向下，单位是参考像素）。</summary>
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            RectTransform rt = Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string name, float x, float y, float w, float h,
            string text, int size, TextAnchor anchor, Color? color = null)
        {
            RectTransform rt = Rect(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.text = text;
            t.color = color.HasValue ? color.Value : TextMain;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, float x, float y, float w, float h,
            string label, Color tint, UnityEngine.Events.UnityAction onClick)
        {
            Image img = Panel(parent, name, x, y, w, h, tint);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.90f, 1f);
            colors.pressedColor = new Color(0.65f, 0.72f, 0.85f);
            btn.colors = colors;
            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }
            Label(img.transform, "Text", 6f, 0f, w - 12f, h, label, 15, TextAnchor.MiddleCenter, TextMain);
            // V0.7：所有按钮统一带点击音 + 按下回弹（在这里挂一次，全界面生效）
            V06Audio.ClickOn(btn);
            return btn;
        }

        public static void SetColor(Graphic g, Color c)
        {
            if (g != null)
            {
                g.color = c;
            }
        }
    }
}
