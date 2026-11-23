using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 复核：五章系统组件的**运行时**存在性（只读）。
    ///
    /// 目的：与 SystemsMatrixCheck（场景静态检查）形成交叉验证。
    ///   静态检查能看全量，但可能被 Prefab 实例化 / 运行时生成骗过；
    ///   运行时检查能看到实际生效的组件。
    ///
    /// ⚠️ 只读：不调用任何会改变状态的方法。
    /// </summary>
    public class SystemsRuntimeCheck : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 0;

        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            // 等两帧，让所有 Awake/Start 跑完
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.5f);

            var ch = AbyssEcho.LevelGen.ChapterLayout.Current;

            _log.AppendLine($"===== 运行时组件复核：{AbyssEcho.LevelGen.ChapterLayout.GetChapterName(ch)} =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();
            _log.AppendLine("系统                  数量   状态");

            Dump<AbyssEcho.Horror.FearSystem>("FearSystem");
            Dump<AbyssEcho.Horror.FearVisuals>("FearVisuals");
            Dump<UnityEngine.Rendering.Volume>("Volume");
            Dump<AbyssEcho.Horror.PlayerDeathSystem>("PlayerDeathSystem");
            Dump<AbyssEcho.Horror.PlayerFootsteps>("PlayerFootsteps");
            Dump<AbyssEcho.Horror.FlickerLight>("FlickerLight");

            _log.AppendLine();
            _log.AppendLine("--- 关键单例 ---");
            _log.AppendLine($"  FearSystem 单例(FindFirst) = {(FindFirstObjectByType<AbyssEcho.Horror.FearSystem>() != null ? "有" : "无")}");
            _log.AppendLine($"  AudioManager.Instance = {(AbyssEcho.Audio.AudioManager.Instance != null ? "有" : "无")}");

            _log.AppendLine();
            _log.AppendLine("--- RenderSettings（运行时）---");
            _log.AppendLine($"  fog={RenderSettings.fog} density={RenderSettings.fogDensity:F3}");
            _log.AppendLine($"  ambientMode={RenderSettings.ambientMode} ambientLight={RenderSettings.ambientLight}");

            _log.AppendLine();
            _log.AppendLine("--- 相机后处理 ---");
            var cam = Camera.main;
            if (cam != null)
            {
                var extra = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                _log.AppendLine($"  Camera={cam.gameObject.name}");
                _log.AppendLine($"  renderPostProcessing={(extra != null ? extra.renderPostProcessing.ToString() : "无 UniversalAdditionalCameraData")}");
                _log.AppendLine($"  FearVisuals 在相机上 = {(cam.GetComponent<AbyssEcho.Horror.FearVisuals>() != null ? "有" : "无")}");
            }
            else _log.AppendLine("  ❌ 无 Camera.main");

            Dump2();
        }

        private void Dump<T>(string label) where T : Behaviour
        {
            var arr = FindObjectsByType<T>(FindObjectsSortMode.None);
            string status = arr.Length == 0 ? "MISSING" : "PRESENT";
            _log.AppendLine($"{label,-22}{arr.Length,4}   {status}");
            foreach (var c in arr)
            {
                _log.AppendLine($"      {GetPath(c.transform)}  active={c.gameObject.activeInHierarchy} enabled={c.enabled}");
            }
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }

        private void Dump2()
        {
            string text = _log.ToString();
            Debug.Log("<b>[SystemsRuntimeCheck]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"SystemsRuntime_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(0);
        }
    }
}