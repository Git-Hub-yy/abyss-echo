using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    public static class Ch4PuzzleCheck
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter4_Graybox.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            var valve = GameObject.Find("HeartValve");
            var gate = GameObject.Find("Door_HeartGate");
            sb.AppendLine($"HeartValve = {(valve != null ? valve.transform.position.ToString() : "NULL")}");
            sb.AppendLine($"Door_HeartGate = {(gate != null ? gate.transform.position.ToString() : "NULL")}");
            if (valve != null)
            {
                var p = valve.GetComponent<AbyssEcho.Puzzle.PulseSyncPuzzle>();
                sb.AppendLine($"PulseSyncPuzzle = {(p != null ? "OK" : "NULL")}");
                if (p != null)
                {
                    sb.AppendLine($"  beatInterval={p.beatInterval} syncWindow={p.syncWindow} requiredSyncs={p.requiredSyncs}");
                    sb.AppendLine($"  doorToUnlock={(p.doorToUnlock != null ? p.doorToUnlock.name : "NULL")}");
                }
            }
            if (gate != null)
            {
                var d = gate.GetComponent<AbyssEcho.Interaction.DoorInteractable>();
                sb.AppendLine($"Door.locked={(d != null ? d.locked.ToString() : "?")} unlockable={(d != null ? d.unlockableByInteraction.ToString() : "?")}");
            }
            Debug.Log(sb.ToString());
            File.WriteAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports/Ch4PuzzleCheck.txt")), sb.ToString(), new UTF8Encoding(false));
        }
    }
}