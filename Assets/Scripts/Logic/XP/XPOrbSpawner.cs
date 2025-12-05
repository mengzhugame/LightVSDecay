// ============================================================
// XPOrbSpawner.cs
// 文件位置: Assets/Scripts/Logic/XP/XPOrbSpawner.cs
// 用途：管理经验光点的生成和对象池
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.XP
{
    /// <summary>
    /// 经验光点生成器（单例）
    /// 监听敌人死亡事件，生成对应的经验光点
    /// </summary>
    public class XPOrbSpawner : Singleton<XPOrbSpawner>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("预制体")]
        [Tooltip("经验光点预制体")]
        [SerializeField] private XPOrb orbPrefab;
        
        [Header("对象池设置")]
        [Tooltip("预热数量")]
        [SerializeField] private int prewarmCount = 20;
        
        [Tooltip("最大数量")]
        [SerializeField] private int maxCount = 100;
        
        [Header("生成设置")]
        [Tooltip("精英怪（Tank）掉落光点数量")]
        [SerializeField] private int eliteOrbCount = 5;
        
        [Tooltip("普通怪掉落光点数量")]
        [SerializeField] private int normalOrbCount = 1;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Queue<XPOrb> availablePool;
        private HashSet<XPOrb> activeOrbs;
        private Transform poolContainer;
        private int totalCreated;
        
        // 目标位置获取器（由 HUDPanel 设置）
        private System.Func<Vector3> targetPositionGetter;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            CreatePoolContainer();
            InitializePool();
        }
        
        private void OnEnable()
        {
            GameEvents.OnEnemyDied += OnEnemyDied;
        }
        
        private void OnDisable()
        {
            GameEvents.OnEnemyDied -= OnEnemyDied;
        }
        
        protected override void OnSingletonDestroy()
        {
            ClearPool();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void CreatePoolContainer()
        {
            GameObject container = new GameObject("[XPOrbPool]");
            container.transform.SetParent(transform);
            poolContainer = container.transform;
        }
        
        private void InitializePool()
        {
            availablePool = new Queue<XPOrb>(prewarmCount);
            activeOrbs = new HashSet<XPOrb>();
            totalCreated = 0;
            
            // 预热
            for (int i = 0; i < prewarmCount; i++)
            {
                XPOrb orb = CreateOrb();
                if (orb != null)
                {
                    orb.gameObject.SetActive(false);
                    availablePool.Enqueue(orb);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[XPOrbSpawner] 对象池初始化完成: 预热 {prewarmCount}");
            }
        }
        
        private XPOrb CreateOrb()
        {
            if (orbPrefab == null)
            {
                Debug.LogError("[XPOrbSpawner] orbPrefab 未设置！");
                return null;
            }
            
            if (totalCreated >= maxCount)
            {
                return null;
            }
            
            XPOrb orb = Instantiate(orbPrefab, poolContainer);
            orb.name = $"XPOrb_{totalCreated:D3}";
            totalCreated++;
            
            return orb;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置目标位置获取器（由 HUDPanel 调用）
        /// </summary>
        public void SetTargetPositionGetter(System.Func<Vector3> getter)
        {
            targetPositionGetter = getter;
            
            if (showDebugInfo)
            {
                Debug.Log("[XPOrbSpawner] 目标位置获取器已设置");
            }
        }
        
        /// <summary>
        /// 在指定位置生成光点
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="xpValue">经验值</param>
        /// <param name="count">生成数量</param>
        public void SpawnOrbs(Vector3 position, int xpValue, int count = 1)
        {
            if (count <= 0 || xpValue <= 0) return;
            
            // 如果生成多个，平分经验值
            int xpPerOrb = Mathf.Max(1, xpValue / count);
            int remainder = xpValue % count;
            
            for (int i = 0; i < count; i++)
            {
                XPOrb orb = GetOrb();
                if (orb == null)
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning("[XPOrbSpawner] 对象池已满，无法生成更多光点");
                    }
                    break;
                }
                
                // 最后一个光点获得余数经验
                int orbXP = (i == count - 1) ? xpPerOrb + remainder : xpPerOrb;
                
                orb.transform.position = position;
                orb.gameObject.SetActive(true);
                orb.Initialize(orbXP, targetPositionGetter, ReturnOrb);
                activeOrbs.Add(orb);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[XPOrbSpawner] 生成 {count} 个光点 @ {position}, 总XP: {xpValue}");
            }
        }
        
        /// <summary>
        /// 回收所有活跃光点
        /// </summary>
        public void ReturnAllOrbs()
        {
            var orbsList = new List<XPOrb>(activeOrbs);
            foreach (var orb in orbsList)
            {
                ReturnOrb(orb);
            }
        }
        
        /// <summary>
        /// 清空对象池
        /// </summary>
        public void ClearPool()
        {
            foreach (var orb in activeOrbs)
            {
                if (orb != null)
                {
                    Destroy(orb.gameObject);
                }
            }
            activeOrbs.Clear();
            
            while (availablePool.Count > 0)
            {
                var orb = availablePool.Dequeue();
                if (orb != null)
                {
                    Destroy(orb.gameObject);
                }
            }
            
            totalCreated = 0;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnEnemyDied(EnemyType type, Vector3 pos, int xp, int coin)
        {
            if (xp <= 0) return;
            
            // 根据敌人类型决定光点数量
            int orbCount = (type == EnemyType.Tank) ? eliteOrbCount : normalOrbCount;
            
            SpawnOrbs(pos, xp, orbCount);
            
            if (showDebugInfo)
            {
                Debug.Log($"[XPOrbSpawner] 敌人 {type} 死亡，生成 {orbCount} 个光点");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private XPOrb GetOrb()
        {
            XPOrb orb = null;
            
            // 尝试从池中获取
            if (availablePool.Count > 0)
            {
                orb = availablePool.Dequeue();
                orb.ResetOrb();
            }
            // 池空时动态创建
            else if (totalCreated < maxCount)
            {
                orb = CreateOrb();
            }
            
            return orb;
        }
        
        private void ReturnOrb(XPOrb orb)
        {
            if (orb == null) return;
            
            if (!activeOrbs.Contains(orb)) return;
            
            orb.gameObject.SetActive(false);
            orb.transform.SetParent(poolContainer);
            
            activeOrbs.Remove(orb);
            availablePool.Enqueue(orb);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(10, 500, 200, 80));
            GUILayout.Label("=== XPOrbSpawner ===");
            GUILayout.Label($"Active: {activeOrbs.Count}");
            GUILayout.Label($"Available: {availablePool.Count}");
            GUILayout.Label($"Total Created: {totalCreated}");
            GUILayout.EndArea();
        }
#endif
    }
}