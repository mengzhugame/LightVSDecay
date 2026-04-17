// ============================================================
// HapticFeedback.cs
// 文件位置: Assets/Scripts/Core/HapticFeedback.cs
// 用途：手机震动（触感反馈）跨平台工具类
//   - Android：通过 AndroidJavaObject 精确控制震动时长
//   - iOS：Handheld.Vibrate()（系统决定时长，单次震动）
//   - Editor：仅打印日志，不震动
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 震动强度预设（值 = 毫秒）
    /// </summary>
    public enum HapticType
    {
        Light    = 100,
        Medium   = 200,
        Heavy    = 400,
        Critical = 600,
    }

    /// <summary>
    /// 手机触感反馈单例
    /// 内置节流机制，防止同一事件短时间内重复震动
    /// </summary>
    public class HapticFeedback : AutoSingleton<HapticFeedback>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("全局开关")]
        [Tooltip("是否启用手机震动（可供玩家在设置中关闭）")]
        [SerializeField] private bool hapticEnabled = true;

        private const string PREF_HAPTIC_ENABLED = "HapticEnabled";

        [Header("调试")]
        [SerializeField] private bool showDebugLog = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 节流：记录每个 throttleKey 上次触发时间
        private readonly Dictionary<string, float> _lastTriggerTime = new Dictionary<string, float>();

        // Android 振动器缓存
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _androidSdkVersion;
#endif

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void Awake()
        {
            base.Awake();

            // 从 PlayerPrefs 恢复玩家上次的震动开关状态
            hapticEnabled = PlayerPrefs.GetInt(PREF_HAPTIC_ENABLED, 1) == 1;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _androidSdkVersion = new AndroidJavaClass("android.os.Build$VERSION")
                                         .GetStatic<int>("SDK_INT");
            }
            catch (System.Exception e)
            {
                GameLogger.LogWarning($"[HapticFeedback] 无法获取 Android Vibrator: {e.Message}");
            }
#endif
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 触发震动（带节流控制）
        /// </summary>
        /// <param name="type">震动预设</param>
        /// <param name="throttleKey">节流 Key，同 Key 在 throttleInterval 内不重复震动（留空=不节流）</param>
        /// <param name="throttleInterval">节流间隔（秒），默认 0.5s</param>
        public void Trigger(HapticType type, string throttleKey = "", float throttleInterval = 0.5f)
        {
            if (!hapticEnabled) return;

            // 节流检查
            if (!string.IsNullOrEmpty(throttleKey))
            {
                float now = Time.unscaledTime;
                if (_lastTriggerTime.TryGetValue(throttleKey, out float lastTime))
                {
                    if (now - lastTime < throttleInterval) return;
                }
                _lastTriggerTime[throttleKey] = now;
            }

            TriggerRaw((int)type);
        }

        /// <summary>
        /// 直接按毫秒触发震动（不节流）
        /// </summary>
        public void TriggerRaw(int durationMs)
        {
            if (!hapticEnabled) return;
            if (durationMs <= 0) return;

            if (showDebugLog)
                GameLogger.Log($"[HapticFeedback] 震动 {durationMs}ms");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_vibrator == null) return;
                if (_androidSdkVersion >= 26)
                {
                    // Android 8+（API 26+）：使用 VibrationEffect，避免小米 HyperOS 对旧接口的 no-op
                    using var vfxClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect   = vfxClass.CallStatic<AndroidJavaObject>(
                                             "createOneShot", (long)durationMs, -1); // -1 = DEFAULT_AMPLITUDE
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    // Android 7 及以下旧设备：使用旧接口兼容
                    _vibrator.Call("vibrate", (long)durationMs);
                }
            }
            catch (System.Exception e)
            {
                GameLogger.LogWarning($"[HapticFeedback] Android 震动调用失败: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#else
            // Editor 模式：仅日志，不震动
            if (!showDebugLog)
                GameLogger.Log($"[HapticFeedback][Editor] 模拟震动 {durationMs}ms（真机才会震）");
#endif
        }

        /// <summary>
        /// 清除指定 Key 的节流记录（用于重置同一事件的节流状态）
        /// </summary>
        public void ClearThrottle(string throttleKey)
        {
            _lastTriggerTime.Remove(throttleKey);
        }

        /// <summary>
        /// 全局启用/禁用（供设置界面调用），自动持久化到 PlayerPrefs
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            hapticEnabled = enabled;
            PlayerPrefs.SetInt(PREF_HAPTIC_ENABLED, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool IsEnabled => hapticEnabled;
    }
}
