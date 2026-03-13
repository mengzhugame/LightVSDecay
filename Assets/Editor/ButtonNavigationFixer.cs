// ============================================================
// ButtonNavigationFixer.cs
// 文件位置: Assets/Editor/ButtonNavigationFixer.cs
// 用途：一键将场景内所有 Button 的 Navigation 设为 None
//       彻底消除"首次点击选中、二次点击触发"问题
// 使用：Unity 菜单 → Tools → Fix All Button Navigations
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public class ButtonNavigationFixer : EditorWindow
{
    [MenuItem("Tools/Fix All Button Navigations (Current Scene)")]
    public static void FixAllButtonNavigations()
    {
        Button[] allButtons = GameObject.FindObjectsOfType<Button>(true);
        int fixedCount = 0;

        foreach (Button btn in allButtons)
        {
            Navigation nav = btn.navigation;
            if (nav.mode != Navigation.Mode.None)
            {
                nav.mode       = Navigation.Mode.None;
                btn.navigation = nav;
                EditorUtility.SetDirty(btn);
                fixedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[ButtonNavigationFixer] 已修复 {fixedCount} 个 Button，Navigation → None");
        EditorUtility.DisplayDialog("完成",
            $"已将 {fixedCount} 个 Button 的 Navigation 设为 None", "OK");
    }
}
#endif