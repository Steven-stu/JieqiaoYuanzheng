using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 战斗界面的"手感层"接线器（V0.7 第二版）。
    ///
    /// 职责边界（与 GPT 交付的 V06HudFx 契约一致）：
    ///   · 只在**结算成功后**读取状态差异，然后播特效；
    ///   · 绝不修改引擎数据、不新增可点目标、不接管输入路径；
    ///   · 所有特效对象都挂在 raycastTarget=false 的 FxLayer 下，播完自动销毁。
    ///
    /// 素材约定（GPT 交付路径，缺哪个就跳过哪个，不会报错）：
    ///   Art/UI/V06/FX/&lt;效果名&gt;/&lt;效果名&gt;_00.png …   或者扁平放 Art/UI/V06/FX/&lt;效果名&gt;_00.png
    ///   效果名见 FxTable；每套帧数固定，帧率 20fps。
    /// 兼容上一批的 hit_00..05 / guard_00..05（作为 slash / guard 的兜底）。
    /// </summary>
    public sealed class V06BattleFx : MonoBehaviour
    {
        /// <summary>全屏特效层（raycastTarget=false，放在 LayoutRoot 下、右键详览浮层之下）。</summary>
        public RectTransform Layer;
        /// <summary>12 个棋盘格：0–5 我方、6–11 敌方（与 V06BattleUI 的 _slotMain 索引一致）。</summary>
        public RectTransform[] Slots = new RectTransform[12];
        /// <summary>棋盘格立绘（用于闪光）。</summary>
        public Image[] SlotArt = new Image[12];
        public Image MineFlagFill;
        public Image EnemyFlagFill;
        public RectTransform MineFlagRect;
        public RectTransform EnemyFlagRect;
        /// <summary>调试用：把每次触发写进 Console。</summary>
        public bool Verbose;

        /// <summary>已经播过多少次特效（自动化测试用来确认确实触发了）。</summary>
        public int FxCount { get; private set; }
        /// <summary>本次会话里用序列帧播出的次数（区别于纯数字/闪光）。</summary>
        public int SequenceCount { get; private set; }
        public bool Ready { get { return Layer != null; } }

        private static readonly Color DamageColor = new Color(1f, 0.86f, 0.62f, 1f);
        private static readonly Color EnemyDamageColor = new Color(1f, 0.55f, 0.48f, 1f);
        private static readonly Color FlagDamageColor = new Color(1f, 0.42f, 0.42f, 1f);

        // ───────────────────────── 特效表 ─────────────────────────

        /// <summary>效果名 → {帧数, 显示边长(设计单位)}。与交付清单第四节一一对应。</summary>
        private static readonly string[] FxNames = new string[]
        {
            "slash", "shot", "spell", "flag", "guard", "wall", "heal", "buff",
            "death", "summon", "evolve", "fuse", "drain", "ember", "echo", "pressure"
        };
        private static readonly int[] FxFrames = new int[]
        {
            6, 6, 6, 8, 6, 6, 6, 6,
            6, 6, 8, 8, 6, 6, 6, 6
        };
        private static readonly float[] FxSize = new float[]
        {
            92f, 92f, 118f, 180f, 92f, 92f, 92f, 92f,
            92f, 118f, 150f, 150f, 118f, 92f, 92f, 150f
        };
        /// <summary>效果名缺失时改用的兜底效果（-1 表示没有兜底）。</summary>
        private static readonly int[] FxFallback = new int[]
        {
            16, 0, 2, 3, 4, 4, 6, 7,
            8, 9, 10, 11, 12, 13, 14, 15
        };

        private Sprite[][] _seq;
        private bool _seqLoaded;

        public bool HasSequence(string name) { return GetSeq(name) != null; }

        /// <summary>已加载到几套序列帧（自动化验收用）。</summary>
        public int LoadedSequences
        {
            get
            {
                LoadSequences();
                int n = 0;
                for (int i = 0; i < _seq.Length; i++) { if (_seq[i] != null) { n++; } }
                return n;
            }
        }

        /// <summary>已加载序列帧的名字列表（自动化验收用）。</summary>
        public string LoadedSequenceNames
        {
            get
            {
                LoadSequences();
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < _seq.Length; i++)
                {
                    if (_seq[i] == null) { continue; }
                    if (sb.Length > 0) { sb.Append(','); }
                    sb.Append(FxNames[i]);
                }
                return sb.ToString();
            }
        }

        private int IndexOf(string name)
        {
            for (int i = 0; i < FxNames.Length; i++) { if (FxNames[i] == name) { return i; } }
            return -1;
        }

        private Sprite[] GetSeq(string name)
        {
            int idx = IndexOf(name);
            if (idx < 0) { return null; }
            LoadSequences();
            if (_seq[idx] != null) { return _seq[idx]; }
            int fb = FxFallback[idx];
            if (fb >= 0 && fb != idx && _seq[fb] != null) { return _seq[fb]; }
            return null;
        }

        private void LoadSequences()
        {
            if (_seqLoaded) { return; }
            _seqLoaded = true;
            _seq = new Sprite[FxNames.Length][];
            for (int i = 0; i < FxNames.Length; i++)
            {
                string name = FxNames[i];
                Sprite[] arr = new Sprite[FxFrames[i]];
                int loaded = 0;
                for (int f = 0; f < arr.Length; f++)
                {
                    string suffix = name + "_" + (f < 10 ? "0" + f : "" + f);
                    Sprite s = Resources.Load<Sprite>("Art/UI/V06/FX/" + name + "/" + suffix);
                    if (s == null) { s = Resources.Load<Sprite>("Art/UI/V06/FX/" + suffix); }
                    arr[f] = s;
                    if (s != null) { loaded++; }
                }
                _seq[i] = loaded > 0 ? arr : null;
            }
            // 上一批的 hit_00..05 作为 slash 的兜底（那时名字叫 hit 不叫 slash）
            if (_seq[0] == null)
            {
                Sprite[] hit = new Sprite[6];
                int n = 0;
                for (int f = 0; f < 6; f++)
                {
                    hit[f] = Resources.Load<Sprite>("Art/UI/V06/FX/hit_0" + f);
                    if (hit[f] != null) { n++; }
                }
                if (n > 0) { _seq[0] = hit; }
            }
            if (Verbose) { Debug.Log("[V06Fx] 已加载序列帧：" + LoadedSequenceNames + "（" + LoadedSequences + " 套）"); }
        }

        private void Awake() { LoadSequences(); }

        // ───────────────────────── 快照 / 差异 ─────────────────────────

        private struct Snap
        {
            public int[,] Hp;      // [camp, slot] 当前生命；空格 = -1
            public int[,] Uid;
            public string[,] Card;
            public int[] Flag;
            public bool Valid;
        }

        private Snap _snap;

        /// <summary>在 Apply 之前调用：记录局面快照。</summary>
        public void Capture(V06Engine e)
        {
            _snap = new Snap();
            if (e == null || e.Players[0] == null || e.Players[1] == null) { _snap.Valid = false; return; }
            _snap.Hp = new int[2, V06Slot.Total];
            _snap.Uid = new int[2, V06Slot.Total];
            _snap.Card = new string[2, V06Slot.Total];
            _snap.Flag = new int[2];
            for (int camp = 0; camp < 2; camp++)
            {
                _snap.Flag[camp] = e.Players[camp].FlagHp;
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = e.Players[camp].At(s);
                    _snap.Hp[camp, s] = u == null ? -1 : u.CurHp;
                    _snap.Uid[camp, s] = u == null ? -1 : u.Uid;
                    _snap.Card[camp, s] = u == null ? null : u.CardId;
                }
            }
            _snap.Valid = true;
        }

        /// <summary>Apply 之后调用：播放与快照之间的差异特效。action 可为 null（如对手 AI 的回合）。</summary>
        public void Diff(V06Engine e) { Diff(e, null); }

        public void Diff(V06Engine e, V06Action action)
        {
            if (!_snap.Valid || e == null || e.Players[0] == null || e.Players[1] == null) { return; }
            if (!Ready) { return; }
            LoadSequences();
            int myCamp = 0;
            bool isAttack = action != null && action.Kind == V06ActionKind.Attack;
            bool isFuse = action != null && action.Kind == V06ActionKind.Fuse;
            bool isPlay = action != null && action.Kind == V06ActionKind.PlayCard;
            int attackCamp = -1;
            bool attackRanged = false;
            if (isAttack)
            {
                attackCamp = 1 - e.Active;               // 结算后 Active 回到攻击方
                if (attackCamp < 0 || attackCamp > 1) { attackCamp = e.Active; }
            }

            bool flagLost0 = e.Players[0].FlagHp < _snap.Flag[0];
            bool flagLost1 = e.Players[1].FlagHp < _snap.Flag[1];
            bool bothFlags = flagLost0 && flagLost1;

            for (int camp = 0; camp < 2; camp++)
            {
                for (int s = 0; s < V06Slot.Total; s++)
                {
                    V06Unit u = e.Players[camp].At(s);
                    int beforeHp = _snap.Hp[camp, s];
                    int beforeUid = _snap.Uid[camp, s];
                    string beforeCard = _snap.Card[camp, s];

                    if (u == null)
                    {
                        // 这一格原来是活的、现在空了 → 离场
                        if (beforeUid >= 0)
                        {
                            PlayEffect("death", camp, s);
                            if (CardHasKeyword(beforeCard, "余烬")) { PlayEffect("ember", camp, s); }
                        }
                        continue;
                    }

                    if (beforeUid != u.Uid)
                    {
                        if (beforeUid >= 0 && !isFuse)
                        {
                            PlayEffect("death", camp, s);
                            if (CardHasKeyword(beforeCard, "余烬")) { PlayEffect("ember", camp, s); }
                        }
                        PlayEffect(isFuse ? "fuse" : "summon", camp, s);
                        continue;
                    }

                    // 同一个单位：形态变了 = 进化
                    if (beforeCard != null && beforeCard != u.CardId)
                    {
                        PlayEffect("evolve", camp, s);
                    }

                    int delta = beforeHp - u.CurHp;
                    if (delta > 0)
                    {
                        bool hit = isAttack && camp != attackCamp;
                        string name = "slash";
                        if (hit)
                        {
                            name = attackRanged ? "shot" : "slash";
                        }
                        else if (isPlay)
                        {
                            name = "spell";
                        }
                        PlayDamage(camp, s, delta, camp == myCamp ? EnemyDamageColor : DamageColor, name);
                        if (u.Has("坚壁")) { PlayEffect("wall", camp, s); }
                        if (delta > 0 && isAttack && u.Has("汲取")) { PlayEffect("drain", camp, s); }
                    }
                    else if (delta < 0)
                    {
                        int healed = -delta;
                        PlayNumber(camp, s, healed, new Color(0.55f, 0.95f, 0.7f, 1f), true);
                        PlayEffect(isAttack ? "drain" : "heal", camp, s);
                    }
                }
            }

            if (flagLost0 || flagLost1)
            {
                if (bothFlags) { PlayPressure(); }
                if (flagLost0) { PlayFlagDamage(0, _snap.Flag[0] - e.Players[0].FlagHp); }
                if (flagLost1) { PlayFlagDamage(1, _snap.Flag[1] - e.Players[1].FlagHp); }
            }
        }

        /// <summary>被完全挡下的攻击播"护卫脉冲"（在声明攻击的目标格上）。</summary>
        public void PlayGuard(int camp, int slot)
        {
            if (!Ready) { return; }
            LoadSequences();
            PlayEffect("guard", camp, slot);
            Image art = SlotArtOf(camp, slot);
            if (art != null) { V06HudFx.Flash(art, new Color(0.62f, 0.86f, 1f, 1f), 0.22f); }
        }

        // ───────────────────────── 具体表现 ─────────────────────────

        private void PlayDamage(int camp, int slot, int amount, Color color, string fxName)
        {
            PlayNumber(camp, slot, amount, color, false);
            Image art = SlotArtOf(camp, slot);
            if (art != null) { V06HudFx.Flash(art, new Color(1f, 0.55f, 0.5f, 1f), 0.2f); }
            PlayEffect(fxName, camp, slot);
            RectTransform marker = SpawnMarker(camp, slot, new Color(1f, 0.35f, 0.3f, 0.16f));
            if (marker != null) { V06HudFx.Shake(marker, 5f, 0.22f); }
        }

        private void PlayNumber(int camp, int slot, int amount, Color color, bool positive)
        {
            RectTransform slotRt = SlotRectOf(camp, slot);
            if (slotRt == null) { return; }
            Vector2 pos = ToLayerLocal(slotRt);
            V06HudFx.DamageNumber(Layer, pos + new Vector2(0f, 16f), positive ? -amount : amount, color);
            FxCount++;
            if (Verbose) { Debug.Log("[V06Fx] 数字 " + (positive ? "+" : "-") + amount + " @ camp" + camp + "/slot" + slot); }
        }

        private void PlayFlagDamage(int camp, int amount)
        {
            RectTransform flagRt = camp == 0 ? MineFlagRect : EnemyFlagRect;
            Image fill = camp == 0 ? MineFlagFill : EnemyFlagFill;
            if (fill != null) { V06HudFx.Flash(fill, new Color(1f, 0.45f, 0.45f, 1f), 0.24f); }
            if (flagRt == null) { return; }
            Vector2 pos = ToLayerLocal(flagRt);
            V06HudFx.DamageNumber(Layer, pos, amount, FlagDamageColor);
            FxCount++;
            PlayAt("flag", pos, FxSize[IndexOf("flag")]);
            if (Verbose) { Debug.Log("[V06Fx] 旗伤害 " + amount + " @ camp" + camp); }
        }

        /// <summary>战场压力：双方旗同时掉血时，在屏幕中间偏下播一次地裂。</summary>
        private void PlayPressure()
        {
            if (Layer == null) { return; }
            Vector2 center = new Vector2(0f, Layer.rect.height * 0.06f);
            PlayAt("pressure", center, FxSize[IndexOf("pressure")]);
        }

        private void PlayEffect(string name, int camp, int slot)
        {
            RectTransform slotRt = SlotRectOf(camp, slot);
            if (slotRt == null) { return; }
            int idx = IndexOf(name);
            float size = idx >= 0 ? FxSize[idx] : 92f;
            PlayAt(name, ToLayerLocal(slotRt), size);
        }

        /// <summary>在 FxLayer 的指定局部坐标播一套序列帧；素材缺失时静默跳过。</summary>
        private void PlayAt(string name, Vector2 pos, float size)
        {
            string sfx = SfxFor(name);
            if (sfx != null) { V06Audio.Play(sfx); }
            Sprite[] frames = GetSeq(name);
            if (frames == null) { return; }
            StartCoroutine(PlayFrameRoutine(pos, size, frames));
            FxCount++;
            SequenceCount++;
        }

        /// <summary>特效名 → 音效名（素材缺就静默）。</summary>
        private static string SfxFor(string fxName)
        {
            switch (fxName)
            {
                case "slash": return "sfx_hit";
                case "shot": return "sfx_shoot";
                case "spell": return "sfx_laser";
                case "flag": return "sfx_flag";
                case "guard": return "sfx_guard";
                case "wall": return "sfx_guard";
                case "heal": return "sfx_heal";
                case "buff": return "sfx_coin";
                case "death": return "sfx_death";
                case "summon": return "sfx_confirm";
                case "evolve": return "sfx_evolve";
                case "fuse": return "sfx_fuse";
                case "drain": return "sfx_heal";
                case "ember": return "sfx_explosion";
                case "echo": return "sfx_coin";
                case "pressure": return "sfx_explosion";
            }
            return null;
        }

        private IEnumerator PlayFrameRoutine(Vector2 pos, float size, Sprite[] frames)
        {
            var go = new GameObject("V06 FX " + (frames.Length > 0 && frames[0] != null ? frames[0].name : "seq"),
                typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            const float perFrame = 0.05f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) { continue; }
                img.sprite = frames[i];
                img.enabled = true;
                float t = 0f;
                while (t < perFrame)
                {
                    if (go == null) { yield break; }
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            if (go != null) { Object.Destroy(go); }
        }

        /// <summary>生成一个不接收射线的抖动标记（V06HudFx.Shake 会拒绝有射线的节点）。</summary>
        private RectTransform SpawnMarker(int camp, int slot, Color color)
        {
            RectTransform slotRt = SlotRectOf(camp, slot);
            if (slotRt == null) { return null; }
            var go = new GameObject("V06 FX Marker", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Layer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = slotRt.rect.size;
            rt.anchoredPosition = ToLayerLocal(slotRt);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            StartCoroutine(FadeAndDestroy(go, img));
            return rt;
        }

        private IEnumerator FadeAndDestroy(GameObject go, Image img)
        {
            float t = 0f;
            const float dur = 0.26f;
            Color start = img.color;
            while (t < dur)
            {
                if (go == null) { yield break; }
                t += Time.unscaledDeltaTime;
                Color c = start;
                c.a = start.a * (1f - t / dur);
                img.color = c;
                yield return null;
            }
            if (go != null) { Object.Destroy(go); }
        }

        // ───────────────────────── 工具 ─────────────────────────

        private static bool CardHasKeyword(string cardId, string kw)
        {
            V06Card c = V06Catalog.ById(cardId);
            if (c == null || c.Keywords == null) { return false; }
            for (int i = 0; i < c.Keywords.Length; i++) { if (c.Keywords[i] == kw) { return true; } }
            return false;
        }

        private RectTransform SlotRectOf(int camp, int slot)
        {
            int idx = camp * V06Slot.Total + slot;
            if (Slots == null || idx < 0 || idx >= Slots.Length) { return null; }
            return Slots[idx];
        }

        private Image SlotArtOf(int camp, int slot)
        {
            int idx = camp * V06Slot.Total + slot;
            if (SlotArt == null || idx < 0 || idx >= SlotArt.Length) { return null; }
            return SlotArt[idx];
        }

        /// <summary>把任意 UI 节点的中心换算成 FxLayer 的局部坐标（Layer 的 pivot 为 0.5）。</summary>
        private Vector2 ToLayerLocal(RectTransform target)
        {
            if (Layer == null || target == null) { return Vector2.zero; }
            Vector3 world = target.TransformPoint(target.rect.center);
            Vector3 local = Layer.InverseTransformPoint(world);
            return new Vector2(local.x, local.y);
        }
    }
}
