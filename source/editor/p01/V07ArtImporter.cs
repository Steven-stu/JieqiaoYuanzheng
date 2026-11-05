#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GPT 交付的 V0.7 系统界美术（Art/UI/V07/**）导入设置。
///
/// 真相来源是它自己给的 `Assets/Editor/V07-import-table.csv`：
///   页面,文件名,Resources路径,宽,高,格式(不透明/透明/九宫格),左,下,右,上,中心,来源
/// 九宫格的行按 左/下/右/上 设置 Sprite Border；透明图开 alphaIsTransparency；
/// 图标类用 Point 过滤；1600×900 背景用 2048 上限。
/// </summary>
public static class V07ArtImporter
{
    private const string TablePath = "Assets/Editor/V07-import-table.csv";
    private const string Root = "Assets/Resources/Art/UI/V07";

    [MenuItem("界桥远征/导入 V0.7 界面美术设置")]
    public static void Apply()
    {
        string[] lines = File.ReadAllLines(TablePath, Encoding.UTF8);
        int applied = 0;
        var bad = new List<string>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) { continue; }
            string[] c = SplitCsv(lines[i]);
            if (c.Length < 6) { continue; }
            string file = c[1].Trim();
            string resPath = c[2].Trim();
            string kind = c[5].Trim();
            string assetPath = resPath.StartsWith("Assets/") ? resPath + ".png" : Root + "/" + file;
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) { bad.Add(assetPath); continue; }
            bool nineSlice = kind.Contains("九宫格");
            bool opaque = kind.Contains("不透明");
            bool isIcon = file.StartsWith("icon_") || file.StartsWith("codex_filter") || file.StartsWith("codex_search")
                          || file.StartsWith("deck_icon") || file.StartsWith("shop_refresh");
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = !opaque;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = isIcon ? FilterMode.Point : FilterMode.Bilinear;
            int wantW = ParseI(c.Length > 3 ? c[3] : "0");
            int wantH = ParseI(c.Length > 4 ? c[4] : "0");
            ti.maxTextureSize = (IsBackground(file) || wantW > 1024 || wantH > 1024) ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.sRGBTexture = true;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            if (nineSlice)
            {
                float l = ParseF(c[6]), b = ParseF(c[7]), r = ParseF(c[8]), t = ParseF(c[9]);
                s.spriteBorder = new Vector4(l, b, r, t);
                s.spriteMeshType = SpriteMeshType.FullRect;
            }
            else
            {
                s.spriteBorder = Vector4.zero;
                s.spriteMeshType = SpriteMeshType.Tight;
            }
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            applied++;
        }
        AssetDatabase.Refresh();
        Debug.Log("[V07Art] 已套用 " + applied + " 个纹理的导入设置" + (bad.Count > 0 ? ("；缺失 " + bad.Count + "：\n" + string.Join("\n", bad.ToArray())) : ""));
    }

    private static bool IsBackground(string file)
    {
        return file.StartsWith("bg_") || file.StartsWith("cardback_") || file.StartsWith("shop_pack_")
               || file.StartsWith("shop_item_") || file.StartsWith("avatar_") || file.StartsWith("checkin_reward")
               || file.StartsWith("home_entry_") || file.StartsWith("banner_") || file.StartsWith("deck_cover");
    }

    private static int ParseI(string v)
    {
        int i;
        return int.TryParse(v.Trim(), out i) ? i : 0;
    }

    private static float ParseF(string v)
    {
        float f;
        return float.TryParse(v.Trim(), out f) ? f : 0f;
    }

    private static string[] SplitCsv(string line)
    {
        // 表里没有引号包起来的逗号，直接切
        return line.Split(',');
    }

    /// <summary>验收：按表逐行回报"文件是否在、尺寸是否与表一致、九宫格边距"。</summary>
    public static string Verify()
    {
        var sb = new StringBuilder();
        string[] lines = File.ReadAllLines(TablePath, Encoding.UTF8);
        int ok = 0, missing = 0, sizeBad = 0, borderBad = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) { continue; }
            string[] c = SplitCsv(lines[i]);
            if (c.Length < 6) { continue; }
            string file = c[1].Trim();
            string assetPath = Root + "/" + file;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) { missing++; sb.AppendLine("  缺：" + file); continue; }
            int wantW, wantH;
            int.TryParse(c[3].Trim(), out wantW);
            int.TryParse(c[4].Trim(), out wantH);
            if (wantW > 0 && (tex.width != wantW || tex.height != wantH))
            {
                sizeBad++;
                sb.AppendLine("  尺寸不符：" + file + " 实际 " + tex.width + "x" + tex.height + " 表 " + wantW + "x" + wantH);
            }
            else { ok++; }
            if (c[5].Trim().Contains("九宫格"))
            {
                var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti != null)
                {
                    var s = new TextureImporterSettings();
                    ti.ReadTextureSettings(s);
                    if (s.spriteBorder == Vector4.zero) { borderBad++; sb.AppendLine("  九宫格边距没设上：" + file); }
                }
            }
        }
        return "[V07Art] 验收：正常 " + ok + " / 缺 " + missing + " / 尺寸不符 " + sizeBad + " / 边距缺失 " + borderBad + "\n" + sb;
    }
}
#endif
