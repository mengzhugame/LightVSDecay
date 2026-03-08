// ============================================================
// TechTreeSaveData.cs
// 文件位置: Assets/Scripts/Data/Runtime/TechTreeSaveData.cs
// 用途：科技树存档数据（PlayerPrefs + JSON）
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Data.Runtime
{
    /// <summary>科技树存档数据</summary>
    [System.Serializable]
    public class TechTreeSaveData
    {
        public const string SAVE_KEY = "TechTree_NodeLevels_v1";

        /// <summary>nodeId → currentLevel 映射</summary>
        public List<TechNodeLevelEntry> nodeLevels = new List<TechNodeLevelEntry>();

        // ─────────────────────────────────────────────────────

        /// <summary>保存到 PlayerPrefs</summary>
        public void Save()
        {
            string json = JsonUtility.ToJson(new Wrapper { entries = nodeLevels });
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        /// <summary>从 PlayerPrefs 加载（不存在则返回空数据）</summary>
        public static TechTreeSaveData Load()
        {
            var d = new TechTreeSaveData();
            string json = PlayerPrefs.GetString(SAVE_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var w = JsonUtility.FromJson<Wrapper>(json);
                    if (w?.entries != null) d.nodeLevels = w.entries;
                }
                catch { /* 解析失败忽略，返回空数据 */ }
            }
            return d;
        }

        /// <summary>清除存档</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            PlayerPrefs.Save();
        }

        [System.Serializable]
        private class Wrapper { public List<TechNodeLevelEntry> entries; }
    }

    /// <summary>节点等级条目</summary>
    [System.Serializable]
    public class TechNodeLevelEntry
    {
        public string nodeId;
        public int    level;

        public TechNodeLevelEntry() { }
        public TechNodeLevelEntry(string id, int lv) { nodeId = id; level = lv; }
    }
}