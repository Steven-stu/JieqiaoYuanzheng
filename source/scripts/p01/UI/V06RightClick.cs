using UnityEngine;
using UnityEngine.EventSystems;

namespace Jieqiao.UI
{
    /// <summary>
    /// 右键探针：挂在卡面上，把"右键点击"转成回调。
    /// 用 IPointerClickHandler 而不是轮询 Input，好处是能被自动化测试
    /// 用 ExecuteEvents.Execute(button=Right) 真实触发同一条代码路径。
    /// </summary>
    public sealed class V06RightClick : MonoBehaviour, IPointerClickHandler
    {
        public string WidgetName;
        public System.Action<string> OnRightClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null) { return; }
            if (eventData.button != PointerEventData.InputButton.Right) { return; }
            if (OnRightClick != null) { OnRightClick(WidgetName); }
        }
    }
}
