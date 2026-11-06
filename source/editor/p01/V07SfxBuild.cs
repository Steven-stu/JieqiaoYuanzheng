#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// 界桥远征 V0.7 · 按用户挑选构建 42 槽位音效包
/// 处理:去首尾静音 → 截到目标时长 → 淡入淡出 → 响度归一到目标 RMS → 峰值限幅 → 44.1k 16bit 单声道 WAV
public static class V07SfxBuild
{
    const int SR = 44100;
    const string SRC_DIR = "Assets/_SfxSrc";
    const string OUT_DIR = "Assets/Resources/Audio/Sfx";
    const string ROOT = @"D:\deepseekharness\界桥远征_音效候选2";

    // 槽位, 相对路径, 最长秒数, 目标RMS(dBFS), 说明
    struct Pick
    {
        public string Slot, Rel, Note;
        public float Max, TargetRmsDb;
        public Pick(string s, string r, float m, float t, string n) { Slot = s; Rel = r; Max = m; TargetRmsDb = t; Note = n; }
    }

    static readonly Pick[] Picks = new Pick[]
    {
        new Pick("R01", @"R01_主按钮\R01_05__click3.wav",                 0.30f, -20f, "主按钮"),
        new Pick("R02", @"R02_次按钮\R02_02__click4.wav",                 0.16f, -21f, "次按钮"),
        new Pick("R03", @"R03_页签切换\R03_06__switch6.wav",              0.35f, -20f, "页签"),
        new Pick("R04", @"R04_列表翻页\R04_06__bookFlip2.ogg",            0.35f, -20f, "翻页"),
        new Pick("R05", @"R04_列表翻页\R04_06__bookFlip2.ogg",            0.45f, -20f, "返回大厅"),
        new Pick("R06", @"R06_操作无效\R06_02__033_Denied_03.wav",        0.35f, -19f, "无效"),
        new Pick("R07", @"R07_文本输入\R07_04__mouserelease1.wav",        0.20f, -22f, "输入"),
        new Pick("R08", @"R08_提示条\R08_03__switch2.wav",                0.35f, -20f, "提示条"),
        new Pick("R09", @"R09_抽牌\R09_06__card-slide-6.ogg",             0.25f, -19f, "抽牌"),
        new Pick("R11", @"R11_打出手牌\R11_01__card-place-1.ogg",         0.35f, -18f, "打出"),
        new Pick("R12", @"R12_洗牌\R12_01__card-shuffle.ogg",             0.80f, -19f, "洗牌"),
        new Pick("R14", @"R14_开卡包\R14_06__Inventory_Open_01.mp3",      0.90f, -18f, "开包"),
        new Pick("R15", @"R15_稀有卡\R15_06__Chime_Save.mp3",             1.40f, -19f, "稀有卡"),
        new Pick("R16", @"R16_金币获得\R16_01__Pickup_Gold_00.mp3",       0.70f, -18f, "金币"),
        new Pick("R17", @"R17_金币不足\R17_01__chips-collide-1.ogg",     0.50f, -19f, "金币不足"),
        new Pick("R18", @"R18_宝石\R18_05__bfh1_glass_breaking_03.ogg",   0.60f, -19f, "宝石"),
        new Pick("R19", @"R19_购买成功\R19_05__Pickup_Gold_03.mp3",       0.80f, -18f, "购买成功"),
        new Pick("R20", @"R20_售罄\R20_06__bfh1_metal_hit_06.ogg",        0.50f, -19f, "售罄"),
        new Pick("R21", @"R21_签到提示\R21_02__Chime_Cancel.mp3",         0.50f, -20f, "签到提示"),
        new Pick("R22", @"R22_签到盖章\R22_06__chip-lay-3.ogg",           0.50f, -18f, "签到盖章"),
        new Pick("R23", @"R23_签到升级\R23_01__Jingle_Achievement_00.mp3",1.20f, -18f, "签到升级"),
        new Pick("R24", @"R24_近战挥击\R24_03__swish_4.wav",              0.35f, -16.5f, "挥击"),
        new Pick("R25", @"R25_近战命中\R25_06__bfh1_hit_06.ogg",          0.30f, -16f, "命中"),
        new Pick("R26", @"R26_远程射击\R26_02__swish_2.wav",              0.40f, -16.5f, "射击"),
        new Pick("R27a",@"R27_法术释放\R27_01__Spell_00.mp3",             0.60f, -17f, "法术(施放)"),
        new Pick("R27b",@"R27_法术释放\R27_03__Spell_02.mp3",             0.60f, -16.5f, "法术(攻击)"),
        new Pick("R28", @"R28_格挡\R28_05__bfh1_metal_hit_05.ogg",        0.50f, -16.5f, "格挡"),
        new Pick("R29", @"R29_轻受击\R29_03__bfh1_hit_06.ogg",            0.25f, -17f, "轻受击"),
        new Pick("R30", @"R29_轻受击\R29_04__bfh1_hit_07.ogg",            0.70f, -15.5f, "重受击"),
        new Pick("R31", @"R31_增益\R31_04__enchant.wav",                  0.50f, -17.5f, "增益"),
        new Pick("R32", @"R32_治疗\R32_01__blessing2.wav",                0.70f, -18f, "治疗"),
        new Pick("R33", @"R33_单位阵亡\R33_03__bfh1_rock_falling_03.ogg", 0.90f, -16.5f, "阵亡"),
        new Pick("R34", @"R34_进化\R34_06__Jingle_Achievement_00.mp3",    1.40f, -17f, "进化"),
        new Pick("R35", @"R34_进化\R34_05__enchant2.wav",                 1.60f, -17f, "融合"),
        new Pick("R36", @"R36_爆炸\R36_01__explode.wav",                  0.80f, -15.5f, "爆炸"),
        new Pick("R37", @"R37_旗受伤\R37_06__door_ripped_2.wav",          0.70f, -16.5f, "旗受伤"),
        new Pick("R38", @"R37_旗受伤\R37_05__door_ripped_1.wav",          1.30f, -16f, "旗破裂"),
        new Pick("R39", @"R39_回合切换\R39_03__turn_page.wav",            1.00f, -19f, "回合"),
        new Pick("R40", @"R17_金币不足\R17_05__ALERT_Error.wav",          1.50f, -18f, "压力警告"),
        new Pick("R41", @"R21_签到提示\R21_01__Jingle_Achievement_00.mp3",4.00f, -17.5f, "胜利"),
        new Pick("R42", @"R42_失败\R42_01__Jingle_Lose_00.mp3",           4.00f, -17.5f, "失败"),
    };

