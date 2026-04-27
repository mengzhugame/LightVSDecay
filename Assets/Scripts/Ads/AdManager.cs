using System;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Ads
{
    public sealed class AdManager : MonoBehaviour
    {
        private const string SharedDailyTotalKey = "ad_shared_total_{0}";
        private const string DailyCountKey       = "ad_daily_count_{0}_{1}";

        private static AdManager instance;

        [Header("Debug")]
        [Tooltip("true=编辑器占位模式（不调用真实广告）；发布微信前务必设为 false")]
        [SerializeField] private bool usePlaceholderAds = true;
        [SerializeField] private bool showDebugInfo = false;

        [Header("Rewarded Video AdUnitId")]
        [SerializeField] private string skillRerollAdUnitId      = string.Empty;
        [SerializeField] private string settlementDoubleAdUnitId = string.Empty;
        [SerializeField] private string reviveAdUnitId           = string.Empty;
        [SerializeField] private string energyTopUpAdUnitId      = string.Empty;
        [SerializeField] private string goldTopUpAdUnitId        = string.Empty;
        [SerializeField] private string blueprintTopUpAdUnitId   = string.Empty;

        private bool hasRevivedThisGame;
        private bool hasSettlementDoubleThisGame;
        private bool hasSkillRerollThisLevel;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 单例
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public static AdManager Instance
        {
            get
            {
                if (instance != null) return instance;
                instance = FindObjectOfType<AdManager>();
                if (instance != null) return instance;
                var go = new GameObject("[AdManager]");
                instance = go.AddComponent<AdManager>();
                DontDestroyOnLoad(go);
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        public int SharedRewardedDailyLimit => 10;

        public bool HasRevivedThisGame() => hasRevivedThisGame;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            PreloadAllAds();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStart             += ResetRunState;
            GameEvents.OnLevelUp               += OnLevelUp;
            GameEvents.OnLevelUpChoiceComplete += OnLevelUpChoiceComplete;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStart             -= ResetRunState;
            GameEvents.OnLevelUp               -= OnLevelUp;
            GameEvents.OnLevelUpChoiceComplete -= OnLevelUpChoiceComplete;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共查询
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public bool CanWatchAd(AdType adType)
        {
            if (IsSharedRewardedType(adType) && GetSharedRewardedDailyCount() >= SharedRewardedDailyLimit)
                return false;

            switch (adType)
            {
                case AdType.SkillReroll:      return !hasSkillRerollThisLevel;
                case AdType.SettlementDouble: return !hasSettlementDoubleThisGame;
                case AdType.Revive:           return !hasRevivedThisGame;
                case AdType.EnergyTopUp:
                case AdType.GoldTopUp:
                case AdType.BlueprintTopUp:   return GetDailyCount(adType) < GetDailyLimit(adType);
                default:                      return false;
            }
        }

        public bool CanOfferRevive(int currentWave)
        {
            return currentWave >= 4 && CanWatchAd(AdType.Revive);
        }

        public int GetDailyCount(AdType adType)
        {
            return IsSharedRewardedType(adType)
                ? GetSharedRewardedDailyCount()
                : PlayerPrefs.GetInt(GetDailyCountKey(adType), 0);
        }

        public int GetDailyLimit(AdType adType)
        {
            switch (adType)
            {
                case AdType.SkillReroll:
                case AdType.SettlementDouble:
                case AdType.Revive:           return SharedRewardedDailyLimit;
                case AdType.EnergyTopUp:      return 5;
                case AdType.GoldTopUp:
                case AdType.BlueprintTopUp:   return 3;
                default:                      return 0;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 展示激励广告
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 展示激励视频广告。
        /// usePlaceholderAds=true 时直接成功（Editor 调试用）；
        /// 否则调用真实微信广告 SDK，完整观看后 onSuccess，跳过/失败则 onFail。
        /// </summary>
        public void ShowRewardedAd(AdType adType, Action onSuccess, Action onFail = null)
        {
            if (!CanWatchAd(adType))
            {
                Log($"广告次数不可用: {adType}");
                onFail?.Invoke();
                return;
            }

            string adUnitId = GetAdUnitId(adType);

            if (usePlaceholderAds || string.IsNullOrWhiteSpace(adUnitId))
            {
                GrantWatchCount(adType);
                onSuccess?.Invoke();
                return;
            }

            WeChatAdsPlugin.Instance.ShowAd(adUnitId,
                onSuccess: () =>
                {
                    GrantWatchCount(adType);
                    onSuccess?.Invoke();
                },
                onFail: () => onFail?.Invoke());
        }

        public void PreloadRewardedAd(AdType adType)
        {
            if (usePlaceholderAds) return;
            string adUnitId = GetAdUnitId(adType);
            if (!string.IsNullOrWhiteSpace(adUnitId))
                WeChatAdsPlugin.Instance.PreloadAd(adUnitId);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void PreloadAllAds()
        {
            if (usePlaceholderAds) return;
            var plugin = WeChatAdsPlugin.Instance;
            plugin.PreloadAd(skillRerollAdUnitId);
            plugin.PreloadAd(settlementDoubleAdUnitId);
            plugin.PreloadAd(reviveAdUnitId);
            plugin.PreloadAd(energyTopUpAdUnitId);
            plugin.PreloadAd(goldTopUpAdUnitId);
            plugin.PreloadAd(blueprintTopUpAdUnitId);
        }

        private void ResetRunState()
        {
            hasRevivedThisGame           = false;
            hasSettlementDoubleThisGame  = false;
            hasSkillRerollThisLevel      = false;
        }

        private void OnLevelUp(int level)
        {
            hasSkillRerollThisLevel = false;
        }

        private void OnLevelUpChoiceComplete()
        {
            hasSkillRerollThisLevel = false;
        }

        private void GrantWatchCount(AdType adType)
        {
            if (IsSharedRewardedType(adType))
            {
                PlayerPrefs.SetInt(GetSharedRewardedDailyKey(), GetSharedRewardedDailyCount() + 1);
            }
            else
            {
                int current = PlayerPrefs.GetInt(GetDailyCountKey(adType), 0);
                PlayerPrefs.SetInt(GetDailyCountKey(adType), current + 1);
            }

            switch (adType)
            {
                case AdType.Revive:           hasRevivedThisGame          = true; break;
                case AdType.SettlementDouble: hasSettlementDoubleThisGame = true; break;
                case AdType.SkillReroll:      hasSkillRerollThisLevel     = true; break;
            }

            PlayerPrefs.Save();
            Log($"广告完成并记次: {adType}");
        }

        private bool IsSharedRewardedType(AdType adType)
        {
            return adType == AdType.SkillReroll  ||
                   adType == AdType.SettlementDouble ||
                   adType == AdType.Revive;
        }

        private int GetSharedRewardedDailyCount()
        {
            return PlayerPrefs.GetInt(GetSharedRewardedDailyKey(), 0);
        }

        private string GetSharedRewardedDailyKey()
        {
            return string.Format(SharedDailyTotalKey, DateTime.Now.ToString("yyyyMMdd"));
        }

        private string GetDailyCountKey(AdType adType)
        {
            return string.Format(DailyCountKey, DateTime.Now.ToString("yyyyMMdd"), adType);
        }

        private string GetAdUnitId(AdType adType)
        {
            switch (adType)
            {
                case AdType.SkillReroll:      return skillRerollAdUnitId;
                case AdType.SettlementDouble: return settlementDoubleAdUnitId;
                case AdType.Revive:           return reviveAdUnitId;
                case AdType.EnergyTopUp:      return energyTopUpAdUnitId;
                case AdType.GoldTopUp:        return goldTopUpAdUnitId;
                case AdType.BlueprintTopUp:   return blueprintTopUpAdUnitId;
                default:                      return string.Empty;
            }
        }

        private void Log(string message)
        {
            if (showDebugInfo)
                GameLogger.Log($"[AdManager] {message}");
        }
    }
}
