using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S43：第四章谜题布置。
    ///
    /// G7 核心腔的"心瓣"（PulseSyncPuzzle）：
    ///   · 位于核心腔中央
    ///   · 有一个发光的瓣膜，按节奏搏动
    ///   · 玩家对它按 E 跟上节奏 → 解锁通往深处的门
    ///
    /// 门的设计：
    ///   第四章原本没有锁门（G1→G7 一路畅通）。
    ///   现在在 G7 北侧加一道"心瓣门"，只有完成脉搏同步才打开，
    ///   打开后露出真正的出口通道。
    /// </summary>
    public static class ChapterFourPuzzleBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Level_Chapter4_Graybox.unity";
        private const string RootName = "--- PUZZLE_CH4 ---";

        [MenuItem("ABYSS ECHO/S43 布置第四章谜题")]
        public static void RunAll()
        {
            var prev = AbyssEcho.LevelGen.ChapterLayout.Current;
            AbyssEcho.LevelGen.ChapterLayout.Current = AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Ch4Puzzle] 无法打开第四章场景");
                EditorApplication.Exit(1);
                return;
            }

            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);
            var rooms = CollectRooms();

            if (!rooms.TryGetValue("G7", out var g7))
            {
                Debug.LogError("[Ch4Puzzle] 找不到 G7 核心腔");
                EditorApplication.Exit(1);
                return;
            }

            // ① 心瓣（谜题本体）
            var valve = GameObject.CreatePrimitive(PrimitiveType.Cube);
            valve.name = "HeartValve";
            valve.transform.SetParent(root.transform, false);
            valve.transform.position = new Vector3(g7.Center.x, 0.12f, g7.Center.z);
            valve.transform.localScale = new Vector3(2.2f, 0.24f, 2.2f);
            valve.GetComponent<Renderer>().sharedMaterial = MakeValveMaterial();

            // ② 心瓣门（通往深层出口）
            var gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = "Door_HeartGate";
            gateGo.transform.SetParent(root.transform, false);
            // 放在 G7 北墙
            gateGo.transform.position = new Vector3(g7.Center.x, 1.4f, g7.MaxZ - 0.3f);
            gateGo.transform.localScale = new Vector3(3.0f, 2.8f, 0.16f);
            gateGo.GetComponent<Renderer>().sharedMaterial = MakeGateMaterial();

            var door = gateGo.AddComponent<AbyssEcho.Interaction.DoorInteractable>();
            door.locked = true;
            door.unlockableByInteraction = false;
            door.lockedPrompt = "门是合上的 —— 心瓣没有回应你";
            door.slideDistance = 3.0f * 0.98f;
            door.autoSlideAxis = true;
            door.speed = 1.6f;
            door.swingAngle = 0f;

            // ③ 谜题组件
            var puzzle = valve.AddComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
            puzzle.beatInterval = 2.0f;
            puzzle.syncWindow = 0.35f;
            puzzle.requiredSyncs = 3;
            puzzle.pulseRenderer = valve.GetComponent<Renderer>();
            puzzle.doorToUnlock = door;
            puzzle.telegraphLead = 0.4f;
            puzzle.promptIdle = "心瓣在缓慢搏动 —— 跟着它的节奏";
            puzzle.promptDone = "心瓣已经平静了";

            EditorUtility.SetDirty(valve);
            EditorUtility.SetDirty(gateGo);
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(puzzle);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"<b>[Ch4Puzzle]</b> 第四章谜题布置完成：心瓣 @ {valve.transform.position}，门 @ {gateGo.transform.position}");
            AbyssEcho.LevelGen.ChapterLayout.Current = prev;
        }

        private class RoomInfo
        {
            public string Id;
            public Vector3 Center;
            public float SizeX, SizeZ, Height;
            public float MaxZ => Center.z + SizeZ * 0.5f;
        }

        private static Dictionary<string, RoomInfo> CollectRooms()
        {
            var d = new Dictionary<string, RoomInfo>();
            foreach (var r in AbyssEcho.LevelGen.ChapterLayout.GetRooms(
                         AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter4))
            {
                d[r.Id] = new RoomInfo
                {
                    Id = r.Id,
                    Center = new Vector3(r.Center.x, 0f, r.Center.z),
                    SizeX = r.SizeX,
                    SizeZ = r.SizeZ,
                    Height = r.Height
                };
            }
            return d;
        }

        private static Material MakeValveMaterial()
        {
            const string folder = "Assets/_Project/Art/Materials/Ch4";
            EnsureFolder(folder);
            string path = folder + "/M_HeartValve.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_HeartValve" };
            var c = new Color(0.32f, 0.14f, 0.14f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.5f);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static Material MakeGateMaterial()
        {
            const string folder = "Assets/_Project/Art/Materials/Ch4";
            EnsureFolder(folder);
            string path = folder + "/M_HeartGate.mat";

            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { name = "M_HeartGate" };
            var c = new Color(0.42f, 0.20f, 0.20f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.2f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
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