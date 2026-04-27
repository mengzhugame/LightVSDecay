using System;
using System.Runtime.InteropServices;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Ads
{
    /// <summary>
    /// 微信小游戏激励视频广告 C# 桥接层。
    /// 在 WebGL 构建时调用 WeChatAds.jslib 中的函数；
    /// 在 Editor / 非 WebGL 平台时所有调用为空操作（由 AdManager 的 usePlaceholderAds 控制）。
    /// JS 回调通过 SendMessage(gameObject.name, ...) 返回此组件。
    /// </summary>
    public class WeChatAdsPlugin : MonoBehaviour
    {
        private static WeChatAdsPlugin instance;

        private Action pendingOnSuccess;
        private Action pendingOnFail;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WXAds_Init(string callbackObject);

        [DllImport("__Internal")]
        private static extern void WXAds_PreloadAd(string adUnitId);

        [DllImport("__Internal")]
        private static extern void WXAds_ShowRewardedAd(string adUnitId);
#else
        private static void WXAds_Init(string callbackObject) { }
        private static void WXAds_PreloadAd(string adUnitId) { }
        private static void WXAds_ShowRewardedAd(string adUnitId) { }
#endif

        public static WeChatAdsPlugin Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("[WeChatAdsPlugin]");
                instance = go.AddComponent<WeChatAdsPlugin>();
                DontDestroyOnLoad(go);
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            WXAds_Init(gameObject.name);
        }

        /// <summary>
        /// 预加载广告实例（游戏启动时批量调用，确保首次展示流畅）。
        /// </summary>
        public void PreloadAd(string adUnitId)
        {
            if (string.IsNullOrWhiteSpace(adUnitId)) return;
            WXAds_PreloadAd(adUnitId);
        }

        /// <summary>
        /// 展示激励视频广告，完整观看后回调 onSuccess，中途跳过或失败回调 onFail。
        /// </summary>
        public void ShowAd(string adUnitId, Action onSuccess, Action onFail)
        {
            if (string.IsNullOrWhiteSpace(adUnitId))
            {
                onFail?.Invoke();
                return;
            }
            pendingOnSuccess = onSuccess;
            pendingOnFail = onFail;
            WXAds_ShowRewardedAd(adUnitId);
        }

        // ── JS → Unity 回调（由 SendMessage 触发）──────────────

        // 消息格式: "adUnitId|1" (1=完整观看) 或 "adUnitId|0" (中途跳过)
        private void OnAdClosed(string message)
        {
            string[] parts = message.Split('|');
            bool completed = parts.Length > 1 && parts[1] == "1";
            if (completed)
                pendingOnSuccess?.Invoke();
            else
                pendingOnFail?.Invoke();

            pendingOnSuccess = null;
            pendingOnFail = null;
        }

        // adUnitId 加载或展示出错
        private void OnAdFailed(string adUnitId)
        {
            GameLogger.LogWarning($"[WeChatAdsPlugin] 广告加载/展示失败: {adUnitId}");
            pendingOnFail?.Invoke();
            pendingOnSuccess = null;
            pendingOnFail = null;
        }
    }
}
