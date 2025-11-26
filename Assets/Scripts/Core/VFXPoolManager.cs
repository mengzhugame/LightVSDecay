using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// VFX类型枚举
    /// </summary>
    public enum VFXType
    {
        // 敌人相关
        EnemySteam,         // 敌人死亡蒸发
        EnemyExplosion,     // 敌人撞塔爆炸
        
        // 激光相关
        LaserHit,           // 激光击中特效
        
        // 塔相关
        ShieldBreak,        // 护盾破碎
        ShieldRecover,      // 护盾恢复
        TowerDamage,        // 塔受伤
        
        // 技能相关（低频，可选不入池）
        UltActivate,        // 大招激活
        UltSweep,           // 大招扫射
    }
    
    /// <summary>
    /// VFX池配置
    /// </summary>
    [System.Serializable]
    public class VFXPoolConfig
    {
        public VFXType type;
        public GameObject prefab;
        
        [Tooltip("预热数量")]
        public int prewarmCount = 10;
        
        [Tooltip("最大数量")]
        public int maxCount = 50;
        
        [Tooltip("是否使用对象池（false则使用普通Instantiate）")]
        public bool usePool = true;
    }
    
    /// <summary>
    /// VFX对象池管理器
    /// 单例模式，统一管理所有特效
    /// </summary>
    public class VFXPoolManager : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public static VFXPoolManager Instance { get; private set; }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("VFX配置")]
        [SerializeField] private List<VFXPoolConfig> vfxConfigs = new List<VFXPoolConfig>();
        
        [Header("非池化VFX自动销毁时间")]
        [SerializeField] private float nonPooledDestroyDelay = 3f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<VFXType, VFXPool> pools;
        private Dictionary<VFXType, VFXPoolConfig> configMap;
        private Transform poolContainer;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 单例设置
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            CreatePoolContainer();
            InitializePools();
        }
        
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            ClearAllPools();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreatePoolContainer()
        {
            GameObject containerGO = new GameObject("[VFXPool]");
            containerGO.transform.SetParent(transform);
            poolContainer = containerGO.transform;
        }
        
        private void InitializePools()
        {
            pools = new Dictionary<VFXType, VFXPool>();
            configMap = new Dictionary<VFXType, VFXPoolConfig>();
            
            foreach (var config in vfxConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogWarning($"[VFXPoolManager] {config.type} 的预制体未设置");
                    continue;
                }
                
                configMap[config.type] = config;
                
                // 只为启用池化的VFX创建对象池
                if (config.usePool)
                {
                    GameObject typeContainer = new GameObject($"VFX_{config.type}");
                    typeContainer.transform.SetParent(poolContainer);
                    
                    var pool = new VFXPool(
                        config.prefab,
                        typeContainer.transform,
                        config.prewarmCount,
                        config.maxCount
                    );
                    
                    pools[config.type] = pool;
                    
                    Debug.Log($"[VFXPoolManager] {config.type} 池初始化: 预热{config.prewarmCount}, 上限{config.maxCount}");
                }
                else
                {
                    Debug.Log($"[VFXPoolManager] {config.type} 使用普通Instantiate（非池化）");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放VFX
        /// </summary>
        /// <param name="type">VFX类型</param>
        /// <param name="position">播放位置</param>
        /// <param name="rotation">旋转（可选）</param>
        /// <returns>VFX GameObject（可能为null）</returns>
        public GameObject Play(VFXType type, Vector3 position, Quaternion rotation = default)
        {
            if (!configMap.TryGetValue(type, out var config))
            {
                Debug.LogWarning($"[VFXPoolManager] 未配置的VFX类型: {type}");
                return null;
            }
            
            if (config.prefab == null)
            {
                return null;
            }
            
            if (rotation == default)
            {
                rotation = Quaternion.identity;
            }
            
            // 使用对象池
            if (config.usePool && pools.TryGetValue(type, out var pool))
            {
                var vfx = pool.Play(position, rotation);
                return vfx != null ? vfx.gameObject : null;
            }
            // 普通Instantiate（低频VFX）
            else
            {
                GameObject go = Instantiate(config.prefab, position, rotation);
                Destroy(go, nonPooledDestroyDelay);
                return go;
            }
        }
        
        /// <summary>
        /// 播放VFX（简化版）
        /// </summary>
        public GameObject Play(VFXType type, Vector3 position)
        {
            return Play(type, position, Quaternion.identity);
        }
        
        /// <summary>
        /// 回收所有VFX
        /// </summary>
        public void ReturnAll()
        {
            foreach (var pool in pools.Values)
            {
                pool.ReturnAll();
            }
        }
        
        /// <summary>
        /// 清空所有池
        /// </summary>
        public void ClearAllPools()
        {
            if (pools == null) return;
            
            foreach (var pool in pools.Values)
            {
                pool.Clear();
            }
            pools.Clear();
        }
        
        /// <summary>
        /// 获取指定类型的统计信息
        /// </summary>
        public (int active, int available, int total) GetPoolStats(VFXType type)
        {
            if (pools.TryGetValue(type, out var pool))
            {
                return (pool.ActiveCount, pool.AvailableCount, pool.TotalCreated);
            }
            return (0, 0, 0);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 便捷方法（常用VFX快捷调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放敌人死亡蒸发特效
        /// </summary>
        public void PlayEnemySteam(Vector3 position)
        {
            Play(VFXType.EnemySteam, position);
        }
        
        /// <summary>
        /// 播放敌人撞塔爆炸特效
        /// </summary>
        public void PlayEnemyExplosion(Vector3 position)
        {
            Play(VFXType.EnemyExplosion, position);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 260, 10, 250, 300));
            GUILayout.Label("=== VFX Pool Debug ===");
            
            foreach (var kvp in pools)
            {
                var stats = GetPoolStats(kvp.Key);
                GUILayout.Label($"{kvp.Key}: A={stats.active}, V={stats.available}, T={stats.total}");
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}