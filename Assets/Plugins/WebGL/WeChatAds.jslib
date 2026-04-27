// WeChatAds.jslib — 微信小游戏激励视频广告 JS Bridge
//
// ★ 架构说明：使用 IIFE 模块模式。
//   团结引擎的 Emscripten DCE 会消除 mergeInto 以外的顶层符号，
//   所有私有变量和辅助函数必须在 IIFE 闭包内定义才能被导出函数访问。
//
// 广告位 ID 数组下标 = AdType 枚举值（与 AdType.cs 严格对应）：
//   0: SkillReroll      1: SettlementDouble  2: Revive
//   3: EnergyTopUp      4: GoldTopUp         5: BlueprintTopUp

var WeChatAdsLib = (function () {

    // ── 私有状态（全部在 IIFE 闭包内，DCE 不可见）────────────
    var callbackObj = '[WeChatAdsPlugin]';  // 与 WeChatAdsPlugin.cs 中 GameObject 名一致
    var adUnitIds = [
        'adunit-56499238b85e3417',
        'adunit-94a01d74e70d5aac',
        'adunit-b3a26c96dd754c35',
        'adunit-5f870c74e9f253e6',
        'adunit-c1439995ee6715f5',
        'adunit-701888590bd10662'
    ];
    var ads = {};  // adTypeIndex → wx RewardedVideoAd 实例（预加载缓存）

    // ── 私有辅助：创建或取缓存的广告实例 ─────────────────────
    function createAd(adTypeIndex) {
        if (ads[adTypeIndex]) return ads[adTypeIndex];

        var adUnitId = adUnitIds[adTypeIndex];
        if (!adUnitId) {
            console.error('[WeChatAds] 无效广告类型 idx=' + adTypeIndex);
            return null;
        }
        if (typeof wx === 'undefined' || typeof wx.createRewardedVideoAd !== 'function') {
            console.warn('[WeChatAds] wx.createRewardedVideoAd 不可用（模拟器不支持真实广告）');
            return null;
        }

        try {
            var ad = wx.createRewardedVideoAd({ adUnitId: adUnitId });
            var idx = adTypeIndex;  // 闭包捕获，防止循环变量共用

            ad.onClose(function (res) {
                var isEnded = (res && res.isEnded) ? '1' : '0';
                try { SendMessage(callbackObj, 'OnAdClosed', idx + '|' + isEnded); }
                catch (e) { console.error('[WeChatAds] onClose SendMessage 异常:', e); }
            });

            ad.onError(function (err) {
                var msg = err ? (err.errMsg || err.errCode || JSON.stringify(err)) : 'unknown';
                console.warn('[WeChatAds] 广告错误 idx=' + idx + ' : ' + msg);
                try { SendMessage(callbackObj, 'OnAdFailed', '' + idx); }
                catch (e) { console.error('[WeChatAds] onError SendMessage 异常:', e); }
            });

            ads[adTypeIndex] = ad;
            console.log('[WeChatAds] 广告实例创建成功 idx=' + adTypeIndex + ' id=' + adUnitId);
            return ad;
        } catch (e) {
            var msg = e ? (e.errMsg || e.message || String(e)) : 'unknown';
            console.error('[WeChatAds] createRewardedVideoAd 失败 idx=' + adTypeIndex + ' : ' + msg);
            return null;
        }
    }

    // ── 公共导出（通过闭包访问上方所有私有符号）───────────────
    return {

        // 预加载全部广告位（游戏启动后延迟一帧调用）
        WXAds_PreloadAll: function () {
            try {
                for (var i = 0; i < adUnitIds.length; i++) {
                    createAd(i);
                }
            } catch (e) {
                console.error('[WeChatAds] PreloadAll 异常:', e ? (e.message || String(e)) : '');
            }
        },

        // 展示激励广告，adTypeIndex 与 AdType 枚举值对应
        WXAds_ShowRewardedAd: function (adTypeIndex) {
            try {
                var ad = createAd(adTypeIndex);
                if (!ad) {
                    try { SendMessage(callbackObj, 'OnAdFailed', '' + adTypeIndex); } catch (e2) {}
                    return;
                }

                // 官方推荐：show → 失败则 load 再 show
                ad.show().catch(function () {
                    console.log('[WeChatAds] show 失败，reload 重试 idx=' + adTypeIndex);
                    ad.load()
                        .then(function () { return ad.show(); })
                        .catch(function (err) {
                            var msg = err ? (err.errMsg || err.errCode || String(err)) : 'unknown';
                            console.error('[WeChatAds] reload 后仍失败 idx=' + adTypeIndex + ' : ' + msg);
                            try { SendMessage(callbackObj, 'OnAdFailed', '' + adTypeIndex); } catch (e2) {}
                        });
                });
            } catch (e) {
                console.error('[WeChatAds] ShowRewardedAd 异常 idx=' + adTypeIndex + ' : ' + (e ? (e.message || String(e)) : ''));
            }
        }

    };

})();

mergeInto(LibraryManager.library, WeChatAdsLib);
