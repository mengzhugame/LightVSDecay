// ============================================================
// AnalyticsManager.cs
// 文件位置: Assets/Scripts/Core/AnalyticsManager.cs
// 用途：微信小游戏数据埋点统一入口
// ============================================================

using System.Collections.Generic;
using UnityEngine;

#if WEIXINMINIGAME || UNITY_WEBGL
using WeChatWASM;
#endif

namespace LightVsDecay.Core
{
    /// <summary>
    /// 逻辑埋点 ID。正式上报的微信 branchId 在 AnalyticsManager.BranchConfigMap 中集中配置。
    /// </summary>
    public static class AnalyticsSceneIds
    {
        public const string AppLaunch = "app_launch";
        public const string FirstBattleStart = "first_battle_start";
        public const string FirstBattleWin = "first_battle_win";
        public const string FirstBattleLose = "first_battle_lose";

        public const string AdClickRevive = "ad_click_revive";
        public const string AdClickDouble = "ad_click_double";
        public const string AdClickReroll = "ad_click_reroll";
        public const string AdClickEnergy = "ad_click_energy";
        public const string AdClickGold = "ad_click_gold";
        public const string AdClickBlueprint = "ad_click_blueprint";
    }

    public sealed class AnalyticsManager : MonoBehaviour
    {
        private const int EventTypeExposure = 1;
        private const int EventTypeClick = 2;
        private const string PlayerIdKey = "Analytics_UserId";
        private const string OncePrefix = "Analytics_Once_";
        private const string FirstBattleStartedKey = "Analytics_FirstBattle_Started";
        private const string FirstBattleResultKey = "Analytics_FirstBattle_ResultReported";

        private static readonly HashSet<string> SessionReportedScenes = new HashSet<string>();

        private static readonly Dictionary<string, BranchAnalyticsConfig> BranchConfigMap =
            new Dictionary<string, BranchAnalyticsConfig>
        {
            { AnalyticsSceneIds.AppLaunch, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRg", EventTypeExposure) },
            { AnalyticsSceneIds.FirstBattleStart, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRj", EventTypeExposure) },
            { AnalyticsSceneIds.FirstBattleWin, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRi", EventTypeExposure) },
            { AnalyticsSceneIds.FirstBattleLose, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRl", EventTypeExposure) },
            { AnalyticsSceneIds.AdClickRevive, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRk", EventTypeClick) },
            { AnalyticsSceneIds.AdClickDouble, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRn", EventTypeClick) },
            { AnalyticsSceneIds.AdClickReroll, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRm", EventTypeClick) },
            { AnalyticsSceneIds.AdClickEnergy, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRp", EventTypeClick) },
            { AnalyticsSceneIds.AdClickGold, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRo", EventTypeClick) },
            { AnalyticsSceneIds.AdClickBlueprint, new BranchAnalyticsConfig("BCBgAAoXHx5d1i8TmhfkRr", EventTypeClick) },
        };

        public static AnalyticsManager Instance { get; private set; }
        public static string UserId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureUserId();
        }

        public static void LogAppLaunch()
        {
            LogSceneOncePerSession(AnalyticsSceneIds.AppLaunch);
        }

        public static void LogScene(string sceneId)
        {
            EnsureInstance().ReportSceneInternal(sceneId);
        }

        public static void LogSceneOnce(string sceneId)
        {
            string key = OncePrefix + sceneId;
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            LogScene(sceneId);
        }

        public static void LogSceneOncePerSession(string sceneId)
        {
            if (!SessionReportedScenes.Add(sceneId))
            {
                return;
            }

            LogScene(sceneId);
        }

        public static void TryLogFirstBattleStart()
        {
            if (PlayerPrefs.GetInt(FirstBattleStartedKey, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(FirstBattleStartedKey, 1);
            PlayerPrefs.Save();
            LogScene(AnalyticsSceneIds.FirstBattleStart);
        }

        public static void TryLogFirstBattleResult(bool victory)
        {
            if (PlayerPrefs.GetInt(FirstBattleStartedKey, 0) != 1 ||
                PlayerPrefs.GetInt(FirstBattleResultKey, 0) == 1)
            {
                return;
            }

            PlayerPrefs.SetInt(FirstBattleResultKey, 1);
            PlayerPrefs.Save();
            LogScene(victory ? AnalyticsSceneIds.FirstBattleWin : AnalyticsSceneIds.FirstBattleLose);
        }

        public static void ClearLocalFlags()
        {
            PlayerPrefs.DeleteKey(OncePrefix + AnalyticsSceneIds.AppLaunch);
            PlayerPrefs.DeleteKey(FirstBattleStartedKey);
            PlayerPrefs.DeleteKey(FirstBattleResultKey);
            PlayerPrefs.Save();
            SessionReportedScenes.Clear();
        }

        private static AnalyticsManager EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var go = new GameObject("[AnalyticsManager]");
            Instance = go.AddComponent<AnalyticsManager>();
            return Instance;
        }

        private static void EnsureUserId()
        {
            if (!string.IsNullOrEmpty(UserId))
            {
                return;
            }

            UserId = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            if (!string.IsNullOrEmpty(UserId))
            {
                return;
            }

            UserId = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(PlayerIdKey, UserId);
            PlayerPrefs.Save();
        }

        private void ReportSceneInternal(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey))
            {
                return;
            }

            EnsureUserId();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameLogger.Log($"[Analytics] LogScene: {sceneKey}");
#endif

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && (WEIXINMINIGAME || UNITY_WEBGL)
            if (!BranchConfigMap.TryGetValue(sceneKey, out BranchAnalyticsConfig config) ||
                string.IsNullOrEmpty(config.BranchId))
            {
                GameLogger.LogWarning($"[Analytics] 未配置微信 branchId，跳过正式上报: {sceneKey}");
                return;
            }

            try
            {
                WX.ReportUserBehaviorBranchAnalytics(new ReportUserBehaviorBranchAnalyticsOption
                {
                    branchId = config.BranchId,
                    eventType = config.EventType,
                    branchDim = string.Empty
                });
            }
            catch (System.Exception e)
            {
                GameLogger.LogWarning($"[Analytics] WX.ReportUserBehaviorBranchAnalytics 上报失败: {sceneKey}, {e.Message}");
            }
#endif
        }

        private readonly struct BranchAnalyticsConfig
        {
            public readonly string BranchId;
            public readonly int EventType;

            public BranchAnalyticsConfig(string branchId, int eventType)
            {
                BranchId = branchId;
                EventType = eventType;
            }
        }
    }
}
