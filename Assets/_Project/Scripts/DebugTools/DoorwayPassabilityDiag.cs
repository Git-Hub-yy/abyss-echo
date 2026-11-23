using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// 门洞可通行性诊断：检查每个门洞前后是否有实体阻挡。
    ///
    /// 做法：在每个门洞处，沿通行方向从地板到天花板做射线，
    /// 看是否有墙段挡在"玩家能过的高度"（0.1 ~ 1.9 米）。
    /// </summary>
    public class DoorwayPassabilityDiag : MonoBehaviour
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

            _log.AppendLine("=== 门洞可通行性诊断 ===");

            var chapter = AbyssEcho.LevelGen.ChapterLayout.Current;
            var rooms = AbyssEcho.LevelGen.ChapterLayout.GetRooms(chapter);
            _log.AppendLine($"章节：{chapter}，房间数：{rooms.Count}");

            int problems = 0;

            foreach (var room in rooms)
            {
                foreach (var door in room.Doorways)
                {
                    // 计算门洞世界坐标
                    Vector3 pos;
                    Vector3 dir;
                    GetDoorPosAndDir(room, door, out pos, out dir);

                    // 检查玩家高度范围内是否有阻挡
                    bool blocked = false;
                    string blocker = "";

                    // 在门洞中心，沿通行方向前后各 0.5 米取样
                    for (float offset = -0.5f; offset <= 0.5f; offset += 0.25f)
                    {
                        Vector3 sample = pos + dir * offset + Vector3.up * 1.0f;

                        // 用一个稍小的球检查
                        var hits = Physics.OverlapSphere(sample, 0.35f);
                        foreach (var h in hits)
                        {
                            if (h is BoxCollider && h.gameObject.name.StartsWith(RoomDefWallPrefix))
                            {
                                blocked = true;
                                blocker = h.gameObject.name;
                                break;
                            }
                        }
                        if (blocked) break;
                    }

                    if (blocked)
                    {
                        _log.AppendLine($"❌ {room.Id} 的 {door.Side} 门（宽{door.Width:F1} 高{door.Height:F1}）被阻挡：{blocker}");
                        problems++;
                    }
                }
            }

            if (problems == 0) _log.AppendLine("✅ 未发现阻挡");

            // 额外：检查 A0 → A1 这个具体位置
            _log.AppendLine();
            _log.AppendLine("=== A0 到 A1 的通道检查 ===");
            var a0 = rooms.Find(r => r.Id == "A0");
            if (a0 != null)
            {
                _log.AppendLine($"A0: 中心={a0.Center} 尺寸={a0.SizeX}x{a0.SizeZ} 层高={a0.Height}");
                _log.AppendLine($"A0 MaxZ={a0.MaxZ}");
                foreach (var d in a0.Doorways)
                    _log.AppendLine($"  A0 门: {d.Side} 偏移={d.Offset} 宽={d.Width} 高={d.Height}");
            }

            var a1 = rooms.Find(r => r.Id == "A1");
            if (a1 != null)
            {
                _log.AppendLine($"A1: 中心={a1.Center} 尺寸={a1.SizeX}x{a1.SizeZ} 层高={a1.Height}");
                _log.AppendLine($"A1 MinZ={a1.MinZ}");
                foreach (var d in a1.Doorways)
                    _log.AppendLine($"  A1 门: {d.Side} 偏移={d.Offset} 宽={d.Width} 高={d.Height}");
            }

            // 沿 z 轴打射线，看从 A0 到 A1 的路上有什么
            _log.AppendLine();
            _log.AppendLine("=== 沿 Z 轴射线检查（x=0，y=1.0，z 从 -42 到 -33）===");
            for (float z = -42f; z <= -33f; z += 0.5f)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Vector3(0f, 1.0f, z), Vector3.forward, out hit, 0.3f))
                {
                    _log.AppendLine($"  z={z:F1} 命中: {hit.collider.gameObject.name}");
                }
            }

            Flush();
            yield return null;
            if (!Application.isEditor) Application.Quit(0);
        }

        private const string RoomDefWallPrefix = "";

        private static void GetDoorPosAndDir(AbyssEcho.LevelGen.RoomDef r, AbyssEcho.LevelGen.DoorwayDef d,
                                             out Vector3 pos, out Vector3 dir)
        {
            switch (d.Side)
            {
                case AbyssEcho.LevelGen.WallSide.North:
                    pos = new Vector3(r.Center.x + d.Offset, r.Center.y, r.MaxZ);
                    dir = Vector3.forward;
                    break;
                case AbyssEcho.LevelGen.WallSide.South:
                    pos = new Vector3(r.Center.x + d.Offset, r.Center.y, r.MinZ);
                    dir = Vector3.forward;
                    break;
                case AbyssEcho.LevelGen.WallSide.East:
                    pos = new Vector3(r.MaxX, r.Center.y, r.Center.z + d.Offset);
                    dir = Vector3.right;
                    break;
                default:
                    pos = new Vector3(r.MinX, r.Center.y, r.Center.z + d.Offset);
                    dir = Vector3.right;
                    break;
            }
        }

        private void Flush()
        {
            string text = _log.ToString();
            Debug.Log("<b>[DoorwayDiag]</b>\n" + text);
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "DoorwayPassabilityDiag.txt"), text,
                                  new UTF8Encoding(false));
            }
            catch { }
        }
    }
}