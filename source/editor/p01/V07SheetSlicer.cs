#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 GPT 交的"横排图集"切成序列帧。
///
/// 约定：放在 `Assets/Resources/Art/UI/V07/Anim/` 下，文件名以 `_sheetN.png` 结尾，
/// 例如 `anim_checkin_stamp_sheet6.png` = 横排 6 格（每格宽 = 图宽/6，高 = 图高）。
/// 也支持 `_sheetRxC`，例如 `_sheet3x2`（3 列 2 行，按行优先）。
///
/// 切完之后 Unity 里就是同一张图的多个 Sprite 子资源，名字为 `xxx_00 … xxx_05`，
/// 运行时 `V06UiV07.SheetFrames("xxx_sheet6")` 直接拿到（按名字排序）。
/// </summary>
public static class V07SheetSlicer
{
    private const string AnimRoot = "Assets/Resources/Art/UI/V07/Anim";

    [MenuItem("界桥远征/切分 V0.7 动画图集")]
    public static void SliceAll()
    {
        if (!Directory.Exists(AnimRoot))
        {
            Debug.Log("[V07Sheet] 还没有 Anim 目录（等 GPT 交动画帧）");
            return;
        }
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AnimRoot });
        int sliced = 0;
        var report = new StringBuilder();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string file = Path.GetFileNameWithoutExtension(path);
            int cols, rows;
            if (!ParseSheet(file, out cols, out rows)) { continue; }
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ti == null || tex == null) { continue; }
            int cw = tex.width / cols;
            int ch = tex.height / rows;
            var metas = new List<SpriteMetaData>();
            int idx = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var m = new SpriteMetaData();
                    m.name = file + "_" + (idx < 10 ? "0" + idx : "" + idx);
                    // Unity 的 sprite rect 原点在左下
                    m.rect = new Rect(c * cw, tex.height - (r + 1) * ch, cw, ch);
                    m.alignment = (int)SpriteAlignment.Center;
                    m.pivot = new Vector2(0.5f, 0.5f);
                    metas.Add(m);
                    idx++;
                }
            }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.spritesheet = metas.ToArray();
            ti.SaveAndReimport();
            sliced++;
            report.AppendLine("  切分 " + file + " → " + cols + "×" + rows + " = " + idx + " 帧（每帧 " + cw + "×" + ch + "）");
        }
        AssetDatabase.Refresh();
        Debug.Log("[V07Sheet] 切分完成：" + sliced + " 张图集\n" + report);
    }

    /// <summary>识别 `xxx_sheet6` / `xxx_sheet3x2` 这类命名。</summary>
    private static bool ParseSheet(string name, out int cols, out int rows)
    {
        cols = 0; rows = 0;
        int at = name.LastIndexOf("_sheet", System.StringComparison.OrdinalIgnoreCase);
        if (at < 0) { return false; }
        string tail = name.Substring(at + 6);
        if (tail.Length == 0) { return false; }
        int x = tail.IndexOf('x');
        if (x > 0)
        {
            int.TryParse(tail.Substring(0, x), out cols);
            int.TryParse(tail.Substring(x + 1), out rows);
        }
        else
        {
            int.TryParse(tail, out cols);
            rows = 1;
        }
        return cols > 0 && rows > 0;
    }

    /// <summary>验收：动画目录里都有什么。</summary>
    public static string Report()
    {
        if (!Directory.Exists(AnimRoot)) { return "[V07Sheet] 没有 Anim 目录"; }
        var sb = new StringBuilder();
        string[] dirs = Directory.GetDirectories(AnimRoot);
        sb.AppendLine("[V07Sheet] 逐帧文件夹 " + dirs.Length + " 个");
        for (int i = 0; i < dirs.Length; i++)
        {
            string[] pngs = Directory.GetFiles(dirs[i], "*.png");
            sb.AppendLine("  " + Path.GetFileName(dirs[i]) + "：" + pngs.Length + " 帧");
        }
        string[] sheets = Directory.GetFiles(AnimRoot, "*_sheet*.png", SearchOption.AllDirectories);
        sb.AppendLine("[V07Sheet] 图集 " + sheets.Length + " 张");
        for (int i = 0; i < sheets.Length; i++) { sb.AppendLine("  " + Path.GetFileName(sheets[i])); }
        return sb.ToString();
    }
}
#endif
