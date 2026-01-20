// ============================================================
// FloatingTextManager.cs
// 文件位置: Assets/Scripts/UI/FloatingText/FloatingTextManager.cs
// 用途：飘字系统管理器（单例）- 多Prefab对象池 + 优先级回收
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
    /// 支持多种 Prefab 类型（Normal, Crit, BossShield, BossCore）
    /// </summary>
    public class FloatingTextManager : Singleton<FloatingTextManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("飘字配置文件")]
        [SerializeField] private FloatingTextConfig config;
        
        [Header("Canvas 引用")]
        [Tooltip("飘字挂载的 Canvas（需要是 Screen Space - Overlay 或 Camera）")]
        [SerializeField] private Canvas targetCanvas;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        // 每种类型独立的对象池
        private Dictionary<FloatingTextType, Queue<FloatingText>> typePools = new Dictionary<FloatingTextType, Queue<FloatingText>>();
        private List<FloatingText> activeTexts = new List<FloatingText>();
        private Transform poolContainer = null;
        private int totalCreated = 0;
        private bool isInitialized = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public int ActiveCount => activeTexts.Count;
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
            containerGO.transform.SetParent(transform, false);
            
            RectTransform rt = containerGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            
            poolContainer = containerGO.transform;
            
            Debug.Log($"[FloatingTextManager] 池容器已创建: {poolContainer.name}");
            
            // 4. 初始化各类型对象池
            InitializeTypePools();
            
            isInitialized = true;
            Debug.Log($"[FloatingTextManager] ===== 初始化完成: 总创建={totalCreated} =====");
        }
        
        /// <summary>
        /// 初始化各类型的对象池
        /// </summary>
        private void InitializeTypePools()
        {
            // 为每种类型创建空队列
            foreach (FloatingTextType type in System.Enum.GetValues(typeof(FloatingTextType)))
            {
                typePools[type] = new Queue<FloatingText>();
            }
            
            // 预热主要类型
            PrewarmType(FloatingTextType.Normal, config.prewarmCount / 2);
            PrewarmType(FloatingTextType.Crit, config.prewarmCount / 4);
            PrewarmType(FloatingTextType.BossShield, 5);
            PrewarmType(FloatingTextType.BossCore, 5);
            // 预热玩家受击飘字类型
            PrewarmType(FloatingTextType.PlayerHealthDamage, 3);
            PrewarmType(FloatingTextType.PlayerShieldDamage, 3);
            PrewarmType(FloatingTextType.PlayerHealthRestore, 2);
            PrewarmType(FloatingTextType.PlayerShieldRestore, 2);
        }
        
        /// <summary>
        /// 预热指定类型的对象池
        /// </summary>
        private void PrewarmType(FloatingTextType type, int count)
        {
            GameObject prefab = config.GetPrefab(type);
            if (prefab == null)
            {
                Debug.LogWarning($"[FloatingTextManager] {type} Prefab 未设置，跳过预热");
                return;
            }
            
            for (int i = 0; i < count; i++)
            {
                FloatingText ft = CreateInstance(type, prefab);
                if (ft != null)
                {
                    ft.gameObject.SetActive(false);
                    typePools[type].Enqueue(ft);
                }
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[FloatingTextManager] 预热 {type}: {count} 个");
            }
        }
        
        /// <summary>
        /// 创建飘字实例
        /// </summary>
        private FloatingText CreateInstance(FloatingTextType type, GameObject prefab)
        {
            if (prefab == null || poolContainer == null) return null;
            
            GameObject go = Instantiate(prefab, poolContainer);
            go.name = $"FloatingText_{type}_{totalCreated:D3}";
            
            FloatingText ft = go.GetComponent<FloatingText>();
            if (ft == null)
            {
                ft = go.AddComponent<FloatingText>();
            }
            
            totalCreated++;
            return ft;
        }
        
        protected override void OnSingletonDestroy()
        {
            ClearAll();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 伤害显示
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示普通伤害飘字（支持暴击）
        /// </summary>
        public void ShowDamage(Vector3 worldPosition, float damage, bool isCrit = false)
        {
            FloatingTextType type = isCrit ? FloatingTextType.Crit : FloatingTextType.Normal;
            string text = Mathf.RoundToInt(damage).ToString();
            Show(worldPosition, text, type);
        }
        // ═══ 【新增】碎冰伤害飘字 ═══
        
        /// <summary>
        /// 显示碎冰伤害飘字（支持暴击叠加）
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="damage">伤害值</param>
        /// <param name="isCrit">是否同时触发暴击</param>
        public void ShowShatterDamage(Vector3 worldPosition, float damage, bool isCrit = false)
        {
            // 碎冰+暴击 = ShatterCrit，纯碎冰 = Shatter
            FloatingTextType type = isCrit ? FloatingTextType.ShatterCrit : FloatingTextType.Shatter;
            string text = Mathf.RoundToInt(damage).ToString();
            Show(worldPosition, text, type);
        }
        
        /// <summary>
        /// 显示处决飘字
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        public void ShowExecution(Vector3 worldPosition)
        {
            Show(worldPosition, "EXECUTE!", FloatingTextType.Execution);
        }
        /// <summary>
        /// 显示 Boss 护甲伤害飘字（银灰色 + 盾牌图标）
        /// </summary>
        public void ShowBossShieldDamage(Vector3 worldPosition, float damage)
        {
            string text = Mathf.RoundToInt(damage).ToString();
            Show(worldPosition, text, FloatingTextType.BossShield);
        }
        
        /// <summary>
        /// 显示 Boss 核心伤害飘字（红色 + 眼睛图标）
        /// </summary>
        /// <param name="isCrit">是否同时触发暴击（弱点+暴击叠加）</param>
        public void ShowBossCoreDamage(Vector3 worldPosition, float damage, bool isCrit = false)
        {
            // 如果弱点命中同时触发暴击，使用暴击样式（更大更明显）
            FloatingTextType type = isCrit ? FloatingTextType.Crit : FloatingTextType.BossCore;
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
                Debug.Log($"[FloatingTextManager] Show: '{text}' @ {worldPosition}, Type: {type}");
            }
            
            // 获取实例
            FloatingText ft = GetInstance(type);
            if (ft == null)
            {
                Debug.LogWarning($"[FloatingTextManager] 无法获取 {type} 类型的飘字实例");
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
            
            foreach (var pool in typePools.Values)
            {
                while (pool.Count > 0)
                {
                    var ft = pool.Dequeue();
                    if (ft != null) Destroy(ft.gameObject);
                }
            }
            typePools.Clear();
            
            totalCreated = 0;
            isInitialized = false;
        }
        /// <summary>
        /// 显示玩家血量受伤飘字（红色）
        /// </summary>
        public void ShowPlayerHealthDamage(Vector3 worldPosition, int damage)
        {
            // 调整Y坐标：塔在-10，飘字在-8
            Vector3 adjustedPos = new Vector3(worldPosition.x, -8f, worldPosition.z);
            string text = $"-{damage}";
            Show(adjustedPos, text, FloatingTextType.PlayerHealthDamage);
        }

        /// <summary>
        /// 显示玩家护盾受伤飘字（青色）
        /// </summary>
        public void ShowPlayerShieldDamage(Vector3 worldPosition, int damage)
        {
            // 调整Y坐标：塔在-10，飘字在-8
            Vector3 adjustedPos = new Vector3(worldPosition.x, -8f, worldPosition.z);
            string text = $"-{damage}";
            Show(adjustedPos, text, FloatingTextType.PlayerShieldDamage);
        }

        /// <summary>
        /// 显示玩家血量恢复飘字（绿色）
        /// </summary>
        public void ShowPlayerHealthRestore(Vector3 worldPosition, int amount)
        {
            Vector3 adjustedPos = new Vector3(worldPosition.x, -8f, worldPosition.z);
            string text = $"+{amount}";
            Show(adjustedPos, text, FloatingTextType.PlayerHealthRestore);
        }

        /// <summary>
        /// 显示玩家护盾恢复飘字（青色+）
        /// </summary>
        public void ShowPlayerShieldRestore(Vector3 worldPosition, int amount)
        {
            Vector3 adjustedPos = new Vector3(worldPosition.x, -8f, worldPosition.z);
            string text = $"+{amount}";
            Show(adjustedPos, text, FloatingTextType.PlayerShieldRestore);
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private FloatingText GetInstance(FloatingTextType requestType)
        {
            // 确保类型池存在
            if (!typePools.ContainsKey(requestType))
            {
                typePools[requestType] = new Queue<FloatingText>();
            }
            
            var pool = typePools[requestType];
            
            // 1. 从对应类型池中取
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }
            
            // 2. 动态创建
            if (totalCreated < config.maxPoolSize && poolContainer != null)
            {
                GameObject prefab = config.GetPrefab(requestType);
                if (prefab != null)
                {
                    return CreateInstance(requestType, prefab);
                }
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
            
            // 根据类型放回对应的池
            FloatingTextType type = ft.CurrentType;
            if (!typePools.ContainsKey(type))
            {
                typePools[type] = new Queue<FloatingText>();
            }
            typePools[type].Enqueue(ft);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo || !Application.isPlaying) return;
            
            GUILayout.BeginArea(new Rect(10, 450, 250, 180));
            GUILayout.Label("=== FloatingText Debug ===");
            GUILayout.Label($"Initialized: {isInitialized}");
            GUILayout.Label($"Active: {activeTexts.Count}");
            GUILayout.Label($"Total Created: {totalCreated}");
            
            // 显示各类型池的数量
            foreach (var kvp in typePools)
            {
                GUILayout.Label($"  {kvp.Key}: {kvp.Value.Count}");
            }
            
            GUILayout.EndArea();
        }
#endif
    }
}