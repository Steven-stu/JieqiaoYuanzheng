#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GPT 交付的 UI 美术（Art/UI/V06/**）导入设置：
///   · 全部 Sprite / Single / 关闭 Mipmap / Alpha Is Transparency / Clamp
///   · Frames 关闭九宫格 Border=(14,14,14,14)、FullRect 网格（供 Image.type = Sliced）
///   · Icons 与 FX 用 Point 过滤（像素级锐利）
/// 另带一个 Verify() 用于验收：逐文件回报尺寸与关键导入参数。
/// </summary>
public static class V06UiArtImporter
{
    private const string UiRoot = "Assets/Resources/Art/UI/V06";

    [MenuItem("界桥远征/导入 V0.7 UI 美术设置")]
    public static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiRoot });
        int n = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { continue; }
            bool isIcon = path.Contains("/Icons/");
            bool isFx = path.Contains("/FX/");
            bool isFrame = path.Contains("/Frames/");
            bool isArena = path.Contains("arena_iron_court");

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = (isIcon || isFx) ? FilterMode.Point : FilterMode.Bilinear;
            ti.maxTextureSize = isArena ? 2048 : 256;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.sRGBTexture = true;

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = isFrame ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            settings.spriteBorder = isFrame ? new Vector4(14f, 14f, 14f, 14f) : Vector4.zero;
            settings.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(settings);

            ti.SaveAndReimport();
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log("[V06UiArt] 已套用导入设置：" + n + " 个纹理\n" + Verify());
    }

    /// <summary>验收：尺寸 + 关键参数，逐文件一行。</summary>
    public static string Verify()
    {
        var sb = new StringBuilder();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiRoot });
        sb.AppendLine("[V06UiArt] 文件 " + guids.Length + " 个");
        var rows = new List<string>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ti == null || tex == null) { rows.Add(path + " 读取失败"); continue; }
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            rows.Add(System.IO.Path.GetFileName(path) + "  " + tex.width + "x" + tex.height
                     + "  type=" + ti.textureType + " filter=" + ti.filterMode
                     + " mip=" + ti.mipmapEnabled + " border=" + s.spriteBorder
                     + " max=" + ti.maxTextureSize);
        }
        rows.Sort();
        for (int i = 0; i < rows.Count; i++) { sb.AppendLine("  " + rows[i]); }
        return sb.ToString();
    }

    [MenuItem("界桥远征/查看 V0.7 UI 美术导入结果")]
    public static void VerifyMenu() { Debug.Log(Verify()); }
}
#endif
