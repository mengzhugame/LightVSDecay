// ============================================================
// LaserPenetrationHandler.cs
// 文件位置: Assets/Scripts/Logic/Player/Laser/LaserPenetrationHandler.cs
// 用途：激光穿透系统 - 从 LaserController 拆分
// ============================================================

using UnityEngine;
using System.Collections.Generic;

namespace LightVsDecay.Logic.Player
{
    // 注意：PenetrationHitInfo 结构体定义在 LaserController.cs 中
    
    /// <summary>
    /// 激光穿透处理器
    /// 负责：穿透检测、穿透伤害衰减、穿透目标排序
    /// </summary>
    [System.Serializable]
    public class LaserPenetrationHandler
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Focus 穿透配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int penetrationCount = 0;        // 穿透数量（-1=无限）
        private float penetrationDecay = 0.1f;   // 穿透衰减率
        private bool trueDamageToBoss = false;   // Boss真实伤害
        
        private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 穿透检测缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private List<PenetrationHitInfo> penetrationHits = new List<PenetrationHitInfo>(16);
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>穿透数量（-1=无限）</summary>
        public int PenetrationCount => penetrationCount;
        
        /// <summary>穿透衰减率</summary>
        public float PenetrationDecay => penetrationDecay;
        
        /// <summary>是否对Boss造成真实伤害</summary>
        public bool TrueDamageToBoss => trueDamageToBoss;
        
        /// <summary>是否启用穿透</summary>
        public bool IsEnabled => penetrationCount != 0;
        
        /// <summary>是否无限穿透</summary>
        public bool IsInfinite => penetrationCount == -1;
        
        /// <summary>穿透命中列表（只读）</summary>
        public IReadOnlyList<PenetrationHitInfo> Hits => penetrationHits;
        
        /// <summary>穿透命中数量</summary>
        public int HitCount => penetrationHits.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置穿透参数（由 SkillEffectManager 调用）
        /// </summary>
        public void SetPenetrationParams(int count, float decay, bool trueDamage)
        {
            penetrationCount = count;
            penetrationDecay = decay;
            trueDamageToBoss = trueDamage;
            
            if (showDebugInfo)
            {
                string penetrationInfo = count == -1 ? "无限" : count.ToString();
                Debug.Log($"[LaserPenetrationHandler] 穿透设置: 数量={penetrationInfo}, 衰减={decay:P0}, Boss真伤={trueDamage}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 穿透处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 清空穿透命中列表
        /// </summary>
        public void ClearHits()
        {
            penetrationHits.Clear();
        }
        
        /// <summary>
        /// 添加穿透命中
        /// </summary>
        public void AddHit(Collider2D collider, float distance, Vector2 hitPoint)
        {
            penetrationHits.Add(new PenetrationHitInfo(collider, distance, hitPoint));
        }
        
        /// <summary>
        /// 按距离排序（近到远）
        /// </summary>
        public void SortByDistance()
        {
            penetrationHits.Sort((a, b) => a.distance.CompareTo(b.distance));
        }
        
        /// <summary>
        /// 获取最大穿透数量
        /// </summary>
        public int GetMaxPenetration()
        {
            if (penetrationCount == -1)
            {
                return penetrationHits.Count; // 无限穿透
            }
            return Mathf.Min(penetrationCount, penetrationHits.Count);
        }
        
        /// <summary>
        /// 计算穿透衰减后的伤害
        /// </summary>
        /// <param name="baseDamage">基础伤害</param>
        /// <param name="penetratedCount">已穿透数量</param>
        public float CalculateDecayedDamage(float baseDamage, int penetratedCount)
        {
            if (penetratedCount <= 0) return baseDamage;
            
            // 每穿透一个目标，伤害衰减 penetrationDecay
            float multiplier = Mathf.Pow(1f - penetrationDecay, penetratedCount);
            return baseDamage * multiplier;
        }
        
        /// <summary>
        /// 获取下一个目标的伤害（应用衰减）
        /// </summary>
        public float GetNextDamage(float currentDamage)
        {
            return currentDamage * (1f - penetrationDecay);
        }
        
        /// <summary>
        /// 获取指定索引的命中信息
        /// </summary>
        public PenetrationHitInfo GetHit(int index)
        {
            if (index >= 0 && index < penetrationHits.Count)
            {
                return penetrationHits[index];
            }
            return default;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 重置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重置穿透效果
        /// </summary>
        public void Reset()
        {
            penetrationCount = 0;
            penetrationDecay = 0.1f;
            trueDamageToBoss = false;
            penetrationHits.Clear();
        }
        
        /// <summary>
        /// 设置调试模式
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            showDebugInfo = enabled;
        }
    }
}
