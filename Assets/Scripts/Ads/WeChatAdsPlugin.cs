using System;
using UnityEngine;
using LightVsDecay.Core;
#if !UNITY_EDITOR && UNITY_WEBGL
using WeChatWASM;
#endif

namespace LightVsDecay.Ads
{
    /// <summary>
    /// 微信小游戏激励视频广告桥接层。
    /// 使用 WeChatWASM SDK（WX.CreateRewardedVideoAd），无需自定义 jslib。
    /// </summary>
    public class WeChatAdsPlugin : MonoBehaviour
    {
        private static WeChatAdsPlugin instance;

        private static readonly string[] AdUnitIds =
        {
            "adunit-56499238b85e3417",  // 0: SkillReroll
            "adunit-94a01d74e70d5aac",  // 1: SettlementDouble
            "adunit-b3a26c96dd754c35",  // 2: Revive
            "adunit-5f870c74e9f253e6",  // 3: EnergyTopUp
            "adunit-c1439995ee6715f5",  // 4: GoldTopUp
            "adunit-701888590bd10662"   // 5: BlueprintTopUp
        };

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

        public void PreloadAll()
        {
            // WeChatWASM SDK 在 Show() 时自动处理加载，无需单独预加载
        }

        public void ShowAd(int adTypeIndex, Action onSuccess, Action onFail)
        {
            if (adTypeIndex < 0 || adTypeIndex >= AdUnitIds.Length)
            {
                GameLogger.LogWarning($"[WeChatAdsPlugin] 无效广告类型 idx={adTypeIndex}");
                onFail?.Invoke();
                return;
            }

#if !UNITY_EDITOR && UNITY_WEBGL
            string adUnitId = AdUnitIds[adTypeIndex];
            var ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam { adUnitId = adUnitId });

            ad.OnClose((res) =>
            {
                ad.OffClose(null);
                ad.OffError(null);
                if (res.isEnded)
                    onSuccess?.Invoke();
                else
                    onFail?.Invoke();
            });

            ad.OnError((res) =>
            {
                GameLogger.LogWarning($"[WeChatAdsPlugin] 广告失败 idx={adTypeIndex}: {res.errMsg}");
                ad.OffClose(null);
                ad.OffError(null);
                onFail?.Invoke();
            });

            ad.Show();
#else
            GameLogger.Log($"[WeChatAdsPlugin] Editor 模拟：广告成功 idx={adTypeIndex}");
            onSuccess?.Invoke();
#endif
        }
    }
}
