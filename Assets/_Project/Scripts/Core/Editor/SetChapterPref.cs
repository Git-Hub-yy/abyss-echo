using UnityEditor;
using UnityEngine;
namespace AbyssEcho.Core.EditorTools {
  public static class SetChapterPref {
    public static void Set3() { PlayerPrefs.SetInt("game.currentChapter", 3); PlayerPrefs.Save(); }
    public static void Set4() { PlayerPrefs.SetInt("game.currentChapter", 4); PlayerPrefs.Save(); }
    public static void Set5() { PlayerPrefs.SetInt("game.currentChapter", 5); PlayerPrefs.Save(); }
  }
}