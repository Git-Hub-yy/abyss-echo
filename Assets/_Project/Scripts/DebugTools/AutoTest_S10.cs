using System.Collections;
using System.IO;
using System.Text;
using AbyssEcho.LevelGen;
using UnityEngine;

namespace AbyssEcho.DebugTools
{
    /// <summary>
    /// S10 章节框架测试：验证 ChapterLayout 统一入口工作正常，
    /// 且未破坏第一章的既有行为。
    /// </summary>
    public class AutoTest_S10 : MonoBehaviour
    {
        public bool runOnStart = true;

        private int _pass, _fail;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start()
        {
            if (runOnStart) StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            _log.AppendLine("ABYSS ECHO · S10 章节框架自动测试");
            _log.AppendLine("=====================================");

            yield return new WaitForSeconds(0.5f);

            T70_DefaultChapterIsOne();
            T71_GetRoomsWorks();
            T72_ChapterOneUnchanged();
            T73_ChapterNameAndScene();
            T74_PlayerStartMatches();
            T75_EndPointMatches();
            T76_SwitchChapter();
            T77_ResetBack();

            _log.AppendLine("=====================================");
            _log.AppendLine($"结果：通过 {_pass}，失败 {_fail}");

            string text = _log.ToString();
            Debug.Log("<b>[AutoTest_S10]</b>\n" + text);
            WriteReport(text);

            yield return new WaitForSeconds(0.2f);
            if (!Application.isEditor) Application.Quit(_fail == 0 ? 0 : 1);
        }

        private void T70_DefaultChapterIsOne()
        {
            // 清理偏好后应默认第一章
            PlayerPrefs.DeleteKey("game.currentChapter");
            var c = ChapterLayout.Current;
            Check("T70 默认章节是第一章", c == ChapterLayout.Chapter.Chapter1, $"当前={c}");
        }

        private void T71_GetRoomsWorks()
        {
            var rooms = ChapterLayout.GetRooms();
            Check("T71 GetRooms() 返回房间列表", rooms != null && rooms.Count > 0,
                  $"共 {rooms?.Count ?? 0} 个房间");
        }

        private void T72_ChapterOneUnchanged()
        {
            var viaLayout = ChapterLayout.GetRooms();
            var direct = ChapterOneLayout.Build();

            bool sameCount = viaLayout.Count == direct.Count;
            bool sameIds = true;
            if (sameCount)
            {
                for (int i = 0; i < viaLayout.Count; i++)
                {
                    if (viaLayout[i].Id != direct[i].Id ||
                        Vector3.Distance(viaLayout[i].Center, direct[i].Center) > 0.01f)
                    {
                        sameIds = false;
                        break;
                    }
                }
            }

            Check("T72 第一章数据完全一致", sameCount && sameIds,
                  $"ChapterLayout={viaLayout.Count} 直接调用={direct.Count}");
        }

        private void T73_ChapterNameAndScene()
        {
            string n1 = ChapterLayout.GetChapterName(ChapterLayout.Chapter.Chapter1);
            string s1 = ChapterLayout.GetSceneName(ChapterLayout.Chapter.Chapter1);

            bool ok = !string.IsNullOrEmpty(n1) && s1 == "Level_Chapter1_Graybox";
            Check("T73 章节名与场景名", ok, $"名称={n1} 场景={s1}");
        }

        private void T74_PlayerStartMatches()
        {
            var a = ChapterLayout.GetPlayerStart();
            var b = ChapterOneLayout.PlayerStart;
            bool ok = Vector3.Distance(a, b) < 0.01f;
            Check("T74 玩家起点一致", ok, $"ChapterLayout={a} 原值={b}");
        }

        private void T75_EndPointMatches()
        {
            var a = ChapterLayout.GetChapterEndPoint();
            var b = ChapterOneLayout.ChapterEndPoint;
            bool ok = Vector3.Distance(a, b) < 0.01f;
            Check("T75 章终点一致", ok, $"ChapterLayout={a} 原值={b}");
        }

        private void T76_SwitchChapter()
        {
            ChapterLayout.Current = ChapterLayout.Chapter.Chapter2;
            bool switched = ChapterLayout.Current == ChapterLayout.Chapter.Chapter2;

            // 第二章未实现时应回退到第一章数据，且不崩
            var rooms = ChapterLayout.GetRooms();
            bool fallbackOk = rooms != null && rooms.Count > 0;

            Check("T76 可切换到第二章（含回退）", switched && fallbackOk,
                  $"切换={switched} 回退房间数={rooms?.Count ?? 0}");
        }

        private void T77_ResetBack()
        {
            ChapterLayout.ResetToFirst();
            bool ok = ChapterLayout.Current == ChapterLayout.Chapter.Chapter1;
            Check("T77 可重置回第一章", ok, $"当前={ChapterLayout.Current}");
        }

        // ────────────────────────── 工具 ──────────────────────────

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; _log.AppendLine($"[通过] {name} {detail}"); }
            else { _fail++; _log.AppendLine($"[失败] {name} {detail}"); }
        }

        private static void WriteReport(string text)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "S10_AutoTestResult.txt"), text, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}