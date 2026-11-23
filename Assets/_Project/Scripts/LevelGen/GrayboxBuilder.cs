using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 灰盒几何生成器（S3A）。
    ///
    /// 把一个 RoomDef 变成实际的方块几何：
    ///  - 地板、天花板
    ///  - 四面墙，并在门洞位置**真正挖出缺口**（门洞两侧墙段 + 门楣）
    ///
    /// 关键设计：门洞是几何上的真实空洞，而不是"看起来有门"。
    /// 这保证"玩家能不能走通"在物理层面成立，而不是靠视觉判断。
    /// </summary>
    public class GrayboxBuilder : MonoBehaviour
    {
        [Tooltip("墙体使用的材质（灰盒阶段为纯色）")]
        public Material wallMaterial;
        public Material floorMaterial;
        public Material ceilingMaterial;

        [Header("S3B 分材质（留空则回退到上面的统一材质）")]
        [Tooltip("各区域使用的材质组，按房间 ID 前缀匹配")]
        public Material wallMaterialMetal;      // 金属墙（走廊、宿舍、餐厅）
        public Material wallMaterialConcrete;   // 混凝土（舱室、闸室）
        public Material wallMaterialTiled;      // 瓷砖（洗手间、医疗室）
        public Material floorMaterialDiamond;   // 花纹钢板（工业地面）
        public Material floorMaterialConcrete;  // 混凝土地面

        [Tooltip("是否生成天花板（灰盒阶段建议关闭以便俯视检查）")]
        public bool buildCeilings = true;

        [Tooltip("每个房间是否生成独立子物体便于管理")]
        public bool groupByRoom = true;

        private const float T = LevelSpec.WallThickness;

        /// <summary>生成全部房间，返回房间根节点。</summary>
        public Transform Build(List<RoomDef> rooms, Transform parent)
        {
            var root = new GameObject("--- ENVIRONMENT ---").transform;
            root.SetParent(parent, false);

            foreach (var room in rooms)
            {
                var roomRoot = new GameObject($"Room_{room.Id}_{room.DisplayName}").transform;
                roomRoot.SetParent(groupByRoom ? root : parent, false);

                BuildFloor(roomRoot, room);
                if (buildCeilings) BuildCeiling(roomRoot, room);

                BuildWall(roomRoot, room, WallSide.North);
                BuildWall(roomRoot, room, WallSide.South);
                BuildWall(roomRoot, room, WallSide.East);
                BuildWall(roomRoot, room, WallSide.West);
            }

            return root;
        }

        private void BuildFloor(Transform p, RoomDef r)
        {
            // 地板覆盖到墙体中线，避免房间交界处出现缝隙
            var go = Box(p, $"Floor_{r.Id}",
                new Vector3(r.Center.x, r.FloorY - 0.25f, r.Center.z),
                new Vector3(r.SizeX + T, 0.5f, r.SizeZ + T),
                PickFloorMaterial(r));
            go.isStatic = true;

            // UV 缩放：让贴图在世界空间保持约 2m 一个循环，避免不同大小房间贴图被拉伸
            ScaleUVFlat(go, r.SizeX + T, r.SizeZ + T);
        }

        private void BuildCeiling(Transform p, RoomDef r)
        {
            // 天花板优先使用金属墙板材质：
            //   原先用纯色深灰，方向光照不到顶面 → 全黑。
            //   换成与墙相同的 PBR 材质后，能接收环境反射与点光，且贴图质感统一。
            Material ceilMat = wallMaterialMetal != null ? wallMaterialMetal : ceilingMaterial;

            var go = Box(p, $"Ceiling_{r.Id}",
                new Vector3(r.Center.x, r.FloorY + r.Height + 0.15f, r.Center.z),
                new Vector3(r.SizeX + T, 0.3f, r.SizeZ + T),
                ceilMat);
            go.isStatic = true;
            ScaleUVFlat(go, r.SizeX + T, r.SizeZ + T);
        }

        // ────────────────────────── 材质选择 ──────────────────────────

        /// <summary>按房间 ID 选择墙面材质（体现区域功能差异）。</summary>
        private Material PickWallMaterial(RoomDef r)
        {
            string id = r.Id;

            // 深潜舱、气密闸室、压力走廊 → 混凝土（粗糙、冷硬）
            if (id.StartsWith("A0") || id == "A2" || id.Contains("Pressure") || id == "A3_B_Link")
                return wallMaterialConcrete != null ? wallMaterialConcrete : wallMaterial;

            // 洗手间、医疗室、宿舍 → 瓷砖/浅色（生活区）
            if (id == "B5" || id.StartsWith("B3") || id.StartsWith("B4") || id == "B2_B5_Link" || id == "B2_B3_Link")
                return wallMaterialTiled != null ? wallMaterialTiled : wallMaterial;

            // 其余（走廊、餐厅、主厅、服务器房等）→ 金属墙板
            return wallMaterialMetal != null ? wallMaterialMetal : wallMaterial;
        }

        private Material PickFloorMaterial(RoomDef r)
        {
            string id = r.Id;
            if (id.StartsWith("A0") || id == "A2")
                return floorMaterialConcrete != null ? floorMaterialConcrete : floorMaterial;
            return floorMaterialDiamond != null ? floorMaterialDiamond : floorMaterial;
        }

        /// <summary>
        /// 为盒子（Cube）做 UV 世界空间缩放。
        /// Unity Cube 的每个面 UV 都是 0~1，若直接贴图会被拉伸成房间大小。
        /// 这里按世界尺寸重算 UV，让贴图保持"每 2m 循环一次"的固定密度。
        /// </summary>
        private void ScaleUVFlat(GameObject go, float worldX, float worldZ, float metersPerTile = 2f)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            // 复制一份 Mesh，避免修改共享的内置 Cube Mesh（会污染全场景所有方块）
            var mesh = Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_UV";

            var uv = mesh.uv;
            var verts = mesh.vertices;

            // 按顶点所在的面判断朝向：向上/向下 = 地板天花板（用 XZ），
            // 法线朝 X = 侧墙（用 ZY），法线朝 Z = 前后墙（用 XY）。
            var normals = mesh.normals;
            for (int i = 0; i < uv.Length && i < verts.Length && i < normals.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 v = verts[i];   // Cube 局部坐标，范围 -0.5~0.5
                float u, vv;

                if (Mathf.Abs(n.y) > 0.5f)
                {
                    // 水平面：局部 x/z × 世界缩放 = 世界偏移，再除以瓦片尺寸
                    u = (v.x + 0.5f) * worldX / metersPerTile;
                    vv = (v.z + 0.5f) * worldZ / metersPerTile;
                }
                else if (Mathf.Abs(n.x) > 0.5f)
                {
                    // 东西墙：用局部 z 与 y
                    u = (v.z + 0.5f) * worldZ / metersPerTile;
                    vv = (v.y + 0.5f) * (go.transform.localScale.y) / metersPerTile;
                }
                else
                {
                    // 南北墙：用局部 x 与 y
                    u = (v.x + 0.5f) * worldX / metersPerTile;
                    vv = (v.y + 0.5f) * (go.transform.localScale.y) / metersPerTile;
                }

                uv[i] = new Vector2(u, vv);
            }

            mesh.uv = uv;
            mf.sharedMesh = mesh;
            var resolved = ResolveGeneratedMesh(mesh, go.name);
            if (resolved != null) mf.sharedMesh = resolved;
        }

        /// <summary>生成一面墙：若有门洞，则拆成"左段 + 右段 + 门楣"。</summary>
        private void BuildWall(Transform p, RoomDef r, WallSide side)
        {
            // 该墙上的门洞（可能多个）
            var doors = new List<DoorwayDef>();
            foreach (var d in r.Doorways)
                if (d.Side == side) doors.Add(d);
            doors.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            bool horizontal = side == WallSide.North || side == WallSide.South;  // 墙沿 X 方向
            float wallLength = horizontal ? r.SizeX : r.SizeZ;
            float centerAlong = horizontal ? r.Center.x : r.Center.z;
            float half = wallLength * 0.5f;

            // 墙所在的固定坐标
            float fixedCoord = side switch
            {
                WallSide.North => r.FloorY,
                WallSide.South => r.FloorY,
                WallSide.East => r.FloorY,
                _ => r.FloorY
            };
            float wallPlane = side switch
            {
                WallSide.North => r.MaxZ,
                WallSide.South => r.MinZ,
                WallSide.East => r.MaxX,
                WallSide.West => r.MinX,
                _ => 0f
            };

            if (doors.Count == 0)
            {
                MakeWallSegment(p, r, side, centerAlong, wallLength, r.Height, r.Height * 0.5f, wallPlane, horizontal, "Full");
                return;
            }

            // 从左到右切分
            float cursor = centerAlong - half;
            int idx = 0;
            foreach (var d in doors)
            {
                float dLeft = centerAlong + d.Offset - d.Width * 0.5f;
                float dRight = centerAlong + d.Offset + d.Width * 0.5f;

                // 门洞左侧的墙段
                if (dLeft - cursor > 0.02f)
                    MakeWallSegment(p, r, side, (cursor + dLeft) * 0.5f, dLeft - cursor,
                                    r.Height, r.Height * 0.5f, wallPlane, horizontal, $"Seg{idx++}");

                // 门楣（门洞上方的墙）
                // ⚠️ S3B 实测缺陷：门高接近层高时（宿舍 2.4 门 / 2.8 层高），
                //   门楣只剩 0.4m，第一人称看像"悬空黑块"。
                //   修正：保证门楣 ≥ LevelSpec.MinLintelHeight，且门洞净高 ≥ 玩家身高 + 余量。
                float maxDoorH = r.Height - LevelSpec.MinLintelHeight;
                float doorH = Mathf.Min(d.Height, maxDoorH);
                doorH = Mathf.Max(doorH, Mathf.Min(LevelSpec.MinDoorHeight, maxDoorH));   // 不能矮于可通行高度

                float lintelHeight = r.Height - doorH;
                if (lintelHeight > 0.05f)
                {
                    MakeWallSegment(p, r, side, centerAlong + d.Offset, d.Width,
                                    lintelHeight, doorH + lintelHeight * 0.5f, wallPlane, horizontal, $"Lintel{idx}");
                }

                cursor = dRight;
            }

            // 最后一扇门右侧的墙段
            float remaining = (centerAlong + half) - cursor;
            if (remaining > 0.02f)
                MakeWallSegment(p, r, side, (cursor + centerAlong + half) * 0.5f, remaining,
                                r.Height, r.Height * 0.5f, wallPlane, horizontal, $"SegEnd");
        }

        private void MakeWallSegment(Transform p, RoomDef r, WallSide side,
                                     float alongCenter, float alongLength,
                                     float height, float heightCenter,
                                     float wallPlane, bool horizontal, string tag)
        {
            Vector3 pos = horizontal
                ? new Vector3(alongCenter, r.FloorY + heightCenter, wallPlane)
                : new Vector3(wallPlane, r.FloorY + heightCenter, alongCenter);

            Vector3 size = horizontal
                ? new Vector3(alongLength, height, T)
                : new Vector3(T, height, alongLength);

            var go = Box(p, $"{side}_{r.Id}_{tag}", pos, size, PickWallMaterial(r));
            go.isStatic = true;

            // 墙体 UV：沿墙方向按世界长度平铺，竖直方向按高度平铺
            ScaleUVWall(go, alongLength, height, horizontal);
        }

        /// <summary>墙体 UV 缩放（只处理朝向玩家的一侧为主要可见面，简化处理）。</summary>
        private void ScaleUVWall(GameObject go, float alongLength, float height, bool horizontal,
                                 float metersPerTile = 2f)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mesh = Instantiate(mf.sharedMesh);
            mesh.name = mf.sharedMesh.name + "_WALLUV";

            var uv = mesh.uv;
            var verts = mesh.vertices;
            var normals = mesh.normals;

            for (int i = 0; i < uv.Length && i < verts.Length && i < normals.Length; i++)
            {
                Vector3 n = normals[i];
                Vector3 v = verts[i];
                float u, vv;

                if (Mathf.Abs(n.y) > 0.5f)
                {
                    // 门楣顶/底面
                    u = (v.x + 0.5f) * alongLength / metersPerTile;
                    vv = (v.z + 0.5f) * LevelSpec.WallThickness / metersPerTile;
                }
                else if (Mathf.Abs(n.x) > 0.5f)
                {
                    // 东西向的墙（法线朝 X）：可见面是 XY 平面
                    u = horizontal
                        ? (v.x + 0.5f) * alongLength / metersPerTile
                        : (v.z + 0.5f) * LevelSpec.WallThickness / metersPerTile;
                    vv = (v.y + 0.5f) * height / metersPerTile;
                }
                else
                {
                    // 南北向的墙（法线朝 Z）：可见面是 XY 或 ZY
                    u = horizontal
                        ? (v.x + 0.5f) * alongLength / metersPerTile
                        : (v.z + 0.5f) * alongLength / metersPerTile;
                    vv = (v.y + 0.5f) * height / metersPerTile;
                }

                uv[i] = new Vector2(u, vv);
            }

            mesh.uv = uv;
            mf.sharedMesh = mesh;
            var resolved = ResolveGeneratedMesh(mesh, go.name);
            if (resolved != null) mf.sharedMesh = resolved;
        }

        private GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            if (mat != null)
            {
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
            }
            return go;
        }
        // ==================== 网格资产化（S3B 关键修复）====================
        // ⚠️ S3B 实测致命 Bug：
        //   Instantiate() 在编辑器里产生的是内存临时 Mesh，没有资产文件。
        //   场景保存后引用无法序列化，重新加载时变 null，
        //   渲染器无网格可画 → 画面只剩清屏色（表现为"一片深灰蓝/方块闪烁"）。
        //   修正：编辑器下把 Mesh 存为资产，保证场景正确引用。

        /// <summary>
        /// 把生成的 Mesh 保存为工程资产，并**返回应赋给 MeshFilter 的网格**。
        ///
        /// ⚠️ S3B 致命 Bug（画面全黑的最终根因）：
        ///   原实现在"资产已存在"分支里 DestroyImmediate(mesh) 后直接 return，
        ///   调用方仍然把已销毁的临时网格赋给 MeshFilter；
        ///   保存场景时这些引用全部退化为 {fileID: 0}，
        ///   实测 259 个 MeshFilter 的 m_Mesh 全为空 → 几何体一个都画不出来。
        ///
        /// 修正：本方法返回"应当使用的网格"，调用方必须用它来设置 sharedMesh。
        /// </summary>
        private static Mesh ResolveGeneratedMesh(Mesh mesh, string hint)
        {
#if UNITY_EDITOR
            if (mesh == null) return null;
            if (Application.isPlaying) return mesh;   // 运行时不需要持久化

            const string folder = "Assets/_Project/Art/GeneratedMeshes";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/_Project/Art", "GeneratedMeshes");

            string safe = hint.Replace('/', '_').Replace('\\', '_');
            string path = $"{folder}/MESH_{safe}.asset";

            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // 复用已有资产：把本次算出的 UV 写进去，然后丢弃临时网格
                existing.uv = mesh.uv;
                UnityEditor.EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
                return existing;      // ← 关键：返回资产，而不是已销毁的临时网格
            }

            mesh.name = "MESH_" + safe;
            UnityEditor.AssetDatabase.CreateAsset(mesh, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
#else
            return mesh;
#endif
        }
    }
}
