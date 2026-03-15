// ============================================================
// DroneRewardTextManager.cs
// 文件位置: Assets/Scripts/UI/TacticalDrop/DroneRewardTextManager.cs
// 用途：无人机奖励飘字管理器（简化版 - 直接Instantiate）
// 改动：移除对象池，使用直接实例化 + 自动销毁
// ============================================================

using UnityEngine;
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
    /// 无人机奖励飘字管理器（简化版）
    /// 低频场景使用直接 Instantiate，动画完成后自动销毁
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
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
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
                GameLogger.LogError("[DroneRewardTextManager] 配置未设置！");
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
                GameLogger.LogError("[DroneRewardTextManager] 找不到 Canvas！");
                return;
            }
            
            isInitialized = true;
            
            if (showDebugInfo)
            {
                GameLogger.Log("[DroneRewardTextManager] 初始化完成（简化版）");
            }
        }
        
        protected override void OnSingletonDestroy()
        {
            // 简化版无需清理
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 补给无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示补给无人机奖励飘字
        /// </summary>
        public void ShowSupplyReward(Vector3 worldPosition, RewardType rewardType, string displayText)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = CreateText(DroneTextType.Supply);
            if (text == null) return;
            
            RewardIconType iconType = config.GetIconTypeFromRewardType(rewardType);
            Sprite icon = config.GetIcon(iconType);
            Color color = config.supplyTextColor;
            
            text.PlaySingle(worldPosition, icon, displayText, color);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[DroneRewardTextManager] 补给飘字: {displayText}");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 问号无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示问号无人机奖励飘字
        /// </summary>
        public void ShowGachaReward(Vector3 worldPosition, RewardType rewardType, string displayText, bool isEpic = false)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = CreateText(DroneTextType.Gacha);
            if (text == null) return;
            
            RewardIconType iconType = config.GetIconTypeFromRewardType(rewardType);
            Sprite icon = config.GetIcon(iconType);
            Color color = config.GetRewardColor(rewardType, isEpic);
            
            text.PlaySingle(worldPosition, icon, displayText, color);
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[DroneRewardTextManager] 问号飘字: {displayText}, Epic={isEpic}");
            }
        }
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口 - 契约无人机
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 显示契约无人机奖励飘字（代价 + 收益）
        /// </summary>
        public void ShowDealReward(
            Vector3 worldPosition,
            RewardType costType,
            string costText,
            RewardType gainType,
            string gainText,
            bool isEpic = false)
        {
            if (!EnsureInitialized()) return;
            
            DroneRewardText text = CreateText(DroneTextType.Deal);
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
                gainIcon, gainText, gainColor
            );
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[DroneRewardTextManager] 契约飘字: {costText} → {gainText}");
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
        
        /// <summary>
        /// 创建飘字实例（直接 Instantiate，动画完成后自动销毁）
        /// </summary>
        private DroneRewardText CreateText(DroneTextType type)
        {
            GameObject prefab = GetPrefab(type);
            if (prefab == null)
            {
                GameLogger.LogError($"[DroneRewardTextManager] 找不到 {type} 类型的 Prefab！");
                return null;
            }
            
            GameObject go = Instantiate(prefab, targetCanvas.transform);
            DroneRewardText text = go.GetComponent<DroneRewardText>();
            
            if (text == null)
            {
                GameLogger.LogError($"[DroneRewardTextManager] Prefab 缺少 DroneRewardText 组件！");
                Destroy(go);
                return null;
            }
            
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
        /// <summary>
        /// 显示金币奖励飘字
        /// </summary>
        public void ShowCoinReward(Vector3 worldPosition, string displayText)
        {
            if (!EnsureInitialized()) return;
    
            // 优先使用金币专用预制体，没有则使用问号预制体
            GameObject prefab = config.coinTextPrefab != null ? config.coinTextPrefab : config.gachaTextPrefab;
            if (prefab == null)
            {
                GameLogger.LogError("[DroneRewardTextManager] 金币/问号飘字 Prefab 未设置！");
                return;
            }
    
            GameObject go = Instantiate(prefab, targetCanvas.transform);
            DroneRewardText text = go.GetComponent<DroneRewardText>();
            if (text == null)
            {
                Destroy(go);
                return;
            }
    
            Sprite icon = config.GetIcon(RewardIconType.Coin);
            Color color = config.coinTextColor;
    
            text.PlaySingle(worldPosition, icon, displayText, color);
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[DroneRewardTextManager] 金币飘字: {displayText}");
            }
        }
        /// <summary>
        /// 显示怪物增强效果飘字
        /// </summary>
        public void ShowMonsterBuffEffect(Vector3 worldPosition, string displayText)
        {
            if (!EnsureInitialized()) return;
    
            // 优先使用怪物增强专用预制体，没有则使用问号预制体
            GameObject prefab = config.monsterBuffTextPrefab != null ? config.monsterBuffTextPrefab : config.gachaTextPrefab;
            if (prefab == null)
            {
                GameLogger.LogError("[DroneRewardTextManager] 怪物增强/问号飘字 Prefab 未设置！");
                return;
            }
    
            GameObject go = Instantiate(prefab, targetCanvas.transform);
            DroneRewardText text = go.GetComponent<DroneRewardText>();
            if (text == null)
            {
                Destroy(go);
                return;
            }
    
            Sprite icon = config.GetIcon(RewardIconType.MonsterBuff);
            Color color = config.negativeColor;  // 怪物增强用红色
    
            text.PlaySingle(worldPosition, icon, displayText, color);
    
            if (showDebugInfo)
            {
                GameLogger.Log($"[DroneRewardTextManager] 怪物增强飘字: {displayText}");
            }
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