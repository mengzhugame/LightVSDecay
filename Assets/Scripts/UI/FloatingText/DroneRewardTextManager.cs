// ============================================================
// DroneRewardTextManager.cs
// 文件位置: Assets/Scripts/UI/TacticalDrop/DroneRewardTextManager.cs
// 用途：无人机奖励飘字管理器（单例 + 对象池）
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Data.SO;

namespace LightVsDecay.UI.FloatingText.TacticalDrop
{
    /// <summary>
    /// 无人机飘字类型
    /// </summary>
    public enum DroneTextType
    {
        Supply,  // 补给无人机（单行）
        Gacha,   // 问号无人机（单行）
        Deal     // 契约无人机（双行）
    }
    
    /// <summary>
    /// 无人机奖励飘字管理器
    /// </summary>
    public class DroneRewardTextManager : Singleton<DroneRewardTextManager>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("无人机奖励配置")]
        [SerializeField] private DroneRewardConfig config;
        
        [Header("Canvas 引用")]
        [Tooltip("飘字挂载的 Canvas")]
        [SerializeField] private Canvas targetCanvas;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private Dictionary<DroneTextType, Queue<DroneRewardText>> pools = 
            new Dictionary<DroneTextType, Queue<DroneRewardText>>();
        private List<DroneRewardText> activeTexts = new List<DroneRewardText>();
        private Transform poolContainer;
        private int totalCreated = 0;
        private bool isInitialized = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public DroneRewardConfig Config => config;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 延迟初始化
        }
        
        private void Start()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            if (isInitialized) return;
            
            if (config == null)
            {
                Debug.LogError("[DroneRewardTextManager] 配置未设置！");
                return;
            }
            
            // 获取 Canvas
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
                Debug.LogError("[DroneRewardTextManager] 找不到 Canvas！");
                return;
            }
            
            // 创建池容器
            GameObject containerGO = new GameObject("[DroneRewardTextPool]");
            containerGO.transform.SetParent(targetCanvas.transform, false);
            
            RectTransform rt = containerGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            
            poolContainer = containerGO.transform;
            
            // 初始化对象池
            InitializePools();
            
            isInitialized = true;
            
            if (showDebugInfo)
            {
                Debug.Log("[DroneRewardTextManager] 初始化完成");
            }
        }
        
        private void InitializePools()
        {
            // 为每种类型创建队列
            pools[DroneTextType.Supply] = new Queue<DroneRewardText>();
            pools[DroneTextType.Gacha] = new Queue<DroneRewardText>();
            pools[DroneTextType.Deal] = new Queue<DroneRewardText>();
            
            // 预热
            PrewarmType(DroneTextType.Supply, config.textPrewarmCount);
            PrewarmType(DroneTextType.Gacha, config.textPrewarmCount);
            PrewarmType(DroneTextType.Deal, config.textPrewarmCount);
        }
        
        private void PrewarmType(DroneTextType type, int count)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab == null) return;
            
            for (int i = 0; i < count; i++)
            {
                DroneRewardText instance = CreateInstance(type, prefab);
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    pools[type].Enqueue(instance);
                }
            }
        }
        
        private DroneRewardText CreateInstance(DroneTextType type, GameObject prefab)
        {
            if (prefab == null || poolContainer == null) return null;
            
            GameObject go = Instantiate(prefab, poolContainer);
            go.name = $"DroneText_{type}_{totalCreated:D3}";
            
            DroneRewardText text = go.GetComponent<DroneRewardText>();
            if (text == null)
            {
                text = go.AddComponent<DroneRewardText>();
            }
            
            totalCreated++;
            return text;
        }
        
        private GameObject GetPrefab(DroneTextType type)
        {
            if (config == null) return null;
            
            switch (type)
            {
                case DroneTextType.Supply:
                    return config.supplyTextPrefab;
                case DroneTextType.Gacha:
                    return config.gachaTextPrefab;
                case DroneTextType.Deal:
                    return config.dealTextPrefab;
                default:
                    return null;
            }
        }
        
        protected override void OnSingletonDestroy()
        {
            ClearAll();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 补给无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示补给无人机奖励飘字
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="rewardType">奖励类型</param>
        /// <param name="displayText">显示文本（如 "+100"）</param>
        public void ShowSupplyReward(Vector3 worldPosition, RewardType rewardType, string displayText)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = GetInstance(DroneTextType.Supply);
            if (text == null) return;
            
            RewardIconType iconType = config.GetIconTypeFromRewardType(rewardType);
            Sprite icon = config.GetIcon(iconType);
            Color color = config.supplyTextColor;
            
            text.PlaySingle(worldPosition, icon, displayText, color, OnTextComplete);
            activeTexts.Add(text);
            
            if (showDebugInfo)
            {
                Debug.Log($"[DroneRewardTextManager] 补给飘字: {displayText}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 问号无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示问号无人机奖励飘字
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="rewardType">奖励类型</param>
        /// <param name="displayText">显示文本</param>
        /// <param name="isEpic">是否为史诗大奖</param>
        public void ShowGachaReward(Vector3 worldPosition, RewardType rewardType, string displayText, bool isEpic = false)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = GetInstance(DroneTextType.Gacha);
            if (text == null) return;
            
            RewardIconType iconType = config.GetIconTypeFromRewardType(rewardType);
            Sprite icon = config.GetIcon(iconType);
            Color color = config.GetRewardColor(rewardType, isEpic);
            
            text.PlaySingle(worldPosition, icon, displayText, color, OnTextComplete);
            activeTexts.Add(text);
            
            if (showDebugInfo)
            {
                Debug.Log($"[DroneRewardTextManager] 问号飘字: {displayText}, Epic={isEpic}");
            }
        }
        
        /// <summary>
        /// 显示问号无人机"谢谢惠顾"飘字
        /// </summary>
        public void ShowGachaNothing(Vector3 worldPosition, string mockText)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = GetInstance(DroneTextType.Gacha);
            if (text == null) return;
            
            Sprite icon = config.GetIcon(RewardIconType.Nothing);
            Color color = config.neutralColor;
            
            text.PlaySingle(worldPosition, icon, mockText, color, OnTextComplete);
            activeTexts.Add(text);
            
            if (showDebugInfo)
            {
                Debug.Log($"[DroneRewardTextManager] 问号飘字（空）: {mockText}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 契约无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示契约无人机奖励飘字（代价 + 收益）
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="costType">代价类型</param>
        /// <param name="costText">代价文本（如 "HP -100"）</param>
        /// <param name="gainType">收益类型</param>
        /// <param name="gainText">收益文本（如 "ATK +10%"）</param>
        /// <param name="isEpic">收益是否为史诗</param>
        public void ShowDealReward(
            Vector3 worldPosition,
            RewardType costType,
            string costText,
            RewardType gainType,
            string gainText,
            bool isEpic = false)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = GetInstance(DroneTextType.Deal);
            if (text == null) return;
            
            // 代价（始终红色）
            RewardIconType costIconType = config.GetIconTypeFromRewardType(costType);
            Sprite costIcon = config.GetIcon(costIconType);
            Color costColor = config.negativeColor;
            
            // 收益（绿色或金色）
            RewardIconType gainIconType = config.GetIconTypeFromRewardType(gainType);
            Sprite gainIcon = config.GetIcon(gainIconType);
            Color gainColor = isEpic ? config.epicColor : config.positiveColor;
            
            text.PlayDual(
                worldPosition,
                costIcon, costText, costColor,
                gainIcon, gainText, gainColor,
                OnTextComplete
            );
            activeTexts.Add(text);
            
            if (showDebugInfo)
            {
                Debug.Log($"[DroneRewardTextManager] 契约飘字: {costText} → {gainText}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool EnsureInitialized()
        {
            if (!isInitialized)
            {
                Initialize();
            }
            return isInitialized;
        }
        
        private DroneRewardText GetInstance(DroneTextType type)
        {
            if (!pools.ContainsKey(type))
            {
                pools[type] = new Queue<DroneRewardText>();
            }
            
            var pool = pools[type];
            
            // 从池中取
            if (pool.Count > 0)
            {
                return pool.Dequeue();
            }
            
            // 动态创建
            if (totalCreated < config.textMaxPoolSize)
            {
                GameObject prefab = GetPrefab(type);
                if (prefab != null)
                {
                    return CreateInstance(type, prefab);
                }
            }
            
            // 回收最旧的
            if (activeTexts.Count > 0)
            {
                DroneRewardText oldest = activeTexts[0];
                activeTexts.RemoveAt(0);
                oldest.ForceStop();
                return oldest;
            }
            
            return null;
        }
        
        private void OnTextComplete(DroneRewardText text)
        {
            if (text == null) return;
    
            activeTexts.Remove(text);
            text.Reset();
            text.gameObject.SetActive(false);
    
            // 返回到通用池（Supply池）以便复用
            if (pools.ContainsKey(DroneTextType.Supply))
            {
                pools[DroneTextType.Supply].Enqueue(text);
            }
        }
        
        /// <summary>
        /// 清空所有
        /// </summary>
        public void ClearAll()
        {
            foreach (var text in activeTexts)
            {
                if (text != null)
                {
                    text.ForceStop();
                }
            }
            activeTexts.Clear();
            
            foreach (var pool in pools.Values)
            {
                while (pool.Count > 0)
                {
                    var text = pool.Dequeue();
                    if (text != null)
                    {
                        Destroy(text.gameObject);
                    }
                }
            }
            pools.Clear();
            
            totalCreated = 0;
            isInitialized = false;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        [ContextMenu("Test: Show Supply")]
        private void TestShowSupply()
        {
            ShowSupplyReward(Vector3.zero, RewardType.HealthRestore, "+100");
        }
        
        [ContextMenu("Test: Show Gacha Epic")]
        private void TestShowGachaEpic()
        {
            ShowGachaReward(Vector3.up * 2f, RewardType.BaseDamagePercent, "ATK +10%", true);
        }
        
        [ContextMenu("Test: Show Deal")]
        private void TestShowDeal()
        {
            ShowDealReward(
                Vector3.up * 4f,
                RewardType.HealthLoss, "HP -100",
                RewardType.BaseDamagePercent, "ATK +10%",
                false
            );
        }
#endif
    }
}