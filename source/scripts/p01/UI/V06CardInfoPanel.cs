using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    public struct V06CardView
    {
        public string CardId, Name, CategoryText, KeywordText, EffectText;
        public int Cost, Atk, Hp, CurrentAtk, CurrentHp, Damage;
        public string ArtPath;
        public bool CanPlayNow;
    }

    public sealed class V06CardInfoPanel : MonoBehaviour
    {
        private static readonly Color Ink = new Color32(228, 236, 245, 255);
        private static readonly Color Secondary = new Color32(178, 193, 209, 255);
        private static readonly Color Gold = new Color32(234, 202, 138, 255);
        private static readonly Color Ready = new Color32(150, 218, 198, 255);
        private RectTransform _root;
        private Text _title, _type, _stats, _keywords, _effect, _availability;
        private Image _art;
        private Font _font;
        private string _artPath;

        public void Build(RectTransform parent)
        {
            if (_root != null || parent == null) { return; }
            _font = PickFont();
            _root = Rect(parent, "V06 Card Info", 0f, 0f, 1f, 1f);
            Panel(_root, new Color32(23, 32, 47, 242));
            Panel(Rect(_root, "Gold Rule", .046f, .938f, .954f, .949f), Gold);
            Panel(Rect(_root, "Art Well", .047f, .492f, .365f, .906f), new Color32(12, 19, 30, 190));
            _art = Panel(Rect(_root, "Art", .058f, .506f, .354f, .893f), Color.white);
            _art.preserveAspect = true;
            _title = Label("Name", .401f, .798f, .951f, .912f, 23, Gold, FontStyle.Bold);
            _type = Label("Type", .401f, .710f, .951f, .795f, 15, Secondary);
            _stats = Label("Stats", .401f, .589f, .951f, .709f, 17, Ink);
            _availability = Label("Availability", .401f, .502f, .951f, .588f, 14, Ready);
            _keywords = Label("Keywords", .046f, .410f, .954f, .477f, 15, Gold);
            Panel(Rect(_root, "Divider", .046f, .393f, .954f, .397f), new Color32(78, 97, 119, 235));
            _effect = Label("Effect", .046f, .057f, .954f, .373f, 17, Ink);
            _effect.alignment = TextAnchor.UpperLeft;
            _effect.lineSpacing = 1.11f;
            Hide();
        }

        public void Show(V06CardView v)
        {
            if (_root == null) { return; }
            _title.text = string.IsNullOrEmpty(v.Name) ? v.CardId ?? "" : v.Name;
            string cost = v.Cost >= 0 ? "  ·  " + v.Cost + " 费" : "";
            _type.text = (v.CategoryText ?? "") + cost;
            _stats.text = v.Hp > 0 || v.CurrentHp > 0
                ? "攻 " + v.CurrentAtk + " / " + v.Atk + "     血 " + v.CurrentHp + " / " + v.Hp
                    + (v.Damage > 0 ? "\n已受伤害 " + v.Damage : "")
                : "";
            _keywords.text = v.KeywordText ?? "";
            _effect.text = v.EffectText ?? "";
            _availability.text = v.CanPlayNow ? "当前可用" : "当前不可用";
            _availability.color = v.CanPlayNow ? Ready : Secondary;
            if (_artPath != v.ArtPath || _art.sprite == null)
            {
                _artPath = v.ArtPath;
                _art.sprite = string.IsNullOrWhiteSpace(_artPath) ? null : Resources.Load<Sprite>(_artPath);
            }
            _art.enabled = _art.sprite != null;
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) { _root.gameObject.SetActive(false); }
        }

        private Text Label(string labelName, float l, float b, float r, float t, int fontSize, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var label = Rect(_root, labelName, l, b, r, t).gameObject.AddComponent<Text>();
            label.font = _font;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = color;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = fontSize;
            label.raycastTarget = false;
            return label;
        }

        private static Image Panel(RectTransform rect, Color color)
        {
            var panel = rect.gameObject.AddComponent<Image>();
            panel.color = color;
            panel.raycastTarget = false;
            return panel;
        }

        private static RectTransform Rect(RectTransform parent, string name, float l, float b, float r, float t)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(l, b);
            rect.anchorMax = new Vector2(r, t);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Font PickFont()
        {
            string[] available = Font.GetOSInstalledFontNames();
            foreach (string preferred in new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun", "Noto Sans CJK SC" })
            {
                if (System.Array.IndexOf(available, preferred) >= 0)
                    return Font.CreateDynamicFontFromOSFont(preferred, 18);
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
