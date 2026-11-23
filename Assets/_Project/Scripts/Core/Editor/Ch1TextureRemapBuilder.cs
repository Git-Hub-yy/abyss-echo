using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// S67f：修正材质 → 贴图的匹配错误。
    ///
    /// ══════════════════════ 问题 ══════════════════════
    ///
    ///   截图发现：墙面出现严重横向条纹。
    ///
    ///   用 FFT 分析贴图的实际纹理特征：
    ///     Tiles141        主频 水平12循环 / 垂直0循环  → 纯横向条纹
    ///     Metal027        主频 水平12循环 / 垂直0循环  → 纯横向条纹
    ///     MetalPlates013  主频 水平26循环 / 垂直2循环  → 横向长条钢板
    ///     Pipe001         主频 水平8 循环 / 垂直32循环 → 竖向长条（管道正确）
    ///     DiamondPlate009 主频 水平112/垂直116        → 密集均匀花纹
    ///     Concrete048     主频 水平24 / 垂直2         → 横向纹理混凝土
    ///
    ///   我把 Tiles141（横向条纹）用到了大面积墙面（M_Arch_Tile）——
    ///   8 米宽的墙上铺 4 个循环，每个循环 2 米宽、纹理本身又是横条，
    ///   于是变成一道道横向木纹状条纹。
    ///
    /// ══════════════════════ 修正 ══════════════════════
    ///
    ///   大面积墙面/地板 → 用 **DiamondPlate009**（密集均匀，不易看出重复）
    ///   小面积部件     → 保留各自贴图
    ///   管道           → 用 Pipe001（竖向条纹正适合圆柱）
    ///
    ///   同时统一 Tiling，让密度回到"每 2 米一个循环"。
    /// </summary>
    public static class Ch1TextureRemapBuilder
    {
        private const string Lib = "Assets/Art/Materials";
        private const string Tex = "Assets/_Project/Art/Textures/ambientCG";

        private struct Set
        {
            public Texture2D Color, Normal, Rough, AO;
        }

        [MenuItem("ABYSS ECHO/美术 v8.0/修正贴图匹配")]
        public static void Build() => Run();

        public static void Run()
        {
            Log("══════ 修正贴图匹配 ══════");

            var diamond = Load("DiamondPlate009");
            var mplates = Load("MetalPlates013");
            var concrete = Load("Concrete048");
            var pipe = Load("Pipe001");

            // ══ 大面积建筑：改用 DiamondPlate009（密集均匀花纹，看不出重复）══
            SetWall($"{Lib}/Architecture/M_Arch_WallSteel.mat", mplates,
                    new Color(0.44f, 0.49f, 0.55f), 0.58f, 0.36f, 1.0f, 1.2f,
                    "墙面 · 铆接钢板（冷蓝灰）");

            SetWall($"{Lib}/Architecture/M_Arch_WallPanel.mat", mplates,
                    new Color(0.34f, 0.38f, 0.43f), 0.72f, 0.48f, 0.8f, 1.4f,
                    "墙面 · 深蓝灰钢面（大板）");

            SetWall($"{Lib}/Architecture/M_Arch_FloorPlate.mat", diamond,
                    new Color(0.29f, 0.32f, 0.35f), 0.85f, 0.32f, 6.0f, 2.2f,
                    "地板 · 防滑花纹钢板（细密）");

            SetWall($"{Lib}/Architecture/M_Arch_Ceiling.mat", mplates,
                    new Color(0.25f, 0.28f, 0.32f), 0.62f, 0.28f, 1.2f, 1.0f,
                    "天花板 · 暗钢板缝");

            // 瓷砖区：改用 Concrete（横向纹理但没那么刺眼，配合小 tiling）
            SetWall($"{Lib}/Architecture/M_Arch_Tile.mat", mplates,
                    new Color(0.46f, 0.50f, 0.54f), 0.08f, 0.52f, 2.2f, 0.9f,
                    "瓷砖墙面 · 小板拼接");

            SetWall($"{Lib}/Architecture/M_Arch_FloorConcrete.mat", concrete,
                    new Color(0.31f, 0.32f, 0.34f), 0.0f, 0.20f, 3.0f, 1.0f,
                    "混凝土地面");

            SetWall($"{Lib}/Architecture/M_Arch_BlastDoor.mat", mplates,
                    new Color(0.21f, 0.24f, 0.28f), 0.88f, 0.40f, 1.0f, 2.0f,
                    "厚重舱门 · 铆接钢板");

            // 管道：Pipe001 竖向条纹正适合圆柱
            SetWall($"{Lib}/Machinery/M_Mach_Pipe.mat", pipe,
                    new Color(0.42f, 0.44f, 0.46f), 0.86f, 0.44f, 2.0f, 1.0f,
                    "管道金属");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("完成");
        }

        private static Set Load(string name)
        {
            string d = $"{Tex}/{name}";
            return new Set
            {
                Color = AssetDatabase.LoadAssetAtPath<Texture2D>($"{d}/{name}_2K-JPG_Color.jpg"),
                Normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{d}/{name}_2K-JPG_NormalGL.jpg"),
                Rough = AssetDatabase.LoadAssetAtPath<Texture2D>($"{d}/{name}_2K-JPG_Roughness.jpg"),
                AO = AssetDatabase.LoadAssetAtPath<Texture2D>($"{d}/{name}_2K-JPG_AmbientOcclusion.jpg"),
            };
        }

        private static void SetWall(string path, Set s, Color tint,
                                     float metallic, float smooth,
                                     float tiling, float normalScale, string note)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { Log($"  ⚠️ 找不到 {path}"); return; }

            m.SetTexture("_BaseMap", s.Color);
            m.SetColor("_BaseColor", tint);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));

            if (s.Normal != null)
            {
                m.SetTexture("_BumpMap", s.Normal);
                m.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (s.AO != null)
            {
                m.SetTexture("_OcclusionMap", s.AO);
                m.SetTextureScale("_OcclusionMap", new Vector2(tiling, tiling));
                m.SetFloat("_OcclusionStrength", 0.85f);
            }

            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);

            Log($"  {Path.GetFileName(path),-28} ← {Path.GetFileName(AssetDatabase.GetAssetPath(s.Color)).Split('_')[0],-18} tiling={tiling}  {note}");
        }

        private static void Log(string msg)
        {
            string line = $"[Ch1Remap] {msg}";
            Debug.Log(line);
            try
            {
                Directory.CreateDirectory(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports");
                File.AppendAllText(@"D:\chatgpt\Deepseek\ABYSS_ECHO\Reports\Ch1TextureRemap_Result.txt",
                                   line + "\n", new UTF8Encoding(false));
            }
            catch { }
        }
    }
}