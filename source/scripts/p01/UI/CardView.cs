using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 一张灰模卡片：只有色块和文字，没有任何贴图。
    /// 同一个对象既用来画场上的单位，也用来画手牌和预备区的牌。
    /// </summary>
    public sealed class CardView
    {
        public readonly RectTransform Rt;
        public readonly Image Frame;
        public readonly Image Fill;
        public readonly Text Title;
        public readonly Text Cost;
        public readonly Text Stats;
        public readonly Text Body;
        public readonly Button Button;

        public CardView(Transform parent, string name, float x, float y, float w, float h)
        {
            Rt = UiKit.Rect(parent, name, x, y, w, h);
            Frame = Rt.gameObject.AddComponent<Image>();
            Frame.color = UiKit.Border;
            Button = Rt.gameObject.AddComponent<Button>();
            var colors = Button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.80f, 0.86f, 0.95f);
            colors.pressedColor = new Color(0.62f, 0.70f, 0.85f);
            Button.colors = colors;

            Fill = UiKit.Panel(Rt, "Fill", 2f, 2f, w - 4f, h - 4f, UiKit.CardBg);
            Fill.raycastTarget = false;

            Cost = UiKit.Label(Rt, "Cost", 5f, 3f, 26f, 24f, "", 17, TextAnchor.MiddleCenter, UiKit.Selected);
            Title = UiKit.Label(Rt, "Title", 32f, 3f, w - 38f, 26f, "", 16, TextAnchor.MiddleLeft, UiKit.TextMain);
            Body = UiKit.Label(Rt, "Body", 5f, 32f, w - 10f, h - 68f, "", 12, TextAnchor.UpperLeft, UiKit.TextDim);
            Stats = UiKit.Label(Rt, "Stats", 5f, h - 32f, w - 10f, 28f, "", 16, TextAnchor.MiddleCenter, UiKit.TextMain);
        }

        public void SetClick(UnityEngine.Events.UnityAction action)
        {
            Button.onClick.RemoveAllListeners();
            Button.interactable = action != null;
            if (action != null)
            {
                Button.onClick.AddListener(action);
            }
        }

        public void Show()
        {
            if (!Rt.gameObject.activeSelf)
            {
                Rt.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (Rt.gameObject.activeSelf)
            {
                Rt.gameObject.SetActive(false);
            }
        }

        public void SetFrame(Color color)
        {
            Frame.color = color;
        }

        public void SetFill(Color color)
        {
            Fill.color = color;
        }

        public void SetText(string cost, string title, string stats, string body)
        {
            Cost.text = cost ?? string.Empty;
            Title.text = title ?? string.Empty;
            Stats.text = stats ?? string.Empty;
            Body.text = body ?? string.Empty;
        }

        public void SetTextColors(Color titleColor, Color bodyColor)
        {
            Title.color = titleColor;
            Body.color = bodyColor;
            Stats.color = titleColor;
        }

        /// <summary>空格子：暗底 + 位置标签，不可点。</summary>
        public void SetEmptySlot(string slotLabel, bool highlightAsTarget, UnityEngine.Events.UnityAction onClick)
        {
            Show();
            SetFrame(highlightAsTarget ? UiKit.Target : new Color(0.20f, 0.23f, 0.28f));
            SetFill(new Color(0.10f, 0.11f, 0.13f));
            SetText("", "", "", "");
            Cost.text = "";
            Title.text = "";
            Stats.text = slotLabel;
            Body.text = highlightAsTarget ? "空位" : "";
            SetTextColors(UiKit.TextDim, UiKit.TextDim);
            SetClick(onClick);
        }

        public void SetUnit(string title, string stats, string body, bool mine,
            Color frame, UnityEngine.Events.UnityAction onClick)
        {
            Show();
            SetFrame(frame);
            SetFill(mine ? UiKit.MyUnit : UiKit.EnemyUnit);
            SetText("", title, stats, body);
            SetTextColors(UiKit.TextMain, UiKit.TextDim);
            SetClick(onClick);
        }

        public void SetCardFace(string cost, string title, string stats, string body, bool playable,
            Color frame, UnityEngine.Events.UnityAction onClick)
        {
            Show();
            SetFrame(frame);
            SetFill(playable ? UiKit.CardBg : new Color(0.13f, 0.14f, 0.16f));
            SetText(cost, title, stats, body);
            SetTextColors(playable ? UiKit.TextMain : UiKit.TextDim, UiKit.TextDim);
            SetClick(onClick);
        }
    }
}
