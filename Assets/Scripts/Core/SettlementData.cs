// ============================================================
// SettlementData.cs
// 文件位置: Assets/Scripts/Core/SettlementData.cs
// ============================================================

using UnityEngine;

namespace LightVsDecay.Core
{
    [System.Serializable]
    public struct SettlementData
    {
        public bool  isVictory;
        public bool  isPerfect;
        public int   coinsEarned;
        public float survivalTime;
        public int   killCount;
        public int   maxHitCount;
        public int   totalCoins;
        public int   totalKills;
        public int   maxCombo;
        public int   finalLevel;

        /// <summary>本局累计总伤害（所有波次之和）</summary>
        public long  totalDamage;

        /// <summary>玩家通过的波次（失败时=死亡前最后完成的波）</summary>
        public int   wavesCleared;

        /// <summary>本章节总波次（显示分母，如 8）</summary>
        public int   totalWaves;

        /// <summary>波次进度字符串 "5/8" 或 "8/8"</summary>
        public string WavesText => $"{wavesCleared}/{(totalWaves > 0 ? totalWaves : 12)}";

        /// <summary>总伤害千分位格式，如 "12,450,800"</summary>
        public string TotalDamageFormatted => totalDamage.ToString("N0");

        public string SurvivalTimeFormatted =>
            $"{Mathf.FloorToInt(survivalTime / 60f):D1}:{Mathf.FloorToInt(survivalTime % 60f):D2}";
    }
}