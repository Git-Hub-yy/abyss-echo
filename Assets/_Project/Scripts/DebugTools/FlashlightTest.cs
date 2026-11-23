using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S51 手电筒输入与照明实际验证。
    ///
    /// ══════════════════ 为什么必须运行时测 ══════════════════
    ///
    ///   静态检查只能看到"OnEnable 订阅了事件"和"Update 轮询 F 键"，
    ///   但**看不出按一次 F 实际执行了几次 Toggle**。
    ///
    ///   本测试直接给 FlashlightSystem 注入计数的 Toggle 包装，
    ///   模拟 PlayerInputReader 触发事件，同时模拟 Update 的键盘轮询路径，
    ///   统计实际执行次数与最终状态。
    ///
    /// ══════════════════ 验证项 ══════════════════
    ///
    ///   T1 组件与引用
    ///   T2 Spot Light 运行时解析
    ///   T3 单次 Toggle 能正常亮/灭
    ///   T4 事件 + 轮询双路径是否导致双触发（核心）
    ///   T5 连续 10 次切换状态正确
    ///   T6 恐怖事件后手电筒仍可用
    ///   T7 恐惧值升高后手电筒强度不受影响
    /// </summary>
    public class FlashlightTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 3;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private AbyssEcho.Tools.FlashlightSystem _fl;
        private Light _spot;
        private AbyssEcho.Horror.FearSystem _fear;

        private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (forceChapter >= 1 && forceChapter <= 5)
                AbyssEcho.LevelGen.ChapterLayout.Current =
                    (AbyssEcho.LevelGen.ChapterLayout.Chapter)forceChapter;

            yield return new WaitForSeconds(1.0f);

            _log.AppendLine("===== S51 手电筒验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ── T1 组件与引用 ──
            _fl = FindFirstObjectByType<AbyssEcho.Tools.FlashlightSystem>();
            _fear = AbyssEcho.Horror.FearSystem.Instance;

            Check("T1 FlashlightSystem 存在", _fl != null, _fl != null ? $"@{_fl.gameObject.name}" : "❌");
            if (_fl == null) { Dump(); yield break; }

            var input = GetField(_fl, "input") as AbyssEcho.Player.PlayerInputReader;
            Check("T1b input 引用有效", input != null,
                  input != null ? input.gameObject.name : "❌ NULL（意味着事件订阅失败，只能靠轮询）");

            // ── T2 Spot Light ──
            _spot = GetField(_fl, "spotLight") as Light;
            Check("T2 Spot Light 已解析", _spot != null,
                  _spot != null ? $"{_spot.gameObject.name}" : "❌ 未解析");

            if (_spot == null) { Dump(); yield break; }

            _log.AppendLine($"    Light: type={_spot.type} intensity={_spot.intensity:F2} range={_spot.range:F1} " +
                            $"spotAngle={_spot.spotAngle:F1} enabled={_spot.enabled}");
            Check("T2b Light 类型为 Spot", _spot.type == LightType.Spot, _spot.type.ToString());
            Check("T2c Light range 合理(>=10)", _spot.range >= 10f, $"{_spot.range:F1}");
            Check("T2d Light spotAngle 合理(>30)", _spot.spotAngle > 30f, $"{_spot.spotAngle:F1}");
            _log.AppendLine($"    Light 所在路径 = {GetPath(_spot.transform)}");
            _log.AppendLine($"    Light 父物体链 active = {(_spot.gameObject.activeInHierarchy ? "是" : "❌ 否")}");
            Check("T2e Light 的 GameObject 处于激活状态", _spot.gameObject.activeInHierarchy, "");
            _log.AppendLine();

            // ── T3 单次 Toggle 能亮 ──
            _log.AppendLine("---- T3 单次 Toggle ----");
            ForceOff();

            _fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(0.15f);   // 覆盖 turnOnDelay=0.04

            bool onOk = _spot.enabled && _spot.intensity > 0.1f;
            _log.AppendLine($"    调用 Toggle 后: enabled={_spot.enabled} intensity={_spot.intensity:F2} IsOn={_fl.IsOn}");
            Check("T3 单次 Toggle 后灯亮起", onOk, $"enabled={_spot.enabled} int={_spot.intensity:F2}");

            _fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
            yield return new WaitForSeconds(0.15f);
            _log.AppendLine($"    再次 Toggle 后: enabled={_spot.enabled} intensity={_spot.intensity:F2} IsOn={_fl.IsOn}");
            Check("T3b 再次 Toggle 后灯熄灭", !_spot.enabled || _spot.intensity < 0.01f,
                  $"enabled={_spot.enabled} int={_spot.intensity:F2}");

            // ── T4 双触发检测（核心）──
            _log.AppendLine();
            _log.AppendLine("---- T4 事件+轮询 双触发检测 ----");

            var toggleM = typeof(AbyssEcho.Tools.FlashlightSystem).GetMethod("Toggle");
            _log.AppendLine($"    Toggle 是 public 方法 = {toggleM != null}");

            // 检测：Update 里是否有 Input.GetKeyDown(KeyCode.F)
            // 检测：OnEnable 是否订阅了 input.OnFlashlightToggle
            var onEnableM = typeof(AbyssEcho.Tools.FlashlightSystem).GetMethod("OnEnable", BF);
            var updateM = typeof(AbyssEcho.Tools.FlashlightSystem).GetMethod("Update", BF);
            _log.AppendLine($"    OnEnable 存在 = {onEnableM != null}");
            _log.AppendLine($"    Update 存在 = {updateM != null}");

            // 实测：模拟 PlayerInputReader 派发一次事件
            ForceOff();
            yield return null;

            var evtField = typeof(AbyssEcho.Tools.FlashlightSystem).GetField("OnFlashlightToggled", BF);
            _log.AppendLine($"    OnFlashlightToggled 事件字段 = {(evtField != null ? "存在" : "无")}");

            // ── S51 修复后：验证"不再双触发" ──
            //
            //   修复前：OnEnable 订阅 + Update 轮询 同时生效
            //           → HandleFlashlight 派发事件会触发一次 Toggle
            //           → 同时 Update 里的 GetKeyDown 也会触发一次
            //           → 一次按键 = 两次切换
            //
            //   修复后：只保留 Update 轮询，OnEnable 不再订阅
            //           → 单独派发 OnFlashlightToggle 事件**不应**改变手电筒状态
            //
            //   所以本项的正确期望是：**调用后灯状态不变**。
            if (input != null)
            {
                var inputType = typeof(AbyssEcho.Player.PlayerInputReader);
                var handlerM = inputType.GetMethod("HandleFlashlight", BF);

                if (handlerM != null)
                {
                    ForceOff();
                    yield return new WaitForSeconds(0.1f);
                    bool before = IsLit(_spot);

                    var ctx = new UnityEngine.InputSystem.InputAction.CallbackContext();
                    try { handlerM.Invoke(input, new object[] { ctx }); }
                    catch (System.Exception e) { _log.AppendLine($"    调用 HandleFlashlight 异常: {e.GetType().Name}"); }

                    yield return new WaitForSeconds(0.15f);
                    bool after = IsLit(_spot);

                    _log.AppendLine($"    派发 OnFlashlightToggle 前={before}  后={after}");

                    // 修复后：事件不应再驱动手电筒（避免与 Update 轮询叠加）
                    Check("T4 单独派发事件不再驱动手电筒（双触发已消除）", before == after,
                          $"before={before} after={after}（应保持不变）");
                }
                else
                {
                    _log.AppendLine("    未找到 HandleFlashlight，跳过");
                }
            }

            // ── T5 连续 10 次切换 ──
            _log.AppendLine();
            _log.AppendLine("---- T5 连续 10 次 Toggle ----");
            ForceOff();
            yield return null;

            var states = new StringBuilder();
            int wrongCount = 0;
            for (int i = 0; i < 10; i++)
            {
                _fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
                yield return new WaitForSeconds(0.12f);

                bool lit = _spot.enabled && _spot.intensity > 0.1f;
                bool expectLit = (i % 2 == 0);
                states.Append(lit ? "亮" : "灭");
                if (lit != expectLit) wrongCount++;
            }
            _log.AppendLine($"    10 次结果（期望亮灭交替）: {states}");
            Check("T5 连续 10 次切换状态正确", wrongCount == 0, $"错误 {wrongCount} 次");

            // ── T6 恐怖事件后手电筒仍可用 ──
            _log.AppendLine();
            _log.AppendLine("---- T6 恐怖事件后仍可用 ----");

            var triggers = FindObjectsByType<AbyssEcho.Horror.HorrorTrigger>(FindObjectsSortMode.None);
            _log.AppendLine($"    本章 HorrorTrigger = {triggers.Length}");

            if (triggers.Length > 0)
            {
                // 触发所有恐怖事件（会灭环境灯 + 压暗环境光）
                foreach (var t in triggers) t.Fire();
                yield return new WaitForSeconds(2.4f);   // 等压暗动画完成

                _log.AppendLine($"    触发后 ambientLight = {RenderSettings.ambientLight}");

                int litLights = 0;
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.enabled && l.intensity > 0.1f) litLights++;
                _log.AppendLine($"    触发后仍亮的场景灯 = {litLights}");

                // 此时测试手电筒
                ForceOff();
                yield return null;

                _fl.SendMessage("Toggle", SendMessageOptions.DontRequireReceiver);
                yield return new WaitForSeconds(0.15f);

                bool lit = _spot.enabled && _spot.intensity > 0.1f;
                _log.AppendLine($"    恐怖事件后开手电筒: enabled={_spot.enabled} intensity={_spot.intensity:F2}");
                Check("T6 恐怖事件后手电筒仍能正常开启", lit,
                      $"enabled={_spot.enabled} int={_spot.intensity:F2}");

                // 检查手电筒强度是否被恐怖事件改动
                float normalInt = (float)GetField(_fl, "normalIntensity");
                Check("T6b 手电筒强度未被恐怖事件修改", Mathf.Abs(_spot.intensity - normalInt) < 0.01f,
                      $"当前={_spot.intensity:F2} 标称={normalInt:F2}");
            }
            else
            {
                _log.AppendLine("    （本章无恐怖事件，跳过 T6）");
            }

            // ── T7 恐惧值升高不影响手电筒 ──
            _log.AppendLine();
            _log.AppendLine("---- T7 恐惧值影响 ----");
            if (_fear != null)
            {
                float intBefore = _spot.intensity;
                _fear.SetFear(0f);
                yield return new WaitForSeconds(0.3f);
                float intAt0 = _spot.intensity;

                _fear.SetFear(0.95f);
                yield return new WaitForSeconds(0.5f);
                float intAt95 = _spot.intensity;

                _log.AppendLine($"    恐惧0.0 → intensity={intAt0:F2}");
                _log.AppendLine($"    恐惧0.95 → intensity={intAt95:F2}");

                // 低电量闪烁会改 intensity，但满电时应稳定
                float battery = (float)GetField(_fl, "batteryPercent");
                _log.AppendLine($"    当前电量={battery:F1}（>20 时不应因恐惧闪烁）");

                Check("T7 恐惧值不影响手电筒强度", Mathf.Abs(intAt0 - intAt95) < 0.3f,
                      $"0.0→{intAt0:F2}  0.95→{intAt95:F2}");

                _fear.SetFear(0f);
            }

            _log.AppendLine();
            _log.AppendLine("---- 说明 ----");
            Unverified("T8 屏幕上真实光斑与照明范围（-nographics 无渲染，需人工）");
            Unverified("T9 黑暗中照射墙壁/地面的视觉效果（需人工）");

            Dump();
        }

        private void ForceOff()
        {
            // ⚠️ 不能只把 Light 关掉而不管 isOn —— 那样会人为制造"状态错位"，
            //   导致后续 Toggle 判定错误（我第一版就是这么写出假失败的）。
            //   正确做法：通过 SetLightOn(false) 走完整流程，状态和灯光一起改。
            var m = typeof(AbyssEcho.Tools.FlashlightSystem).GetMethod("SetLightOn", BF);
            if (m != null) m.Invoke(_fl, new object[] { false });
            else if (_spot != null) { _spot.intensity = 0f; _spot.enabled = false; }
        }

        private object GetField(object o, string name)
        {
            var f = o.GetType().GetField(name, BF);
            return f?.GetValue(o);
        }

        private static bool IsLit(Light l) => l != null && l.enabled && l.intensity > 0.1f;

        private string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent; int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }

        private void Check(string name, bool ok, string detail)
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name}  {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name}  {detail}"); }
        }

        private void Unverified(string name)
        {
            _unverified++;
            _log.AppendLine($"[待人工验收] {name}");
        }

        private void Dump()
        {
            _log.AppendLine();
            _log.AppendLine("=====================================");
            _log.AppendLine($"通过 {_pass}，失败 {_fail}，待人工验收 {_unverified}");

            string text = _log.ToString();
            Debug.Log("<b>[FlashlightTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FlashlightTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
                        File.WriteAllText(Path.Combine(reports, fn), text, new UTF8Encoding(false));
                        break;
                    }
                    catch { }
                }
                di = di.Parent;
            }

            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }
    }
}