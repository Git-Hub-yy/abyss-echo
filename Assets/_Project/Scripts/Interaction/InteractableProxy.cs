using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 交互代理：让一个物体把交互请求转发给另一个物体。
    ///
    /// 为什么需要：
    ///   第二章的抽水泵有一个大机柜和一块小面板，
    ///   玩家可能对准其中任何一个。但只有机柜上有 WaterPumpSwitch 组件。
    ///   给面板挂一个代理，对准面板也能触发同一套逻辑。
    ///
    /// 用法：把本组件挂到"附属物"上，target 指向真正的交互组件。
    /// </summary>
    public class InteractableProxy : InteractableBase
    {
        [Tooltip("真正处理交互的组件")]
        public InteractableBase target;

        protected override string DefaultPrompt => "使用";

        public override bool CanInteract =>
            base.CanInteract && target != null && target.CanInteract;

        public override string GetPrompt()
        {
            if (target == null) return "";
            return target.GetPrompt();
        }

        public override void OnInteract(GameObject interactor)
        {
            if (target != null) target.OnInteract(interactor);
        }
    }
}