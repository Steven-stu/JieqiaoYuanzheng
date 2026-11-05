using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Jieqiao.UI
{
    /// <summary>
    /// 开发用：把大厅 8 个页面逐页截图到 reports/pages/，方便人工检查布局。
    /// 只有编辑器里把 EditorPrefs["V06PageShot"] 置 1 才会跑；正式包永远不运行。
    /// </summary>
    public sealed class V06PageShot : MonoBehaviour
    {
        private static readonly string[] Pages = new string[] { "home", "shop", "codex", "deck", "profile", "checkin", "net", "trade" };

        private void Start()
        {
            if (!Wanted()) { enabled = false; return; }
            StartCoroutine(Run());
        }

        private static bool Wanted()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool("V06PageShot", false);
#else
            return false;
#endif
        }

        private static string OutDir
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../reports/pages"));
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                return dir;
            }
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1.5f);
            V06ShellUI shell = FindObjectOfType<V06ShellUI>();
            if (shell == null) { Debug.LogWarning("[V06PageShot] 找不到大厅"); yield break; }
            for (int i = 0; i < Pages.Length; i++)
            {
                shell.Show();
                shell.ShowPage(Pages[i]);
                // 等两帧让布局与动画稳定
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.45f);
                string f = Path.Combine(OutDir, (i + 1).ToString("00") + "_" + Pages[i] + ".png");
                ScreenCapture.CaptureScreenshot(f);
                Debug.Log("[V06PageShot] 截 " + Pages[i] + " → " + f);
                yield return new WaitForSeconds(0.7f);
            }
            Debug.Log("[V06PageShot] 完成，共 " + Pages.Length + " 张");
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool("V06PageShot", false);
#endif
        }
    }
}
