// WeChatAds.jslib  —— 微信小游戏激励视频广告 JS Bridge
// 注意：wx.createRewardedVideoAd 在微信开发者工具【模拟器】中不可用，
//       必须使用【真机调试】或【体验版】才能看到真实广告。

var _wxAdCallbackObject = '';
var _wxRewardedAds = {};

// 内部辅助：创建广告实例并绑定回调，成功返回 ad 对象，失败返回 null
function _wxCreateAd(adUnitId) {
    if (typeof wx === 'undefined' || typeof wx.createRewardedVideoAd !== 'function') {
        console.warn('[WeChatAds] wx.createRewardedVideoAd 不可用（可能是模拟器环境）');
        return null;
    }
    try {
        var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });
        var id = adUnitId; // 闭包捕获
        ad.onClose(function (res) {
            try {
                var isEnded = (res && res.isEnded) ? '1' : '0';
                if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                    SendMessage(_wxAdCallbackObject, 'OnAdClosed', id + '|' + isEnded);
                }
            } catch (e) { console.error('[WeChatAds] onClose 回调异常:', e); }
        });
        ad.onError(function (err) {
            try {
                var msg = err ? (err.errMsg || err.errCode || JSON.stringify(err)) : 'unknown';
                console.warn('[WeChatAds] 广告错误 [' + id + ']:', msg);
                if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                    SendMessage(_wxAdCallbackObject, 'OnAdFailed', id);
                }
            } catch (e) { console.error('[WeChatAds] onError 回调异常:', e); }
        });
        _wxRewardedAds[adUnitId] = ad;
        return ad;
    } catch (e) {
        var errMsg = e ? (e.errMsg || e.message || String(e)) : 'unknown error';
        console.error('[WeChatAds] createRewardedVideoAd 失败 [' + adUnitId + ']:', errMsg);
        return null;
    }
}

var WeChatAdsLib = {

    WXAds_Init: function (callbackObjectPtr) {
        try {
            _wxAdCallbackObject = UTF8ToString(callbackObjectPtr);
            console.log('[WeChatAds] 初始化完成，回调对象:', _wxAdCallbackObject);
        } catch (e) { console.error('[WeChatAds] Init 异常:', e); }
    },

    // 预加载广告（游戏启动后延迟调用），失败不影响后续 Show 时的重试
    WXAds_PreloadAd: function (adUnitIdPtr) {
        try {
            var adUnitId = UTF8ToString(adUnitIdPtr);
            if (!adUnitId || _wxRewardedAds[adUnitId]) return;
            var ad = _wxCreateAd(adUnitId);
            if (ad) {
                console.log('[WeChatAds] 预加载成功:', adUnitId);
            }
        } catch (e) { console.error('[WeChatAds] PreloadAd 异常:', e); }
    },

    // 展示广告：若预加载失败会尝试即时创建，之后 load + show
    WXAds_ShowRewardedAd: function (adUnitIdPtr) {
        try {
            if (typeof wx === 'undefined') {
                console.warn('[WeChatAds] wx 未定义，无法展示广告');
                return;
            }
            var adUnitId = UTF8ToString(adUnitIdPtr);

            // 取缓存实例，若预加载时失败则即时创建
            var ad = _wxRewardedAds[adUnitId] || _wxCreateAd(adUnitId);
            if (!ad) {
                console.warn('[WeChatAds] 无法获取广告实例:', adUnitId);
                if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                    SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
                }
                return;
            }

            // show → 失败时 reload 再 show
            ad.show().catch(function () {
                console.log('[WeChatAds] 首次 show 失败，尝试 reload:', adUnitId);
                ad.load()
                    .then(function () { return ad.show(); })
                    .catch(function (err) {
                        var msg = err ? (err.errMsg || err.errCode || String(err)) : 'unknown';
                        console.error('[WeChatAds] reload 后 show 仍失败 [' + adUnitId + ']:', msg);
                        try {
                            if (_wxAdCallbackObject && typeof SendMessage !== 'undefined') {
                                SendMessage(_wxAdCallbackObject, 'OnAdFailed', adUnitId);
                            }
                        } catch (e2) { console.error('[WeChatAds] SendMessage 异常:', e2); }
                    });
            });
        } catch (e) { console.error('[WeChatAds] ShowRewardedAd 异常:', e); }
    }
};

mergeInto(LibraryManager.library, WeChatAdsLib);
