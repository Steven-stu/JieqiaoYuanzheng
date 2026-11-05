using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// V0.7 界面皮（GPT 交付的 Art/UI/V07 素材）加载 + 轻量动画。
    ///
    /// 设计原则：
    ///   · 素材缺失一律返回 null，调用方退化成纯色，不报错（这样美术可以分批到）；
    ///   · 动画只用补间（缩放/透明度/位移），不需要序列帧；
    ///   · 所有动画都在 unscaled time 上跑，暂停游戏也不会卡住界面。
    /// </summary>
    public static class V06UiV07
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>按资源名取图，例如 Skin("bg_home")、Skin("home_entry_battle")。</summary>
        public static Sprite Skin(string name)
        {
            if (string.IsNullOrEmpty(name)) { return null; }
            Sprite s;
            if (Cache.TryGetValue(name, out s))
            {
                if (s != null) { return s; }
                Cache.Remove(name);
            }
            s = Resources.Load<Sprite>("Art/UI/V07/" + name);
            if (s != null) { Cache[name] = s; }
            return s;
        }

        public static void ClearCache() { Cache.Clear(); }

        public static int CachedCount { get { return Cache.Count; } }

        // ───────────────────────── 建件助手 ─────────────────────────

        /// <summary>满幅背景图（挂在给定父节点最底层）。素材缺失则不建。</summary>
        public static Image Background(Transform parent, string skinName, float alpha)
        {
            Sprite s = Skin(skinName);
            if (s == null) { return null; }
            var go = new GameObject("Bg_" + skinName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
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
            Color c = Color.white;
            c.a = alpha;
            img.color = c;
            return img;
        }

        /// <summary>九宫格面板（自动 Sliced）。素材缺失则退化成半透明纯色。</summary>
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h,
            string skinName, Color fallback)
        {
            Image img = UiKit.Panel(parent, name, x, y, w, h, fallback);
            Sprite s = Skin(skinName);
            if (s != null)
            {
                img.sprite = s;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.97f);
            }
            return img;
        }

        /// <summary>图标（Point 过滤、不接收射线）。素材缺失则不建，返回 null。</summary>
        public static Image Icon(Transform parent, string name, float x, float y, float size, string skinName, Color tint)
        {
            Sprite s = Skin(skinName);
            if (s == null) { return null; }
            RectTransform rt = UiKit.Rect(parent, name, x, y, size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint;
            return img;
        }

        /// <summary>带图标的按钮：底板用九宫格皮肤，图标+文字居中。</summary>
        public static Button IconButton(Transform parent, string name, float x, float y, float w, float h,
            string skinName, string iconName, string label, UnityEngine.Events.UnityAction onClick)
        {
            Image bg = Panel(parent, name, x, y, w, h, skinName, UiKit.ButtonNormal);
            var btn = bg.gameObject.AddComponent<Button>();
            bg.raycastTarget = true;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.86f);
            colors.pressedColor = new Color(0.82f, 0.85f, 0.92f);
            btn.colors = colors;
            if (onClick != null) { btn.onClick.AddListener(onClick); }
            bool hasIcon = false;
            Sprite icon = Skin(iconName);
            if (icon != null)
            {
                var ig = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ig.transform.SetParent(bg.transform, false);
                var irt = ig.GetComponent<RectTransform>();
                irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(10f, 0f);
                irt.sizeDelta = new Vector2(h - 14f, h - 14f);
                var img = ig.GetComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                hasIcon = true;
            }
            float textX = hasIcon ? (h - 4f) : 6f;
            UiKit.Label(bg.transform, "Text", textX, 0f, w - textX - 6f, h, label, 15, TextAnchor.MiddleCenter, UiKit.TextMain);
            V06Audio.ClickOn(btn);
            return btn;
        }

        // ───────────────────────── 序列帧动画（等 GPT 交关键帧后自动生效） ─────────────────────────

        private static readonly Dictionary<string, Sprite[]> AnimCache = new Dictionary<string, Sprite[]>();

        /// <summary>取一套动画的帧（Art/UI/V07/Anim/&lt;name&gt;/&lt;name&gt;_NN）。没交就返回 null。</summary>
        public static Sprite[] Anim(string name)
        {
            if (string.IsNullOrEmpty(name)) { return null; }
            Sprite[] arr;
            if (AnimCache.TryGetValue(name, out arr))
            {
                if (arr != null) { return arr; }
                AnimCache.Remove(name);
            }
            Sprite[] frames = new Sprite[8];
            int loaded = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                string f = name + "_" + (i < 10 ? "0" + i : "" + i);
                Sprite s = Resources.Load<Sprite>("Art/UI/V07/Anim/" + name + "/" + f);
                frames[i] = s;
                if (s != null) { loaded++; }
            }
            if (loaded == 0) { return null; }
            AnimCache[name] = frames;
            return frames;
        }

        public static bool HasAnim(string name) { return Anim(name) != null; }

        /// <summary>第 1 批 + 第 2 批动画套名（验收用）。GPT 一共交了 47 套。</summary>
        public static readonly string[] AnimSetNames = new string[] {
            // 第 1 批（27 套）
            "anim_arrows_flow", "anim_banner_defeat", "anim_banner_draw", "anim_banner_victory",
            "anim_checkin_ready", "anim_checkin_stamp", "anim_coin", "anim_entry_battle",
            "anim_entry_checkin", "anim_entry_codex", "anim_entry_profile", "anim_entry_shop",
            "anim_envelope_open", "anim_gem", "anim_levelup", "anim_locked_shake",
            "anim_pack_open_beast", "anim_pack_open_girl", "anim_pack_open_starter", "anim_page_wipe",
            "anim_rarity_1", "anim_rarity_2", "anim_rarity_3", "anim_rarity_4",
            "anim_reward_open", "anim_seal_press", "anim_soldout",
            // 第 2 批（20 套）
            "anim_avatar_hover", "anim_bg_dust", "anim_bg_embers", "anim_card_deal", "anim_card_flip",
            "anim_cardback_glow", "anim_coin_fly", "anim_flag_flash", "anim_keyword_ping",
            "anim_loading_spin", "anim_rarity_glow_5", "anim_refresh_wipe", "anim_save_confirm",
            "anim_shelf_hover", "anim_slot_ring_evolve", "anim_slot_ring_fuse", "anim_toast_wipe",
            "anim_trade_sent", "anim_turn_banner", "anim_winrate_glow",
            // 第 3 批（61 套）
            // A 组 · 战斗核心演出
            "anim_deploy_summon", "anim_slot_select_ring", "anim_slot_target_reticle", "anim_attack_lunge",
            "anim_guard_ripple", "anim_death_dissolve", "anim_hp_drain", "anim_ap_drain", "anim_round_tick",
            "anim_flag_break", "anim_coin_toss", "anim_pressure_warning", "anim_fuse_link", "anim_evolve_burst",
            "anim_response_pop", "anim_hand_refill", "anim_deck_pile_pulse", "anim_discard_pile_pulse",
            "anim_victory_stars", "anim_defeat_vignette",
            // B 组 · 大厅/通用控件
            "anim_nav_active_sweep", "anim_window_open", "anim_window_close", "anim_notice_scroll",
            "anim_home_entry_unlock", "anim_click_ripple", "anim_button_press_glow", "anim_tooltip_in",
            // C 组 · 图鉴/组卡
            "anim_codex_unlock", "anim_codex_lock_shimmer", "anim_codex_progress_fill", "anim_deck_row_highlight",
            "anim_deck_valid_stamp", "anim_deck_invalid_shake", "anim_deck_cover_flip", "anim_reserve_slot_ping",
            "anim_card_gain_fly",
            // D 组 · 商店/签到/交换
            "anim_shop_shelf_pulse", "anim_price_pop", "anim_buy_burst", "anim_soldout_stamp",
            "anim_daily_pack_glow", "anim_checkin_flame_flicker", "anim_checkin_board_glow",
            "anim_reward_card_reveal", "anim_trade_player_row_glow", "anim_trade_seal_press", "anim_trade_arrows_flow",
            // E 组 · 资料/卡框
            "anim_level_bar_fill", "anim_badge_shine", "anim_winrate_ring_fill", "anim_title_ribbon_glow",
            "anim_card_frame_holo", "anim_card_frame_legend",
            // F 组 · 联机/天气
            "anim_net_linked", "anim_net_sync_check", "anim_bg_stars_twinkle", "anim_bg_fog_drift",
            "anim_bg_fireflies", "anim_bg_rain", "anim_loading_mask_breath"
        };

        /// <summary>已交的动画套数（验收用）。</summary>
        public static int AnimSetsLoaded
        {
            get
            {
                int n = 0;
                for (int i = 0; i < AnimSetNames.Length; i++) { if (HasAnim(AnimSetNames[i])) { n++; } }
                return n;
            }
        }

        /// <summary>缺失的动画套名（验收用）。</summary>
        public static string MissingAnimSets()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < AnimSetNames.Length; i++)
            {
                if (!HasAnim(AnimSetNames[i])) { sb.Append(AnimSetNames[i]).Append(' '); }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 关键帧动画（"补帧"方案）：只给 2–4 张关键帧，中间用交叉淡入 + 变换曲线补出来。
        /// UI 的发光/展开/砸落这类动画用这个几乎看不出缺帧，而且不需要模型插帧。
        /// 若同名逐帧序列存在则优先用真帧（PlayAnim），这里只是兜底。
        /// </summary>
        public static Coroutine PlayKeyframes(Image target, Sprite[] keys, float duration, AnimationCurve scaleCurve)
        {
            if (target == null || keys == null || keys.Length == 0) { return null; }
            return TweenHost.Instance.StartCoroutine(KeyframeRoutine(target, keys, duration <= 0f ? 0.35f : duration, scaleCurve));
        }

        private static IEnumerator KeyframeRoutine(Image target, Sprite[] keys, float dur, AnimationCurve scaleCurve)
        {
            if (target == null) { yield break; }
            RectTransform rt = target.rectTransform;
            float t = 0f;
            while (t < dur)
            {
                if (target == null) { yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                // 关键帧位置
                float slot = k * (keys.Length - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(slot), 0, keys.Length - 1);
                int i1 = Mathf.Clamp(i0 + 1, 0, keys.Length - 1);
                float frac = slot - i0;
                // 交叉淡入：前 60% 用前一帧，后 40% 融到下一帧（视觉上像补帧）
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, frac));
                if (keys[i0] != null) { target.sprite = keys[i0]; }
                Color c = target.color;
                c.a = Mathf.Lerp(1f, blend, 0.55f);
                target.color = c;
                if (keys[i1] != null && blend > 0.5f) { target.sprite = keys[i1]; c.a = 1f; target.color = c; }
                if (scaleCurve != null)
                {
                    float s = scaleCurve.Evaluate(k);
                    rt.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            if (target != null)
            {
                if (keys[keys.Length - 1] != null) { target.sprite = keys[keys.Length - 1]; }
                Color c = target.color; c.a = 1f; target.color = c;
                if (scaleCurve != null) { rt.localScale = Vector3.one; }
            }
        }

        /// <summary>从图集里取帧（GPT 若交的是 xxx_sheetN 横排图集，切图工具会生成同名多 sprite）。</summary>
        public static Sprite[] SheetFrames(string sheetName)
        {
            Sprite[] all = Resources.LoadAll<Sprite>("Art/UI/V07/Anim/" + sheetName);
            if (all == null || all.Length == 0) { return null; }
            System.Array.Sort(all, delegate (Sprite a, Sprite b) { return string.CompareOrdinal(a.name, b.name); });
            return all;
        }

        /// <summary>在指定 Image 上逐帧播放一套动画；缺帧就保持原样。
        /// 循环模式下 Image 会一直跳帧，非循环播完停在最后一帧（并把原 sprite 交给回调还原）。</summary>
        public static Coroutine PlayAnim(Image target, string animName, float fps, bool loop)
        {
            Sprite[] frames = Anim(animName);
            if (target == null || frames == null) { return null; }
            return TweenHost.Instance.StartCoroutine(AnimRoutine(target, frames, fps <= 0f ? 20f : fps, loop));
        }

        private static IEnumerator AnimRoutine(Image target, Sprite[] frames, float fps, bool loop)
        {
            Sprite original = target.sprite;
            float per = 1f / fps;
            while (true)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (target == null) { yield break; }
                    if (frames[i] != null) { target.sprite = frames[i]; }
                    float t = 0f;
                    while (t < per)
                    {
                        if (target == null) { yield break; }
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                if (!loop) { break; }
            }
            if (target != null)
            {
                // 非循环：停在最后一帧；调用方可用 RestoreSprite 还原
                target.sprite = frames[frames.Length - 1] != null ? frames[frames.Length - 1] : original;
            }
        }

        /// <summary>播一次性动画（例如印章砸下），播完把 sprite 还原。</summary>
        public static Coroutine PlayOnce(Image target, string animName, float fps)
        {
            Sprite[] frames = Anim(animName);
            if (target == null || frames == null) { return null; }
            return TweenHost.Instance.StartCoroutine(OnceRoutine(target, frames, fps <= 0f ? 20f : fps));
        }

        private static IEnumerator OnceRoutine(Image target, Sprite[] frames, float fps)
        {
            Sprite original = target.sprite;
            float per = 1f / fps;
            for (int i = 0; i < frames.Length; i++)
            {
                if (target == null) { yield break; }
                if (frames[i] != null) { target.sprite = frames[i]; }
                float t = 0f;
                while (t < per)
                {
                    if (target == null) { yield break; }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            if (target != null && frames[frames.Length - 1] != null) { target.sprite = frames[frames.Length - 1]; }
            else if (target != null) { target.sprite = original; }
        }

        /// <summary>全屏扫光（页面切换用）。没交 anim_page_wipe 时自动退化成淡入。</summary>
        public static void PageWipe(RectTransform parent, System.Action onDone)
        {
            if (parent == null) { return; }
            Sprite[] frames = Anim("anim_page_wipe");
            if (frames == null)
            {
                if (onDone != null) { onDone(); }
                return;
            }
            var go = new GameObject("PageWipe", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.85f);
            TweenHost.Instance.StartCoroutine(WipeRoutine(go, img, frames, onDone));
        }

        private static IEnumerator WipeRoutine(GameObject go, Image img, Sprite[] frames, System.Action onDone)
        {
            float per = 0.05f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (go == null) { yield break; }
                if (frames[i] != null) { img.sprite = frames[i]; }
                float t = 0f;
                while (t < per) { t += Time.unscaledDeltaTime; yield return null; }
            }
            if (onDone != null) { onDone(); }
            if (go != null) { Object.Destroy(go); }
        }

        /// <summary>放一张会"补帧"的图：静态图 + 若有关键帧就用交叉淡入补间。返回 Image。</summary>
        public static Image AnimatedSprite(Transform parent, string name, float x, float y, float w, float h,
            string skinName, string animName, float duration)
        {
            Sprite s = Skin(skinName);
            if (s == null) { return null; }
            Image img = UiKit.Panel(parent, name, x, y, w, h, Color.white);
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Sprite[] frames = string.IsNullOrEmpty(animName) ? null : SheetFrames(animName);
            if (frames == null && !string.IsNullOrEmpty(animName)) { frames = Anim(animName); }
            if (frames != null && frames.Length >= 2)
            {
                // 关键帧补间：缩放从 0.86 → 1.04 → 1.0，中间用交叉淡入补
                AnimationCurve curve = new AnimationCurve(
                    new Keyframe(0f, 0.86f), new Keyframe(0.65f, 1.04f), new Keyframe(1f, 1f));
                PlayKeyframes(img, frames, duration <= 0f ? 0.34f : duration, curve);
            }
            else
            {
                Pop(img.rectTransform, 0.18f);
            }
            return img;
        }
        // ───────────────────────── 轻量动画 ─────────────────────────

        /// <summary>弹入：缩放 0.92 → 1，透明度 0 → 1。</summary>
        public static void Pop(RectTransform rt, float duration)
        {
            if (rt == null) { return; }
            TweenHost.Instance.StartCoroutine(PopRoutine(rt, duration <= 0f ? 0.18f : duration));
        }

        private static IEnumerator PopRoutine(RectTransform rt, float dur)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) { cg = rt.gameObject.AddComponent<CanvasGroup>(); }
            float t = 0f;
            while (t < dur)
            {
                if (rt == null) { yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - k) * (1f - k);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, e);
                cg.alpha = Mathf.LerpUnclamped(0f, 1f, e);
                yield return null;
            }
            if (rt != null) { rt.localScale = Vector3.one; cg.alpha = 1f; }
        }

        /// <summary>淡入（切页用）。</summary>
        public static void FadeIn(RectTransform rt, float duration)
        {
            if (rt == null) { return; }
            TweenHost.Instance.StartCoroutine(FadeRoutine(rt, duration <= 0f ? 0.16f : duration));
        }

        private static IEnumerator FadeRoutine(RectTransform rt, float dur)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) { cg = rt.gameObject.AddComponent<CanvasGroup>(); }
            cg.alpha = 0f;
            float t = 0f;
            while (t < dur)
            {
                if (rt == null) { yield break; }
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(t / dur);
                yield return null;
            }
            if (cg != null) { cg.alpha = 1f; }
        }

        /// <summary>呼吸脉冲（签到"今天可领"格子用）。</summary>
        public static void Pulse(RectTransform rt, float speed)
        {
            if (rt == null) { return; }
            var pulse = rt.GetComponent<V06Pulse>();
            if (pulse == null) { pulse = rt.gameObject.AddComponent<V06Pulse>(); }
            pulse.speed = speed <= 0f ? 2.2f : speed;
            pulse.enabled = true;
        }

        public static void StopPulse(RectTransform rt)
        {
            if (rt == null) { return; }
            var pulse = rt.GetComponent<V06Pulse>();
            if (pulse != null) { rt.localScale = Vector3.one; pulse.enabled = false; }
        }

        /// <summary>缓慢自转（奖励光晕、加载圈用）。</summary>
        public static void Spin(RectTransform rt, float degPerSec)
        {
            if (rt == null) { return; }
            var spin = rt.GetComponent<V06Spin>();
            if (spin == null) { spin = rt.gameObject.AddComponent<V06Spin>(); }
            spin.degPerSec = degPerSec == 0f ? -24f : degPerSec;
        }

        /// <summary>按下回弹（按钮手感）。</summary>
        public static void Press(RectTransform rt)
        {
            if (rt == null) { return; }
            TweenHost.Instance.StartCoroutine(PressRoutine(rt));
        }

        private static IEnumerator PressRoutine(RectTransform rt)
        {
            float t = 0f;
            const float half = 0.06f;
            while (t < half)
            {
                if (rt == null) { yield break; }
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.955f, t / half);
                yield return null;
            }
            t = 0f;
            while (t < half)
            {
                if (rt == null) { yield break; }
                t += Time.unscaledDeltaTime;
                rt.localScale = Vector3.one * Mathf.Lerp(0.955f, 1f, t / half);
                yield return null;
            }
            if (rt != null) { rt.localScale = Vector3.one; }
        }

        /// <summary>从上滑入（提示条/横幅）。</summary>
        public static void SlideIn(RectTransform rt, float fromY, float duration)
        {
            if (rt == null) { return; }
            TweenHost.Instance.StartCoroutine(SlideRoutine(rt, fromY, duration <= 0f ? 0.22f : duration));
        }

        private static IEnumerator SlideRoutine(RectTransform rt, float fromY, float dur)
        {
            Vector2 target = rt.anchoredPosition;
            float t = 0f;
            while (t < dur)
            {
                if (rt == null) { yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = 1f - (1f - k) * (1f - k);
                rt.anchoredPosition = target + new Vector2(0f, Mathf.LerpUnclamped(fromY, 0f, e));
                yield return null;
            }
            if (rt != null) { rt.anchoredPosition = target; }
        }
    }

    /// <summary>跑补间的宿主（全局一个，不随界面销毁）。</summary>
    public sealed class TweenHost : MonoBehaviour
    {
        private static TweenHost _instance;
        public static TweenHost Instance
        {
            get
            {
                if (_instance != null) { return _instance; }
                var go = new GameObject("V06TweenHost");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TweenHost>();
                return _instance;
            }
        }
    }

    /// <summary>缩放呼吸。</summary>
    public sealed class V06Pulse : MonoBehaviour
    {
        public float speed = 2.2f;
        public float amplitude = 0.045f;
        private void OnEnable() { transform.localScale = Vector3.one; }
        private void Update()
        {
            float k = 1f + Mathf.Sin(Time.unscaledTime * speed) * amplitude;
            transform.localScale = new Vector3(k, k, 1f);
        }
    }

    /// <summary>缓慢自转。</summary>
    public sealed class V06Spin : MonoBehaviour
    {
        public float degPerSec = -24f;
        private void Update() { transform.Rotate(0f, 0f, degPerSec * Time.unscaledDeltaTime); }
    }
}