    [MenuItem("界桥远征/构建音效包(42 槽位)")]
    public static void Build()
    {
        Directory.CreateDirectory(SRC_DIR);
        Directory.CreateDirectory(OUT_DIR);
        var report = new StringBuilder();
        report.AppendLine("槽位,用途,源文件,原时长s,处理后s,峰值dBFS,RMS dBFS");
        int okCount = 0;

        foreach (var p in Picks)
        {
            string src = Path.Combine(ROOT, p.Rel);
            if (!File.Exists(src)) { Debug.LogError("[SfxBuild] 缺文件 " + p.Slot + " -> " + src); continue; }

            // 1) 拷进工程,让 Unity 解码
            string ext = Path.GetExtension(src).ToLower();
            string tmp = Path.Combine(SRC_DIR, p.Slot + ext);
            File.Copy(src, tmp, true);
            AssetDatabase.ImportAsset(tmp, ImportAssetOptions.ForceUpdate);
            var imp = AssetImporter.GetAtPath(tmp) as AudioImporter;
            if (imp != null)
            {
                var s = imp.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.PCM;
                s.preloadAudioData = true;
                imp.forceToMono = false;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(tmp);
            if (clip == null) { Debug.LogError("[SfxBuild] 解码失败 " + p.Slot); continue; }

            int ch = clip.channels;
            int n = clip.samples * ch;
            var raw = new float[n];
            if (!clip.GetData(raw, 0)) { Debug.LogError("[SfxBuild] 取样本失败 " + p.Slot); continue; }
            float srcDur = clip.length;
            int srcRate = clip.frequency;

            // 2) 混成单声道
            var mono = new float[clip.samples];
            for (int i = 0; i < mono.Length; i++)
            {
                float v = 0f;
                for (int c = 0; c < ch; c++) v += raw[i * ch + c];
                mono[i] = v / ch;
            }

            // 3) 重采样到 44.1k
            float[] s44 = Resample(mono, srcRate, SR);

            // 4) 去掉开头静音
            int start = 0, guard = Math.Min(s44.Length - 1, (int)(0.05f * SR));
            for (int i = 0; i < guard; i++) { if (Math.Abs(s44[i]) > 0.01f) { start = i; break; } }

            // 5) 截到目标时长
            int maxLen = (int)(p.Max * SR);
            int len = Math.Min(s44.Length - start, maxLen);
            if (len < 32) len = Math.Min(s44.Length - start, 32);
            var outS = new float[len];
            Array.Copy(s44, start, outS, 0, len);

            // 6) 淡入 2ms / 淡出 18ms
            int fi = Math.Min(len / 4, (int)(0.002f * SR));
            for (int i = 0; i < fi; i++) outS[i] *= (float)i / fi;
            int fo = Math.Min(len / 3, (int)(0.018f * SR));
            for (int i = 0; i < fo; i++) outS[len - 1 - i] *= (float)i / fo;

            // 7) 响度:全部峰值统一到 -1 dBFS,再按 TrimFor 给密集音效降 1~2 dB
            float peak = 0f;
            for (int i = 0; i < len; i++) { float a = Math.Abs(outS[i]); if (a > peak) peak = a; }
            double gain = (peak > 1e-6f ? 0.89 / peak : 1.0) * Math.Pow(10.0, TrimFor(p.Slot) / 20.0);
            for (int i = 0; i < len; i++) outS[i] = (float)(outS[i] * gain);

            // 8) 写 44.1k 16bit WAV
            string outPath = Path.Combine(OUT_DIR, "sfx_" + p.Slot.ToLowerInvariant() + ".wav");
            WriteWav(outPath, outS, SR);

            // 9) 统计
            double sum2 = 0; float peak2 = 0f;
            for (int i = 0; i < len; i++) { double v = outS[i]; sum2 += v * v; if (Math.Abs(outS[i]) > peak2) peak2 = Math.Abs(outS[i]); }
            double rms2 = Math.Sqrt(sum2 / Math.Max(1, len));
            double pkDb = 20 * Math.Log10(Math.Max(1e-6, peak2));
            double rmDb = 20 * Math.Log10(Math.Max(1e-6, rms2));
            report.AppendLine(string.Format("{0},{1},{2},{3:0.00},{4:0.00},{5:0.0},{6:0.0}",
                p.Slot, p.Note, Path.GetFileName(p.Rel), srcDur, (float)len / SR, pkDb, rmDb));
            okCount++;

            File.Delete(tmp);
            AssetDatabase.DeleteAsset(tmp);
        }

        AssetDatabase.Refresh();
        Directory.CreateDirectory("reports");
        File.WriteAllText("reports/sfx_build.csv", report.ToString(), new UTF8Encoding(true));
        Debug.Log("[SfxBuild] 完成 " + okCount + "/" + Picks.Length + " 个槽位 → " + OUT_DIR + "\n" + report);
    }

    /// 铃音/法术/爆炸这类本身能量密集的音效,归一后再压一点,避免一响就盖过其他音
    static float TrimFor(string slot)
    {
        switch (slot)
        {
            case "R15": return -2f;
            case "R21": return -2f;
            case "R23": return -2f;
            case "R34": return -2f;
            case "R35": return -2f;
            case "R36": return -2f;
            case "R41": return -1f;
            case "R42": return -1f;
            default: return 0f;
        }
    }

    static float[] Resample(float[] src, int from, int to)
    {
        if (from == to) return src;
        double ratio = (double)to / from;
        int n = (int)(src.Length * ratio);
        var dst = new float[n];
        for (int i = 0; i < n; i++)
        {
            double pos = i / ratio;
            int i0 = (int)pos;
            int i1 = Math.Min(i0 + 1, src.Length - 1);
            float f = (float)(pos - i0);
            dst[i] = src[i0] * (1 - f) + src[i1] * f;
        }
        return dst;
    }

    static void WriteWav(string path, float[] data, int rate)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int bytes = data.Length * 2;
            bw.Write(new char[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + bytes);
            bw.Write(new char[] { 'W', 'A', 'V', 'E' });
            bw.Write(new char[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)1);
            bw.Write(rate);
            bw.Write(rate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(new char[] { 'd', 'a', 't', 'a' });
            bw.Write(bytes);
            for (int i = 0; i < data.Length; i++)
            {
                float v = Mathf.Clamp(data[i], -1f, 1f);
                bw.Write((short)Mathf.RoundToInt(v * 32767f));
            }
        }
    }

    [MenuItem("界桥远征/检查音效包")]
    public static void Check()
    {
        var sb = new StringBuilder();
        int bad = 0;
        foreach (var p in Picks)
        {
            string n = "sfx_" + p.Slot.ToLowerInvariant() + ".wav";
            string path = Path.Combine(OUT_DIR, n);
            if (!File.Exists(path)) { sb.AppendLine("缺 " + n); bad++; continue; }
            var bytes = File.ReadAllBytes(path);
            int rate = BitConverter.ToInt32(bytes, 24);
            int chn = BitConverter.ToInt16(bytes, 22);
            int bits = BitConverter.ToInt16(bytes, 34);
            int dataLen = BitConverter.ToInt32(bytes, 40);
            float sec = dataLen / 2f / rate;
            if (rate != 44100 || chn != 1 || bits != 16) { sb.AppendLine("参数异常 " + n + " " + rate + "Hz " + chn + "ch " + bits + "bit"); bad++; }
            sb.AppendLine(n + "  " + sec.ToString("0.00") + "s  " + (bytes.Length / 1024) + "KB");
        }
        sb.AppendLine("异常 " + bad + " 个");
        Debug.Log("[SfxCheck]\n" + sb);
    }
}
#endif
