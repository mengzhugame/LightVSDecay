using System;
using System.Collections;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Ads
{
    public sealed class AdManager : MonoBehaviour
    {
        private const string SharedDailyTotalKey = "ad_shared_total_{0}";
        private const string DailyCountKey       = "ad_daily_count_{0}_{1}";
        private const string PREFAB_PATH         = "Prefab/AdManager";

        private static AdManager instance;

        [Header("Debug")]
        [Tooltip("true=编辑器占位模式（直接成功，不调用真实广告）；发布微信前设为 false")]
        [SerializeField] private bool usePlaceholderAds = true;
        [SerializeField] private bool showDebugInfo = false;

        private bool hasRevivedThisGame;
        private bool hasSettlementDoubleThisGame;

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

                var prefab = Resources.Load<GameObject>(PREFAB_PATH);
                if (prefab != null)
                {
                    Instantiate(prefab).name = "[AdManager]";
                }
                else
                {
                    GameLogger.LogWarning("[AdManager] Prefab 未找到: Resources/" + PREFAB_PATH);
                    var go = new GameObject("[AdManager]");
                    instance = go.AddComponent<AdManager>();
                    DontDestroyOnLoad(go);
                }
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
        }

        private void Start()
        {
            StartCoroutine(PreloadAllAdsDelayed());
        }

        private IEnumerator PreloadAllAdsDelayed()
        {
            yield return null;  // 等一帧，确保团结引擎 JS 桥接就绪
            PreloadAllAds();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStart             += ResetRunState;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStart             -= ResetRunState;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共查询
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public bool CanWatchAd(AdType adType)
        {
            if (adType == AdType.SkillReroll)
                return true;

            if (IsSharedRewardedType(adType) && GetSharedRewardedDailyCount() >= SharedRewardedDailyLimit)
                return false;

            switch (adType)
            {
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
                case AdType.SkillReroll:      return int.MaxValue;
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
        /// usePlaceholderAds=true 时直接触发成功（Editor 调试）；
        /// 否则通过 WeChatAdsPlugin 调用真实微信广告 SDK。
        /// </summary>
        public void ShowRewardedAd(AdType adType, Action onSuccess, Action onFail = null)
        {
            if (!CanWatchAd(adType))
            {
                Log($"广告次数不可用: {adType}");
                onFail?.Invoke();
                return;
            }

            if (usePlaceholderAds)
            {
                GrantWatchCount(adType);
                onSuccess?.Invoke();
                return;
            }

            WeChatAdsPlugin.Instance.ShowAd((int)adType,
                onSuccess: () =>
                {
                    GrantWatchCount(adType);
                    onSuccess?.Invoke();
                },
                onFail: () => onFail?.Invoke());
        }

        public void PreloadRewardedAd(AdType adType)
        {
            // 广告位 ID 已统一在 jslib 中管理，单独预加载已无必要
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 私有实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void PreloadAllAds()
        {
            if (usePlaceholderAds) return;
            WeChatAdsPlugin.Instance.PreloadAll();
        }

        private void ResetRunState()
        {
            hasRevivedThisGame          = false;
            hasSettlementDoubleThisGame = false;
        }

        private void GrantWatchCount(AdType adType)
        {
            if (IsSharedRewardedType(adType))
            {
                PlayerPrefs.SetInt(GetSharedRewardedDailyKey(), GetSharedRewardedDailyCount() + 1);
            }
            else
            {
                int cur = PlayerPrefs.GetInt(GetDailyCountKey(adType), 0);
                PlayerPrefs.SetInt(GetDailyCountKey(adType), cur + 1);
            }

            switch (adType)
            {
                case AdType.Revive:           hasRevivedThisGame          = true; break;
                case AdType.SettlementDouble: hasSettlementDoubleThisGame = true; break;
            }

            PlayerPrefs.Save();
            Log($"广告完成并记次: {adType}");
        }

        private bool IsSharedRewardedType(AdType adType)
        {
            return adType == AdType.SettlementDouble ||
                   adType == AdType.Revive;
        }

        private int GetSharedRewardedDailyCount()
            => PlayerPrefs.GetInt(GetSharedRewardedDailyKey(), 0);

        private string GetSharedRewardedDailyKey()
            => string.Format(SharedDailyTotalKey, DateTime.Now.ToString("yyyyMMdd"));

        private string GetDailyCountKey(AdType adType)
            => string.Format(DailyCountKey, DateTime.Now.ToString("yyyyMMdd"), adType);

        private void Log(string message)
        {
            if (showDebugInfo)
                GameLogger.Log($"[AdManager] {message}");
        }
    }
}
