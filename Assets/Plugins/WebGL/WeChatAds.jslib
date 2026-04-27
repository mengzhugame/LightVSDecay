// WeChatAds.jslib — 微信小游戏激励视频广告 JS Bridge
//
// ★ 关键设计：C# 侧只传 int（AdType 枚举值），不传任何字符串指针。
//   原因：团结引擎的 WebGL 模块中 UTF8ToString 不是全局函数，
//         在 jslib 里直接调用会抛 ReferenceError。
//
// 广告位 ID 数组下标 = AdType 枚举值（顺序与 AdType.cs 严格对应）
//   0: SkillReroll      1: SettlementDouble  2: Revive
//   3: EnergyTopUp      4: GoldTopUp         5: BlueprintTopUp
var _wxAdUnitIds = [
    'adunit-56499238b85e3417',
    'adunit-94a01d74e70d5aac',
    'adunit-b3a26c96dd754c35',
    'adunit-5f870c74e9f253e6',
    'adunit-c1439995ee6715f5',
    'adunit-701888590bd10662'
];

// 与 WeChatAdsPlugin.cs 中 new GameObject("[WeChatAdsPlugin]") 名称一致
var _wxCallbackObj = '[WeChatAdsPlugin]';
var _wxAds = {};  // adTypeIndex -> ad 实例

// 内部：创建并缓存指定类型的广告实例
function _wxGetOrCreateAd(adTypeIndex) {
    if (_wxAds[adTypeIndex]) return _wxAds[adTypeIndex];

    var adUnitId = _wxAdUnitIds[adTypeIndex];
    if (!adUnitId) {
        console.error('[WeChatAds] 无效广告类型 index=' + adTypeIndex);
        return null;
    }
    if (typeof wx === 'undefined' || typeof wx.createRewardedVideoAd !== 'function') {
        console.warn('[WeChatAds] wx.createRewardedVideoAd 不可用（模拟器不支持广告，请用真机调试）');
        return null;
    }

    try {
        var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });
        var idx = adTypeIndex;  // 闭包捕获

        ad.onClose(function (res) {
            var isEnded = (res && res.isEnded) ? '1' : '0';
            try {
                SendMessage(_wxCallbackObj, 'OnAdClosed', idx + '|' + isEnded);
            } catch (e) {
                console.error('[WeChatAds] onClose SendMessage 异常:', e);
            }
        });

        ad.onError(function (err) {
            var msg = err ? (err.errMsg || err.errCode || JSON.stringify(err)) : 'unknown';
            console.warn('[WeChatAds] 广告错误 [idx=' + idx + '] ' + msg);
            try {
                SendMessage(_wxCallbackObj, 'OnAdFailed', '' + idx);
            } catch (e) {
                console.error('[WeChatAds] onError SendMessage 异常:', e);
            }
        });

        _wxAds[adTypeIndex] = ad;
        console.log('[WeChatAds] 广告实例创建成功 idx=' + adTypeIndex + ' id=' + adUnitId);
        return ad;
    } catch (e) {
        var msg = e ? (e.errMsg || e.message || String(e)) : 'unknown';
        console.error('[WeChatAds] createRewardedVideoAd 失败 idx=' + adTypeIndex + ' : ' + msg);
        return null;
    }
}

var WeChatAdsLib = {

    // 预加载全部 6 个广告位（无参数，无字符串指针）
    WXAds_PreloadAll: function () {
        try {
            for (var i = 0; i < _wxAdUnitIds.length; i++) {
                _wxGetOrCreateAd(i);
            }
        } catch (e) {
            console.error('[WeChatAds] PreloadAll 异常:', e ? (e.message || String(e)) : '');
        }
    },

    // 展示广告，adTypeIndex 与 AdType 枚举值对应（int，无字符串指针）
    WXAds_ShowRewardedAd: function (adTypeIndex) {
        try {
            var ad = _wxGetOrCreateAd(adTypeIndex);
            if (!ad) {
                try { SendMessage(_wxCallbackObj, 'OnAdFailed', '' + adTypeIndex); } catch (e2) {}
                return;
            }

            // 官方推荐写法：show → 失败则 load 再 show
            ad.show().catch(function () {
                console.log('[WeChatAds] show 失败，尝试 reload idx=' + adTypeIndex);
                ad.load()
                    .then(function () { return ad.show(); })
                    .catch(function (err) {
                        var msg = err ? (err.errMsg || err.errCode || String(err)) : 'unknown';
                        console.error('[WeChatAds] reload 后仍失败 idx=' + adTypeIndex + ' : ' + msg);
                        try { SendMessage(_wxCallbackObj, 'OnAdFailed', '' + adTypeIndex); } catch (e2) {}
                    });
            });
        } catch (e) {
            var msg = e ? (e.message || String(e)) : 'unknown';
            console.error('[WeChatAds] ShowRewardedAd 异常 idx=' + adTypeIndex + ' : ' + msg);
        }
    }
};

mergeInto(LibraryManager.library, WeChatAdsLib);
