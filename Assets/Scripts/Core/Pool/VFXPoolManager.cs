using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// VFX类型枚举
    /// </summary>
    public enum VFXType
    {
        // ━━━ 高频特效（使用对象池） ━━━
        // 敌人相关
        EnemySteam,         // 敌人死亡蒸发
        EnemyExplosion,     // 敌人撞塔爆炸
        // 激光相关
        LaserHit,           // 激光击中特效
        FrostHit,           // 极寒光束命中特效
        ShatterExplosion, // 数据破碎 - 漏洞扩散爆炸（冰块碎裂效果）
        // ━━━ 低频特效（直接实例化） ━━━
        // 护盾相关
        ShieldHit,//护盾受击
        ShieldBreak,        // 护盾破碎（低频）
        ShieldRecover,      // 护盾恢复（低频）
        TowerDamage,        // 塔受伤
        // Boss相关
        BossSpawn,          // Boss出场
        BossSkill,          // Boss技能
        BossDeath,          // Boss死亡
        BossPhaseChange,    // Boss阶段转换
        // 其他低频特效
        LevelUp,            // 升级特效
        // 无人机相关（低频）
        DroneExplosion,         // 无人机爆炸
    }
    
    /// <summary>
    /// VFX池配置
    /// </summary>
    [System.Serializable]
    public class VFXPoolConfig
    {
        public VFXType type;
        public GameObject prefab;
    
        [Header("对象池设置")]
        [Tooltip("是否使用对象池（false则直接Instantiate）")]
        public bool usePool = true;
    
        [Tooltip("预热数量（仅池化有效）")]
        public int prewarmCount = 10;
    
        [Tooltip("最大数量（仅池化有效）")]
        public int maxCount = 50;
    
        [Header("非池化设置")]
        [Tooltip("自定义销毁延迟（<=0则自动检测粒子时长）")]
        public float customDestroyDelay = -1f;
    }
    
    /// <summary>
    /// VFX对象池管理器
    /// 单例模式，统一管理所有特效
    /// </summary>
    public class VFXPoolManager : PersistentSingleton<VFXPoolManager>
    {
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
        
        protected override void OnSingletonAwake()
        {
            CreatePoolContainer();
            InitializePools();
        }
        
        protected override void OnSingletonDestroy()
        {
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
                    if (showDebugInfo)
                        Debug.Log($"[VFXPoolManager] {config.type} 池初始化: 预热{config.prewarmCount}, 上限{config.maxCount}");
                }
                else
                {
                    if (showDebugInfo)
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
    
                // 确定销毁时间
                float destroyDelay;
                if (config.customDestroyDelay > 0f)
                {
                    // 使用自定义时间
                    destroyDelay = config.customDestroyDelay;
                }
                else
                {
                    // 自动检测粒子系统时长
                    destroyDelay = CalculateParticleDuration(go);
                }
    
                Destroy(go, destroyDelay);
    
                if (showDebugInfo)
                {
                    Debug.Log($"[VFXPoolManager] 播放非池化VFX: {config.type}, 将在 {destroyDelay:F2}s 后销毁");
                }
    
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
        /// <summary>
        /// 播放激光击中特效
        /// </summary>
        public void PlayLaserHit(Vector3 position)
        {
            Play(VFXType.LaserHit, position);
        }
        /// <summary>
        /// 播放极寒光束命中特效
        /// </summary>
        public void PlayFrostHit(Vector3 position)
        {
            Play(VFXType.FrostHit, position);
        }
        /// <summary>
        /// 播放护盾破碎特效
        /// </summary>
        public void PlayShieldBreak(Vector3 position)
        {
            Play(VFXType.ShieldBreak, position);
        }
        /// <summary>
        /// 播放护盾受击特效
        /// </summary>
        public void PlayShieldHit(Vector3 position)
        {
            Play(VFXType.ShieldHit, position);
        }
        /// <summary>
        /// 播放护盾恢复特效
        /// </summary>
        public void PlayShieldRecover(Vector3 position)
        {
            Play(VFXType.ShieldRecover, position);
        }
        
        /// <summary>
        /// 播放塔受伤特效
        /// </summary>
        public void PlayTowerDamage(Vector3 position)
        {
            Play(VFXType.TowerDamage, position);
        } 
        /// <summary>
        /// 播放Boss出场特效
        /// </summary>
        public void PlayBossSpawn(Vector3 position)
        {
            Play(VFXType.BossSpawn, position);
        }

        /// <summary>
        /// 播放Boss技能特效
        /// </summary>
        public void PlayBossSkill(Vector3 position, Quaternion rotation = default)
        {
            Play(VFXType.BossSkill, position, rotation);
        }

        /// <summary>
        /// 播放Boss死亡特效
        /// </summary>
        public void PlayBossDeath(Vector3 position)
        {
            Play(VFXType.BossDeath, position);
        }

        /// <summary>
        /// 播放Boss阶段转换特效
        /// </summary>
        public void PlayBossPhaseChange(Vector3 position)
        {
            Play(VFXType.BossPhaseChange, position);
        }

        /// <summary>
        /// 播放升级特效
        /// </summary>
        public void PlayLevelUp(Vector3 position)
        {
            Play(VFXType.LevelUp, position);
        }
        /// <summary>
        /// 播放无人机爆炸特效
        /// </summary>
        public void PlayDroneExplosion(Vector3 position)
        {
            Play(VFXType.DroneExplosion, position);
        }
        /// <summary>
        /// 播放数据破碎爆炸特效（漏洞扩散 - 冰块碎裂效果）
        /// </summary>
        public void PlayShatterExplosion(Vector3 position)
        {
            Play(VFXType.ShatterExplosion, position);
        }
        /// <summary>
        /// 计算粒子系统总时长（用于非池化VFX）
        /// </summary>
        private float CalculateParticleDuration(GameObject vfxObject)
        {
            ParticleSystem[] particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
    
            if (particleSystems.Length == 0)
            {
                return nonPooledDestroyDelay;
            }
    
            float maxDuration = 0f;
    
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
        
                var main = ps.main;
        
                // 跳过循环粒子
                if (main.loop) continue;
        
                float totalTime = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
        
                if (totalTime > maxDuration)
                {
                    maxDuration = totalTime;
                }
            }
    
            // 添加0.5秒缓冲
            return maxDuration > 0f ? maxDuration + 0.5f : nonPooledDestroyDelay;
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