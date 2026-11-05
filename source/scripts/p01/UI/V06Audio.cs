using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jieqiao.UI
{
    /// <summary>
    /// 音频（V0.7 第二版）：能**无缝衔接**的背景音乐 + 界面/战斗音效。
    ///
    /// 不用 AudioSource.loop（循环点会硬切"咔"一下），改成**双 AudioSource 交叉淡入**：
    ///   · 当前曲播到离结尾 1.6 秒时，在另一个源上把同一首从头起播并淡入，旧源同时淡出 → 无缝循环；
    ///   · 切曲（大厅 ↔ 对战）也走同一套交叉淡入，不会突然断。
    ///
    /// 曲目（全部 Kevin MacLeod，授权 CC BY 4.0，署名见 Assets/Resources/Audio/CREDITS.txt）：
    ///   menu         = Village Consort             （大厅：村庄合奏，明亮松弛）
    ///   lobby_soft   = Royal Coupling              （图鉴/组卡/资料/联机/签到：宫廷管弦，悠扬华丽）
    ///   battle_intro = The Snow Queen              （进入对局那一刻的进场曲）
    ///   battle       = The Path of the Goblin King （对局进行中的主曲）
    /// 用户要求：这两首**专用于对战**（进场 + 对局中）。
    /// </summary>
    public static class V06Audio
    {
        public static float BgmVolume = 0.32f;
        public static float SfxVolume = 0.75f;
        public static bool Muted;

        /// <summary>进入对局的进场曲（07 The Snow Queen）。</summary>
        public const string BattleIntroTrack = "battle_intro";
        /// <summary>对局进行中的主曲（08 The Path of the Goblin King）。</summary>
        public const string BattleTrack = "battle";
        /// <summary>进场曲播多久后交叉淡入主曲（秒）。</summary>
        public const float BattleIntroSeconds = 9f;

        /// <summary>对战曲（保留旧接口：现在固定返回主曲，进场曲由 BattleIntroTrack 负责）。</summary>
        public static string PickBattle(int seed)
        {
            return BattleTrack;
        }

        private const float FadeSeconds = 2.8f;   // 更长的淡入淡出，接缝听不出来
        private const float CrossStart = 3.2f;    // 提前 3.2 秒就开始交叉

        private static AudioSource _srcA;
        private static AudioSource _srcB;
        private static AudioSource _active;
        private static AudioSource _fading;
        private static AudioSource _sfx;

        private static float _activeFade = 1f;
        private static float _fadingAmount;
        private static bool _loopArmed = true;
        private static string _currentBgm = "";
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static bool Ready { get { return _srcA != null && _srcB != null && _sfx != null; } }
        public static string CurrentBgm { get { return _currentBgm; } }
        public static int CachedClips { get { return Clips.Count; } }
        public static float CurrentLength { get { return _active != null && _active.clip != null ? _active.clip.length : 0f; } }
        /// <summary>双源是否都在使用（交叉淡入生效的证据）。</summary>
        public static bool Crossfading { get { return _fading != null && _fading.isPlaying; } }

        public static void Init()
        {
            if (Ready) { return; }
            var go = new GameObject("V06Audio");
            Object.DontDestroyOnLoad(go);
            _srcA = go.AddComponent<AudioSource>();
            _srcB = go.AddComponent<AudioSource>();
            _sfx = go.AddComponent<AudioSource>();
            AudioSource[] all = new AudioSource[] { _srcA, _srcB };
            for (int i = 0; i < all.Length; i++)
            {
                all[i].loop = false;          // 循环由交叉淡入完成
                all[i].playOnAwake = false;
                all[i].volume = 0f;
                all[i].spatialBlend = 0f;
            }
            _sfx.loop = false;
            _sfx.playOnAwake = false;
            _sfx.volume = SfxVolume;
            _active = _srcA;
            var driver = go.AddComponent<V06AudioDriver>();
            driver.Bind();
        }

        private static AudioClip Clip(string folder, string name)
        {
            string key = folder + "/" + name;
            AudioClip c;
            if (Clips.TryGetValue(key, out c))
            {
                if (c != null) { return c; }
                Clips.Remove(key);
            }
            c = Resources.Load<AudioClip>(key);
            if (c != null) { Clips[key] = c; }
            return c;
        }

        /// <summary>切背景音乐（交叉淡入淡出）。重复调用同一首不会重播。</summary>
        public static void Music(string name)
        {
            Init();
            if (_currentBgm == name) { return; }
            AudioClip c = Clip("Audio/Bgm", name);
            if (c == null) { Debug.LogWarning("[V06Audio] 缺 BGM：" + name); return; }
            _currentBgm = name;
            SwapTo(c, true);
        }

        private static void SwapTo(AudioClip clip, bool fadeOld)
        {
            AudioSource next = (_active == _srcA) ? _srcB : _srcA;
            next.clip = clip;
            next.time = 0f;
            next.volume = 0f;
            next.Play();
            if (fadeOld && _active != null && _active.isPlaying && _active.clip != null)
            {
                _fading = _active;
                _fadingAmount = 1f;
            }
            _active = next;
            _activeFade = 0f;
            _loopArmed = false;
        }

        public static void StopMusic()
        {
            if (_srcA != null) { _srcA.Stop(); _srcA.volume = 0f; }
            if (_srcB != null) { _srcB.Stop(); _srcB.volume = 0f; }
            _fading = null;
            _currentBgm = "";
        }

        /// <summary>每帧驱动（V06AudioDriver 调用）。</summary>
        internal static void Tick(float dt)
        {
            if (!Ready || _active == null || _active.clip == null) { return; }
            float target = Muted ? 0f : BgmVolume;
            float step = dt / FadeSeconds;

            // 无缝循环：快到结尾就把同一首在另一个源上从头淡入（每次循环只做一次）
            float remain = _active.clip.length - _active.time;
            if (remain > CrossStart + 0.5f) { _loopArmed = true; }
            if (_loopArmed && remain <= CrossStart && remain > 0f)
            {
                _loopArmed = false;
                SwapTo(_active.clip, true);
            }

            _activeFade = Mathf.MoveTowards(_activeFade, 1f, step);
            // 等功率曲线：WAV 音量线性相加会在交叉点掉一截，用 sqrt 补偿更连贯
            _active.volume = target * Mathf.Sqrt(Mathf.Clamp01(_activeFade));

            if (_fading != null)
            {
                if (_fading == _active) { _fading = null; }
                else
                {
                    _fadingAmount = Mathf.MoveTowards(_fadingAmount, 0f, step);
                    _fading.volume = target * Mathf.Sqrt(Mathf.Clamp01(_fadingAmount));
                    if (_fadingAmount <= 0.002f)
                    {
                        _fading.Stop();
                        _fading = null;
                    }
                }
            }
        }

        /// <summary>播一个音效（不带路径，例如 "sfx_click"）。</summary>
        public static void Play(string name)
        {
            if (string.IsNullOrEmpty(name) || Muted) { return; }
            Init();
            AudioClip c = Clip("Audio/Sfx", name);
            if (c == null) { return; }
            _sfx.PlayOneShot(c, SfxVolume);
        }

        /// <summary>给按钮挂"点击音 + 按下回弹"。UiKit.Button 里统一调用，全界面生效。</summary>
        public static void ClickOn(Button btn)
        {
            if (btn == null) { return; }
            var rt = btn.GetComponent<RectTransform>();
            btn.onClick.AddListener(delegate
            {
                Play("sfx_click");
                if (rt != null) { V06UiV07.Press(rt); }
            });
        }

        public static void SetMuted(bool muted)
        {
            Muted = muted;
            if (_sfx != null) { _sfx.volume = muted ? 0f : SfxVolume; }
        }

        public static string StatusForTest
        {
            get
            {
                return "ready=" + Ready + " bgm=" + (_currentBgm.Length == 0 ? "—" : _currentBgm)
                       + " 时长=" + Mathf.RoundToInt(CurrentLength) + "s 交叉淡入=" + Crossfading
                       + " 缓存=" + CachedClips + " 静音=" + Muted;
            }
        }
    }

    /// <summary>每帧驱动 V06Audio 的交叉淡入与无缝循环。</summary>
    public sealed class V06AudioDriver : MonoBehaviour
    {
        internal void Bind() { }
        private void Update() { V06Audio.Tick(Time.unscaledDeltaTime); }
    }
}
