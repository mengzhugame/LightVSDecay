// WeChatAds.jslib
// 微信小游戏激励视频广告 JS Bridge
// Unity C# 通过 DllImport 调用这里的函数，JS 回调通过 SendMessage 返回 Unity

var _wxAdCallbackObject = '';
var _wxRewardedAds = {};

var WeChatAdsLib = {

    WXAds_Init: function (callbackObjectPtr) {
        _wxAdCallbackObject = UTF8ToString(callbackObjectPtr);
    },

    // 预加载一个广告位实例，游戏启动时批量调用
    WXAds_PreloadAd: function (adUnitIdPtr) {
        if (typeof wx === 'undefined') return;
        var adUnitId = UTF8ToString(adUnitIdPtr);
        if (_wxRewardedAds[adUnitId]) return;

        var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });

        // 用 IIFE 捕获 adUnitId，避免闭包引用问题
        (function (id) {
            ad.onClose(function (res) {
                // res.isEnded = true 表示完整观看，false 表示中途跳过
                var isEnded = (res && res.isEnded) ? '1' : '0';
                if (_wxAdCallbackObject) {
                    SendMessage(_wxAdCallbackObject, 'OnAdClosed', id + '|' + isEnded);
                }
            });
            ad.onError(function (err) {
                if (_wxAdCallbackObject) {
                    SendMessage(_wxAdCallbackObject, 'OnAdFailed', id);
                }
            });
        })(adUnitId);

        _wxRewardedAds[adUnitId] = ad;
    },

    // 展示一个广告位，失败时自动 reload 重试一次
    WXAds_ShowRewardedAd: function (adUnitIdPtr) {
        if (typeof wx === 'undefined') return;
        var adUnitId = UTF8ToString(adUnitIdPtr);
        var ad = _wxRewardedAds[adUnitId];

        if (!ad) {
            if (_wxAdCallbackObject) {
                SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
            }
            return;
        }

        ad.show().catch(function () {
            ad.load()
                .then(function () { return ad.show(); })
                .catch(function () {
                    if (_wxAdCallbackObject) {
                        SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
                    }
                });
        });
    }
};

mergeInto(LibraryManager.library, WeChatAdsLib);
