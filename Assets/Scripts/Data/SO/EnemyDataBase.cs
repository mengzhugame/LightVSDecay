// ============================================================
// EnemyDatabase.cs
// 文件位置: Assets/Scripts/Data/SO/EnemyDatabase.cs
// 用途：管理所有敌人配置的数据库（ScriptableObject）
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Data.SO
{
    /// <summary>
    /// 敌人数据库 (ScriptableObject)
    /// 统一管理所有敌人类型的配置
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDatabase", menuName = "LightVsDecay/Enemy Database", order = 0)]
    public class EnemyDatabase : ScriptableObject
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("敌人配置列表")]
        [Tooltip("所有敌人类型的配置")]
        [SerializeField] private List<EnemyData> enemies = new List<EnemyData>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓存（运行时自动构建）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<EnemyType, EnemyData> _cache;
        
        private Dictionary<EnemyType, EnemyData> Cache
        {
            get
            {
                if (_cache == null)
                {
                    BuildCache();
                }
                return _cache;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 获取指定类型的敌人配置
        /// </summary>
        public EnemyData GetData(EnemyType type)
        {
            if (Cache.TryGetValue(type, out var data))
            {
                return data;
            }
            
            Debug.LogWarning($"[EnemyDatabase] 未找到 {type} 类型的配置！");
            return null;
        }
        
        /// <summary>
        /// 尝试获取指定类型的敌人配置
        /// </summary>
        public bool TryGetData(EnemyType type, out EnemyData data)
        {
            return Cache.TryGetValue(type, out data);
        }
        
        /// <summary>
        /// 获取所有敌人配置
        /// </summary>
        public IReadOnlyList<EnemyData> GetAllData() => enemies;
        
        /// <summary>
        /// 检查是否包含指定类型
        /// </summary>
        public bool Contains(EnemyType type) => Cache.ContainsKey(type);
        
        /// <summary>
        /// 获取敌人数量
        /// </summary>
        public int Count => enemies.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void BuildCache()
        {
            _cache = new Dictionary<EnemyType, EnemyData>();
            
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                
                if (_cache.ContainsKey(enemy.type))
                {
                    Debug.LogWarning($"[EnemyDatabase] 重复的敌人类型: {enemy.type}，将使用第一个配置");
                    continue;
                }
                
                _cache[enemy.type] = enemy;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnValidate()
        {
            // 编辑器中修改时清除缓存，下次访问重建
            _cache = null;
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// 编辑器：验证数据完整性
        /// </summary>
        [ContextMenu("验证数据")]
        public void ValidateData()
        {
            Debug.Log("=== 敌人数据库验证 ===");
            
            HashSet<EnemyType> types = new HashSet<EnemyType>();
            int errorCount = 0;
            
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                
                if (enemy == null)
                {
                    Debug.LogError($"[{i}] 空引用！");
                    errorCount++;
                    continue;
                }
                
                if (types.Contains(enemy.type))
                {
                    Debug.LogWarning($"[{i}] {enemy.name}: 类型 {enemy.type} 重复！");
                    errorCount++;
                }
                else
                {
                    types.Add(enemy.type);
                }
                
                // 验证数值合理性
                if (enemy.maxHealth <= 0)
                {
                    Debug.LogError($"[{i}] {enemy.name}: 生命值必须大于0！");
                    errorCount++;
                }
                
                if (enemy.moveSpeed <= 0)
                {
                    Debug.LogError($"[{i}] {enemy.name}: 移动速度必须大于0！");
                    errorCount++;
                }
                
                Debug.Log($"[{i}] {enemy.type}: HP={enemy.maxHealth}, Speed={enemy.moveSpeed}, Mass={enemy.mass}");
            }
            
            Debug.Log($"=== 验证完成: {enemies.Count} 条记录, {errorCount} 个错误 ===");
        }
#endif
    }
}