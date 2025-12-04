// ============================================================
// Singleton.cs
// 文件位置: Assets/Scripts/Core/Singleton.cs
// 用途：通用单例基类，减少重复的单例代码
// ============================================================

using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 通用单例基类（场景内有效）
    /// 场景切换时会被销毁
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    /// <example>
    /// public class UIManager : Singleton&lt;UIManager&gt;
    /// {
    ///     protected override void Awake()
    ///     {
    ///         base.Awake();
    ///         // 你的初始化代码...
    ///     }
    /// }
    /// </example>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例实例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting = false;
        
        /// <summary>
        /// 单例实例访问器
        /// </summary>
        public static T Instance
        {
            get
            {
                // 应用退出时不再创建新实例
                if (_isQuitting)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T).Name} 实例在应用退出时被请求，返回 null");
                    return null;
                }
                
                lock (_lock)
                {
                    return _instance;
                }
            }
        }
        
        /// <summary>
        /// 检查实例是否存在（不会触发创建）
        /// </summary>
        public static bool HasInstance => _instance != null;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected virtual void Awake()
        {
            lock (_lock)
            {
                if (_instance != null && _instance != this)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T).Name} 已存在，销毁重复实例: {gameObject.name}");
                    Destroy(gameObject);
                    return;
                }
                
                _instance = this as T;
                OnSingletonAwake();
            }
        }
        
        protected virtual void OnDestroy()
        {
            lock (_lock)
            {
                if (_instance == this)
                {
                    _instance = null;
                    OnSingletonDestroy();
                }
            }
        }
        
        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 可重写方法（子类使用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 单例初始化时调用（在 Awake 中，实例设置之后）
        /// </summary>
        protected virtual void OnSingletonAwake() { }
        
        /// <summary>
        /// 单例销毁时调用（在 OnDestroy 中）
        /// </summary>
        protected virtual void OnSingletonDestroy() { }
    }
    
    /// <summary>
    /// 持久单例基类（跨场景保持）
    /// 使用 DontDestroyOnLoad，场景切换时不会被销毁
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    /// <example>
    /// public class GameManager : PersistentSingleton&lt;GameManager&gt;
    /// {
    ///     protected override void OnSingletonAwake()
    ///     {
    ///         // 你的初始化代码...
    ///     }
    /// }
    /// </example>
    public abstract class PersistentSingleton<T> : Singleton<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            // 如果已存在实例，销毁自己
            if (HasInstance && Instance != this)
            {
                Debug.LogWarning($"[PersistentSingleton] {typeof(T).Name} 已存在，销毁重复实例: {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            
            base.Awake();
            
            // 跨场景保持
            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }
    
    /// <summary>
    /// 自动创建单例基类（场景内有效）
    /// 如果场景中没有实例，会自动创建一个
    /// </summary>
    /// <typeparam name="T">子类类型</typeparam>
    /// <example>
    /// public class AudioManager : AutoSingleton&lt;AudioManager&gt;
    /// {
    ///     // 可以直接调用 AudioManager.Instance，即使场景中没有预先放置
    /// }
    /// </example>
    public abstract class AutoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _isQuitting = false;
        
        /// <summary>
        /// 单例实例访问器（自动创建）
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_isQuitting)
                {
                    return null;
                }
                
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        // 先尝试在场景中查找
                        _instance = FindObjectOfType<T>();
                        
                        // 找不到则自动创建
                        if (_instance == null)
                        {
                            GameObject go = new GameObject($"[{typeof(T).Name}]");
                            _instance = go.AddComponent<T>();
                            Debug.Log($"[AutoSingleton] 自动创建 {typeof(T).Name}");
                        }
                    }
                    return _instance;
                }
            }
        }
        
        public static bool HasInstance => _instance != null;
        
        protected virtual void Awake()
        {
            lock (_lock)
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                _instance = this as T;
            }
        }
        
        protected virtual void OnDestroy()
        {
            lock (_lock)
            {
                if (_instance == this)
                {
                    _instance = null;
                }
            }
        }
        
        protected virtual void OnApplicationQuit()
        {
            _isQuitting = true;
        }
    }
}