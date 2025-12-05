// ============================================================
// XPOrb.cs
// 文件位置: Assets/Scripts/Logic/XP/XPOrb.cs
// 用途：经验光点控制器，控制光点飞向经验条的行为
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Logic.XP
{
    /// <summary>
    /// 经验光点控制器
    /// 生成后等待一段时间，然后自动飞向经验条
    /// </summary>
    public class XPOrb : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("飞行设置")]
        [Tooltip("生成后等待时间（秒）")]
        [SerializeField] private float waitBeforeFly = 0.5f;
        
        [Tooltip("飞行平滑时间")]
        [SerializeField] private float smoothTime = 0.3f;
        
        [Tooltip("到达目标的判定距离")]
        [SerializeField] private float arriveThreshold = 0.5f;
        
        [Tooltip("最大飞行速度")]
        [SerializeField] private float maxSpeed = 50f;
        
        [Header("视觉效果")]
        [Tooltip("生成时的随机散开力度")]
        [SerializeField] private float spawnScatterForce = 3f;
        
        [Tooltip("悬浮摆动幅度")]
        [SerializeField] private float floatAmplitude = 0.1f;
        
        [Tooltip("悬浮摆动速度")]
        [SerializeField] private float floatSpeed = 5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int xpValue = 1;
        private float timer = 0f;
        private bool isFlying = false;
        private bool isCollected = false;
        
        private Vector3 velocity = Vector3.zero;
        private Vector3 basePosition;
        private Vector3 scatterVelocity;
        
        private System.Func<Vector3> getTargetPosition;
        private System.Action<XPOrb> onCollected;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化光点
        /// </summary>
        /// <param name="xp">经验值</param>
        /// <param name="targetGetter">获取目标位置的委托</param>
        /// <param name="collectedCallback">收集回调</param>
        public void Initialize(int xp, System.Func<Vector3> targetGetter, System.Action<XPOrb> collectedCallback)
        {
            xpValue = xp;
            getTargetPosition = targetGetter;
            onCollected = collectedCallback;
            
            // 重置状态
            timer = 0f;
            isFlying = false;
            isCollected = false;
            velocity = Vector3.zero;
            
            // 随机散开
            basePosition = transform.position;
            scatterVelocity = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(0.5f, 1f), // 偏向上方
                0f
            ).normalized * spawnScatterForce;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Update()
        {
            if (isCollected) return;
            
            // 使用 unscaledDeltaTime 以确保暂停时也能继续飞行
            float deltaTime = Time.unscaledDeltaTime;
            timer += deltaTime;
            
            if (!isFlying)
            {
                // 等待阶段：散开 + 悬浮
                UpdateWaitingPhase(deltaTime);
                
                // 等待时间到，开始飞行
                if (timer >= waitBeforeFly)
                {
                    isFlying = true;
                }
            }
            else
            {
                // 飞行阶段
                UpdateFlyingPhase(deltaTime);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 阶段更新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateWaitingPhase(float deltaTime)
        {
            // 散开运动（逐渐减速）
            scatterVelocity = Vector3.Lerp(scatterVelocity, Vector3.zero, deltaTime * 5f);
            basePosition += scatterVelocity * deltaTime;
            
            // 悬浮效果
            float floatOffset = Mathf.Sin(timer * floatSpeed) * floatAmplitude;
            transform.position = basePosition + Vector3.up * floatOffset;
        }
        
        private void UpdateFlyingPhase(float deltaTime)
        {
            if (getTargetPosition == null)
            {
                // 没有目标，直接收集
                OnArrive();
                return;
            }
            
            Vector3 targetPos = getTargetPosition();
            
            // 使用 SmoothDamp 平滑飞向目标
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref velocity,
                smoothTime,
                maxSpeed,
                deltaTime
            );
            
            // 检查是否到达
            float distance = Vector3.Distance(transform.position, targetPos);
            if (distance <= arriveThreshold)
            {
                OnArrive();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 到达处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnArrive()
        {
            if (isCollected) return;
            isCollected = true;
            
            // 触发经验收集事件
            GameEvents.TriggerXPOrbCollected(xpValue);
            
            // 回调通知（用于对象池回收）
            onCollected?.Invoke(this);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 对象池支持
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 重置状态（从对象池取出时调用）
        /// </summary>
        public void ResetOrb()
        {
            timer = 0f;
            isFlying = false;
            isCollected = false;
            velocity = Vector3.zero;
            scatterVelocity = Vector3.zero;
        }
    }
}