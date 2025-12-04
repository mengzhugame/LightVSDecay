// ============================================================
// LaserController.cs (修复版)
// 文件位置: Assets/Scripts/Logic/Player/LaserController.cs
// 用途：激光伤害判定和击退 - 修复接口调用
// ============================================================

using UnityEngine;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic.Enemy;

namespace LightVsDecay.Logic.Player
{
    /// <summary>
    /// 激光控制器
    /// 负责伤害判定、击退效果
    /// 配置从 GameSettings ScriptableObject 读取
    /// </summary>
    public class LaserController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("游戏设置")]
        [SerializeField] private GameSettings settings;
        
        [Header("组件引用")]
        [SerializeField] private LaserBeam laserBeam;
        [SerializeField] private Transform firePoint;
        
        [Header("检测设置")]
        [Tooltip("激光检测层")]
        [SerializeField] private LayerMask enemyLayer;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseDPS = 100f;
        private float tickRate = 0.1f;
        private float baseKnockbackForce = 10f;
        private float maxLaserLength = 15f;
        private float baseLaserWidth = 0.5f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float tickTimer = 0f;
        private bool isUltMode = false;
        private float ultDamageMultiplier = 2f;
        private float ultKnockbackMultiplier = 1.5f;
        
        // 技能加成
        private float skillDamageMultiplier = 1f;
        private float skillKnockbackMultiplier = 1f;
        private float skillWidthMultiplier = 1f;
        
        // 缓存
        private List<EnemyBlob> hitEnemies = new List<EnemyBlob>();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前每次判定伤害</summary>
        public float CurrentDamagePerTick
        {
            get
            {
                float damage = baseDPS * tickRate * skillDamageMultiplier;
                if (isUltMode) damage *= ultDamageMultiplier;
                return damage;
            }
        }
        
        /// <summary>当前击退力</summary>
        public float CurrentKnockbackForce
        {
            get
            {
                float force = baseKnockbackForce * skillKnockbackMultiplier;
                if (isUltMode) force *= ultKnockbackMultiplier;
                return force;
            }
        }
        
        /// <summary>当前激光宽度</summary>
        public float CurrentLaserWidth => baseLaserWidth * skillWidthMultiplier * (isUltMode ? 2f : 1f);
        
        /// <summary>是否处于大招模式</summary>
        public bool IsUltMode => isUltMode;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            LoadConfig();
        }
        
        private void Start()
        {
            // 订阅大招事件
            GameEvents.OnUltReady += OnUltReady;
            GameEvents.OnUltUsed += OnUltUsed;
        }
        
        private void OnDestroy()
        {
            GameEvents.OnUltReady -= OnUltReady;
            GameEvents.OnUltUsed -= OnUltUsed;
        }
        
        private void Update()
        {
            tickTimer += Time.deltaTime;
            
            if (tickTimer >= tickRate)
            {
                tickTimer = 0f;
                ProcessDamage();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置加载
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void LoadConfig()
        {
            if (settings != null)
            {
                baseDPS = settings.baseDPS;
                tickRate = settings.tickRate;
                baseKnockbackForce = settings.baseKnockbackForce;
                maxLaserLength = settings.maxLaserLength;
                baseLaserWidth = settings.baseLaserWidth;
            }
            else
            {
                // 使用 GameConstants 默认值
                baseDPS = GameConstants.BASE_DPS;
                tickRate = GameConstants.DAMAGE_TICK_RATE;
                baseKnockbackForce = GameConstants.BASE_KNOCKBACK_FORCE;
                maxLaserLength = GameConstants.LASER_MAX_LENGTH;
                baseLaserWidth = GameConstants.LASER_DEFAULT_WIDTH;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ProcessDamage()
        {
            if (firePoint == null) return;
            
            hitEnemies.Clear();
            
            // 获取激光当前长度 - 使用 LaserBeam 的正确接口
            float currentLength = maxLaserLength;
            if (laserBeam != null)
            {
                // LaserBeam 使用 GetCurrentHit() 来获取击中信息
                var hit = laserBeam.GetCurrentHit();
                if (hit.collider != null)
                {
                    currentLength = hit.distance;
                }
            }
            
            // BoxCast 检测
            Vector2 origin = firePoint.position;
            Vector2 direction = firePoint.up;
            Vector2 size = new Vector2(CurrentLaserWidth, currentLength);
            float angle = firePoint.eulerAngles.z;
            
            // 中心点偏移
            Vector2 center = origin + direction * (currentLength * 0.5f);
            
            // 检测所有敌人
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, angle, enemyLayer);
            
            foreach (var hit in hits)
            {
                EnemyBlob enemy = hit.GetComponent<EnemyBlob>();
                if (enemy != null && !hitEnemies.Contains(enemy))
                {
                    hitEnemies.Add(enemy);
                    ApplyDamageAndKnockback(enemy, direction);
                }
            }
            
            if (showDebugInfo && hitEnemies.Count > 0)
            {
                Debug.Log($"[LaserController] 击中 {hitEnemies.Count} 个敌人, 伤害: {CurrentDamagePerTick}");
            }
        }
        
        private void ApplyDamageAndKnockback(EnemyBlob enemy, Vector2 laserDirection)
        {
            // 计算击退方向（激光方向）
            Vector2 knockbackDir = laserDirection.normalized;
            Vector2 knockbackForce = knockbackDir * CurrentKnockbackForce;
            
            // 应用伤害和击退
            enemy.TakeDamage(CurrentDamagePerTick, knockbackForce);
            
            // 播放击中特效 - 使用 VFXPoolManager.Play 方法
            if (VFXPoolManager.Instance != null)
            {
                VFXPoolManager.Instance.Play(VFXType.LaserHit, enemy.transform.position);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 大招模式
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnUltReady()
        {
            // 大招准备好时的提示
        }
        
        private void OnUltUsed()
        {
            // 大招使用由 UltController 调用 ActivateUlt
        }
        
        /// <summary>
        /// 激活大招模式（保持原有方法名兼容性）
        /// </summary>
        public void ActivateUlt()
        {
            isUltMode = true;
            
            // 更新激光视觉 - 使用 LaserBeam 的正确接口
            if (laserBeam != null)
            {
                laserBeam.SetLaserWidth(CurrentLaserWidth);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[LaserController] 进入大招模式");
            }
        }
        
        /// <summary>
        /// 进入大招模式（别名方法）
        /// </summary>
        public void EnterUltMode() => ActivateUlt();
        
        /// <summary>
        /// 停用大招模式
        /// </summary>
        public void DeactivateUlt()
        {
            isUltMode = false;
            
            // 恢复激光视觉
            if (laserBeam != null)
            {
                laserBeam.SetLaserWidth(CurrentLaserWidth);
            }
            
            if (showDebugInfo)
            {
                Debug.Log("[LaserController] 退出大招模式");
            }
        }
        
        /// <summary>
        /// 退出大招模式（别名方法）
        /// </summary>
        public void ExitUltMode() => DeactivateUlt();
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能加成接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>设置伤害倍率（技能加成）</summary>
        public void SetDamageMultiplier(float multiplier)
        {
            skillDamageMultiplier = Mathf.Max(0.1f, multiplier);
        }
        
        /// <summary>设置击退力倍率（技能加成）</summary>
        public void SetKnockbackMultiplier(float multiplier)
        {
            skillKnockbackMultiplier = Mathf.Max(0f, multiplier);
        }
        
        /// <summary>设置宽度倍率（技能加成）</summary>
        public void SetWidthMultiplier(float multiplier)
        {
            skillWidthMultiplier = Mathf.Max(0.1f, multiplier);
            
            if (laserBeam != null)
            {
                laserBeam.SetLaserWidth(CurrentLaserWidth);
            }
        }
        
        /// <summary>增加伤害百分比</summary>
        public void AddDamagePercent(float percent)
        {
            skillDamageMultiplier += percent;
        }
        
        /// <summary>增加宽度百分比</summary>
        public void AddWidthPercent(float percent)
        {
            skillWidthMultiplier += percent;
            
            if (laserBeam != null)
            {
                laserBeam.SetLaserWidth(CurrentLaserWidth);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmosSelected()
        {
            if (firePoint == null) return;
            
            float length = maxLaserLength;
            if (Application.isPlaying && laserBeam != null)
            {
                var hit = laserBeam.GetCurrentHit();
                if (hit.collider != null)
                {
                    length = hit.distance;
                }
            }
            float width = CurrentLaserWidth;
            
            // 绘制检测范围
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            
            Vector3 center = firePoint.position + firePoint.up * (length * 0.5f);
            Vector3 size = new Vector3(width, length, 0.1f);
            
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, firePoint.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.matrix = oldMatrix;
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 230, 10, 220, 150));
            GUILayout.Label("=== Laser Stats ===");
            GUILayout.Label($"DPS: {baseDPS * skillDamageMultiplier:F1} {(isUltMode ? "(x2 ULT)" : "")}");
            GUILayout.Label($"Damage/Tick: {CurrentDamagePerTick:F1}");
            GUILayout.Label($"Knockback: {CurrentKnockbackForce:F1}");
            GUILayout.Label($"Width: {CurrentLaserWidth:F2}");
            GUILayout.Label($"Ult Mode: {isUltMode}");
            GUILayout.EndArea();
        }
#endif
    }
}