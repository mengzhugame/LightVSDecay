// WeChatAds.jslib
// 微信小游戏激励视频广告 JS Bridge
// 所有 wx API 调用均包裹 try-catch，防止异常污染 WebGL 渲染上下文

var _wxAdCallbackObject = '';
var _wxRewardedAds = {};

var WeChatAdsLib = {

    WXAds_Init: function (callbackObjectPtr) {
        try {
            _wxAdCallbackObject = UTF8ToString(callbackObjectPtr);
        } catch (e) {
            console.error('[WeChatAds] WXAds_Init error:', e);
        }
    },

    // 预加载广告实例，游戏启动后延迟一帧调用
    WXAds_PreloadAd: function (adUnitIdPtr) {
        try {
            if (typeof wx === 'undefined') return;
            var adUnitId = UTF8ToString(adUnitIdPtr);
            if (!adUnitId || _wxRewardedAds[adUnitId]) return;

            var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });

            // IIFE 捕获 adUnitId，避免闭包引用同一变量
            (function (id) {
                ad.onClose(function (res) {
                    try {
                        var isEnded = (res && res.isEnded) ? '1' : '0';
                        if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                            SendMessage(_wxAdCallbackObject, 'OnAdClosed', id + '|' + isEnded);
                        }
                    } catch (e) {
                        console.error('[WeChatAds] onClose callback error:', e);
                    }
                });
                ad.onError(function (err) {
                    try {
                        console.warn('[WeChatAds] ad error for', id, err);
                        if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                            SendMessage(_wxAdCallbackObject, 'OnAdFailed', id);
                        }
                    } catch (e) {
                        console.error('[WeChatAds] onError callback error:', e);
                    }
                });
            })(adUnitId);

            _wxRewardedAds[adUnitId] = ad;
        } catch (e) {
            console.error('[WeChatAds] WXAds_PreloadAd error:', e);
        }
    },

    // 展示广告，失败时自动 reload 重试一次
    WXAds_ShowRewardedAd: function (adUnitIdPtr) {
        try {
            if (typeof wx === 'undefined') return;
            var adUnitId = UTF8ToString(adUnitIdPtr);
            var ad = _wxRewardedAds[adUnitId];

            if (!ad) {
                console.warn('[WeChatAds] ad not preloaded:', adUnitId);
                if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                    SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
                }
                return;
            }

            ad.show().catch(function () {
                ad.load()
                    .then(function () { return ad.show(); })
                    .catch(function (err) {
                        console.error('[WeChatAds] show failed after reload:', err);
                        try {
                            if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                                SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
                            }
                        } catch (e) {
                            console.error('[WeChatAds] SendMessage error:', e);
                        }
                    });
            });
        } catch (e) {
            console.error('[WeChatAds] WXAds_ShowRewardedAd error:', e);
        }
    }
};

mergeInto(LibraryManager.library, WeChatAdsLib);
