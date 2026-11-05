using System.Collections.Generic;
using Jieqiao;
using UnityEngine;

namespace Jieqiao.UI
{
    /// <summary>
    /// 卡面立绘：把上传的 10 张「三姿设定图」切成卡面用的人像。
    ///
    /// 每张图是横向三格立绘（默认 1536×1024，每格 512 宽）。
    /// 卡牌按派系取图，按费用取姿态：
    ///   费用 1–2 → 第 1 姿，费用 3 → 第 2 姿，费用 4 → 第 3 姿；
    ///   非单位牌统一用最后一姿。
    /// 这样同一派系的卡看起来是一套人，而不是随机贴图。
    /// </summary>
    public static class V06Art
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> OwnCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>派系 → 立绘文件名（Resources/Art/chars 下）。</summary>
        public static string SheetFor(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) { return "miya"; }
            // 单位与晋阶：按派系用对应角色的立绘
            if (cardId.StartsWith("U-H") || cardId == "J05") { return "nora"; }
            if (cardId.StartsWith("U-E") || cardId == "J06") { return "liv"; }
            if (cardId.StartsWith("U-D") || cardId == "J07") { return "brin"; }
            if (cardId.StartsWith("U-G") || cardId == "J08") { return "goblin"; }
            if (cardId.StartsWith("U-V") || cardId == "J09") { return "selin"; }
            if (cardId.StartsWith("U-O") || cardId == "J10") { return "via"; }
            if (cardId.StartsWith("U-R") || cardId == "J11") { return "adel"; }
            if (cardId.StartsWith("U-M") || cardId == "J12") { return "ile"; }
            // 战术/装备/附印/支援/战况：交给法师（弥娅）；
            // 精灵、哥布林、血族、荒原、龙群 的战术与全部响应/环境/遭遇/事件交给狐女（绯萝）
            if (cardId.StartsWith("S-E") || cardId.StartsWith("S-G") || cardId.StartsWith("S-V")
                || cardId.StartsWith("S-O") || cardId.StartsWith("S-R"))
            {
                return "feiluo";
            }
            // 衍生单位按出处派系
            switch (cardId)
            {
                case "T01": case "T02": return "liv";
                case "T03": return "goblin";
                case "T04": return "selin";
                case "T05": return "via";
                case "T06": case "T07": case "T08": return "ile";
            }
            // 响应/环境/遭遇/事件 → 狐女；装备/附印/支援/战况 → 法师
            if (cardId.StartsWith("X") || cardId.StartsWith("W") || cardId.StartsWith("C") || cardId.StartsWith("E"))
            {
                return "feiluo";
            }
            return "miya";
        }

        /// <summary>卡牌该用第几姿（0–2）。</summary>
        public static int PoseIndex(string cardId, int cost)
        {
            V06Card c = V06Catalog.ById(cardId);
            if (c != null && c.Category != V06Category.Unit && c.Category != V06Category.Token)
            {
                return 2;
            }
            if (cost <= 2) { return 0; }
            if (cost == 3) { return 1; }
            return 2;
        }

        /// <summary>清空缓存：贴图重新导入后旧 Sprite/Texture 会变成"已销毁对象"，必须丢弃。</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            OwnCache.Clear();
            Textures.Clear();
            Placeholders.Clear();
        }

        private static readonly Dictionary<string, Sprite> Placeholders = new Dictionary<string, Sprite>();

        /// <summary>
        /// V0.7 文字版占位卡面：扩展卡（兽群 B* / 少女 G*）暂时没有专属插画时使用。
        /// 生成 96×128（严格 3:4）的渐变 + 斜纹 + 亮边纹理，卡名与数值由界面文字层负责，
        /// 所以占位图上不画字。精灵名以 "ph:" 开头，便于断言把"占位"与"专属图"分开统计。
        /// </summary>
        public static Sprite Placeholder(string cardId)
        {
            Sprite cached;
            if (Placeholders.TryGetValue(cardId, out cached))
            {
                if (cached != null) { return cached; }
                Placeholders.Remove(cardId);
            }
            const int W = 96;
            const int H = 128;
            bool beast = !string.IsNullOrEmpty(cardId) && cardId[0] == 'B';
            // 兽群：荒野暖褐；少女：都市冷紫。按编号做一点亮度抖动，避免整版同色。
            int h = 0;
            for (int i = 0; i < cardId.Length; i++) { h = (h * 31 + cardId[i]) & 0xffff; }
            float k = 0.80f + (h % 26) / 100f;
            Color top = beast ? new Color(0.20f, 0.15f, 0.10f) : new Color(0.16f, 0.11f, 0.22f);
            Color bot = beast ? new Color(0.40f, 0.29f, 0.15f) : new Color(0.30f, 0.18f, 0.40f);
            top = new Color(top.r * k, top.g * k, top.b * k, 1f);
            bot = new Color(bot.r * k, bot.g * k, bot.b * k, 1f);

            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.name = "ph:" + cardId;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Color[] px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);
                Color row = Color.Lerp(bot, top, t);
                for (int x = 0; x < W; x++)
                {
                    Color c = row;
                    if (((x + y) / 7) % 2 == 0) { c = Color.Lerp(c, Color.white, 0.045f); }
                    if (x < 2 || y < 2 || x >= W - 2 || y >= H - 2) { c = Color.Lerp(c, Color.white, 0.30f); }
                    if (y > H - 30) { c = Color.Lerp(c, new Color(0.04f, 0.05f, 0.07f), 0.55f); }  // 底部数值条
                    px[y * W + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            Sprite sp = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), 100f);
            sp.name = "ph:" + cardId;
            Placeholders[cardId] = sp;
            return sp;
        }

        private static bool IsExpansion(string cardId)
        {
            V06Card c = V06Catalog.ById(cardId);
            return c != null && c.Exp;
        }

        private static Texture2D TextureOf(string sheet)
        {
            Texture2D cachedTex;
            if (Textures.TryGetValue(sheet, out cachedTex))
            {
                // Unity 的 == 重载会把"已销毁对象"判为 null，正好用来剔除失效缓存
                if (cachedTex != null) { return cachedTex; }
                Textures.Remove(sheet);
            }
            Texture2D tex = null;
            Sprite s = Resources.Load<Sprite>("Art/chars/" + sheet);
            if (s != null) { tex = s.texture; }
            if (tex == null) { tex = Resources.Load<Texture2D>("Art/chars/" + sheet); }
            // 只缓存成功结果：以前把 null 也塞进缓存，导致导入完成前查过一次的贴图永远返回空
            if (tex != null) { Textures[sheet] = tex; }
            return tex;
        }

        /// <summary>
        /// 取一张卡面精灵。优先级：
        ///   ① 专属单位插画 Resources/Art/units/&lt;卡编号&gt;（58/64 单位 + 8 衍生物有）；
        ///   ② 兜底用派系三姿图，按费用选姿态。
        /// </summary>
        public static Sprite Portrait(string cardId, int cost)
        {
            if (string.IsNullOrEmpty(cardId)) { return null; }
            // ① 专属立绘
            string ownKey = "own:" + cardId;
            Sprite own;
            if (OwnCache.TryGetValue(ownKey, out own))
            {
                if (own != null) { return own; }
                OwnCache.Remove(ownKey);
            }
            Sprite loaded = Resources.Load<Sprite>("Art/units/" + cardId);
            if (loaded != null) { OwnCache[ownKey] = loaded; return loaded; }

            // ①.5 V0.7 扩展卡（兽群/少女）还没有专属插画 → 用文字版占位图，绝不借用派系三姿图
            if (IsExpansion(cardId)) { return Placeholder(cardId); }

            // ② 派系三姿图兜底
            string sheet = SheetFor(cardId);
            int pose = PoseIndex(cardId, cost);
            string key = sheet + "#" + pose;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached))
            {
                if (cached != null) { return cached; }
                Cache.Remove(key);
            }

            Texture2D tex = TextureOf(sheet);
            if (tex == null) { return null; }   // 不缓存失败结果
            float colW = tex.width / 3f;
            float x = Mathf.Clamp(pose, 0, 2) * colW;
            // 稍微向内收一点边，避免切到相邻姿态
            float inset = colW * 0.04f;
            Rect rect = new Rect(x + inset, 0f, colW - inset * 2f, tex.height);
            Sprite sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            sp.name = key;
            Cache[key] = sp;
            return sp;
        }

        /// <summary>
        /// 给一个 Image 装上卡面立绘：cover 裁切（宽高都铺满卡框），顶端对齐，
        /// 这样人物头部始终留在卡面上半部不会被裁掉，底部留给名称与数值。
        /// </summary>
        public static void Apply(UnityEngine.UI.Image img, string cardId, int cost, float cardW, float cardH, float topInset)
        {
            if (img == null) { return; }
            Sprite sp = Portrait(cardId, cost);
            if (sp == null) { img.enabled = false; return; }
            img.enabled = true;
            img.sprite = sp;
            img.type = UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = true;
            img.color = Color.white;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            float sw = sp.rect.width;
            float sh = sp.rect.height;
            float spriteAspect = sw / sh;
            float cardAspect = cardW / cardH;
            // 比例接近卡框（专属立绘就是 3:4）→ cover 铺满，精确对齐零裁切；
            // 比例差很多（三姿图切出来的 0.46 竖条）→ 改成 contain，宁可两侧留底色也不要裁头。
            float scale = Mathf.Abs(spriteAspect - cardAspect) < 0.05f
                ? Mathf.Max(cardW / sw, cardH / sh)
                : Mathf.Min(cardW / sw, cardH / sh);
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(sw * scale, sh * scale);
            rt.anchoredPosition = new Vector2(0f, -topInset);

            // V0.7 第 4 批：卡面人物关键帧（Art/Units/Anim/<卡ID>/<卡ID>_NN）存在就让立绘"呼吸"；
            // 没交帧的卡（目前还有 144 张）走"程序化呼吸"，保证棋盘上手牌里没有一张是完全静止的。
            Sprite[] frames = CardFrames(cardId);
            var anim = img.GetComponent<V06CardFaceAnim>();
            if (anim == null) { anim = img.gameObject.AddComponent<V06CardFaceAnim>(); }
            anim.Bind(frames, 3.2f);
        }

        // ── 第 4 批：卡面人物关键帧加载 ──
        private static readonly System.Collections.Generic.Dictionary<string, Sprite[]> FrameCache =
            new System.Collections.Generic.Dictionary<string, Sprite[]>();

        /// <summary>取一张卡的人物关键帧（最多 4 帧）。没有交就返回 null。</summary>
        public static Sprite[] CardFrames(string cardId)
        {
            return CardFrames(cardId, "");
        }

        /// <summary>取一张卡某个动作的关键帧。action = "" 待机 / "attack" 出手 / "hit" 受击 / "bloom" 绽放。</summary>
        public static Sprite[] CardFrames(string cardId, string action)
        {
            if (string.IsNullOrEmpty(cardId)) { return null; }
            string key = string.IsNullOrEmpty(action) ? cardId : (cardId + "/" + action);
            Sprite[] arr;
            if (FrameCache.TryGetValue(key, out arr))
            {
                if (arr != null) { return arr; }
                FrameCache.Remove(key);
            }
            string folder = string.IsNullOrEmpty(action)
                ? ("Art/Units/Anim/" + cardId + "/")
                : ("Art/Units/Anim/" + cardId + "/" + action + "/");
            Sprite[] frames = new Sprite[4];
            int loaded = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                string f = folder + cardId + "_" + (i < 10 ? "0" + i : "" + i);
                Sprite s = Resources.Load<Sprite>(f);
                frames[i] = s;
                if (s != null) { loaded++; }
            }
            if (loaded == 0) { return null; }
            FrameCache[key] = frames;
            return frames;
        }

        /// <summary>第 4 批的卡面动画是否到货（验收用）：待机/出手/受击/绽放各多少套。</summary>
        public static string ReportFrames(int howMany)
        {
            int idle = 0, atk = 0, hit = 0, bloom = 0;
            int n = 0;
            for (int i = 0; i < V06Catalog.All.Count && n < howMany; i++)
            {
                string id = V06Catalog.All[i].Id;
                n++;
                if (CardFrames(id) != null) { idle++; }
                if (CardFrames(id, "attack") != null) { atk++; }
                if (CardFrames(id, "hit") != null) { hit++; }
                if (CardFrames(id, "bloom") != null) { bloom++; }
            }
            return "抽查 " + n + " 张：待机 " + idle + "，出手 " + atk + "，受击 " + hit + "，绽放 " + bloom;
        }
    }

    /// <summary>
    /// 卡面立绘的"呼吸"播放器（挂在卡面 Image 上）。
    ///   · 有关键帧 → 逐帧循环（3~4 fps）；
    ///   · 没有关键帧 → **程序化呼吸**（极小幅缩放 + 轻微亮度起伏），保证任何一张卡都不是死的。
    /// </summary>
    public sealed class V06CardFaceAnim : MonoBehaviour
    {
        private Sprite[] _frames;
        private float _fps = 3.2f;
        private float _t;
        private int _index;
        private UnityEngine.UI.Image _img;
        private bool _proc;             // 程序化呼吸模式
        private float _phase;           // 相位错开，避免整屏卡同时起伏
        private Vector3 _baseScale = Vector3.one;
        private Color _baseColor = Color.white;
        private bool _bound;

        public void Bind(Sprite[] frames, float fps)
        {
            bool proc = frames == null || frames.Length < 2;
            // 状态没变就不重绑（Refresh 每帧都会调，重绑会让动画卡住）
            if (_bound && proc == _proc && ReferenceEquals(frames, _frames)) { return; }
            _frames = frames;
            _proc = proc;
            _fps = fps <= 0f ? 3.2f : fps;
            _t = 0f;
            _index = 0;
            _phase = (GetInstanceID() & 0x7ff) / 2048f * 6.283f;
            if (!_bound)
            {
                _baseScale = transform.localScale;
                _img = GetComponent<UnityEngine.UI.Image>();
                if (_img != null) { _baseColor = _img.color; }
            }
            _bound = true;
            enabled = true;
        }

        private void OnDisable()
        {
            // 离开时把形变还原，避免残留在界面上
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            if (!_bound) { return; }
            if (_img == null) { _img = GetComponent<UnityEngine.UI.Image>(); }
            if (_proc)
            {
                // 程序化呼吸：缩放 1.000~1.014、亮度 0.97~1.00，周期约 3.2 秒
                float w = Time.unscaledTime * 1.96f + _phase;
                float s = 1f + 0.014f * (0.5f + 0.5f * Mathf.Sin(w));
                transform.localScale = new Vector3(_baseScale.x * s, _baseScale.y * s, _baseScale.z);
                if (_img != null)
                {
                    float b = 0.965f + 0.035f * (0.5f + 0.5f * Mathf.Sin(w + 1.1f));
                    _img.color = new Color(_baseColor.r * b, _baseColor.g * b, _baseColor.b * b, _baseColor.a);
                }
                return;
            }
            if (_frames == null || _frames.Length < 2) { return; }
            _t += Time.unscaledDeltaTime;
            float per = 1f / _fps;
            if (_t < per) { return; }
            _t -= per;
            _index = (_index + 1) % _frames.Length;
            Sprite sp = _frames[_index];
            if (sp != null && _img != null) { _img.sprite = sp; }
        }
    }
}
