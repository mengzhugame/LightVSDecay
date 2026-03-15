// GameLogger.cs - 全局日志管理器
// 用途：统一管理所有Debug.Log输出，支持一键开关
// 使用方法：
// 1. 开发时保留 ENABLE_LOG = true
// 2. 打包时改为 ENABLE_LOG = false
// 3. 代码中使用 GameLogger.Log() 替代 Debug.Log()

using UnityEngine;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 全局日志管理器（兼容版）
    /// 功能：
    /// 1. 使用 const bool 控制日志输出，编译期零性能开销
    /// 2. 统一日志格式，方便调试和追踪
    /// </summary>
    public static class GameLogger
    {
        // ==================== 🔧 日志开关（打包时改为 false） ====================
        private const bool ENABLE_LOG = true;  // ← 开发时 true，打包时 false
        // ==========================================================================

        // ==================== 普通日志 ====================

        /// <summary>普通日志（白色）</summary>
        public static void Log(string message)
        {
            if (ENABLE_LOG)
            {
                Debug.Log(message);
            }
        }

        /// <summary>带标签的日志</summary>
        public static void Log(string tag, string message)
        {
            if (ENABLE_LOG)
            {
                Debug.Log($"[{tag}] {message}");
            }
        }

        /// <summary>带颜色的日志</summary>
        public static void LogColor(string message, string color)
        {
            if (ENABLE_LOG)
            {
                Debug.Log($"<color={color}>{message}</color>");
            }
        }

        // ==================== 警告日志 ====================

        /// <summary>警告日志（黄色）- 总是输出</summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>带标签的警告日志</summary>
        public static void LogWarning(string tag, string message)
        {
            Debug.LogWarning($"[{tag}] {message}");
        }

        // ==================== 错误日志 ====================

        /// <summary>错误日志（红色）- 总是输出</summary>
        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        /// <summary>带标签的错误日志</summary>
        public static void LogError(string tag, string message)
        {
            Debug.LogError($"[{tag}] {message}");
        }

        // ==================== 性能日志 ====================

        /// <summary>性能相关日志（青色）- 用于性能分析</summary>
        public static void LogPerformance(string message)
        {
            if (ENABLE_LOG)
            {
                Debug.Log($"<color=cyan>[Performance] {message}</color>");
            }
        }

        /// <summary>带计时的性能日志</summary>
        public static void LogPerformance(string tag, float timeMs)
        {
            if (ENABLE_LOG)
            {
                Debug.Log($"<color=cyan>[Performance] {tag}: {timeMs:F2}ms</color>");
            }
        }
    }
}
