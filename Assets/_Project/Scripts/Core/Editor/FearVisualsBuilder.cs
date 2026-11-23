using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S48：给第 2~5 章补 Volume + FearVisuals。
    ///
    /// ══════════════════ 共享 Profile 污染问题 ══════════════════════
    ///
    ///   FearVisuals.LateUpdate() 会调用：
    ///       _vignette.intensity.Override(...)
    ///       _aberration.intensity.Override(...)
    ///       _grain.intensity.Override(...)
    ///
    ///   而它通过 `v.sharedProfile.TryGet(out _vignette)` 拿到组件引用，
    ///   返回的是 **Profile ScriptableObject 里的组件实例本身**。
    ///   所以 Override() 是在直接改 Profile 资产的运行时数据。
    ///
    ///   如果五章共用同一个 Volume_Main.asset：
    ///     · 运行时每场景只有一个 FearVisuals，独占写入 → 当下无冲突
    ///     · 但 Editor 里在 Play 模式下切场景，值会从上一章带过来
    ///     · 且任务明确要求"不要修改原始 Volume_Main 资源"
    ///
    ///   → 本批采用**每章独立 Profile 副本**：
    ///       复制 Volume_Main.asset → Volume_ChapterN.asset
    ///     内容与第一章完全一致，但每章改自己的副本，互不影响。
    ///     原始 Volume_Main.asset **完全不动**（哈希可验证）。
    ///
    ///   ⚠️ 不改 FearVisuals.cs（它只需要 sharedProfile 里有对应组件即可）。
    ///
    /// ══════════════════ 与第一章保持一致的配置 ══════════════════════
    ///
    ///   Volume：名为 --- POST_PROCESS ---，场景根节点
    ///           isGlobal=true, weight=1, priority=10
    ///   FearVisuals：挂在 Player/CameraPivot/Main Camera
    ///               fear=Player 上的 FearSystem，targetCamera=Main Camera
    ///               所有数值参数用脚本默认值（第一章也是默认值）
    /// </summary>
    public static class FearVisualsBuilder
    {
        private const string SceneFmt = "Assets/_Project/Scenes/Level_Chapter{0}_Graybox.unity";
        private const string SrcProfilePath = "Assets/_Project/Settings/Volume_Main.asset";
        private const string SettingsFolder = "Assets/_Project/Settings";

        [MenuItem("ABYSS ECHO/修复 v5.4/补 FearVisuals+Volume - 第二章")]
        public static void BuildCh2() => BuildFor(2);

        [MenuItem("ABYSS ECHO/修复 v5.4/补 FearVisuals+Volume - 第三章")]
        public static void BuildCh3() => BuildFor(3);

        [MenuItem("ABYSS ECHO/修复 v5.4/补 FearVisuals+Volume - 第四章")]
        public static void BuildCh4() => BuildFor(4);

        [MenuItem("ABYSS ECHO/修复 v5.4/补 FearVisuals+Volume - 第五章")]
        public static void BuildCh5() => BuildFor(5);

        public static void BuildFor(int chapterNum)
        {
            string path = string.Format(SceneFmt, chapterNum);
            if (!File.Exists(path)) { Fail(chapterNum, $"场景不存在：{path}"); return; }

            // ── 1. 准备该章专属的 Profile 副本 ──
            string chapterProfilePath = $"{SettingsFolder}/Volume_Chapter{chapterNum}.asset";
            var profilTemp = EnsureProfileCopy(chapterNum, chapterProfilePath);
            if (profilTemp == null) { Fail(chapterNum, "无法创建 Profile 副本"); return; }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Log(chapterNum, $"已打开 {path}");

            // ⚠️ 必须在这里**重新加载** Profile：
            //   AssetDatabase.CopyAsset + Refresh 会让之前拿到的 VolumeProfile 引用失效
            //   （实测 MissingReferenceException: VolumeProfile has been destroyed）。
            //   OpenScene 也要放在前面，避免刷新把已打开场景的引用弄乱。
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(chapterProfilePath);
            if (profile == null) { Fail(chapterNum, "重新加载 Profile 失败"); return; }

            int changed = 0;

            // ── 2. Volume ──
            Volume vol = null;
            var allVols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (var v in allVols)
                if (v.gameObject.name == "--- POST_PROCESS ---") { vol = v; break; }

            if (vol != null)
            {
                Log(chapterNum, $"已存在 Volume @ {vol.gameObject.name}，检查配置");
                if (vol.sharedProfile != profile)
                {
                    vol.sharedProfile = profile;
                    EditorUtility.SetDirty(vol);
                    changed++;
                    Log(chapterNum, $"  已重新指定 sharedProfile = {profile.name}");
                }
                if (!vol.isGlobal) { vol.isGlobal = true; EditorUtility.SetDirty(vol); changed++; Log(chapterNum, "  isGlobal → true"); }
                if (!Mathf.Approximately(vol.weight, 1f)) { vol.weight = 1f; EditorUtility.SetDirty(vol); changed++; Log(chapterNum, "  weight → 1"); }
                if (vol.priority != 10) { vol.priority = 10; EditorUtility.SetDirty(vol); changed++; Log(chapterNum, "  priority → 10"); }
            }
            else
            {
                var go = new GameObject("--- POST_PROCESS ---");
                go.transform.position = new Vector3(0f, 2f, 0f);   // 与第一章一致
                vol = go.AddComponent<Volume>();
                vol.isGlobal = true;
                vol.weight = 1f;
                vol.priority = 10;
                vol.sharedProfile = profile;
                changed++;
                Log(chapterNum, $"已创建 Volume @ --- POST_PROCESS ---  sharedProfile={profile.name} " +
                                $"isGlobal={vol.isGlobal} weight={vol.weight} priority={vol.priority}");
            }

            // ── 3. FearVisuals ──
            var motor = Object.FindFirstObjectByType<AbyssEcho.Player.PlayerMotor>();
            if (motor == null) { Fail(chapterNum, "找不到 PlayerMotor"); return; }

            var camPivot = motor.transform.Find("CameraPivot");
            if (camPivot == null) { Fail(chapterNum, "找不到 Player/CameraPivot"); return; }

            var cam = camPivot.GetComponentInChildren<Camera>(true);
            if (cam == null) { Fail(chapterNum, "CameraPivot 下找不到 Camera"); return; }
            Log(chapterNum, $"相机 = {GetPath(cam.transform)}");

            var fear = motor.GetComponent<AbyssEcho.Horror.FearSystem>();
            if (fear == null) { Fail(chapterNum, "Player 上没有 FearSystem（上一批应该已补）"); return; }

            var fv = cam.GetComponent<AbyssEcho.Horror.FearVisuals>();
            if (fv != null)
            {
                Log(chapterNum, $"已存在 FearVisuals @ {GetPath(cam.transform)}，检查引用");
                bool fixedRef = false;
                if (fv.fear != fear) { fv.fear = fear; EditorUtility.SetDirty(fv); fixedRef = true; }
                if (fv.targetCamera != cam) { fv.targetCamera = cam; EditorUtility.SetDirty(fv); fixedRef = true; }
                if (fixedRef) { changed++; Log(chapterNum, "  已修正 fear / targetCamera 引用"); }
            }
            else
            {
                fv = cam.gameObject.AddComponent<AbyssEcho.Horror.FearVisuals>();
                fv.fear = fear;
                fv.targetCamera = cam;
                // 其余数值参数保持脚本默认值 —— 第一章也是默认值
                changed++;
                Log(chapterNum, $"已添加 FearVisuals @ {GetPath(cam.transform)}  " +
                                $"fear={fv.fear.gameObject.name} targetCamera={fv.targetCamera.gameObject.name}");
            }

            // ── 4. 保存 ──
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Log(chapterNum, saved ? $"场景已保存（改动 {changed} 项）" : "❌ 保存失败");

            Verify(chapterNum, cam, fv, vol, profile);
        }

        /// <summary>确保该章有独立的 Profile 副本（内容复制自 Volume_Main）。</summary>
        private static VolumeProfile EnsureProfileCopy(int ch, string destPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(destPath);
            if (existing != null)
            {
                Log(ch, $"已存在独立 Profile：{destPath}");
                return existing;
            }

            var src = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SrcProfilePath);
            if (src == null) { Fail(ch, $"找不到源 Profile：{SrcProfilePath}"); return null; }

            // 用 AssetDatabase.CopyAsset 复制，保证内容完全一致
            bool ok = AssetDatabase.CopyAsset(SrcProfilePath, destPath);
            if (!ok) { Fail(ch, $"复制 Profile 失败：{SrcProfilePath} → {destPath}"); return null; }

            AssetDatabase.Refresh();
            var copy = AssetDatabase.LoadAssetAtPath<VolumeProfile>(destPath);

            if (copy != null)
                Log(ch, $"已创建独立 Profile 副本：{destPath}（源 = Volume_Main.asset，内容完全一致）");

            return copy;
        }

        private static void Verify(int ch, Camera cam, AbyssEcho.Horror.FearVisuals fv, Volume vol, VolumeProfile prof)
        {
            var allVols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var allFv = Object.FindObjectsByType<AbyssEcho.Horror.FearVisuals>(FindObjectsSortMode.None);
            var allFear = Object.FindObjectsByType<AbyssEcho.Horror.FearSystem>(FindObjectsSortMode.None);

            Log(ch, "---- 结果 ----");
            Log(ch, $"  Volume 数量 = {allVols.Length}");
            Log(ch, $"    isGlobal={vol.isGlobal} weight={vol.weight} priority={vol.priority} " +
                    $"profile={(vol.sharedProfile != null ? vol.sharedProfile.name : "NULL")}");
            Log(ch, $"  FearVisuals 数量 = {allFv.Length}");
            Log(ch, $"    @ {GetPath(fv.transform)}");
            Log(ch, $"    fear={(fv.fear != null ? fv.fear.gameObject.name : "NULL")}  " +
                    $"targetCamera={(fv.targetCamera != null ? fv.targetCamera.gameObject.name : "NULL")}");
            Log(ch, $"    vignette={fv.vignetteCalm}/{fv.vignettePanic} " +
                    $"aberration={fv.aberrationCalm}/{fv.aberrationPanic} " +
                    $"grain={fv.grainCalm}/{fv.grainPanic}");
            Log(ch, $"  FearSystem 数量 = {allFear.Length}");

            // 相机
            var extra = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (extra != null)
            {
                Log(ch, $"  相机 renderPostProcessing={extra.renderPostProcessing} " +
                        $"volumeLayerMask={extra.volumeLayerMask.value}");
                if (!extra.renderPostProcessing)
                {
                    extra.renderPostProcessing = true;
                    EditorUtility.SetDirty(extra);
                    Log(ch, "  ⚠️ 已开启 renderPostProcessing");
                }
            }
            else
            {
                Log(ch, "  ⚠️ 相机缺少 UniversalAdditionalCameraData");
            }

            // Profile 内容核对
            if (prof != null)
            {
                int n = 0;
                foreach (var c in prof.components) if (c != null) n++;
                Log(ch, $"  Profile {prof.name} 组件数 = {n}");

                bool hasV = prof.TryGet<UnityEngine.Rendering.Universal.Vignette>(out _);
                bool hasA = prof.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out _);
                bool hasG = prof.TryGet<UnityEngine.Rendering.Universal.FilmGrain>(out _);
                Log(ch, $"    含 Vignette={hasV} ChromaticAberration={hasA} FilmGrain={hasG}");
            }

            Log(ch, "完成");
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            var cur = t.parent;
            int g = 0;
            while (cur != null && g++ < 20) { p = cur.name + "/" + p; cur = cur.parent; }
            return p;
        }

        private static void Log(int ch, string msg)
        {
            string line = $"[FVBuilder] Ch{ch} | {msg}";
            Debug.Log(line);
            Write(line);
        }

        private static void Fail(int ch, string msg)
        {
            string line = $"[FVBuilder] Ch{ch} | ❌ {msg}";
            Debug.LogError(line);
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "FearVisualsBuilder_Result.txt"),
                                   line + "\n", new System.Text.UTF8Encoding(false));
            }
            catch { }
        }
    }
}