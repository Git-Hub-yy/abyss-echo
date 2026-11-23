using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.DebugTools
{
    /// <summary>门锁状态检查：确认哪些门初始是锁着的。</summary>
    public class DoorLockCheck : MonoBehaviour
    {
        public bool runOnStart = true;

        private void Start()
        {
            if (!runOnStart) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 门锁状态 ===");
            sb.AppendLine($"场景：{SceneManager.GetActiveScene().name}");

            var doors = FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                sb.AppendLine($"  {d.name}: locked={d.locked} " +
                              $"requiredItem={(string.IsNullOrEmpty(d.requiredItemId) ? "（未设置！）" : d.requiredItemId)} " +
                              $"pos={d.transform.position}");
            }

            string text = sb.ToString();
            Debug.Log("<b>[DoorLock]</b>\n" + text);

            try
            {
                string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.dataPath), "../Reports"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "DoorLockCheck.txt"),
                    text, new System.Text.UTF8Encoding(false));
            }
            catch { }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}