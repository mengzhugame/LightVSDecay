using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Weapon
{
    /// <summary>
    /// 激光控制器 - 只负责伤害判定和击退处理
    /// 【修改】优化击退力计算，与测试脚本保持一致
    /// 旋转控制由 TurretController 负责
    /// </summary>
    public class LaserController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 可配置参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("激光Beam组件")]
        [SerializeField] private LaserBeam laserBeam;
        
        [Tooltip("塔身控制器（用于大招控制）")]
        [SerializeField] private TurretController turretController;
        
        [Header("伤害设置")]
        [Tooltip("基础DPS")]
        [SerializeField] private float baseDPS = GameConstants.BASE_DPS;
        
        [Tooltip("伤害判定间隔")]
        [SerializeField] private float damageTickRate = GameConstants.DAMAGE_TICK_RATE;
        
        [Header("击退设置")]
        [Tooltip("基础击退力（每Tick施加的力）")]
        [SerializeField] private float baseKnockback = 50f;
        
        [Header("大招设置")]
        [Tooltip("大招持续时间")]
        [SerializeField] private float ultDuration = 5f;
        
        [Tooltip("大招旋转速度（度/秒）")]
        [SerializeField] private float ultRotationSpeed = 360f;
        
        [Tooltip("大招伤害倍率")]
        [SerializeField] private float ultDamageMultiplier = 2f;
        
        [Tooltip("大招击退力倍率")]
        [SerializeField] private float ultKnockbackMultiplier = 1.5f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugLog = false;
        [SerializeField] private bool showDebugGizmos = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private bool isUltActive = false;
        private float ultEndTime;
        private float ultCurrentAngle = 0f;
        
        // 伤害计时器
        private float lastDamageTickTime;
        
        // 当前属性（考虑技能加成）
        private float currentDPS;
        private float currentKnockback;
        
        // 【新增】上一次击中的信息（用于 Gizmo 绘制）
        private Vector2 lastHitPoint;
        private Vector2 lastKnockbackDirection;
        private bool hasLastHit;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>大招激活时触发</summary>
        public event System.Action OnUltActivated;
        
        /// <summary>大招结束时触发</summary>
        public event System.Action OnUltEnded;
        
        /// <summary>造成伤害时触发（参数：伤害值，位置）</summary>
        public event System.Action<float, Vector2> OnDamageDealt;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            // 自动获取组件（如果没有手动赋值）
            if (laserBeam == null)
            {
                laserBeam = GetComponentInChildren<LaserBeam>();
            }
            
            if (turretController == null)
            {
                turretController = GetComponentInChildren<TurretController>();
            }
            
            // 验证必要组件
            if (laserBeam == null)
            {
                Debug.LogError("[LaserController] 未找到 LaserBeam 组件！", this);
            }
            
            if (turretController == null)
            {
                Debug.LogWarning("[LaserController] 未找到 TurretController，大招旋转将无法工作", this);
            }
            
            // 初始化当前属性
            currentDPS = baseDPS;
            currentKnockback = baseKnockback;
        }
        
        private void Update()
        {
            // 更新大招状态
            if (isUltActive)
            {
                UpdateUltMode();
            }
            
            // 处理伤害判定
            ProcessDamage();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招系统
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 激活大招（外部调用，如能量系统）
        /// </summary>
        public void ActivateUlt()
        {
            if (isUltActive) return;
            
            isUltActive = true;
            ultEndTime = Time.time + ultDuration;
            ultCurrentAngle = turretController != null ? turretController.GetCurrentAngle() : 0f;
            
            // 通知 TurretController 进入大招模式
            if (turretController != null)
            {
                turretController.SetUltActive(true);
            }
            
            // 触发事件
            OnUltActivated?.Invoke();
            
            Debug.Log($"[LaserController] 大招激活！持续 {ultDuration} 秒");
        }
        
        /// <summary>
        /// 更新大招模式（自动360度旋转）
        /// </summary>
        private void UpdateUltMode()
        {
            // 自动旋转
            ultCurrentAngle += ultRotationSpeed * Time.deltaTime;
            
            // 保持在 -180~180 范围（可选，360度旋转可以不限制）
            if (ultCurrentAngle > 180f)
                ultCurrentAngle -= 360f;
            else if (ultCurrentAngle < -180f)
                ultCurrentAngle += 360f;
            
            // 强制设置 TurretController 的角度
            if (turretController != null)
            {
                turretController.SetAngle(ultCurrentAngle);
            }
            
            // 检查是否结束
            if (Time.time >= ultEndTime)
            {
                EndUlt();
            }
        }
        
        /// <summary>
        /// 结束大招
        /// </summary>
        private void EndUlt()
        {
            isUltActive = false;
            
            // 通知 TurretController 退出大招模式
            if (turretController != null)
            {
                turretController.SetUltActive(false);
                turretController.ResetRotation(); // 重置到朝上
            }
            
            // 触发事件
            OnUltEnded?.Invoke();
            
            Debug.Log("[LaserController] 大招结束");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 处理伤害判定
        /// 【修改】优化击退力计算方式
        /// </summary>
        private void ProcessDamage()
        {
            // 检查 Tick 间隔
            if (Time.time - lastDamageTickTime < damageTickRate)
                return;
            
            lastDamageTickTime = Time.time;
            hasLastHit = false;
            
            // 获取激光当前击中的目标
            if (laserBeam == null) return;
            
            RaycastHit2D hit = laserBeam.GetCurrentHit();
            if (hit.collider == null)
            {
                return;
            }
            
            // 计算当前伤害（考虑大招加成）
            float damage = currentDPS * damageTickRate;
            if (isUltActive)
                damage *= ultDamageMultiplier;
            
            // 【修改】计算击退力方向（从激光原点指向敌人）
            Vector2 laserOrigin = laserBeam.transform.position;
            Vector2 knockbackDirection = (hit.point - laserOrigin).normalized;
            
            // 【修改】计算击退力大小
            float knockbackMagnitude = currentKnockback;
            if (isUltActive)
                knockbackMagnitude *= ultKnockbackMultiplier;
            
            Vector2 knockbackForce = knockbackDirection * knockbackMagnitude;
            
            // 保存击中信息（用于 Gizmo）
            lastHitPoint = hit.point;
            lastKnockbackDirection = knockbackDirection;
            hasLastHit = true;
            
            if (showDebugLog)
            {
                Debug.Log($"[LaserController] 击中: {hit.collider.name}" +
                          $"\n  伤害: {damage:F1}" +
                          $"\n  击退方向: {knockbackDirection}" +
                          $"\n  击退力: {knockbackForce.magnitude:F2}");
            }
            
            // 调用敌人的受伤接口
            var enemy = hit.collider.GetComponent<Enemy.EnemyBlob>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage, knockbackForce);
                
                // 触发事件
                OnDamageDealt?.Invoke(damage, hit.point);
            }
            else
            {
                // 【新增】尝试直接操作 Rigidbody2D（兼容测试方块）
                var rb = hit.collider.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.AddForce(knockbackForce, ForceMode2D.Force);
                    
                    if (showDebugLog)
                        Debug.Log($"[LaserController] 直接击退 Rigidbody2D: {rb.name}");
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口（用于技能升级系统）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置 DPS 倍率（技能加成）
        /// </summary>
        public void SetDPSMultiplier(float multiplier)
        {
            currentDPS = baseDPS * multiplier;
        }
        
        /// <summary>
        /// 设置击退力倍率（技能加成）
        /// </summary>
        public void SetKnockbackMultiplier(float multiplier)
        {
            currentKnockback = baseKnockback * multiplier;
        }
        
        /// <summary>
        /// 【新增】直接设置击退力
        /// </summary>
        public void SetKnockbackForce(float force)
        {
            currentKnockback = force;
        }
        
        /// <summary>
        /// 获取 LaserBeam 引用
        /// </summary>
        public LaserBeam GetLaserBeam() => laserBeam;
        
        /// <summary>
        /// 获取 TurretController 引用
        /// </summary>
        public TurretController GetTurretController() => turretController;
        
        /// <summary>
        /// 检查是否在大招状态
        /// </summary>
        public bool IsUltActive() => isUltActive;
        
        /// <summary>
        /// 获取当前 DPS
        /// </summary>
        public float GetCurrentDPS() => currentDPS;
        
        /// <summary>
        /// 获取当前击退力
        /// </summary>
        public float GetCurrentKnockback() => currentKnockback;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试可视化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;
            
            // 绘制击中点和击退方向
            if (hasLastHit)
            {
                // 击中点
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(lastHitPoint, 0.3f);
                
                // 击退方向
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(lastHitPoint, lastHitPoint + lastKnockbackDirection * 2f);
                Gizmos.DrawWireSphere(lastHitPoint + lastKnockbackDirection * 2f, 0.1f);
            }
        }
    }
}