// ============================================================
// FloatingTextManager.cs
// 文件位置: Assets/Scripts/UI/FloatingText/FloatingTextManager.cs
// 用途：飘字系统管理器（单例）- 对象池 + 优先级回收
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.UI.FloatingText
{
    /// <summary>
    /// 飘字系统管理器
    /// 单例模式，管理飘字对象池和显示
    /// </summary>
    public class FloatingTextManager : Singleton<FloatingTextManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("飘字配置文件")]
        [SerializeField] private FloatingTextConfig config;
        
        [Tooltip("飘字预制体")]
        [SerializeField] private GameObject floatingTextPrefab;
        
        [Header("Canvas 引用")]
        [Tooltip("飘字挂载的 Canvas（需要是 Screen Space - Overlay 或 Camera）")]
        [SerializeField] private Canvas targetCanvas;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Queue<FloatingText> availablePool = new Queue<FloatingText>();
        private List<FloatingText> activeTexts = new List<FloatingText>();
        private Transform poolContainer = null;
        private int totalCreated = 0;
        private bool isInitialized = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int ActiveCount => activeTexts.Count;
        public int AvailableCount => availablePool.Count;
        public int TotalCreated => totalCreated;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 不在 Awake 做任何事
        }
        
        private IEnumerator Start()
        {
            // 等待一帧，确保所有 UI 组件都已初始化
            yield return null;
            
            if (Instance != this)
            {
                Debug.LogWarning("[FloatingTextManager] 非单例实例，跳过初始化");
                yield break;
            }
            
            if (isInitialized)
            {
                yield break;
            }
            
            Initialize();
        }
        
        private void Initialize()
        {
            Debug.Log("[FloatingTextManager] ===== 开始初始化 =====");
            
            // 1. 验证配置
            if (config == null)
            {
                Debug.LogError("[FloatingTextManager] 初始化失败: config 未设置！");
                return;
            }
            
            if (floatingTextPrefab == null)
            {
                Debug.LogError("[FloatingTextManager] 初始化失败: floatingTextPrefab 未设置！");
                return;
            }
            
            // 2. 获取 Canvas
            if (targetCanvas == null)
            {
                targetCanvas = GetComponentInParent<Canvas>();
            }
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
            }
            if (targetCanvas == null)
            {
                Debug.LogError("[FloatingTextManager] 初始化失败: 找不到 Canvas！");
                return;
            }
            
            Debug.Log($"[FloatingTextManager] 使用 Canvas: {targetCanvas.name}");
            
            // 3. 创建池容器
            GameObject containerGO = new GameObject("[FloatingTextPool]");
            containerGO.transform.SetParent(targetCanvas.transform, false);
            
            RectTransform rt = containerGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            
            poolContainer = containerGO.transform;
            
            Debug.Log($"[FloatingTextManager] 池容器已创建: {poolContainer.name}");
            
            // 4. 预热对象池
            int prewarmCount = config.prewarmCount;
            
            for (int i = 0; i < prewarmCount; i++)
            {
                GameObject go = Instantiate(floatingTextPrefab, poolContainer);
                go.name = $"FloatingText_{i:D3}";
                
                FloatingText ft = go.GetComponent<FloatingText>();
                if (ft == null)
                {
                    ft = go.AddComponent<FloatingText>();
                }
                
                go.SetActive(false);
                availablePool.Enqueue(ft);
                totalCreated++;
            }
            
            isInitialized = true;
            Debug.Log($"[FloatingTextManager] ===== 初始化完成: 池={availablePool.Count} =====");
        }
        
        protected override void OnSingletonDestroy()
        {
            ClearAll();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示伤害飘字
        /// </summary>
        public void ShowDamage(Vector3 worldPosition, float damage, bool isCrit = false)
        {
            FloatingTextType type = isCrit ? FloatingTextType.Crit : FloatingTextType.Normal;
            string text = Mathf.RoundToInt(damage).ToString();
            Show(worldPosition, text, type);
        }
        
        /// <summary>
        /// 显示状态文本
        /// </summary>
        public void ShowStatus(Vector3 worldPosition, string statusText)
        {
            Show(worldPosition, statusText, FloatingTextType.Status);
        }
        
        /// <summary>
        /// 显示飘字（通用接口）
        /// </summary>
        public void Show(Vector3 worldPosition, string text, FloatingTextType type)
        {
            // 如果尚未初始化，尝试立即初始化
            if (!isInitialized)
            {
                Initialize();
            }
            
            if (!isInitialized)
            {
                Debug.LogWarning("[FloatingTextManager] Show 失败: 初始化未完成");
                return;
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[FloatingTextManager] Show: '{text}' @ {worldPosition}");
            }
            
            // 获取实例
            FloatingText ft = GetInstance(type);
            if (ft == null)
            {
                return;
            }
            
            // 获取样式
            FloatingTextStyle style = config.GetStyle(type);
            int priority = config.GetPriority(type);
            
            // 播放
            ft.Play(text, worldPosition, type, style, priority, OnFloatingTextComplete);
            activeTexts.Add(ft);
        }
        
        /// <summary>
        /// 回收所有飘字
        /// </summary>
        public void ReturnAll()
        {
            var list = new List<FloatingText>(activeTexts);
            foreach (var ft in list)
            {
                if (ft != null) ft.ForceStop();
            }
            activeTexts.Clear();
        }
        
        /// <summary>
        /// 清空所有
        /// </summary>
        public void ClearAll()
        {
            ReturnAll();
            
            while (availablePool.Count > 0)
            {
                var ft = availablePool.Dequeue();
                if (ft != null) Destroy(ft.gameObject);
            }
            
            totalCreated = 0;
            isInitialized = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private FloatingText GetInstance(FloatingTextType requestType)
        {
            // 1. 从池中取
            if (availablePool.Count > 0)
            {
                return availablePool.Dequeue();
            }
            
            // 2. 动态创建
            if (totalCreated < config.maxPoolSize && poolContainer != null)
            {
                GameObject go = Instantiate(floatingTextPrefab, poolContainer);
                go.name = $"FloatingText_{totalCreated:D3}";
                
                FloatingText ft = go.GetComponent<FloatingText>();
                if (ft == null) ft = go.AddComponent<FloatingText>();
                
                totalCreated++;
                return ft;
            }
            
            // 3. 优先级回收
            return TryRecycleLowPriority(requestType);
        }
        
        private FloatingText TryRecycleLowPriority(FloatingTextType requestType)
        {
            int requestPriority = config.GetPriority(requestType);
            
            FloatingText candidate = null;
            float minScore = float.MaxValue;
            
            foreach (var ft in activeTexts)
            {
                if (ft == null || !ft.IsPlaying) continue;
                if (ft.Priority > requestPriority) continue;
                
                float score = ft.Priority * 100f + ft.RemainingPercent * 100f;
                if (score < minScore)
                {
                    minScore = score;
                    candidate = ft;
                }
            }
            
            if (candidate != null)
            {
                activeTexts.Remove(candidate);
                candidate.Reset();
                return candidate;
            }
            
            return null;
        }
        
        private void OnFloatingTextComplete(FloatingText ft)
        {
            if (ft == null) return;
            
            activeTexts.Remove(ft);
            ft.Reset();
            availablePool.Enqueue(ft);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 450, 250, 140));
            GUILayout.Label("=== FloatingText Debug ===");
            GUILayout.Label($"Initialized: {isInitialized}");
            GUILayout.Label($"PoolContainer: {(poolContainer != null ? poolContainer.name : "NULL")}");
            GUILayout.Label($"Active: {activeTexts.Count}");
            GUILayout.Label($"Available: {availablePool.Count}");
            GUILayout.Label($"Total Created: {totalCreated}");
            GUILayout.EndArea();
        }
#endif
    }
}
