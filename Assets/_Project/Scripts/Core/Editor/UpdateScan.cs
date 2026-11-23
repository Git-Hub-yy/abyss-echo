using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AbyssEcho.Core.EditorTools
{
    /// <summary>
    /// 静态分析：精确找出 Update 方法体内的危险调用。
    ///
    /// 上一版用简单字符串匹配，逻辑有缺陷（方法边界判定错误）。
    /// 本版用花括号深度跟踪，准确划定方法体范围。
    /// </summary>
    public static class UpdateScan
    {
        [MenuItem("ABYSS ECHO/审计/Update 危险调用扫描")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== Update 方法体内危险调用（精确扫描）==========");

            string root = "Assets/_Project/Scripts";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            string[] danger =
            {
                "FindFirstObjectByType",
                "FindObjectsByType",
                "FindObjectOfType",
                "FindObjectsOfType",
                "Camera.main",
                ".material",
                "Instantiate(",
                "new GameObject",
                "Resources.Load",
                "GetComponent<",
            };

            int totalHits = 0;
            var perFile = new Dictionary<string, int>();

            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                if (name.StartsWith("AutoTest_") || name.Contains("Diag") || name.Contains("Check") ||
                    name.Contains("Audit") || name.Contains("Scan") || name.Contains("Probe") ||
                    name.Contains("Walkthrough") || name.Contains("Visual"))
                    continue;   // 跳过测试/诊断脚本

                var lines = File.ReadAllLines(f);
                int fileHits = 0;

                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i];

                    // 找方法声明
                    var m = Regex.Match(ln, @"\b(void|IEnumerator)\s+(Update|LateUpdate|FixedUpdate)\s*\(");
                    if (!m.Success) continue;

                    // 从声明行开始跟踪花括号深度
                    int depth = 0;
                    bool started = false;
                    int j = i;

                    while (j < lines.Length)
                    {
                        string cur = lines[j];

                        foreach (char ch in cur)
                        {
                            if (ch == '{') { depth++; started = true; }
                            else if (ch == '}') depth--;
                        }

                        // 在方法体内检查危险调用
                        if (started && j >= i)
                        {
                            string trimmed = cur.Trim();
                            // 跳过注释行
                            if (!trimmed.StartsWith("//"))
                            {
                                foreach (var d in danger)
                                {
                                    if (cur.Contains(d))
                                    {
                                        // Camera.main 例外：InteractionDetector 在 Start 里缓存了
                                        sb.AppendLine($"  {name}:{j + 1}  [{d}]");
                                        sb.AppendLine($"      {trimmed}");
                                        fileHits++;
                                        totalHits++;
                                        break;
                                    }
                                }
                            }
                        }

                        if (started && depth <= 0) break;
                        j++;
                    }

                    i = j;
                }

                if (fileHits > 0) perFile[name] = fileHits;
            }

            sb.AppendLine();
            sb.AppendLine($"合计命中：{totalHits}");
            sb.AppendLine();
            sb.AppendLine("按文件：");
            foreach (var kv in perFile)
                sb.AppendLine($"  {kv.Key}: {kv.Value} 处");

            string text = sb.ToString();
            Debug.Log(text);

            string dir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "../Reports"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "UpdateScan.txt"), text, new UTF8Encoding(false));
        }
    }
}