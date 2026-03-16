// ============================================================
// SafeSceneLoader.cs
// 文件位置: Assets/Scripts/Core/SafeSceneLoader.cs
// 用途：安全异步场景加载工具（专为微信小游戏/WebGL优化）
// 使用方法：StartCoroutine(SafeSceneLoader.LoadSceneSafe("SceneName"))
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LightVsDecay.Core
{
    /// <summary>
    /// 安全场景加载器（静态工具类）
    /// 核心策略：
    /// 1. 分段式加载 - 关键步骤间插入等待帧，防止WebGL销毁时序崩溃
    /// 2. 主动GC + 卸载资源 - 防止微信小游戏内存峰值
    /// 3. 超时保护 - 避免加载卡死
    /// 4. 异常捕获 - 报告并安全退出
    /// </summary>
    public static class SafeSceneLoader
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private const float PRE_LOAD_WAIT    = 0.2f;   // 加载前GC等待（秒）
        private const float ACTIVATION_WAIT  = 0.3f;   // 场景激活后等待（秒）
        private const float POST_LOAD_WAIT   = 0.5f;   // 最终稳定等待（秒）
        private const int   GC_FRAME_INTERVAL = 3;     // GC后额外等待帧数
        private const int   MAX_WAIT_FRAMES  = 300;    // 超时保护帧数（约5秒@60fps）

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 安全异步加载场景（供 MonoBehaviour.StartCoroutine 调用）
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        public static IEnumerator LoadSceneSafe(string sceneName)
        {
            GameLogger.Log($"[SafeSceneLoader] 开始安全加载场景: {sceneName}");

            // ========== 阶段1: 加载前准备（GC + 卸载资源）==========
            yield return PreLoadPhase();

            // ========== 阶段2: 异步加载场景 ==========
            AsyncOperation asyncOp = null;
            yield return LoadScenePhase(sceneName, op => asyncOp = op);

            if (asyncOp == null)
            {
                GameLogger.LogError($"[SafeSceneLoader] 场景 {sceneName} 加载失败，终止流程");
                yield break;
            }

            // ========== 阶段3: 等待场景激活完成 ==========
            yield return WaitForActivationPhase(asyncOp);

            // ========== 阶段4: 激活后稳定等待 ==========
            yield return PostActivationPhase();

            // ========== 阶段5: 额外帧等待（让各Manager完成Awake/Start）==========
            for (int i = 0; i < 5; i++) yield return null;

            // ========== 阶段6: 最终稳定 ==========
            yield return FinalStabilizePhase();

            GameLogger.Log($"[SafeSceneLoader] 场景 {sceneName} 加载完成");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 内部各阶段实现
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static IEnumerator PreLoadPhase()
        {
            // 重置时间缩放，防止暂停状态遗留
            Time.timeScale = 1f;

            // 主动触发GC，释放上一场景残留内存
            System.GC.Collect();
            for (int i = 0; i < GC_FRAME_INTERVAL; i++) yield return null;

            // 卸载未使用资源（对WebGL内存尤为重要）
            yield return Resources.UnloadUnusedAssets();
            yield return new WaitForSeconds(PRE_LOAD_WAIT);
        }

        private static IEnumerator LoadScenePhase(string sceneName, System.Action<AsyncOperation> onReady)
        {
            AsyncOperation asyncOp;

            try
            {
                asyncOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (System.Exception e)
            {
                GameLogger.LogError($"[SafeSceneLoader] 启动异步加载异常: {e.Message}");
                onReady(null);
                yield break;
            }

            if (asyncOp == null)
            {
                GameLogger.LogError($"[SafeSceneLoader] LoadSceneAsync 返回 null，场景名: {sceneName}");
                onReady(null);
                yield break;
            }

            // 阻止场景自动激活，等加载到90%再放行
            asyncOp.allowSceneActivation = false;

            int safetyCounter = 0;
            while (asyncOp.progress < 0.9f)
            {
                safetyCounter++;
                if (safetyCounter > MAX_WAIT_FRAMES)
                {
                    GameLogger.LogError("[SafeSceneLoader] 场景加载进度卡死，超时终止");
                    onReady(null);
                    yield break;
                }
                yield return null;
            }

            // 加载到90%后，再等几帧让引擎完成内部准备
            for (int i = 0; i < 3; i++) yield return null;

            onReady(asyncOp);
        }

        private static IEnumerator WaitForActivationPhase(AsyncOperation asyncOp)
        {
            asyncOp.allowSceneActivation = true;

            int frameCount = 0;
            while (!asyncOp.isDone)
            {
                frameCount++;
                if (frameCount > MAX_WAIT_FRAMES)
                {
                    GameLogger.LogError("[SafeSceneLoader] 场景激活超时");
                    yield break;
                }
                yield return null;
            }
        }

        private static IEnumerator PostActivationPhase()
        {
            yield return new WaitForSeconds(ACTIVATION_WAIT);
            // 额外等待5帧，确保所有OnEnable/Start执行完毕
            for (int i = 0; i < 5; i++) yield return null;
            Time.timeScale = 1f;
        }

        private static IEnumerator FinalStabilizePhase()
        {
            Time.timeScale = 1f;
            yield return new WaitForSeconds(POST_LOAD_WAIT);
        }
    }
}
