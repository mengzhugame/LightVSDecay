// ============================================================
// DrifterSpawnHelper.cs
// 文件位置: Assets/Scripts/Logic/Enemy/DrifterSpawnHelper.cs
// 用途：Drifter 专用生成辅助 - 屏幕内安全区域生成 + 传送门特效 + 缩放动画
// ============================================================

using UnityEngine;
using System.Collections;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// Drifter 生成配置
    /// </summary>
    [System.Serializable]
    public class DrifterSpawnConfig
    {
        [Header("安全区域")]
        [Tooltip("距离塔/护盾的最小安全距离")]
        public float minDistanceFromTower = 6f;
        
        [Tooltip("距离墙壁的最小距离")]
        public float minDistanceFromWall = 1.5f;
        
        [Tooltip("只在屏幕上半区生成")]
        public bool spawnInUpperHalf = true;
        
        [Header("特效时间")]
        [Tooltip("传送门特效持续时间")]
        public float portalEffectDuration = 0.5f;
        
        [Tooltip("缩放动画持续时间")]
        public float scaleAnimDuration = 0.3f;
        
        [Header("特效引用（可选）")]
        [Tooltip("传送门特效 Prefab（可为空，使用占位效果）")]
        public GameObject portalEffectPrefab;
    }
    
    /// <summary>
    /// Drifter 生成辅助器
    /// 单例模式，管理 Drifter 的特殊生成逻辑
    /// </summary>
    public class DrifterSpawnHelper : Singleton<DrifterSpawnHelper>
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("生成配置")]
        [SerializeField] private DrifterSpawnConfig config = new DrifterSpawnConfig();
        
        [Header("塔引用")]
        [Tooltip("玩家塔的 Transform（用于计算安全距离）")]
        [SerializeField] private Transform towerTransform;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showGizmos = true;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 缓存
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private int bouncingEnemyLayerIndex;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        protected override void OnSingletonAwake()
        {
            // 缓存 Layer
            bouncingEnemyLayerIndex = LayerMask.NameToLayer(GameConstants.BOUNCING_ENEMY_LAYER);
            
            // 自动查找塔
            if (towerTransform == null)
            {
                var tower = FindObjectOfType<Player.TurretHealth>();
                if (tower != null)
                {
                    towerTransform = tower.transform;
                }
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 生成 Drifter（带传送门特效和缩放动画）
        /// </summary>
        /// <param name="onSpawnComplete">生成完成回调（返回 EnemyBlob 实例）</param>
        public void SpawnDrifter(System.Action<EnemyBlob> onSpawnComplete = null)
        {
            StartCoroutine(SpawnDrifterCoroutine(onSpawnComplete));
        }
        
        /// <summary>
        /// 获取安全生成位置
        /// </summary>
        public Vector3 GetSafeSpawnPosition()
        {
            return CalculateSafePosition();
        }
        
        /// <summary>
        /// 检查位置是否安全
        /// </summary>
        public bool IsPositionSafe(Vector3 position)
        {
            return ValidatePosition(position);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生成流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// Drifter 生成协程
        /// </summary>
        private IEnumerator SpawnDrifterCoroutine(System.Action<EnemyBlob> onSpawnComplete)
        {
            // 1. 计算安全位置
            Vector3 spawnPos = CalculateSafePosition();
            
            if (showDebugInfo)
            {
                Debug.Log($"[DrifterSpawnHelper] 准备在 {spawnPos} 生成 Drifter");
            }
            
            // 2. 播放传送门特效
            GameObject portalEffect = null;
            if (config.portalEffectPrefab != null)
            {
                portalEffect = Instantiate(config.portalEffectPrefab, spawnPos, Quaternion.identity);
            }
            else
            {
                // 占位特效：简单的缩放圆圈（后续替换）
                portalEffect = CreatePlaceholderPortalEffect(spawnPos);
            }
            
            // 等待特效播放
            yield return new WaitForSeconds(config.portalEffectDuration);
            
            // 销毁特效
            if (portalEffect != null)
            {
                Destroy(portalEffect);
            }
            
            // 3. 从对象池生成 Drifter
            if (EnemyPoolManager.Instance == null)
            {
                Debug.LogError("[DrifterSpawnHelper] EnemyPoolManager 不存在！");
                yield break;
            }
            
            EnemyBlob drifter = EnemyPoolManager.Instance.Spawn(EnemyType.Drifter, spawnPos);
            
            if (drifter == null)
            {
                Debug.LogWarning("[DrifterSpawnHelper] Drifter 生成失败（可能达到上限）");
                yield break;
            }
            
            // 4. 设置 Layer 为 BouncingEnemy（直接可撞墙）
            drifter.gameObject.layer = bouncingEnemyLayerIndex;
            
            // 5. 执行缩放动画
            yield return StartCoroutine(ScaleInAnimation(drifter));
            
            // 6. 通知 Drifter 已完全入境
            drifter.SetFullyEnteredScreen();
            
            if (showDebugInfo)
            {
                Debug.Log($"[DrifterSpawnHelper] Drifter 生成完成 @ {spawnPos}");
            }
            
            // 7. 回调
            onSpawnComplete?.Invoke(drifter);
        }
        
        /// <summary>
        /// 缩放入场动画
        /// </summary>
        private IEnumerator ScaleInAnimation(EnemyBlob enemy)
        {
            if (enemy == null) yield break;
            
            Transform t = enemy.transform;
            Vector3 targetScale = t.localScale;
            
            // 从 0 开始
            t.localScale = Vector3.zero;
            
            float elapsed = 0f;
            float duration = config.scaleAnimDuration;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                // 使用 EaseOutBack 曲线，产生"弹出"效果
                float easedProgress = EaseOutBack(progress);
                
                t.localScale = targetScale * easedProgress;
                
                yield return null;
            }
            
            // 确保最终缩放正确
            t.localScale = targetScale;
        }
        
        /// <summary>
        /// EaseOutBack 缓动函数（弹出效果）
        /// </summary>
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 位置计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 计算安全生成位置
        /// </summary>
        private Vector3 CalculateSafePosition()
        {
            // 获取屏幕边界
            if (ScreenBoundaryManager.Instance == null)
            {
                Debug.LogWarning("[DrifterSpawnHelper] ScreenBoundaryManager 不存在，使用默认位置");
                return new Vector3(0, 5f, 0);
            }
            
            float left = ScreenBoundaryManager.Instance.ScreenLeft + config.minDistanceFromWall;
            float right = ScreenBoundaryManager.Instance.ScreenRight - config.minDistanceFromWall;
            float top = ScreenBoundaryManager.Instance.ScreenTop - config.minDistanceFromWall;
            float bottom = ScreenBoundaryManager.Instance.ScreenBottom + config.minDistanceFromWall;
            
            // 只在上半区生成
            if (config.spawnInUpperHalf)
            {
                float midY = (top + bottom) * 0.5f;
                bottom = midY;
            }
            
            // 尝试找到安全位置（最多尝试20次）
            for (int i = 0; i < 20; i++)
            {
                float x = Random.Range(left, right);
                float y = Random.Range(bottom, top);
                Vector3 candidate = new Vector3(x, y, 0);
                
                if (ValidatePosition(candidate))
                {
                    return candidate;
                }
            }
            
            // 找不到理想位置，返回屏幕上方中央
            if (showDebugInfo)
            {
                Debug.LogWarning("[DrifterSpawnHelper] 无法找到理想安全位置，使用备选位置");
            }
            
            return new Vector3(0, top - 1f, 0);
        }
        
        /// <summary>
        /// 验证位置是否安全
        /// </summary>
        private bool ValidatePosition(Vector3 position)
        {
            // 检查与塔的距离
            if (towerTransform != null)
            {
                float distToTower = Vector3.Distance(position, towerTransform.position);
                if (distToTower < config.minDistanceFromTower)
                {
                    return false;
                }
            }
            
            // 检查是否在安全区域内
            if (ScreenBoundaryManager.Instance != null)
            {
                float left = ScreenBoundaryManager.Instance.ScreenLeft + config.minDistanceFromWall;
                float right = ScreenBoundaryManager.Instance.ScreenRight - config.minDistanceFromWall;
                float top = ScreenBoundaryManager.Instance.ScreenTop - config.minDistanceFromWall;
                float bottom = ScreenBoundaryManager.Instance.ScreenBottom + config.minDistanceFromWall;
                
                if (position.x < left || position.x > right ||
                    position.y < bottom || position.y > top)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 占位特效
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 创建占位传送门特效（简单的缩放圆圈）
        /// </summary>
        private GameObject CreatePlaceholderPortalEffect(Vector3 position)
        {
            // 创建一个简单的圆形 Sprite 作为占位
            GameObject portal = new GameObject("PortalEffect_Placeholder");
            portal.transform.position = position;
            
            // 添加 SpriteRenderer
            SpriteRenderer sr = portal.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(0f, 1f, 1f, 0.8f); // 青色（霓虹风格）
            sr.sortingOrder = 100;
            
            // 启动缩放动画
            StartCoroutine(PortalEffectAnimation(portal));
            
            return portal;
        }
        
        /// <summary>
        /// 占位特效动画
        /// </summary>
        private IEnumerator PortalEffectAnimation(GameObject portal)
        {
            if (portal == null) yield break;
            
            Transform t = portal.transform;
            SpriteRenderer sr = portal.GetComponent<SpriteRenderer>();
            
            float duration = config.portalEffectDuration;
            float elapsed = 0f;
            
            while (elapsed < duration && portal != null)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                
                // 缩放：0 → 1.5 → 0
                float scale;
                if (progress < 0.5f)
                {
                    scale = Mathf.Lerp(0f, 1.5f, progress * 2f);
                }
                else
                {
                    scale = Mathf.Lerp(1.5f, 0.5f, (progress - 0.5f) * 2f);
                }
                
                t.localScale = Vector3.one * scale;
                
                // 旋转
                t.Rotate(Vector3.forward, 360f * Time.deltaTime);
                
                // 透明度脉冲
                if (sr != null)
                {
                    float alpha = 0.5f + 0.3f * Mathf.Sin(progress * Mathf.PI * 4f);
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// 创建简单的圆形 Sprite（运行时生成）
        /// </summary>
        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            
            Color transparent = new Color(0, 0, 0, 0);
            Color white = Color.white;
            
            float center = size * 0.5f;
            float radius = size * 0.4f;
            float innerRadius = size * 0.3f;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    
                    // 环形
                    if (dist < radius && dist > innerRadius)
                    {
                        float edge = 1f - Mathf.Abs(dist - (radius + innerRadius) * 0.5f) / ((radius - innerRadius) * 0.5f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, edge));
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }
            
            tex.Apply();
            
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Gizmos
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (ScreenBoundaryManager.Instance == null) return;
            
            // 绘制安全生成区域
            float left = ScreenBoundaryManager.Instance.ScreenLeft + config.minDistanceFromWall;
            float right = ScreenBoundaryManager.Instance.ScreenRight - config.minDistanceFromWall;
            float top = ScreenBoundaryManager.Instance.ScreenTop - config.minDistanceFromWall;
            float bottom = ScreenBoundaryManager.Instance.ScreenBottom + config.minDistanceFromWall;
            
            if (config.spawnInUpperHalf)
            {
                bottom = (top + bottom) * 0.5f;
            }
            
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Vector3 center = new Vector3((left + right) * 0.5f, (top + bottom) * 0.5f, 0);
            Vector3 size = new Vector3(right - left, top - bottom, 0.1f);
            Gizmos.DrawCube(center, size);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(center, size);
            
            // 绘制塔的安全距离
            if (towerTransform != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawWireSphere(towerTransform.position, config.minDistanceFromTower);
            }
        }
    }
}