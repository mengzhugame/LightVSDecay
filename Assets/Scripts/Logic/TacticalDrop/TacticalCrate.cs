// ============================================================
// TacticalCrate.cs
// 文件位置: Assets/Scripts/Logic/TacticalDrop/TacticalCrate.cs
// 用途：战术空投宝箱基类
// ============================================================

using UnityEngine;
using DG.Tweening;
using LightVsDecay.Data.SO;
using LightVsDecay.UI.TacticalDrop;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.TacticalDrop
{
    /// <summary>
    /// 战术空投宝箱
    /// 功能：
    /// - 接收激光伤害
    /// - 显示圆环进度条
    /// - 血量归零时触发爆炸
    /// - 调用 TacticalDropManager 处理奖励
    /// </summary>
    public class TacticalCrate : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 组件引用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("组件引用")]
        [Tooltip("宝箱 SpriteRenderer")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        [Tooltip("碰撞体")]
        [SerializeField] private Collider2D crateCollider;
        
        [Tooltip("圆环进度条UI（子物体）")]
        [SerializeField] private CrateProgressUI progressUI;
        
        [Tooltip("爆炸特效预制体")]
        [SerializeField] private GameObject explosionVFXPrefab;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("配置")]
        [Tooltip("宝箱类型")]
        [SerializeField] private CrateType crateType = CrateType.Supply;
        
        [Tooltip("受击时抖动强度")]
        [SerializeField] private float hitShakeStrength = 0.1f;
        
        [Tooltip("受击时发光强度")]
        [SerializeField] private float hitGlowIntensity = 0.5f;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int maxHP = 500;
        private int currentHP;
        private bool isDead = false;
        private bool canBeDamaged = false; // 落地后才能受伤
        private Vector3 originalPosition;
        private Tween shakeTween;
        private Material spriteMaterial;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共属性
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        public CrateType CrateType => crateType;
        public bool IsDead => isDead;
        public bool CanBeDamaged => canBeDamaged;
        public float HPPercent => maxHP > 0 ? (float)currentHP / maxHP : 0f;
        public float DamageProgress => 1f - HPPercent; // 伤害进度（0-1）
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            if (spriteRenderer != null)
            {
                spriteMaterial = spriteRenderer.material;
            }
            
            // 初始化血量
            currentHP = maxHP;
        }
        
        private void OnDestroy()
        {
            shakeTween?.Kill();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 初始化宝箱
        /// </summary>
        /// <param name="type">宝箱类型</param>
        /// <param name="hp">血量</param>
        public void Initialize(CrateType type, int hp)
        {
            crateType = type;
            maxHP = hp;
            currentHP = hp;
            isDead = false;
            canBeDamaged = false;
            
            // 重置进度条
            if (progressUI != null)
            {
                progressUI.ResetProgress();
            }
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalCrate] 初始化: 类型={type}, HP={hp}");
            }
        }
        
        /// <summary>
        /// 播放入场动画（临时空实现，后续替换为无人机入场动画）
        /// </summary>
        public void PlayDropAnimation(float startY, float targetY, float duration, System.Action onComplete = null)
        {
            // 直接设置到目标位置
            Vector3 pos = transform.position;
            pos.y = targetY;
            transform.position = pos;
            originalPosition = pos;
    
            // 立即启用伤害
            canBeDamaged = true;
            onComplete?.Invoke();
    
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalCrate] 入场完成，可以受伤了");
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 伤害处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 受到伤害（由激光系统调用）
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <param name="knockbackForce">击退力（宝箱忽略）</param>
        /// <param name="isCrit">是否暴击</param>
        public void TakeDamage(float damage, Vector2 knockbackForce, bool isCrit = false)
        {
            if (isDead) return;
            if (!canBeDamaged) return;
            
            // 扣血
            currentHP = Mathf.Max(0, currentHP - Mathf.RoundToInt(damage));
            
            // 更新进度条
            if (progressUI != null)
            {
                progressUI.SetProgress(DamageProgress);
            }
            
            // 受击反馈
            PlayHitFeedback();
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalCrate] 受伤: -{damage:F1}, 剩余HP: {currentHP}/{maxHP}, 进度: {DamageProgress:P0}");
            }
            
            // 检查死亡
            if (currentHP <= 0)
            {
                Die();
            }
        }
        
        /// <summary>
        /// 播放受击反馈
        /// </summary>
        private void PlayHitFeedback()
        {
            // 抖动
            shakeTween?.Kill();
            transform.position = originalPosition;
            shakeTween = transform.DOShakePosition(0.15f, hitShakeStrength, 20, 90f, false, true);
            
            // 发光效果（如果有 Material）
            if (spriteMaterial != null)
            {
                // 假设使用的 Shader 有 _EmissionColor 属性
                spriteMaterial.SetColor("_EmissionColor", Color.white * hitGlowIntensity);
                DOTween.To(
                    () => hitGlowIntensity,
                    x => spriteMaterial.SetColor("_EmissionColor", Color.white * x),
                    0f,
                    0.2f
                );
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 死亡处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 宝箱被击破
        /// </summary>
        private void Die()
        {
            if (isDead) return;
            isDead = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalCrate] 宝箱被击破: {crateType}");
            }
            
            // 禁用碰撞
            if (crateCollider != null)
            {
                crateCollider.enabled = false;
            }
            
            // 播放进度条完成动画
            if (progressUI != null)
            {
                progressUI.PlayCompleteAnimation();
            }
            
            // 播放爆炸特效
            PlayExplosionVFX();
            
            // 通知管理器
            if (TacticalDropManager.Instance != null)
            {
                TacticalDropManager.Instance.OnCrateDestroyed(this);
            }
            
            // 隐藏宝箱
            if (spriteRenderer != null)
            {
                spriteRenderer.DOFade(0f, 0.2f);
            }
            
            // 延迟销毁
            Destroy(gameObject, 1f);
        }
        
        /// <summary>
        /// 被其他宝箱击破导致消失
        /// </summary>
        public void Vanish()
        {
            if (isDead) return;
            isDead = true;
            
            if (showDebugInfo)
            {
                Debug.Log($"[TacticalCrate] 宝箱消失: {crateType}");
            }
            
            // 禁用碰撞
            if (crateCollider != null)
            {
                crateCollider.enabled = false;
            }
            
            // 隐藏进度条
            if (progressUI != null)
            {
                progressUI.HideImmediate();
            }

            // 缩小消失动画
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 特效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 播放爆炸特效
        /// </summary>
        private void PlayExplosionVFX()
        {
            if (explosionVFXPrefab != null)
            {
                Instantiate(explosionVFXPrefab, transform.position, Quaternion.identity);
            }
            
            // 也可以使用 VFXPoolManager
            if (VFXPoolManager.Instance != null)
            {
                // 根据宝箱类型播放不同特效
                switch (crateType)
                {
                    case CrateType.Supply:
                        // 蓝绿色医疗特效
                        VFXPoolManager.Instance.PlayShieldRecover(transform.position);
                        break;
                    case CrateType.Gacha:
                        // 金色烟花特效
                        VFXPoolManager.Instance.PlayEnemyExplosion(transform.position);
                        break;
                    case CrateType.Deal:
                        // 红黑色邪恶特效
                        VFXPoolManager.Instance.PlayShieldBreak(transform.position);
                        break;
                }
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 绘制宝箱碰撞范围
            if (crateCollider != null && crateCollider is BoxCollider2D box)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
            }
        }
        
        [ContextMenu("Test: Take 100 Damage")]
        private void TestTakeDamage()
        {
            canBeDamaged = true;
            TakeDamage(100f, Vector2.zero, false);
        }
        
        [ContextMenu("Test: Kill")]
        private void TestKill()
        {
            canBeDamaged = true;
            TakeDamage(maxHP, Vector2.zero, false);
        }
#endif
    }
}
