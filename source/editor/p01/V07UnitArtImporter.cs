using UnityEditor;
using UnityEngine;

namespace Jieqiao.EditorTools
{
    /// <summary>
    /// V0.7 第 4 批：卡面立绘与卡面人物关键帧的导入设置。
    ///
    ///   Art/Units/**            卡面 PNG（含新交付的 B/G 扩展卡）→ Sprite / Single / 不压缩质量损失可见
    ///   Art/Units/Anim/**/      人物关键帧（待机 / attack / hit / bloom）→ Sprite / Single
    ///   Art/units/**（老目录）   基础 192 张立绘，同样设置
    ///
    /// 立绘是 384×512，不是图集，所以不需要九宫格边框；关键是
    /// Sprite 类型 + Mesh Type FullRect + alphaIsTransparency，否则 UI 里会出现边缘黑边。
    /// </summary>
    public static class V07UnitArtImporter
    {
        private static readonly string[] Roots = new string[]
        {
            "Assets/Resources/Art/Units",
            "Assets/Resources/Art/units"
        };

        [MenuItem("界桥远征/导入卡面立绘设置")]
        public static void Apply()
        {
            int done = 0, anim = 0;
            for (int r = 0; r < Roots.Length; r++)
            {
                if (!System.IO.Directory.Exists(Roots[r])) { continue; }
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Roots[r] });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null) { continue; }
                    bool isAnim = path.IndexOf("/Anim/") >= 0;
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spriteImportMode = SpriteImportMode.Single;
                    ti.mipmapEnabled = false;
                    ti.alphaIsTransparency = true;
                    ti.maxTextureSize = 2048;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.filterMode = FilterMode.Bilinear;
                    ti.wrapMode = TextureWrapMode.Clamp;
                    var s = new TextureImporterSettings();
                    ti.ReadTextureSettings(s);
                    s.spriteMeshType = SpriteMeshType.FullRect;
                    s.spriteExtrude = 1;
                    ti.SetTextureSettings(s);
                    ti.SaveAndReimport();
                    if (isAnim) { anim++; } else { done++; }
                }
            }
            AssetDatabase.Refresh();
            Debug.Log("[V07单位图] 立绘 " + done + " 张、人物关键帧 " + anim + " 张已设置\n" + Verify());
        }

        public static string Verify()
        {
            var sb = new System.Text.StringBuilder();
            for (int r = 0; r < Roots.Length; r++)
            {
                if (!System.IO.Directory.Exists(Roots[r])) { continue; }
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Roots[r] });
                int ok = 0, bad = 0;
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti != null && ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single) { ok++; }
                    else { bad++; }
                }
                sb.AppendLine("[V07单位图] " + Roots[r] + "：合计 " + guids.Length + "，Sprite 正常 " + ok + "，异常 " + bad);
            }
            return sb.ToString();
        }
    }
}
