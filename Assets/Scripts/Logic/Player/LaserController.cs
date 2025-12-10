// ============================================================
// LaserController.cs (重构版 - 支持多激光)
// 文件位置: Assets/Scripts/Logic/Player/LaserController.cs
// 用途：激光伤害判定和击退 - 支持 Prism 分裂和 Focus 聚能
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
    /// 副激光数据结构
    /// </summary>
    [System.Serializable]
    public class SubLaserData
    {
        public LaserBeam beam;
        public float angle;           // 相对主激光的角度偏移
        public float damageMultiplier; // 伤害倍率（如 0.3 = 30%）
        public float lengthMultiplier; // 长度倍率
    }
    
    /// <summary>
    /// 激光控制器（重构版）
    /// 负责：主激光 + 副激光管理、伤害判定、击退效果
    /// 支持：Prism 分裂、Focus 聚能
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
        [Tooltip("主激光（始终存在）")]
        [SerializeField] private LaserBeam mainLaserBeam;
        
        [Tooltip("激光挂载点（LaserPivot - 控制旋转）")]
        [SerializeField] private Transform laserPivot;
        
        [Tooltip("发射点")]
        [SerializeField] private Transform firePoint;
        
        [Tooltip("激光 Prefab（用于生成副激光）")]
        [SerializeField] private GameObject laserBeamPrefab;
        
        [Tooltip("VFX颜色同步组件")]
        [SerializeField] private LaserVFXColorSync vfxColorSync;
        
        [Header("检测设置")]
        [Tooltip("激光检测层")]
        [SerializeField] private LayerMask enemyLayer;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时配置缓存（从 GameSettings 读取）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float baseDPS = 100f;
        private float tickRate = 0.1f;
        private float baseKnockbackForce = 10f;
        private float maxLaserLength = 20f;
        private float baseLaserWidth = 1.0f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float tickTimer = 0f;
        private bool isUltMode = false;
        private float ultDamageMultiplier = 2f;
        private float ultKnockbackMultiplier = 1.5f;
        
        // 技能加成（主激光）
        private float skillDamageMultiplier = 1f;
        private float skillKnockbackMultiplier = 1f;
        private float skillWidthMultiplier = 1f;
        
        // 副激光管理
        private List<SubLaserData> subLasers = new List<SubLaserData>();
        private float subLaserDamageMultiplier = 0.3f;
        private float subLaserLengthMultiplier = 0.5f;
        
        // 颜色状态
        private Color mainLaserColor = Color.white;
        private bool hasCustomColor = false;
        
        // 伤害检测缓存
        private HashSet<EnemyBlob> hitEnemies = new HashSet<EnemyBlob>();
        private Collider2D[] hitBuffer = new Collider2D[32];
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 常量
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>副激光宽度倍率（相对主激光）</summary>
        private const float SUB_LASER_WIDTH_RATIO = 0.65f;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当前激光宽度</summary>
        public float CurrentLaserWidth => baseLaserWidth * skillWidthMultiplier;
        
        /// <summary>当前副激光宽度</summary>
        public float CurrentSubLaserWidth => CurrentLaserWidth * SUB_LASER_WIDTH_RATIO;
        
        /// <summary>每 Tick 伤害</summary>
        public float CurrentDamagePerTick => baseDPS * tickRate * skillDamageMultiplier * (isUltMode ? ultDamageMultiplier : 1f);
        
        /// <summary>当前击退力</summary>
        public float CurrentKnockbackForce => baseKnockbackForce * skillKnockbackMultiplier * (isUltMode ? ultKnockbackMultiplier : 1f);
        
        /// <summary>大招伤害倍率</summary>
        public float UltDamageMultiplier => isUltMode ? ultDamageMultiplier : 1f;
        
        /// <summary>大招击退倍率</summary>
        public float UltKnockbackMultiplier => isUltMode ? (ultKnockbackMultiplier * 2f) : 1f;
        
        /// <summary>副激光数量</summary>
        public int SubLaserCount => subLasers.Count;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Start()
        {
            InitializeFromSettings();
            CacheComponents();
            SubscribeEvents();
        }
        
        private void OnDestroy()
        {
            UnsubscribeEvents();
            ClearAllSubLasers();
        }
        
        private void Update()
        {
            tickTimer += Time.deltaTime;
            
            if (tickTimer >= tickRate)
            {
                tickTimer = 0f;
                PerformDamageDetection();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void InitializeFromSettings()
        {
            if (settings != null)
            {
                baseDPS = settings.baseDPS;
                tickRate = settings.tickRate;
                baseKnockbackForce = settings.baseKnockbackForce;
                maxLaserLength = settings.maxLaserLength;
                baseLaserWidth = settings.baseLaserWidth;
            }
            
            // 初始化主激光
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
                mainLaserBeam.SetMaxLength(maxLaserLength);
            }
            
            // 验证 LaserPivot
            if (laserPivot == null && mainLaserBeam != null)
            {
                laserPivot = mainLaserBeam.transform.parent;
                Debug.LogWarning("[LaserController] LaserPivot 未设置，使用 mainLaserBeam 的父物体");
            }
            
            // 验证 FirePoint
            if (firePoint == null && mainLaserBeam != null)
            {
                firePoint = mainLaserBeam.transform;
            }
        }
        
        private void CacheComponents()
        {
            // 自动查找 VFX 颜色同步组件
            if (vfxColorSync == null && mainLaserBeam != null)
            {
                vfxColorSync = mainLaserBeam.GetComponent<LaserVFXColorSync>();
            }
        }
        
        private void SubscribeEvents()
        {
            GameEvents.OnUltReady += OnUltReady;
            GameEvents.OnUltUsed += OnUltUsed;
        }
        
        private void UnsubscribeEvents()
        {
            GameEvents.OnUltReady -= OnUltReady;
            GameEvents.OnUltUsed -= OnUltUsed;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害判定（支持多激光）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void PerformDamageDetection()
        {
            hitEnemies.Clear();
            
            // 1. 主激光伤害检测
            DetectAndDamageEnemies(mainLaserBeam, firePoint, CurrentDamagePerTick, 1f);
            
            // 2. 副激光伤害检测
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subDamage = CurrentDamagePerTick * subLaser.damageMultiplier;
                    DetectAndDamageEnemies(subLaser.beam, subLaser.beam.transform, subDamage, subLaser.damageMultiplier);
                }
            }
        }
        
        private void DetectAndDamageEnemies(LaserBeam beam, Transform origin, float damage, float knockbackMultiplier)
        {
            if (beam == null || origin == null) return;
            
            float length = beam.GetMaxLength();
            float width = beam.GetLaserWidth();
            
            Vector2 boxCenter = (Vector2)origin.position + (Vector2)origin.up * (length * 0.5f);
            Vector2 boxSize = new Vector2(width, length);
            float angle = origin.eulerAngles.z;
            
            int hitCount = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, angle, hitBuffer, enemyLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var collider = hitBuffer[i];
                if (collider == null) continue;
                
                EnemyBlob enemy = collider.GetComponentInParent<EnemyBlob>();
                if (enemy == null || hitEnemies.Contains(enemy)) continue;
                
                hitEnemies.Add(enemy);
                
                Vector2 knockbackDir = (enemy.transform.position - origin.position).normalized;
                Vector2 knockbackForce = knockbackDir * CurrentKnockbackForce * knockbackMultiplier;
                
                enemy.TakeDamage(damage, knockbackForce);
                
                // 应用 Frost 减速效果
                ApplyFrostEffect(enemy);
            }
        }
        
        /// <summary>
        /// 对敌人应用 Frost 减速效果
        /// </summary>
        private void ApplyFrostEffect(EnemyBlob enemy)
        {
            if (SkillEffectManager.Instance == null) return;
            
            float slowPercent, duration;
            SkillEffectManager.Instance.GetFrostParams(out slowPercent, out duration);
            
            if (slowPercent <= 0f) return;
    
            // Lv.5 有 20% 概率完全冰冻
            if (SkillEffectManager.Instance.TryFrostFreeze())
            {
                enemy.ApplyFrostFreeze(1.0f); // 冰冻 1 秒
            }
            else
            {
                enemy.ApplyFrostSlow(slowPercent, duration);
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Prism 效果（副激光管理）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置 Prism 分裂效果
        /// </summary>
        /// <param name="level">Prism 等级 (1-5)</param>
        public void SetPrismLevel(int level)
        {
            // 先清除现有副激光
            ClearAllSubLasers();
            
            if (level <= 0)
            {
                if (showDebugInfo) Debug.Log("[LaserController] Prism 等级为 0，无副激光");
                return;
            }
            
            // 根据等级获取角度配置
            float[] angles = GetPrismAngles(level);
            float damageMultiplier = GetPrismDamageMultiplier(level);
            float lengthMultiplier = GetPrismLengthMultiplier(level);
            
            // 更新副激光参数
            subLaserDamageMultiplier = damageMultiplier;
            subLaserLengthMultiplier = lengthMultiplier;
            
            // 生成副激光
            foreach (float angle in angles)
            {
                CreateSubLaser(angle, damageMultiplier, lengthMultiplier);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Prism Lv.{level}: 生成 {angles.Length} 条副激光, " +
                          $"伤害倍率={damageMultiplier:P0}, 长度倍率={lengthMultiplier:P0}");
            }
        }
        
        /// <summary>
        /// 根据 Prism 等级获取角度配置
        /// </summary>
        private float[] GetPrismAngles(int level)
        {
            switch (level)
            {
                case 1: return new float[] { -15f, 15f };                              // 2条
                case 2: return new float[] { -20f, 20f };                              // 2条（角度变宽）
                case 3: return new float[] { -10f, 10f, -25f, 25f };                   // 4条
                case 4: return new float[] { -10f, 10f, -25f, 25f };                   // 4条（伤害提升）
                case 5: return new float[] { -8f, 8f, -20f, 20f, -35f, 35f };          // 6条
                default: return new float[0];
            }
        }
        
        /// <summary>
        /// 根据 Prism 等级获取副激光伤害倍率
        /// </summary>
        private float GetPrismDamageMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 0.30f; // 30%
                case 2: return 0.40f; // 40%
                case 3: return 0.40f; // 40%
                case 4: return 0.50f; // 50%
                case 5: return 0.50f; // 50%
                default: return 0.30f;
            }
        }
        
        /// <summary>
        /// 根据 Prism 等级获取副激光长度倍率
        /// 【修改】Lv1: 长度10, Lv5: 长度18
        /// </summary>
        private float GetPrismLengthMultiplier(int level)
        {
            switch (level)
            {
                case 1:
                case 2:
                    return 10f / maxLaserLength; // 长度 10.0
                case 3:
                case 4:
                    return 12f / maxLaserLength; // 长度 12.0
                case 5:
                    return 18f / maxLaserLength; // 长度 18.0（光棱风暴）
                default:
                    return 10f / maxLaserLength;
            }
        }
        
        /// <summary>
        /// 创建单条副激光
        /// </summary>
        private void CreateSubLaser(float angle, float damageMultiplier, float lengthMultiplier)
        {
            if (laserBeamPrefab == null)
            {
                Debug.LogError("[LaserController] LaserBeam Prefab 未设置！无法创建副激光");
                return;
            }
            
            if (laserPivot == null)
            {
                Debug.LogError("[LaserController] LaserPivot 未设置！无法创建副激光");
                return;
            }
            
            // 实例化副激光
            GameObject subLaserObj = Instantiate(laserBeamPrefab, laserPivot);
            subLaserObj.name = $"LaserBeam_Sub_{subLasers.Count}";
            
            // 设置位置和旋转
            subLaserObj.transform.localPosition = Vector3.zero;
            subLaserObj.transform.localRotation = Quaternion.Euler(0, 0, angle);
            
            // 获取 LaserBeam 组件
            LaserBeam beam = subLaserObj.GetComponent<LaserBeam>();
            if (beam == null)
            {
                Debug.LogError("[LaserController] Prefab 缺少 LaserBeam 组件！");
                Destroy(subLaserObj);
                return;
            }
            
            // 设置副激光属性
            float subLength = maxLaserLength * lengthMultiplier;
            beam.SetMaxLength(subLength);
            beam.SetLaserWidth(CurrentSubLaserWidth); // 【修改】使用统一的副激光宽度
            
            // 如果主激光有自定义颜色，副激光也使用
            if (hasCustomColor)
            {
                beam.SetColor(mainLaserColor);
            }
            
            // 添加到列表
            subLasers.Add(new SubLaserData
            {
                beam = beam,
                angle = angle,
                damageMultiplier = damageMultiplier,
                lengthMultiplier = lengthMultiplier
            });
        }
        
        /// <summary>
        /// 清除所有副激光
        /// </summary>
        public void ClearAllSubLasers()
        {
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    Destroy(subLaser.beam.gameObject);
                }
            }
            subLasers.Clear();
            
            if (showDebugInfo)
            {
                Debug.Log("[LaserController] 清除所有副激光");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Focus 效果（主激光强化）
        // 【修改】只增加伤害，不缩减宽度
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置 Focus 聚能效果
        /// 【修改】只增加伤害和变色，不再缩减宽度
        /// </summary>
        /// <param name="level">Focus 等级 (1-5)</param>
        /// <param name="skillColor">技能颜色（从SkillData读取）</param>
        public void SetFocusLevel(int level, Color skillColor)
        {
            if (level <= 0)
            {
                // 重置为默认状态
                ResetFocusEffect();
                return;
            }
            
            // 获取等级配置 - 只获取伤害倍率
            float damageMultiplier = GetFocusDamageMultiplier(level);
            
            // 【修改】只应用伤害倍率，不修改宽度
            skillDamageMultiplier = damageMultiplier;
            // skillWidthMultiplier 保持不变，不再缩减
            
            // 设置激光颜色（从SkillData读取）
            SetLaserColor(skillColor);
            
            // 更新主激光宽度（宽度倍率未改变，但需要确保应用）
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[LaserController] Focus Lv.{level}: 伤害={damageMultiplier:P0}, 颜色={skillColor}");
            }
        }
        
        /// <summary>
        /// 设置 Focus 聚能效果（兼容旧接口）
        /// </summary>
        public void SetFocusLevel(int level)
        {
            Color defaultFocusColor = new Color(3f, 0.3f, 0.2f, 1f); // 红色 HDR
            SetFocusLevel(level, defaultFocusColor);
        }
        
        /// <summary>
        /// 根据 Focus 等级获取伤害倍率
        /// </summary>
        private float GetFocusDamageMultiplier(int level)
        {
            switch (level)
            {
                case 1: return 1.50f; // 150%
                case 2: return 1.80f; // 180%
                case 3: return 2.20f; // 220%
                case 4: return 2.60f; // 260%
                case 5: return 3.50f; // 350%
                default: return 1.0f;
            }
        }
        
        /// <summary>
        /// 重置 Focus 效果
        /// </summary>
        private void ResetFocusEffect()
        {
            skillDamageMultiplier = 1f;
            // skillWidthMultiplier 不重置，保留 Wide 技能的加成
            
            // 重置颜色
            SetLaserColor(new Color(0f, 3f, 3f, 1f));
            hasCustomColor = false;
            
            // 更新主激光宽度
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
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
        /// 激活大招模式
        /// </summary>
        public void ActivateUlt()
        {
            isUltMode = true;
            UpdateAllLaserWidths();
            
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
            UpdateAllLaserWidths();
            
            if (showDebugInfo)
            {
                Debug.Log("[LaserController] 退出大招模式");
            }
        }
        
        /// <summary>
        /// 退出大招模式（别名方法）
        /// </summary>
        public void ExitUltMode() => DeactivateUlt();
        
        /// <summary>
        /// 更新所有激光的宽度
        /// </summary>
        private void UpdateAllLaserWidths()
        {
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetLaserWidth(CurrentLaserWidth);
            }
            
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.SetLaserWidth(CurrentSubLaserWidth); // 【修改】使用统一的副激光宽度
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 颜色控制
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置所有激光的颜色
        /// </summary>
        public void SetLaserColor(Color color)
        {
            mainLaserColor = color;
            hasCustomColor = (color != Color.white);
            
            // 设置主激光颜色
            if (mainLaserBeam != null)
            {
                mainLaserBeam.SetColor(color);
            }
            
            // 设置所有副激光颜色
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    subLaser.beam.SetColor(color);
                }
            }
        }
        
        /// <summary>
        /// 设置VFX特效颜色（用于 Frost 等技能）
        /// </summary>
        public void SetVFXColor(Color color)
        {
            if (vfxColorSync != null)
            {
                vfxColorSync.SetVFXColor(color);
            }
            else
            {
                Debug.LogWarning("[LaserController] VFX颜色同步组件未设置，无法修改VFX颜色");
            }
        }
        
        /// <summary>
        /// 重置VFX特效颜色为默认
        /// </summary>
        public void ResetVFXColor()
        {
            if (vfxColorSync != null)
            {
                vfxColorSync.ResetVFXColor();
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 技能加成接口（兼容原有代码）
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
            UpdateAllLaserWidths();
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
            UpdateAllLaserWidths();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmosSelected()
        {
            if (firePoint == null) return;
            
            // 绘制主激光检测范围
            DrawLaserGizmo(firePoint, maxLaserLength, CurrentLaserWidth, Color.green);
            
            // 绘制副激光检测范围
            foreach (var subLaser in subLasers)
            {
                if (subLaser.beam != null)
                {
                    float subLength = maxLaserLength * subLaser.lengthMultiplier;
                    DrawLaserGizmo(subLaser.beam.transform, subLength, CurrentSubLaserWidth, Color.cyan);
                }
            }
        }
        
        private void DrawLaserGizmo(Transform origin, float length, float width, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
            
            Vector3 center = origin.position + origin.up * (length * 0.5f);
            Vector3 size = new Vector3(width, length, 0.1f);
            
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, origin.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.matrix = oldMatrix;
        }
        
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!showDebugInfo) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 220));
            GUILayout.Label("=== Laser Stats ===");
            GUILayout.Label($"DPS: {baseDPS * skillDamageMultiplier:F1} {(isUltMode ? "(x2 ULT)" : "")}");
            GUILayout.Label($"Damage/Tick: {CurrentDamagePerTick:F1}");
            GUILayout.Label($"Knockback: {CurrentKnockbackForce:F1}");
            GUILayout.Label($"Width: {CurrentLaserWidth:F2}");
            GUILayout.Label($"Sub Width: {CurrentSubLaserWidth:F2}");
            GUILayout.Label($"Sub Lasers: {subLasers.Count}");
            GUILayout.Label($"Sub Damage: {subLaserDamageMultiplier:P0}");
            GUILayout.Label($"Ult Mode: {isUltMode}");
            GUILayout.Label($"Custom Color: {hasCustomColor}");
            GUILayout.EndArea();
        }
#endif
    }
}