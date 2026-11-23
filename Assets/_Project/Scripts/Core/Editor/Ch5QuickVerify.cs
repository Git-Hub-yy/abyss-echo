using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
    public static class Ch5QuickVerify {
        public static void Run() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_Chapter5_Graybox.unity", OpenSceneMode.Single);
            var go = GameObject.Find("East_H1_Full");
            var mesh = go?.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) { Debug.Log("找不到"); return; }
            var uv = mesh.uv; var n = mesh.normals;
            float mnU=999,mxU=-999,mnV=999,mxV=-999;
            for (int i=0;i<n.Length;i++) {
                if (Mathf.Abs(n[i].x) < 0.5f) continue;
                mnU=Mathf.Min(mnU,uv[i].x); mxU=Mathf.Max(mxU,uv[i].x);
                mnV=Mathf.Min(mnV,uv[i].y); mxV=Mathf.Max(mxV,uv[i].y);
            }
            Debug.Log($"East_H1_Full X面 UV={mxU-mnU:F3}×{mxV-mnV:F3} (修复后应为 4.000×2.500)");
        }
    }
}