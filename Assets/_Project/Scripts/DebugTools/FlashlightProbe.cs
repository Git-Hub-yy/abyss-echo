using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 验收审计：运行时验证手电筒与恐惧系统的**实际行为**。
    ///
    /// 这是只读探测无法覆盖的部分 ——
    ///   序列化字段为空 ≠ 运行时失效（因为有 autoBuildPlaceholder 自动生成）。
    ///   必须实际跑起来看。
    ///
    /// 本探测会：
    ///   · 检查运行时手电筒的 spotLight 是否被 ResolveSpotLight 补上
    ///   · 按 F 键验证手电筒能否真的点亮
    ///   · 检查恐惧系统是否响应
    ///   · 恢复现场（关闭手电筒、重置恐惧）
    /// </summary>
    public class FlashlightProbe : MonoBehaviour
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

            yield return new WaitForSeconds(0.8f);

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            _log.AppendLine($"===== 手电筒运行时探测：{AbyssEcho.LevelGen.ChapterLayout.GetChapterName(chapter)} =====");
            _log.AppendLine();

            var fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            if (fl == null)
            {
                _log.AppendLine("❌ 场景中没有 FlashlightSystem");
                Dump();
                yield break;
            }

            var t = typeof(AbyssEcho.Tools.FlashlightSystem);
            var slF = t.GetField("spotLight",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);

            // ── 运行时引用 ──
            object spot = slF?.GetValue(fl);
            _log.AppendLine($"运行时 spotLight = {(spot != null ? "✓ 已解析" : "❌ 仍为空")}");

            if (spot is Light lightComp)
            {
                _log.AppendLine($"  Light: type={lightComp.type} intensity={lightComp.intensity:F2} " +
                                $"enabled={lightComp.enabled} range={lightComp.range}");
            }

            // ── 玩家层级里有没有 SpotLight 物体 ──
            int spotCount = 0;
            foreach (var l in fl.GetComponentsInChildren<Light>(true))
            {
                spotCount++;
                _log.AppendLine($"  子物体 Light: {l.gameObject.name} type={l.type} enabled={l.enabled} intensity={l.intensity:F2}");
            }
            _log.AppendLine($"  子物体 Light 数量 = {spotCount}");

            // ── 手电筒状态 ──
            _log.AppendLine($"  IsOn = {fl.IsOn}");
            _log.AppendLine($"  BatteryPercent = {fl.BatteryPercent:F1}");
            _log.AppendLine($"  HasBattery = {fl.HasBattery}");
            _log.AppendLine($"  SpotLightIntensity01 = {fl.SpotLightIntensity01:F2}");

            // ── 尝试打开（物理按键） ──
            _log.AppendLine();
            _log.AppendLine("--- 尝试打开手电筒 ---");
            bool before = fl.IsOn;

            fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(0.4f);

            bool after = fl.IsOn;
            _log.AppendLine($"  Toggle() 调用前 IsOn={before}，调用后 IsOn={after} {(after != before ? "✓ 状态变了" : "❌ 状态没变")}");

            if (spot is Light l2)
                _log.AppendLine($"  Light intensity = {l2.intensity:F2} enabled={l2.enabled}");

            _log.AppendLine($"  SpotLightIntensity01 = {fl.SpotLightIntensity01:F2}");
            bool emitting = fl.IsEmittingLight;
            _log.AppendLine($"  IsEmittingLight = {emitting} {(emitting ? "✓ 手电筒真的亮了" : "❌ 手电筒没亮")}");

            // 关回去
            fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(0.3f);
            _log.AppendLine($"  关闭后 IsOn = {fl.IsOn}");

            // ── 恐惧系统 ──
            _log.AppendLine();
            _log.AppendLine("--- 恐惧系统 ---");

            var fear = FindFirstObjectByType<AbyssEcho.Horror.FearSystem>();
            if (fear == null)
            {
                _log.AppendLine("❌ FearSystem 不存在（恐惧值不会累积，恐怖氛围全部失效）");
            }
            else
            {
                _log.AppendLine($"✓ FearSystem 存在，currentFear = {fear.currentFear:F2}");
                float orig = fear.currentFear;
                fear.SetFear(0.5f);
                yield return null;
                _log.AppendLine($"  SetFear(0.5) 后 = {fear.currentFear:F2} {(Mathf.Abs(fear.currentFear - 0.5f) < 0.05f ? "✓" : "❌")}");
                fear.SetFear(orig);
            }

            var fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            _log.AppendLine($"FearVisuals = {(fv != null ? "✓ 存在" : "❌ 不存在（后处理不会随恐惧变化）")}");

            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            _log.AppendLine($"PlayerDeathSystem = {(death != null ? "✓ 存在" : "❌ 不存在（怪物碰到玩家不会死）")}");

            var footsteps = FindFirstObjectByType<AbyssEcho.Horror.PlayerFootsteps>();
            _log.AppendLine($"PlayerFootsteps = {(footsteps != null ? "✓ 存在" : "❌ 不存在（走路没声音）")}");

            _log.AppendLine();
            _log.AppendLine("--- MonsterAI 依赖检查 ---");
            var monster = FindFirstObjectByType<AbyssEcho.Monster.MonsterAI>();
            if (monster != null)
            {
                var mt = typeof(AbyssEcho.Monster.MonsterAI);
                var fF = mt.GetField("flashlight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var pF = mt.GetField("player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var agF = mt.GetField("agent", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var anF = mt.GetField("animator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                _log.AppendLine($"  MonsterAI @ {monster.gameObject.name}");
                _log.AppendLine($"    player = {(pF?.GetValue(monster) != null ? "✓" : "❌空")}");
                _log.AppendLine($"    flashlight = {(fF?.GetValue(monster) != null ? "✓" : "❌空")}");
                _log.AppendLine($"    agent = {(agF?.GetValue(monster) != null ? "✓" : "❌空")}");
                _log.AppendLine($"    animator = {(anF?.GetValue(monster) != null ? "✓" : "❌空")}");
            }
            else _log.AppendLine("  本章无 MonsterAI");

            Dump();
        }

        private void Dump()
        {
            string text = _log.ToString();
            Debug.Log("<b>[FlashlightProbe]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FlashlightProbe_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
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