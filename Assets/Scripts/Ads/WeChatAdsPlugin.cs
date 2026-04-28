using System;
using System.Collections;
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
        private const float ShowTimeoutSeconds = 180f;
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

#if !UNITY_EDITOR && UNITY_WEBGL
        private readonly WXRewardedVideoAd[] rewardedAds = new WXRewardedVideoAd[AdUnitIds.Length];
        private readonly Action[] pendingSuccessCallbacks = new Action[AdUnitIds.Length];
        private readonly Action[] pendingFailCallbacks = new Action[AdUnitIds.Length];
        private readonly bool[] isShowing = new bool[AdUnitIds.Length];
        private readonly Coroutine[] showTimeoutCoroutines = new Coroutine[AdUnitIds.Length];
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

        public void PreloadAll()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            for (int i = 0; i < AdUnitIds.Length; i++)
            {
                int index = i;
                var ad = GetOrCreateAd(index);
                if (ad == null) continue;

                ad.Load(
                    success: _ => GameLogger.LogWarning($"[WeChatAdsPlugin] 预加载成功 idx={index}"),
                    failed: res => GameLogger.LogWarning($"[WeChatAdsPlugin] 预加载失败 idx={index}: {FormatAdError(res)}"));
            }
#endif
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
            if (isShowing[adTypeIndex])
            {
                GameLogger.LogWarning($"[WeChatAdsPlugin] 广告正在播放或加载中，忽略重复点击 idx={adTypeIndex}");
                onFail?.Invoke();
                return;
            }

            var ad = GetOrCreateAd(adTypeIndex);
            if (ad == null)
            {
                onFail?.Invoke();
                return;
            }

            pendingSuccessCallbacks[adTypeIndex] = onSuccess;
            pendingFailCallbacks[adTypeIndex] = onFail;
            isShowing[adTypeIndex] = true;
            StartShowTimeout(adTypeIndex);

            ShowAdInternal(adTypeIndex, allowReloadRetry: true);
#else
            GameLogger.Log($"[WeChatAdsPlugin] Editor 模拟：广告成功 idx={adTypeIndex}");
            onSuccess?.Invoke();
#endif
        }

#if !UNITY_EDITOR && UNITY_WEBGL
        private WXRewardedVideoAd GetOrCreateAd(int adTypeIndex)
        {
            if (rewardedAds[adTypeIndex] != null)
                return rewardedAds[adTypeIndex];

            string adUnitId = AdUnitIds[adTypeIndex];

            try
            {
                var ad = WX.CreateRewardedVideoAd(new WXCreateRewardedVideoAdParam
                {
                    adUnitId = adUnitId,
                    multiton = true
                });
                int index = adTypeIndex;

                ad.OnClose(res =>
                {
                    if (res == null || res.isEnded)
                        CompleteSuccess(index);
                    else
                        CompleteFail(index, "用户未完整观看广告");
                });

                ad.OnError(res =>
                {
                    string error = FormatAdError(res);
                    GameLogger.LogWarning($"[WeChatAdsPlugin] 广告组件错误 idx={index}: {error}");
                    if (isShowing[index])
                    {
                        CompleteFail(index, error);
                    }
                });

                rewardedAds[adTypeIndex] = ad;
                GameLogger.LogWarning($"[WeChatAdsPlugin] 广告实例创建成功 idx={adTypeIndex} id={adUnitId}");
                return ad;
            }
            catch (Exception e)
            {
                GameLogger.LogError($"[WeChatAdsPlugin] 创建广告实例异常 idx={adTypeIndex} id={adUnitId}: {e.Message}");
                return null;
            }
        }

        private void ShowAdInternal(int adTypeIndex, bool allowReloadRetry)
        {
            var ad = rewardedAds[adTypeIndex];
            if (ad == null)
            {
                CompleteFail(adTypeIndex, "广告实例为空");
                return;
            }

            ad.Show(
                success: _ => GameLogger.LogWarning($"[WeChatAdsPlugin] 广告开始展示 idx={adTypeIndex}"),
                failed: res =>
                {
                    GameLogger.LogWarning($"[WeChatAdsPlugin] Show 失败 idx={adTypeIndex}: {FormatTextError(res)}");

                    if (!allowReloadRetry)
                    {
                        CompleteFail(adTypeIndex, "Show 重试失败");
                        return;
                    }

                    ad.Load(
                        success: _ =>
                        {
                            GameLogger.LogWarning($"[WeChatAdsPlugin] Load 成功，重试 Show idx={adTypeIndex}");
                            ShowAdInternal(adTypeIndex, allowReloadRetry: false);
                        },
                        failed: loadRes =>
                        {
                            GameLogger.LogWarning($"[WeChatAdsPlugin] Load 失败 idx={adTypeIndex}: {FormatAdError(loadRes)}");
                            CompleteFail(adTypeIndex, "Load 失败");
                        });
                });
        }

        private void CompleteSuccess(int adTypeIndex)
        {
            if (!isShowing[adTypeIndex])
                return;

            var callback = pendingSuccessCallbacks[adTypeIndex];
            ClearPending(adTypeIndex);
            callback?.Invoke();
        }

        private void CompleteFail(int adTypeIndex, string reason)
        {
            if (!isShowing[adTypeIndex])
                return;

            GameLogger.LogWarning($"[WeChatAdsPlugin] 广告未完成 idx={adTypeIndex}: {reason}");
            var callback = pendingFailCallbacks[adTypeIndex];
            ClearPending(adTypeIndex);
            callback?.Invoke();
        }

        private void ClearPending(int adTypeIndex)
        {
            StopShowTimeout(adTypeIndex);
            isShowing[adTypeIndex] = false;
            pendingSuccessCallbacks[adTypeIndex] = null;
            pendingFailCallbacks[adTypeIndex] = null;
        }

        private void StartShowTimeout(int adTypeIndex)
        {
            StopShowTimeout(adTypeIndex);
            showTimeoutCoroutines[adTypeIndex] = StartCoroutine(ShowTimeoutRoutine(adTypeIndex));
        }

        private void StopShowTimeout(int adTypeIndex)
        {
            Coroutine routine = showTimeoutCoroutines[adTypeIndex];
            if (routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            showTimeoutCoroutines[adTypeIndex] = null;
        }

        private IEnumerator ShowTimeoutRoutine(int adTypeIndex)
        {
            yield return new WaitForSecondsRealtime(ShowTimeoutSeconds);

            if (isShowing[adTypeIndex])
            {
                CompleteFail(adTypeIndex, "广告关闭回调超时");
            }
        }

        private static string FormatTextError(WXTextResponse res)
        {
            return res == null ? "unknown" : string.IsNullOrEmpty(res.errMsg) ? "unknown" : res.errMsg;
        }

        private static string FormatAdError(WXADErrorResponse res)
        {
            if (res == null) return "unknown";
            string errMsg = string.IsNullOrEmpty(res.errMsg) ? "unknown" : res.errMsg;
            return res.errCode == 0 ? errMsg : $"{errMsg} ({res.errCode})";
        }
#endif
    }
}
