using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// 通用单类型对象池
    /// 管理单一Prefab的实例化和回收
    /// </summary>
    public class ObjectPool<T> where T : Component, IPoolable
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private readonly T prefab;
        private readonly Transform container;
        private readonly int maxSize;
        private readonly bool allowExpand;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private readonly Queue<T> availablePool;      // 可用对象队列
        private readonly HashSet<T> activeInstances;  // 当前活跃的实例
        private int totalCreated;                     // 已创建总数
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前可用数量</summary>
        public int AvailableCount => availablePool.Count;
        
        /// <summary>当前活跃数量</summary>
        public int ActiveCount => activeInstances.Count;
        
        /// <summary>已创建总数</summary>
        public int TotalCreated => totalCreated;
        
        /// <summary>是否已达上限</summary>
        public bool IsAtCapacity => totalCreated >= maxSize;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 构造函数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 创建对象池
        /// </summary>
        /// <param name="prefab">预制体</param>
        /// <param name="container">容器Transform（用于组织层级）</param>
        /// <param name="initialSize">初始预热数量</param>
        /// <param name="maxSize">最大数量（0=无限制）</param>
        /// <param name="allowExpand">池空时是否允许动态创建</param>
        public ObjectPool(T prefab, Transform container, int initialSize, int maxSize = 0, bool allowExpand = true)
        {
            this.prefab = prefab;
            this.container = container;
            this.maxSize = maxSize > 0 ? maxSize : int.MaxValue;
            this.allowExpand = allowExpand;
            
            availablePool = new Queue<T>(initialSize);
            activeInstances = new HashSet<T>();
            totalCreated = 0;
            
            // 预热
            Prewarm(initialSize);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 核心方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 预热对象池（提前创建实例）
        /// </summary>
        public void Prewarm(int count)
        {
            for (int i = 0; i < count && totalCreated < maxSize; i++)
            {
                T instance = CreateInstance();
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    availablePool.Enqueue(instance);
                }
            }
            
            Debug.Log($"[ObjectPool] {prefab.name} 预热完成: {totalCreated} 个");
        }
        
        /// <summary>
        /// 从池中获取对象
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成旋转</param>
        /// <returns>对象实例，如果池已满且不允许扩展则返回null</returns>
        public T Get(Vector3 position, Quaternion rotation)
        {
            T instance = null;
            
            // 尝试从池中获取
            if (availablePool.Count > 0)
            {
                instance = availablePool.Dequeue();
            }
            // 池空时尝试动态创建
            else if (allowExpand && totalCreated < maxSize)
            {
                instance = CreateInstance();
            }
            
            // 获取失败
            if (instance == null)
            {
                Debug.LogWarning($"[ObjectPool] {prefab.name} 池已满！Active:{ActiveCount}, Max:{maxSize}");
                return null;
            }
            
            // 初始化并激活
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.gameObject.SetActive(true);
            instance.OnSpawn();
            
            activeInstances.Add(instance);
            
            return instance;
        }
        
        /// <summary>
        /// 回收对象到池中
        /// </summary>
        public void Return(T instance)
        {
            if (instance == null) return;
            
            // 确保是我们管理的实例
            if (!activeInstances.Contains(instance))
            {
                Debug.LogWarning($"[ObjectPool] 尝试回收不属于此池的对象: {instance.name}");
                return;
            }
            
            // 调用回收回调
            instance.OnDespawn();
            
            // 禁用并归还
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(container);
            
            activeInstances.Remove(instance);
            availablePool.Enqueue(instance);
        }
        
        /// <summary>
        /// 回收所有活跃对象
        /// </summary>
        public void ReturnAll()
        {
            // 复制列表避免迭代时修改
            var activeList = new List<T>(activeInstances);
            
            foreach (var instance in activeList)
            {
                Return(instance);
            }
            
            Debug.Log($"[ObjectPool] {prefab.name} 全部回收: {availablePool.Count} 个");
        }
        
        /// <summary>
        /// 清空对象池（销毁所有实例）
        /// </summary>
        public void Clear()
        {
            // 销毁活跃实例
            foreach (var instance in activeInstances)
            {
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
            activeInstances.Clear();
            
            // 销毁池中实例
            while (availablePool.Count > 0)
            {
                var instance = availablePool.Dequeue();
                if (instance != null)
                {
                    Object.Destroy(instance.gameObject);
                }
            }
            
            totalCreated = 0;
            Debug.Log($"[ObjectPool] {prefab.name} 已清空");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 创建新实例
        /// </summary>
        private T CreateInstance()
        {
            if (totalCreated >= maxSize)
            {
                return null;
            }
            
            GameObject go = Object.Instantiate(prefab.gameObject, container);
            go.name = $"{prefab.name}_{totalCreated:D3}";
            
            T instance = go.GetComponent<T>();
            totalCreated++;
            
            return instance;
        }
    }
}