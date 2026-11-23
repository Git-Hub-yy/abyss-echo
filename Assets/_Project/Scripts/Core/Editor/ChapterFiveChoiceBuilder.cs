using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S43：第五章终局选择布置。
    ///
    /// 在 H7 出口舱放置撤离控制台（EvacuationConsole）。
    ///
    /// 位置策略：
    ///   · 放在舱门左侧（玩家进门后第一眼就能看到）
    ///   · 不放在正中，避免挡住通往舱门的路
    ///   · 与已有道具（出口舱门/舱门框/控制台/引导线）错开
    /// </summary>
    public static class ChapterFiveChoiceBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter5_Graybox.unity";
        private const string RootName = "--- CHOICE_CH5 ---";

        [MenuItem("ABYSS ECHO/S43 布置第五章终局选择")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch5Choice] 无法打开第五章场景");
                EditorApplication.Exit(1);
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            if (!rooms.TryGetValue("H7", out var h7))
            {
                Debug.LogError("[Ch5Choice] 找不到 H7 出口舱");
                EditorApplication.Exit(1);
                return;
            }

            // 撤离控制台：舱门左侧 3 米处，靠近北墙
            var consolePos = new Vector3(h7.Center.x - 3.2f, 1.05f, h7.MaxZ - 2.2f);

            var consoleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            consoleGo.name = "EvacConsole";
            consoleGo.transform.SetParent(root.transform, false);
            consoleGo.transform.position = consolePos;
            consoleGo.transform.localScale = new Vector3(1.4f, 1.1f, 0.7f);
            consoleGo.GetComponent<Renderer>().sharedMaterial = MakeConsoleMaterial();

            // 屏幕（发光面）
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "EvacConsole_Screen";
            screen.transform.SetParent(consoleGo.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.18f, -0.52f);
            screen.transform.localScale = new Vector3(1.15f, 0.55f, 0.05f);
            screen.GetComponent<Renderer>().sharedMaterial = MakeScreenMaterial();
            var sc = screen.GetComponent<Collider>();
            if (sc != null) Object.DestroyImmediate(sc);

            var console = consoleGo.AddComponent<AbyssEcho.Puzzle.EvacuationConsole>();
            console.endingIdAlone = "A";
            console.endingIdRecord = "B";
            console.endingIdSeal = "C";
            console.promptIdle = "撤离协议 —— 做出你的选择";
            console.promptDone = "协议已确认";

            // 关联 H7 的出口舱门（如果场景里有的话按名字找）
            var doors = Object.FindObjectsByType<AbyssEcho.Interaction.DoorInteractable>(FindObjectsSortMode.None);
            foreach (var d in doors)
            {
                if (d.name.Contains("Exit") || d.name.Contains("H7") || d.name.Contains("出口"))
                {
                    console.exitDoor = d;
                    Debug.Log($"[Ch5Choice] 控制台关联门 {d.name}");
                    break;
                }
            }

            EditorUtility.SetDirty(consoleGo);
            EditorUtility.SetDirty(console);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch5Choice]</b> 第五章终局选择布置完成：撤离控制台 @ {consolePos}");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float MaxZ => Center.z + SizeZ * 0.5f;
            public float SizeZ;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var d = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter5))
            {
                d[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeZ = r.SizeZ
                };
            }
            return d;
        }

        private static Material MakeConsoleMaterial()
        {
            const string folder = "Assets/_Project/Art/Materials/Ch5";
            EnsureFolder(folder);
            string path = folder + "/M_EvacConsole.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_EvacConsole" };
            var c = new Color(0.28f, 0.30f, 0.34f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material MakeScreenMaterial()
        {
            const string folder = "Assets/_Project/Art/Materials/Ch5";
            EnsureFolder(folder);
            string path = folder + "/M_EvacScreen.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_EvacScreen" };
            var c = new Color(0.20f, 0.62f, 0.55f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 2.0f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int idx = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, idx));
            AssetDatabase.CreateFolder(path.Substring(0, idx), path.Substring(idx + 1));
        }
    }
}