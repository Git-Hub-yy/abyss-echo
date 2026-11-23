using UnityEngine;

namespace AbyssEcho.Interaction
{
    /// <summary>
    /// 所有可交互物体的统一接口。
    ///
    /// 设计原则：
    ///   - 交互逻辑与"怎么被发现"解耦（射线检测在 InteractionDetector 里）
    ///   - 提示文本由物体自己提供，便于局部化与状态化
    ///     （例如保险丝未拾取时提示"拾取保险丝"，拾取后提示变为"已拾取"）
    ///   - 不依赖 SendMessage，全部走强类型接口调用
    /// </summary>
    public interface IInteractable
    {
        /// <summary>是否当前可交互（false 时射线仍会显示，但按键无效）。</summary>
        bool CanInteract { get; }

        /// <summary>准星上显示的提示文本，例如「拾取保险丝」「打开门」。</summary>
        string GetPrompt();

        /// <summary>玩家按下交互键时调用。</summary>
        void OnInteract(GameObject interactor);

        /// <summary>视线进入/离开时调用（可用于高亮）。</summary>
        void OnFocusEnter();
        void OnFocusExit();
    }
}