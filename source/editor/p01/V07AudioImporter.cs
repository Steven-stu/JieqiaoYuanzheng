#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// BGM 导入设置：长曲用 Streaming + Vorbis，避免整个 mp3 解到内存里。
/// （背景音乐每首 3 分钟以上，DecompressOnLoad 会占几十 MB。）
/// </summary>
public static class V07AudioImporter
{
    private const string BgmRoot = "Assets/Resources/Audio/Bgm";
    private const string SfxRoot = "Assets/Resources/Audio/Sfx";

    [MenuItem("界桥远征/导入音频设置")]
    public static void Apply()
    {
        int bgm = 0, sfx = 0;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { BgmRoot, SfxRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) { continue; }
            bool isBgm = path.StartsWith(BgmRoot);
            var s = ai.defaultSampleSettings;
            if (isBgm)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                ai.forceToMono = false;
                s.preloadAudioData = false;
                bgm++;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
                s.quality = 0.9f;
                ai.forceToMono = false;
                s.preloadAudioData = false;
                sfx++;
            }
            ai.defaultSampleSettings = s;
            ai.SaveAndReimport();
        }
        AssetDatabase.Refresh();
        Debug.Log("[V07Audio] BGM " + bgm + " 首（Streaming）、音效 " + sfx + " 个（ADPCM）\n" + Verify());
    }

    public static string Verify()
    {
        var sb = new System.Text.StringBuilder();
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { BgmRoot, SfxRoot });
        sb.AppendLine("[V07Audio] 共 " + guids.Length + " 个音频");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (clip == null || ai == null) { continue; }
            if (path.StartsWith(BgmRoot))
            {
                sb.AppendLine("  BGM " + System.IO.Path.GetFileName(path) + "  " + Mathf.RoundToInt(clip.length) + " 秒  "
                    + ai.defaultSampleSettings.loadType + " / " + ai.defaultSampleSettings.compressionFormat);
            }
        }
        return sb.ToString();
    }
}
#endif
