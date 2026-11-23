using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S48 FearVisuals + Volume 真实渲染验证。
    ///
    /// ══════════════════ 为什么需要这个测试 ══════════════════
    ///
    ///   任务特别要求：「重点确认：恐惧值变化确实能改变画面，
    ///   而不是只验证组件存在或字段不为空。」
    ///
    ///   所以本测试直接读取 **Volume Profile 里的实际参数值**，
    ///   确认 FearVisuals 的 Override() 真的写进去了。
    ///
    ///   ═══ 重要 ═══
    ///   这验证的是"参数被正确写入 Profile"，
    ///   但 **"屏幕像素真的变化"** 仍需要真实渲染才能确认。
    ///   在 -batchmode -nographics 下无法截图。
    ///   所以最终视觉观感标记为待人工验收。
    ///
    /// ══════════════════ 测试内容 ══════════════════
    ///
    ///   T1 组件存在性（Volume / FearVisuals / FearSystem）
    ///   T2 FearVisuals 的引用正确
    ///   T3 恐惧值 0 → Vignette/Aberration/Grain 应等于 calm 值
    ///   T4 恐惧值 0.30 → 应介于 calm 与 panic 之间（偏 calm）
    ///   T5 恐惧值 0.60 → 应明显增长
    ///   T6 恐惧值 0.85 → 应接近 panic
    ///   T7 恐惧值 1.0  → 应等于 panic
    ///   T8 恢复到 0 → 应回到 calm（验证可恢复）
    ///   T9 死亡后 FearVisuals 被禁用
    /// </summary>
    public class FearVisualTest : MonoBehaviour
    {
        public bool runOnStart = true;
        public int forceChapter = 2;

        private readonly StringBuilder _log = new StringBuilder();
        private int _pass, _fail, _unverified;

        private Vignette _vig;
        private ChromaticAberration _aber;
        private FilmGrain _grain;
        private AbyssEcho.Horror.FearVisuals _fv;
        private AbyssEcho.Horror.FearSystem _fear;

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

            _log.AppendLine("===== S48 FearVisuals + Volume 验证 =====");
            _log.AppendLine($"场景 = {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            _log.AppendLine();

            // ── T1 组件存在性 ──
            var vols = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            _fv = FindFirstObjectByType<AbyssEcho.Horror.FearVisuals>();
            _fear = AbyssEcho.Horror.FearSystem.Instance;

            Check("T1 Volume 恰好 1 个", vols.Length == 1, $"数量={vols.Length}");
            Check("T1b FearVisuals 恰好 1 个", FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length == 1,
                  $"数量={FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None).Length}");
            Check("T1c FearSystem 存在", _fear != null, _fear != null ? $"@{_fear.gameObject.name}" : "❌");
            Check("T1d FearVisuals 存在", _fv != null, _fv != null ? $"@{_fv.gameObject.name}" : "❌");

            if (vols.Length == 0 || _fv == null || _fear == null) { Dump(); yield break; }

            var vol = vols[0];
            Check("T1e Volume isGlobal", vol.isGlobal, $"isGlobal={vol.isGlobal}");
            Check("T1f Volume weight=1", Mathf.Approximately(vol.weight, 1f), $"weight={vol.weight}");
            Check("T1g Volume 有 Profile", vol.sharedProfile != null,
                  vol.sharedProfile != null ? vol.sharedProfile.name : "❌ NULL");

            // ── T2 引用正确 ──
            Check("T2 FearVisuals.fear 正确", _fv.fear == _fear,
                  _fv.fear != null ? _fv.fear.gameObject.name : "❌ NULL");
            Check("T2b FearVisuals.targetCamera 正确", _fv.targetCamera != null,
                  _fv.targetCamera != null ? _fv.targetCamera.gameObject.name : "❌ NULL");

            var cam = Camera.main;
            if (cam != null)
            {
                var extra = cam.GetComponent<UniversalAdditionalCameraData>();
                Check("T2c 相机 renderPostProcessing 开启", extra != null && extra.renderPostProcessing,
                      extra != null ? extra.renderPostProcessing.ToString() : "无 extra");
                Check("T2d 相机 volumeLayerMask 包含 Volume 层",
                      extra == null || (extra.volumeLayerMask.value & 1) != 0,
                      extra != null ? extra.volumeLayerMask.value.ToString() : "?");
            }

            // ── 获取 Profile 里的三个组件 ──
            if (vol.sharedProfile == null) { Dump(); yield break; }

            bool okV = vol.sharedProfile.TryGet(out _vig);
            bool okA = vol.sharedProfile.TryGet(out _aber);
            bool okG = vol.sharedProfile.TryGet(out _grain);

            Check("T3 Profile 含 Vignette", okV, okV ? $"当前 intensity={_vig.intensity.value:F3}" : "❌");
            Check("T3b Profile 含 ChromaticAberration", okA, okA ? $"当前 intensity={_aber.intensity.value:F3}" : "❌");
            Check("T3c Profile 含 FilmGrain", okG, okG ? $"当前 intensity={_grain.intensity.value:F3}" : "❌");

            if (!okV || !okA || !okG)
            {
                _log.AppendLine("❌ 缺少关键后处理组件，无法继续");
                Dump();
                yield break;
            }

            _log.AppendLine();
            _log.AppendLine($"    FearVisuals 配置：vignette {_fv.vignetteCalm}→{_fv.vignettePanic}  " +
                            $"aberration {_fv.aberrationCalm}→{_fv.aberrationPanic}  " +
                            $"grain {_fv.grainCalm}→{_fv.grainPanic}");
            _log.AppendLine();

            // ── T4~T8 逐档位验证 ──
            yield return SetFearAndSample(0.00f, "恐惧 0.00（平静）", expectCalm: true);
            yield return SetFearAndSample(0.30f, "恐惧 0.30（紧张）", expectCalm: false);
            yield return SetFearAndSample(0.60f, "恐惧 0.60（恐惧）", expectCalm: false);
            yield return SetFearAndSample(0.85f, "恐惧 0.85（濒临崩溃）", expectCalm: false);
            yield return SetFearAndSample(1.00f, "恐惧 1.00（满值）", expectPanic: true);
            yield return SetFearAndSample(0.00f, "恢复 0.00（验证可恢复）", expectCalm: true);

            // ── T9 死亡后 FearVisuals 被禁用 ──
            var death = FindFirstObjectByType<AbyssEcho.Horror.PlayerDeathSystem>();
            if (death != null && !death.IsDead)
            {
                _log.AppendLine();
                _fear.SetFear(0.5f);
                yield return null;

                death.Kill("测试：验证死亡冻结 FearVisuals");
                yield return null;

                Check("T9 死亡后 FearVisuals 被禁用", !_fv.enabled, $"enabled={_fv.enabled}");
                Check("T9b 死亡后恐惧拉满", _fear.currentFear > 0.98f, $"currentFear={_fear.currentFear:F4}");
            }
            else
            {
                _log.AppendLine();
                _log.AppendLine("    （无 PlayerDeathSystem 或已死亡，跳过 T9）");
            }

            _log.AppendLine();
            _log.AppendLine("---- 重要说明 ----");
            Unverified("T10 屏幕上看到的暗角/色差/颗粒是否真的随恐惧变化（-nographics 无法渲染）");
            Unverified("T11 相机抖动与呼吸 FOV 的观感（无渲染）");
            Unverified("T12 真机 Play Mode 下的实际画面（需人工）");

            Dump();
        }

        private IEnumerator SetFearAndSample(float value, string label, bool expectCalm = false, bool expectPanic = false)
        {
            _fear.SetFear(value);
            yield return null;   // 等 FearSystem.Update
            yield return null;   // 等 FearVisuals.LateUpdate
            yield return null;

            float v = _vig.intensity.value;
            float a = _aber.intensity.value;
            float g = _grain.intensity.value;

            float expV = Mathf.Lerp(_fv.vignetteCalm, _fv.vignettePanic, value);
            float expA = Mathf.Lerp(_fv.aberrationCalm, _fv.aberrationPanic, value);
            float expG = Mathf.Lerp(_fv.grainCalm, _fv.grainPanic, value);

            _log.AppendLine($"─── {label} ───");
            _log.AppendLine($"    currentFear = {_fear.currentFear:F3}（设 {value:F2}）");
            _log.AppendLine($"    Vignette   实际={v:F4}  期望={expV:F4}  差={Mathf.Abs(v - expV):F4}");
            _log.AppendLine($"    Aberration 实际={a:F4}  期望={expA:F4}  差={Mathf.Abs(a - expA):F4}");
            _log.AppendLine($"    FilmGrain  实际={g:F4}  期望={expG:F4}  差={Mathf.Abs(g - expG):F4}");

            // 用 0.02 容差（每帧 FearSystem 会自然衰减 recoveryRate*dt，导致轻微偏差）
            const float tol = 0.02f;
            bool okV = Mathf.Abs(v - expV) < tol;
            bool okA = Mathf.Abs(a - expA) < tol;
            bool okG = Mathf.Abs(g - expG) < tol;

            Check($"{label} Vignette 被正确驱动", okV, $"实际 {v:F4} vs 期望 {expV:F4}");
            Check($"{label} Aberration 被正确驱动", okA, $"实际 {a:F4} vs 期望 {expA:F4}");
            Check($"{label} FilmGrain 被正确驱动", okG, $"实际 {g:F4} vs 期望 {expG:F4}");

            if (expectCalm)
            {
                Check($"{label} 应接近 calm 值",
                      Mathf.Abs(v - _fv.vignetteCalm) < 0.03f, $"v={v:F4} calm={_fv.vignetteCalm}");
            }
            if (expectPanic)
            {
                Check($"{label} 应接近 panic 值",
                      Mathf.Abs(v - _fv.vignettePanic) < 0.03f, $"v={v:F4} panic={_fv.vignettePanic}");
            }

            _log.AppendLine();
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
            Debug.Log("<b>[FearVisualTest]</b>\n" + text);

            var di = new DirectoryInfo(Application.dataPath);
            for (int hop = 0; hop < 6 && di != null; hop++)
            {
                string reports = Path.Combine(di.FullName, "Reports");
                if (Directory.Exists(reports))
                {
                    try
                    {
                        string fn = $"FearVisualTest_Ch{(int)AbyssEcho.LevelGen.ChapterLayout.Current}.txt";
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