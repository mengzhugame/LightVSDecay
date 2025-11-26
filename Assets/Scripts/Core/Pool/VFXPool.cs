using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// VFX对象池
    /// 专为粒子特效设计，支持播放完毕自动回收
    /// </summary>
    public class VFXPool
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private readonly GameObject prefab;
        private readonly Transform container;
        private readonly int maxSize;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private readonly Queue<PoolableVFX> availablePool;
        private readonly HashSet<PoolableVFX> activeInstances;
        private int totalCreated;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int AvailableCount => availablePool.Count;
        public int ActiveCount => activeInstances.Count;
        public int TotalCreated => totalCreated;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 构造函数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public VFXPool(GameObject prefab, Transform container, int prewarmCount, int maxSize)
        {
            this.prefab = prefab;
            this.container = container;
            this.maxSize = maxSize > 0 ? maxSize : 100;
            
            availablePool = new Queue<PoolableVFX>(prewarmCount);
            activeInstances = new HashSet<PoolableVFX>();
            totalCreated = 0;
            
            Prewarm(prewarmCount);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 预热
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && totalCreated < maxSize; i++)
            {
                PoolableVFX instance = CreateInstance();
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    availablePool.Enqueue(instance);
                }
            }
        }
        
        /// <summary>
        /// 播放VFX（从池中获取并播放）
        /// </summary>
        public PoolableVFX Play(Vector3 position, Quaternion rotation = default)
        {
            PoolableVFX instance = null;
            
            // 尝试从池中获取
            if (availablePool.Count > 0)
            {
                instance = availablePool.Dequeue();
            }
            // 池空时动态创建
            else if (totalCreated < maxSize)
            {
                instance = CreateInstance();
            }
            
            if (instance == null)
            {
                Debug.LogWarning($"[VFXPool] {prefab.name} 池已满！");
                return null;
            }
            
            // 设置位置并播放
            instance.transform.SetPositionAndRotation(position, rotation == default ? Quaternion.identity : rotation);
            instance.gameObject.SetActive(true);
            instance.Play(this);
            
            activeInstances.Add(instance);
            
            return instance;
        }
        
        /// <summary>
        /// 播放VFX（简化版）
        /// </summary>
        public PoolableVFX Play(Vector3 position)
        {
            return Play(position, Quaternion.identity);
        }
        
        /// <summary>
        /// 回收VFX
        /// </summary>
        public void Return(PoolableVFX instance)
        {
            if (instance == null) return;
            
            if (!activeInstances.Contains(instance))
            {
                return;
            }
            
            instance.Stop();
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(container);
            
            activeInstances.Remove(instance);
            availablePool.Enqueue(instance);
        }
        
        /// <summary>
        /// 回收所有活跃VFX
        /// </summary>
        public void ReturnAll()
        {
            var activeList = new List<PoolableVFX>(activeInstances);
            foreach (var instance in activeList)
            {
                Return(instance);
            }
        }
        
        /// <summary>
        /// 清空池
        /// </summary>
        public void Clear()
        {
            foreach (var instance in activeInstances)
            {
                if (instance != null)
                    Object.Destroy(instance.gameObject);
            }
            activeInstances.Clear();
            
            while (availablePool.Count > 0)
            {
                var instance = availablePool.Dequeue();
                if (instance != null)
                    Object.Destroy(instance.gameObject);
            }
            
            totalCreated = 0;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private PoolableVFX CreateInstance()
        {
            if (totalCreated >= maxSize)
                return null;
            
            GameObject go = Object.Instantiate(prefab, container);
            go.name = $"{prefab.name}_{totalCreated:D3}";
            
            // 确保有PoolableVFX组件
            PoolableVFX vfx = go.GetComponent<PoolableVFX>();
            if (vfx == null)
            {
                vfx = go.AddComponent<PoolableVFX>();
            }
            
            totalCreated++;
            return vfx;
        }
    }
}