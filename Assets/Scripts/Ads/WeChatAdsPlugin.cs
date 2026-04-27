using System;
using System.Runtime.InteropServices;
using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Ads
{
    /// <summary>
    /// 微信小游戏激励视频广告 C# 桥接层。
    /// C# → JS 只传 int（AdType 枚举值），避免字符串指针在团结引擎中引发 ReferenceError。
    /// JS → Unity 回调通过 SendMessage("[WeChatAdsPlugin]", ...) 触发。
    /// </summary>
    public class WeChatAdsPlugin : MonoBehaviour
    {
        private static WeChatAdsPlugin instance;

        private Action pendingOnSuccess;
        private Action pendingOnFail;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WXAds_PreloadAll();

        [DllImport("__Internal")]
        private static extern void WXAds_ShowRewardedAd(int adTypeIndex);
#else
        private static void WXAds_PreloadAll() { }
        private static void WXAds_ShowRewardedAd(int adTypeIndex) { }
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
        }

        /// <summary>游戏启动后预加载全部广告位。</summary>
        public void PreloadAll()
        {
            WXAds_PreloadAll();
        }

        /// <summary>
        /// 展示激励视频广告。
        /// adTypeIndex = (int)AdType 枚举值，与 jslib 中 _wxAdUnitIds 数组下标对应。
        /// </summary>
        public void ShowAd(int adTypeIndex, Action onSuccess, Action onFail)
        {
            pendingOnSuccess = onSuccess;
            pendingOnFail    = onFail;
            WXAds_ShowRewardedAd(adTypeIndex);
        }

        // ── JS → Unity 回调 ────────────────────────────────────

        // 消息格式: "adTypeIndex|1"（完整看完）或 "adTypeIndex|0"（中途跳过）
        private void OnAdClosed(string message)
        {
            string[] parts = message.Split('|');
            bool completed = parts.Length > 1 && parts[1] == "1";
            if (completed)
                pendingOnSuccess?.Invoke();
            else
                pendingOnFail?.Invoke();
            pendingOnSuccess = null;
            pendingOnFail    = null;
        }

        // adTypeIndex（字符串形式）
        private void OnAdFailed(string adTypeIndexStr)
        {
            GameLogger.LogWarning("[WeChatAdsPlugin] 广告失败 adTypeIndex=" + adTypeIndexStr);
            pendingOnFail?.Invoke();
            pendingOnSuccess = null;
            pendingOnFail    = null;
        }
    }
}
