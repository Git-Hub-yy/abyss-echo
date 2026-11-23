using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 玩家对象体检：列出场景里所有对象，找出玩家去哪了。
    /// </summary>
    public class PlayerAudit : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1f);

            _log.AppendLine("=== 玩家对象体检 ===");
            _log.AppendLine($"场景: {SceneManager.GetActiveScene().name}");

            // ① 各种方式找玩家
            var byMotor = FindObjectsByType<AbyssEcho.Player.PlayerMotor>(FindObjectsSortMode.None);
            var byInput = FindObjectsByType<AbyssEcho.Player.PlayerInputReader>(FindObjectsSortMode.None);
            var byLook = FindObjectsByType<AbyssEcho.Player.PlayerLook>(FindObjectsSortMode.None);
            var byCam = Camera.main;

            _log.AppendLine($"PlayerMotor: {byMotor.Length} 个");
            foreach (var m in byMotor)
                _log.AppendLine($"  · {m.name} active={m.gameObject.activeInHierarchy} enabled={m.enabled} pos={m.transform.position}");

            _log.AppendLine($"PlayerInputReader: {byInput.Length} 个");
            foreach (var i in byInput)
                _log.AppendLine($"  · {i.name} active={i.gameObject.activeInHierarchy} enabled={i.enabled}");

            _log.AppendLine($"PlayerLook: {byLook.Length} 个");
            _log.AppendLine($"MainCamera: {(byCam != null ? byCam.name + " @ " + byCam.transform.position : "无")}");

            // ② 按名字找
            var named = GameObject.Find("Player");
            _log.AppendLine($"GameObject.Find(\"Player\"): {(named != null ? named.name + " active=" + named.activeInHierarchy : "未找到")}");

            // ③ 列出所有根对象
            _log.AppendLine();
            _log.AppendLine("=== 场景根对象 ===");
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var comps = go.GetComponents<Component>();
                string compNames = "";
                foreach (var c in comps) if (c != null) compNames += c.GetType().Name + " ";
                _log.AppendLine($"  · {go.name} active={go.activeInHierarchy} 子对象={go.transform.childCount} 组件=[{compNames.Trim()}]");
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[PlayerAudit]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "PlayerAudit.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}