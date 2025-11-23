using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Weapon
{
    /// <summary>
    /// 激光控制器 - 负责旋转、伤害、击退
    /// 挂载到光棱塔主体上
    /// </summary>
    public class LaserController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 可配置参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("激光引用")]
        [Tooltip("激光Beam预制体或子对象")]
        [SerializeField] private LaserBeam laserBeam;
        
        [Header("输入设置")]
        [Tooltip("旋转速度（度/秒）")]
        [SerializeField] private float rotationSpeed = 180f;
        
        [Tooltip("正常模式角度限制（下=0, 上=180）")]
        [SerializeField] private Vector2 angleClamp = new Vector2(0f, 180f);
        
        [Header("伤害设置")]
        [Tooltip("基础DPS")]
        [SerializeField] private float baseDPS = GameConstants.BASE_DPS;
        
        [Tooltip("基础击退力")]
        [SerializeField] private float baseKnockback = GameConstants.BASE_KNOCKBACK_FORCE;
        
        [Tooltip("伤害判定间隔")]
        [SerializeField] private float damageTickRate = GameConstants.DAMAGE_TICK_RATE;
        
        [Header("大招设置")]
        [Tooltip("大招持续时间")]
        [SerializeField] private float ultDuration = 5f;
        
        [Tooltip("大招旋转速度（度/秒）")]
        [SerializeField] private float ultRotationSpeed = 360f;
        
        [Tooltip("大招伤害倍率")]
        [SerializeField] private float ultDamageMultiplier = 2f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentAngle = 90f; // 初始朝上
        private bool isUltActive = false;
        private float ultEndTime;
        
        // 伤害计时器
        private float lastDamageTickTime;
        
        // 当前属性（考虑技能加成）
        private float currentDPS;
        private float currentKnockback;
        
        // 缓存
        private Transform cachedTransform;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            cachedTransform = transform;
            
            // 如果没有手动赋值LaserBeam，尝试从子对象获取
            if (laserBeam == null)
            {
                laserBeam = GetComponentInChildren<LaserBeam>();
            }
            
            if (laserBeam == null)
            {
                Debug.LogError("[LaserController] 未找到 LaserBeam 组件！", this);
            }
            
            // 初始化当前属性
            currentDPS = baseDPS;
            currentKnockback = baseKnockback;
        }
        
        private void Update()
        {
            if (isUltActive)
            {
                UpdateUltMode();
            }
            else
            {
                UpdateNormalMode();
            }
            
            // 应用旋转
            ApplyRotation();
            
            // 处理伤害判定
            ProcessDamage();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 正常模式
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateNormalMode()
        {
            // 获取输入（支持鼠标和触摸）
            Vector2 inputDelta = GetRotationInput();
            
            if (inputDelta != Vector2.zero)
            {
                // 根据水平输入调整角度
                currentAngle += inputDelta.x * rotationSpeed * Time.deltaTime;
                
                // 限制在180度范围（左-上-右）
                currentAngle = Mathf.Clamp(currentAngle, angleClamp.x, angleClamp.y);
            }
        }
        
        /// <summary>
        /// 获取旋转输入（统一鼠标和触摸）
        /// </summary>
        private Vector2 GetRotationInput()
        {
            // 移动端触摸输入
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Moved)
                {
                    // 归一化屏幕滑动距离
                    return touch.deltaPosition / Screen.width;
                }
            }
            
            // PC端鼠标输入（用于编辑器测试）
            if (Input.GetMouseButton(0))
            {
                float mouseDelta = Input.GetAxis("Mouse X");
                return new Vector2(mouseDelta, 0f);
            }
            
            return Vector2.zero;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招模式
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void UpdateUltMode()
        {
            // 自动360度旋转
            currentAngle += ultRotationSpeed * Time.deltaTime;
            
            // 保持在0-360范围
            if (currentAngle >= 360f)
                currentAngle -= 360f;
            
            // 检查是否结束
            if (Time.time >= ultEndTime)
            {
                EndUlt();
            }
        }
        
        /// <summary>
        /// 激活大招（外部调用）
        /// </summary>
        public void ActivateUlt()
        {
            if (isUltActive) return;
            
            isUltActive = true;
            ultEndTime = Time.time + ultDuration;
            
            // TODO: 塔飞到屏幕中心的动画
            // TODO: 激光变粗2倍的视觉效果
            
            Debug.Log($"[LaserController] 大招激活！持续{ultDuration}秒");
        }
        
        private void EndUlt()
        {
            isUltActive = false;
            
            // 重置到初始角度（朝上）
            currentAngle = 90f;
            
            // TODO: 塔飞回底部的动画
            
            Debug.Log("[LaserController] 大招结束");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 旋转应用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ApplyRotation()
        {
            // 将角度转换为四元数旋转（2D游戏只旋转Z轴）
            // 0度 = 右, 90度 = 上, 180度 = 左
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, currentAngle);
            cachedTransform.rotation = targetRotation;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ProcessDamage()
        {
            if (Time.time - lastDamageTickTime < damageTickRate)
                return;
            
            lastDamageTickTime = Time.time;
            
            // 获取激光当前击中的目标
            RaycastHit2D hit = laserBeam.GetCurrentHit();
            if (hit.collider == null) return;
            
            // 计算当前伤害（考虑大招加成）
            float damage = currentDPS * damageTickRate;
            if (isUltActive)
                damage *= ultDamageMultiplier;
            
            // TODO: 调用敌人的受伤接口
            // 示例：hit.collider.GetComponent<Enemy>()?.TakeDamage(damage);
            
            // 应用击退力
            ApplyKnockback(hit);
            
            // 调试输出
            Debug.Log($"[Laser] 造成伤害: {damage:F1} to {hit.collider.name}");
        }
        
        private void ApplyKnockback(RaycastHit2D hit)
        {
            Rigidbody2D rb = hit.collider.GetComponent<Rigidbody2D>();
            if (rb == null) return;
            
            // 计算击退方向（从塔指向敌人）
            Vector2 knockbackDirection = (hit.point - (Vector2)cachedTransform.position).normalized;
            
            // 应用击退力
            float force = currentKnockback;
            if (isUltActive)
                force *= 1.5f; // 大招期间击退力更强
            
            rb.AddForce(knockbackDirection * force, ForceMode2D.Impulse);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口（用于技能升级系统）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置DPS倍率（技能加成）
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
        /// 获取LaserBeam引用（用于技能直接修改）
        /// </summary>
        public LaserBeam GetLaserBeam() => laserBeam;
        
        /// <summary>
        /// 检查是否在大招状态
        /// </summary>
        public bool IsUltActive() => isUltActive;
    }
}