using UnityEngine;

namespace AbyssEcho.Core
{
    /// <summary>
    /// Persistent 场景中的管理器占位宿主。
    ///
    /// S1 阶段：没有任何逻辑，只用于验证 Persistent 场景结构成立、脚本可被场景引用。
    /// 后续阶段（S2 起）会在此挂载 GameManager / UIManager / AudioManager 等全局管理器。
    ///
    /// ⚠️ 该脚本必须位于运行时程序集（不能放在 Editor 文件夹里）。
    /// </summary>
    public class ManagersHost : MonoBehaviour
    {
        [Tooltip("S1 占位：显示当前编辑器版本，用于在 Inspector 中快速确认工程环境。")]
        public string editorVersion = Application.unityVersion;
    }
}