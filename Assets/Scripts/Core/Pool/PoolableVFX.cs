using UnityEngine;
using System.Collections;

namespace LightVsDecay.Core.Pool
{
    /// <summary>
    /// 可池化的VFX组件
    /// 挂载到粒子特效预制体的根节点上（支持子级多个ParticleSystem）
    /// </summary>
    public class PoolableVFX : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("回收设置")]
        [Tooltip("是否自动检测粒子系统时长（推荐开启）")]
        [SerializeField] private bool autoDetectDuration = true;
        
        [Tooltip("手动设置的回收延迟（仅当autoDetectDuration为false时生效）")]
        [SerializeField] private float manualDuration = 2f;
        
        [Tooltip("额外延迟（等待粒子完全消失）")]
        [SerializeField] private float extraDelay = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时数据
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private ParticleSystem[] particleSystems; // 所有粒子系统（包括子级）
        private VFXPool ownerPool;
        private Coroutine returnCoroutine;
        private float cachedDuration;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 获取自身及所有子级的ParticleSystem
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            
            if (particleSystems.Length == 0)
            {
                Debug.LogWarning($"[PoolableVFX] {gameObject.name} 没有找到任何ParticleSystem！");
            }
            
            // 计算持续时间
            cachedDuration = CalculateTotalDuration();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 时长计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算所有粒子系统的最大持续时间
        /// </summary>
        private float CalculateTotalDuration()
        {
            if (!autoDetectDuration || particleSystems.Length == 0)
            {
                return manualDuration + extraDelay;
            }
            
            float maxDuration = 0f;
            
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                
                var main = ps.main;
                
                // 跳过循环粒子系统的时长计算（它们会一直播放）
                if (main.loop)
                {
                    Debug.LogWarning($"[PoolableVFX] {ps.name} 是循环粒子系统，可能导致无法正确回收");
                    continue;
                }
                
                // 计算该粒子系统的总时长
                // 总时长 = startDelay + duration + startLifetime
                float startDelay = main.startDelay.constantMax;
                float duration = main.duration;
                float lifetime = main.startLifetime.constantMax;
                
                float totalTime = startDelay + duration + lifetime;
                
                if (totalTime > maxDuration)
                {
                    maxDuration = totalTime;
                }
            }
            
            // 如果没有有效的粒子系统，使用手动时长
            if (maxDuration <= 0f)
            {
                maxDuration = manualDuration;
            }
            
            return maxDuration + extraDelay;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放VFX（由VFXPool调用）
        /// </summary>
        public void Play(VFXPool pool)
        {
            ownerPool = pool;
            
            // 停止之前可能还在运行的协程
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
            }
            
            // 重置并播放所有粒子系统
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    ps.Clear();
                    ps.Play();
                }
            }
            
            // 启动自动回收协程
            returnCoroutine = StartCoroutine(AutoReturnCoroutine());
        }
        
        /// <summary>
        /// 停止VFX
        /// </summary>
        public void Stop()
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }
            
            // 停止所有粒子系统
            foreach (var ps in particleSystems)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
        
        /// <summary>
        /// 立即回收（外部强制调用）
        /// </summary>
        public void ReturnToPool()
        {
            if (ownerPool != null)
            {
                ownerPool.Return(this);
            }
            else
            {
                // 没有池引用，直接禁用
                gameObject.SetActive(false);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 自动回收
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private IEnumerator AutoReturnCoroutine()
        {
            // 等待粒子播放完毕
            yield return new WaitForSeconds(cachedDuration);
            
            // 回收到池
            ReturnToPool();
        }
    }
}