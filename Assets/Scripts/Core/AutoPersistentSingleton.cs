// ============================================================
// AutoPersistentSingleton.cs
// 文件位置: Assets/Scripts/Core/AutoPersistentSingleton.cs
// 用途：自动创建的持久化单例基类
// ============================================================

using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 自动持久化单例基类
    /// 特性：
    /// 1. 游戏启动时自动从 Resources 文件夹创建（无需手动放置到场景）
    /// 2. 跨场景持久化（DontDestroyOnLoad）
    /// 3. 支持预制体配置（保留 Inspector 设置）
    /// 
    /// 使用方法：
    /// 1. 继承此类：public class MyManager : AutoPersistentSingleton&lt;MyManager&gt;
    /// 2. 创建预制体并放入 Resources 文件夹，命名与类名相同（如 MyManager.prefab）
    /// 3. 在预制体上配置所需的引用和参数
    /// 4. 代码中直接使用 MyManager.Instance 访问
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    public abstract class AutoPersistentSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例实例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting = false;
        private static bool _isInitialized = false;
        
        /// <summary>
        /// 单例实例访问器
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
                    Debug.LogWarning($"[AutoPersistentSingleton] {typeof(T).Name} 实例在退出时被请求，返回 null");
                    return null;
                }
                
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 尝试在场景中查找
                        _instance = FindObjectOfType<T>();
                        
                        if (_instance == null)
                        {
                            // 自动创建
                            CreateInstance();
                        }
                    }
                    
                    return _instance;
                }
            }
        }
        
        /// <summary>
        /// 是否存在实例
        /// </summary>
        public static bool HasInstance => _instance != null;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 自动初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 游戏启动时自动初始化（在任何场景加载前）
        /// 子类需要调用此方法来启用自动初始化
        /// </summary>
        protected static void AutoInitialize()
        {
            if (_isInitialized || _instance != null) return;
            
            _isInitialized = true;
            CreateInstance();
        }
        
        /// <summary>
        /// 创建单例实例
        /// </summary>
        private static void CreateInstance()
        {
            string typeName = typeof(T).Name;
            
            // 优先从 Resources 加载预制体
            GameObject prefab = Resources.Load<GameObject>("Prefab/"+typeName);
            
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab);
                instance.name = typeName;
                _instance = instance.GetComponent<T>();
                
                if (_instance == null)
                {
                    Debug.LogError($"[AutoPersistentSingleton] 预制体 Resources/{typeName} 上没有 {typeName} 组件！");
                    _instance = instance.AddComponent<T>();
                }
                
                Debug.Log($"[AutoPersistentSingleton] {typeName} 已从预制体创建");
            }
            else
            {
                // 没有预制体，创建空对象
                GameObject instance = new GameObject(typeName);
                _instance = instance.AddComponent<T>();
                
                Debug.LogWarning($"[AutoPersistentSingleton] 未找到 Resources/{typeName} 预制体，已创建空实例。请确保配置正确！");
            }
            
            // 跨场景持久化
            if (_instance != null)
            {
                DontDestroyOnLoad(_instance.gameObject);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected virtual void Awake()
        {
            // 处理重复实例
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[AutoPersistentSingleton] {typeof(T).Name} 已存在，销毁重复实例: {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
            
            // 调用子类初始化
            OnSingletonAwake();
        }
        
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                OnSingletonDestroy();
                _instance = null;
            }
        }
        
        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 子类扩展点
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 单例初始化时调用（替代 Awake）
        /// </summary>
        protected virtual void OnSingletonAwake() { }
        
        /// <summary>
        /// 单例销毁时调用（替代 OnDestroy）
        /// </summary>
        protected virtual void OnSingletonDestroy() { }
    }
}