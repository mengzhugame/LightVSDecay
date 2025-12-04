// ============================================================
// PlayerDataResetTool.cs
// 文件位置: Assets/Scripts/Editor/PlayerDataResetTool.cs
// 用途：编辑器菜单工具，用于重置玩家数据
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace LightVsDecay.Editor
{
    public static class PlayerDataResetTool
    {
        /// <summary>
        /// 重置能量到满
        /// </summary>
        [MenuItem("光与朽/调试/恢复满能量")]
        public static void RefillEnergy()
        {
            PlayerPrefs.SetInt("PlayerEnergy", 5);
            PlayerPrefs.Save();
            Debug.Log("[调试工具] 能量已恢复到 5/5");
        }
        
        /// <summary>
        /// 重置所有玩家数据
        /// </summary>
        [MenuItem("光与朽/调试/重置所有数据")]
        public static void ResetAllPlayerData()
        {
            PlayerPrefs.SetInt("PlayerGems", 0);
            PlayerPrefs.SetInt("PlayerGoldCoins", 100);
            PlayerPrefs.SetInt("PlayerEnergy", 5);
            PlayerPrefs.SetInt("CurrentChapter", 1);
            PlayerPrefs.SetInt("CurrentDifficulty", 1);
            PlayerPrefs.Save();
            Debug.Log("[调试工具] 所有玩家数据已重置");
        }
        
        /// <summary>
        /// 给予测试资源
        /// </summary>
        [MenuItem("光与朽/调试/给予测试资源")]
        public static void GiveTestResources()
        {
            PlayerPrefs.SetInt("PlayerGems", 999);
            PlayerPrefs.SetInt("PlayerGoldCoins", 9999);
            PlayerPrefs.SetInt("PlayerEnergy", 5);
            PlayerPrefs.Save();
            Debug.Log("[调试工具] 已给予测试资源: 999宝石, 9999金币, 5能量");
        }
        
        /// <summary>
        /// 清除所有PlayerPrefs
        /// </summary>
        [MenuItem("光与朽/调试/清除所有PlayerPrefs")]
        public static void ClearAllPlayerPrefs()
        {
            if (EditorUtility.DisplayDialog("确认清除", 
                "确定要清除所有PlayerPrefs数据吗？这将重置所有游戏进度！", 
                "确定", "取消"))
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.Log("[调试工具] 所有PlayerPrefs已清除");
            }
        }
        
        /// <summary>
        /// 显示当前玩家数据
        /// </summary>
        [MenuItem("光与朽/调试/显示当前数据")]
        public static void ShowCurrentData()
        {
            int gems = PlayerPrefs.GetInt("PlayerGems", 0);
            int goldCoins = PlayerPrefs.GetInt("PlayerGoldCoins", 0);
            int energy = PlayerPrefs.GetInt("PlayerEnergy", 5);
            int chapter = PlayerPrefs.GetInt("CurrentChapter", 1);
            int difficulty = PlayerPrefs.GetInt("CurrentDifficulty", 1);
            
            Debug.Log($"[玩家数据]\n" +
                      $"  宝石: {gems}\n" +
                      $"  金币: {goldCoins}\n" +
                      $"  能量: {energy}/5\n" +
                      $"  章节: {chapter}\n" +
                      $"  难度: {difficulty}");
        }
    }
}
#endif