using System.Collections;
using UnityEngine;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// 黑油怪物眼睛行为
    /// 功能：注视塔、随机眨眼、受击眯眼
    /// </summary>
    public class EnemyEyes : MonoBehaviour
    {
        [Header("LookAt Settings")]
        [SerializeField] private bool enableLookAt = true;
        [SerializeField] private float lookAtSmoothness = 5f; // 眼睛旋转平滑度
        
        [Header("Blink Settings")]
        [SerializeField] private bool enableBlink = true;
        [SerializeField] private float blinkIntervalMin = 2f; // 眨眼间隔（最小）
        [SerializeField] private float blinkIntervalMax = 5f; // 眨眼间隔（最大）
        [SerializeField] private float blinkDuration = 0.1f; // 眨眼持续时间
        [SerializeField] private float blinkScaleY = 0.1f; // 眨眼时Y轴缩放
        
        [Header("Squint Settings (受击眯眼)")]
        [SerializeField] private float squintDuration = 0.15f;
        [SerializeField] private float squintScaleY = 0.5f; // 眯眼时Y轴缩放
        
        [Header("References")]
        [SerializeField] private SpriteRenderer eyesSprite; // 眼睛Sprite
        
        // 运行时数据
        private Transform targetTower;
        private Vector3 originalScale;
        private Coroutine blinkCoroutine;
        private bool isSquinting = false;
        
        private void Awake()
        {
            if (eyesSprite == null)
            {
                eyesSprite = GetComponent<SpriteRenderer>();
            }
            
            originalScale = transform.localScale;
            
            // 查找光棱塔
            GameObject tower = GameObject.FindGameObjectWithTag("Tower");
            if (tower != null)
            {
                targetTower = tower.transform;
            }
            else
            {
                Debug.LogWarning("[EnemyEyes] Tower not found! LookAt disabled.");
                enableLookAt = false;
            }
        }
        
        private void Start()
        {
            // 启动眨眼协程
            if (enableBlink)
            {
                blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }
        
        private void Update()
        {
            if (enableLookAt && targetTower != null)
            {
                LookAtTower();
            }
        }
        
        /// <summary>
        /// 眼睛始终注视光棱塔
        /// </summary>
        private void LookAtTower()
        {
            Vector3 direction = targetTower.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            // 平滑旋转
            Quaternion targetRotation = Quaternion.Euler(0, 0, angle + 90); // -90因为Sprite朝上
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lookAtSmoothness);
        }
        
        /// <summary>
        /// 随机眨眼协程
        /// </summary>
        private IEnumerator BlinkRoutine()
        {
            while (true)
            {
                // 等待随机间隔
                float interval = Random.Range(blinkIntervalMin, blinkIntervalMax);
                yield return new WaitForSeconds(interval);
                
                // 执行眨眼（如果没有在眯眼）
                if (!isSquinting)
                {
                    yield return StartCoroutine(BlinkAnimation());
                }
            }
        }
        
        /// <summary>
        /// 眨眼动画
        /// </summary>
        private IEnumerator BlinkAnimation()
        {
            // 闭眼
            transform.localScale = new Vector3(originalScale.x, originalScale.y * blinkScaleY, originalScale.z);
            
            yield return new WaitForSeconds(blinkDuration);
            
            // 睁眼
            transform.localScale = originalScale;
        }
        
        /// <summary>
        /// 受击眯眼（公开接口，由EnemyBlob调用）
        /// </summary>
        public void TriggerSquint()
        {
            if (!isSquinting)
            {
                StartCoroutine(SquintAnimation());
            }
        }
        
        /// <summary>
        /// 眯眼动画
        /// </summary>
        private IEnumerator SquintAnimation()
        {
            isSquinting = true;
            
            // 眯眼
            transform.localScale = new Vector3(originalScale.x, originalScale.y * squintScaleY, originalScale.z);
            
            yield return new WaitForSeconds(squintDuration);
            
            // 恢复
            transform.localScale = originalScale;
            
            isSquinting = false;
        }
        
        /// <summary>
        /// 停止眨眼（怪物死亡时调用）
        /// </summary>
        public void StopBlink()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
            }
        }
        
        private void OnDestroy()
        {
            StopBlink();
        }
    }
}