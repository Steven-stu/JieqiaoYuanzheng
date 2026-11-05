#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// V0.7 动画帧导入设置：全部 Sprite/Single、无 Mipmap、透明、Point 过滤（像素帧更干净）；
/// 横幅/卡包这类大图给 2048 上限，其余 512。
/// </summary>
public static class V07AnimImporter
{
    private const string AnimRoot = "Assets/Resources/Art/UI/V07/Anim";

    [MenuItem("界桥远征/导入 V0.7 动画帧设置")]
    public static void Apply()
    {
        if (!Directory.Exists(AnimRoot)) { Debug.Log("[V07Anim] 没有 Anim 目录"); return; }
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AnimRoot });
        int n = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ti == null || tex == null) { continue; }
            bool big = tex.width > 600 || tex.height > 600;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = big ? 2048 : 512;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.sRGBTexture = true;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteBorder = Vector4.zero;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log("[V07Anim] 已设置 " + n + " 帧\n" + Verify());
    }

    /// <summary>验收：逐套回报帧数与尺寸是否一致（同套内尺寸必须相同）。</summary>
    public static string Verify()
    {
        var sb = new System.Text.StringBuilder();
        if (!Directory.Exists(AnimRoot)) { return "[V07Anim] 没有 Anim 目录"; }
        string[] dirs = Directory.GetDirectories(AnimRoot);
        int okSets = 0;
        sb.AppendLine("[V07Anim] 动画 " + dirs.Length + " 套");
        for (int i = 0; i < dirs.Length; i++)
        {
            string[] files = Directory.GetFiles(dirs[i], "*.png");
            System.Array.Sort(files);
            int w = 0, h = 0;
            bool same = true;
            for (int f = 0; f < files.Length; f++)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(files[f].Replace('\\', '/'));
                if (tex == null) { continue; }
                if (f == 0) { w = tex.width; h = tex.height; }
                else if (tex.width != w || tex.height != h) { same = false; }
            }
            if (same) { okSets++; } else { sb.AppendLine("  ⚠ 尺寸不一致：" + Path.GetFileName(dirs[i])); }
        }
        sb.AppendLine("  尺寸一致 " + okSets + "/" + dirs.Length);
        return sb.ToString();
    }
}
#endif
