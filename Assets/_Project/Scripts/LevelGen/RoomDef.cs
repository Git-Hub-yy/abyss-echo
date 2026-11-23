using System.Collections.Generic;
using UnityEngine;

namespace AbyssEcho.LevelGen
{
    /// <summary>
    /// 灰盒房间定义（S3A）。
    ///
    /// 设计原因：
    ///  第一章有 20+ 个房间，若在场景里手工摆墙体，
    ///  ① 尺寸容易与策划文档不一致  ② 改一处尺寸要重摆全部  ③ 无法用代码验证"能否走通"。
    ///  因此这里用**数据驱动**：房间只描述"中心点 + 尺寸 + 门洞"，
    ///  由 GrayboxBuilder 自动生成墙体、地板、天花板，并保证门洞是真实缺口。
    ///
    /// 坐标约定（与《03 区域空间布局设计》一致）：
    ///  - 1 单位 = 1 米
    ///  - X 向右，Z 向前（+Z 为北），Y 向上，地板面为 y = 0
    /// </summary>
    public class RoomDef
    {
        public string Id;           // 例：A1
        public string DisplayName;  // 例：入口走廊
        public Vector3 Center;      // 房间中心（y 为地板高度，通常 0）
        public float SizeX;         // 东西向长度
        public float SizeZ;         // 南北向长度
        public float Height = 3.2f; // 层高

        /// <summary>门洞：每面墙上的开口，用于连通。</summary>
        public List<DoorwayDef> Doorways = new List<DoorwayDef>();

        public RoomDef(string id, string displayName, Vector3 center, float sizeX, float sizeZ, float height = 3.2f)
        {
            Id = id;
            DisplayName = displayName;
            Center = center;
            SizeX = sizeX;
            SizeZ = sizeZ;
            Height = height;
        }

        public RoomDef WithDoor(DoorwayDef dw)
        {
            Doorways.Add(dw);
            return this;
        }

        public float MinX => Center.x - SizeX * 0.5f;
        public float MaxX => Center.x + SizeX * 0.5f;
        public float MinZ => Center.z - SizeZ * 0.5f;
        public float MaxZ => Center.z + SizeZ * 0.5f;
        public float FloorY => Center.y;

        public override string ToString() => $"{Id} {DisplayName} ({SizeX}x{SizeZ}x{Height}) @ {Center}";
    }

    public enum WallSide { North, South, East, West }

    /// <summary>墙上的一个门洞。</summary>
    public class DoorwayDef
    {
        public WallSide Side;
        /// <summary>门洞中心沿墙方向的偏移（相对房间中心）。North/South 墙用 X 偏移，East/West 墙用 Z 偏移。</summary>
        public float Offset;
        public float Width = 1.6f;
        public float Height = 2.3f;

        public DoorwayDef(WallSide side, float offset, float width = 1.6f, float height = 2.3f)
        {
            Side = side;
            Offset = offset;
            Width = width;
            Height = height;
        }
    }

    /// <summary>墙体规格（全项目统一，避免每面墙尺寸各自为政）。</summary>
    public static class LevelSpec
    {
        public const float WallThickness = 0.3f;
        public const float DefaultDoorWidth = 2.0f;
        public const float DefaultDoorHeight = 2.3f;

        public const float CorridorWidth = 3.0f;   // 主通路 3m
        public const float CorridorHeight = 3.2f;

        /// <summary>
        /// 门洞上方必须保留的最小门楣高度（米）。
        ///
        /// 为什么需要这个值：
        ///   S3B 实测发现，当门高接近层高（宿舍 2.4m 门 / 2.8m 层高）时，
        ///   门楣只剩 0.4m，从第一人称看像一个"悬空黑块"，视觉上像 bug。
        ///   规定门楣 ≥ 0.45m 后，门洞看起来是规整的门框而不是异常缺口。
        /// </summary>
        public const float MinLintelHeight = 0.45f;

        /// <summary>玩家站立时总高度（用于校验门洞高度是否足够）。</summary>
        public const float PlayerHeight = 1.78f;

        /// <summary>门洞最小净高：玩家身高 + 余量。</summary>
        public const float MinDoorHeight = PlayerHeight + 0.25f;

        /// <summary>玩家胶囊半径 0.32，门洞 1.6 宽，两侧各留 0.48 余量，足够通过。</summary>
        public static bool IsDoorWideEnough(float width) => width >= 1.5f;   // 第一人称实测：<1.5m 过门别扭
    }
}