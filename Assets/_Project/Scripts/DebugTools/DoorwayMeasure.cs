using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 精确测量门洞：在 A0→A1 的通道上逐高度打射线，找出阻挡物的实际尺寸与位置。
    /// </summary>
    public class DoorwayMeasure : MonoBehaviour
    {
        public bool runOnStart = true;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(0.5f);

            _log.AppendLine("=== 门洞精确测量 ===");

            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(AbyssEcho.LevelGen.ChapterLayout.Chapter.Chapter1);
            var a0 = rooms.Find(r => r.Id == "A0");
            var link = rooms.Find(r => r.Id == "A0_A1_Link");
            var a1 = rooms.Find(r => r.Id == "A1");

            if (a0 != null) _log.AppendLine($"A0: z[{a0.MinZ:F1},{a0.MaxZ:F1}] 层高={a0.Height:F1}");
            if (link != null) _log.AppendLine($"LINK: z[{link.MinZ:F1},{link.MaxZ:F1}] 层高={link.Height:F1} 中心=({link.Center.x:F1},{link.Center.z:F1})");
            if (a1 != null) _log.AppendLine($"A1: z[{a1.MinZ:F1},{a1.MaxZ:F1}] 层高={a1.Height:F1}");

            // A0 出口在 z = a0.MaxZ, 连接廊入口 z = link.MinZ
            float zStart = a0 != null ? a0.MaxZ - 0.5f : -40.5f;
            float zEnd = link != null ? link.MaxZ + 0.5f : -38f;

            _log.AppendLine();
            _log.AppendLine($"从 z={zStart:F1} 走到 z={zEnd:F1}，每 0.3 米采样");

            // 逐 z 逐高度打射线（朝 +Z）
            for (float z = zStart; z <= zEnd; z += 0.3f)
            {
                var blockedHeights = new System.Text.StringBuilder();
                for (float y = 0.2f; y <= 2.6f; y += 0.2f)
                {
                    RaycastHit hit;
                    // 从当前 z 点向 +Z 方向打 0.2 米，看是否被挡住
                    if (Physics.Raycast(new Vector3(0f, y, z), Vector3.forward, out hit, 0.2f,
                                        ~0, QueryTriggerInteraction.Ignore))
                    {
                        // 排除门本身（Door_A3_B 之类）
                        blockedHeights.Append($" y={y:F1}[{hit.collider.name}]");
                    }
                }

                if (blockedHeights.Length > 0)
                    _log.AppendLine($"  z={z:F1}: 阻挡{blockedHeights}");
            }

            _log.AppendLine();
            _log.AppendLine("=== 列出门洞附近所有碰撞体 ===");
            var colliders = Physics.OverlapBox(
                new Vector3(0f, 1.3f, (zStart + zEnd) * 0.5f),
                new Vector3(2f, 1.3f, (zEnd - zStart) * 0.5f + 1f));

            foreach (var c in colliders)
            {
                if (c is BoxCollider bc)
                {
                    Vector3 center = bc.transform.TransformPoint(bc.center);
                    Vector3 size = Vector3.Scale(bc.size, bc.transform.lossyScale);
                    float top = center.y + size.y * 0.5f;
                    float bottom = center.y - size.y * 0.5f;

                    // 只报"底部低于 2.0 米"的（会挡住玩家）
                    if (bottom < 2.0f && top > 0.1f)
                    {
                        _log.AppendLine($"  {c.gameObject.name}: " +
                                        $"y[{bottom:F2},{top:F2}] " +
                                        $"z[{center.z - size.z * 0.5f:F1},{center.z + size.z * 0.5f:F1}] " +
                                        $"isTrigger={bc.isTrigger}");
                    }
                }
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DoorwayMeasure]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorwayMeasure.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}