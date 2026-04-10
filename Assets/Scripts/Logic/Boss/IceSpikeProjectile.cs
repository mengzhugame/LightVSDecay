// ============================================================
// IceSpikeProjectile.cs
// 文件位置: Assets/Scripts/Logic/Boss/IceSpikeProjectile.cs
// 用途：极寒之核技能4"绝对零度"发射的冰刺投射体。
//       HP=5000，可被激光击落。命中光棱塔或超时后触发回调。
// 预制体需挂载：Rigidbody2D + CircleCollider2D(isTrigger)
// 预制体层级：BossPollutionBall（使激光可以检测到）
// ============================================================

using UnityEngine;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 极寒之核"绝对零度"冰刺投射体。
    /// </summary>
    public class IceSpikeProjectile : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 16f;
        [SerializeField] private float maxLifetime = 10f;

        private float currentHP;
        private bool isResolved;
        private Vector2 moveDirection;
        private float elapsed;
        private Rigidbody2D rb;
        private AudioSource flightAudioSource;
        private bool audioPausedByOverlay;

        public bool IsDestroyed => isResolved;

        /// <summary>冰刺被激光摧毁时回调（计入"被拦截"）</summary>
        public System.Action OnDestroyedByLaser;

        /// <summary>冰刺命中塔/护盾或超时时回调（计入"未被拦截"）</summary>
        public System.Action OnReachedTower;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            flightAudioSource = GetComponent<AudioSource>();

            if (flightAudioSource != null)
            {
                flightAudioSource.playOnAwake = false;
                flightAudioSource.loop = true;
            }
        }

        /// <summary>初始化并发射</summary>
        public void Launch(Vector2 direction, float hp = 5000f)
        {
            moveDirection = direction.normalized;
            currentHP = hp;
            isResolved = false;
            elapsed = 0f;
            audioPausedByOverlay = false;

            if (rb != null)
                rb.velocity = moveDirection * moveSpeed;

            StartFlightAudio();
        }

        private void Update()
        {
            if (isResolved) return;

            UpdateFlightAudioPauseState();
            elapsed += Time.deltaTime;
            if (elapsed >= maxLifetime)
                Resolve(hitTower: true);
        }

        /// <summary>被激光命中时调用（由 LaserController 驱动）</summary>
        public void TakeDamage(float damage)
        {
            if (isResolved) return;
            currentHP -= damage;
            if (currentHP <= 0f)
                Resolve(hitTower: false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isResolved) return;
            if (other.CompareTag("Tower") || other.CompareTag("Shield"))
                Resolve(hitTower: true);
        }

        private void Resolve(bool hitTower)
        {
            if (isResolved) return;
            isResolved = true;
            StopFlightAudio();

            if (rb != null) rb.velocity = Vector2.zero;

            if (hitTower)
            {
                OnReachedTower?.Invoke();
            }
            else
            {
                // 被激光击落：播放爆炸特效和音效
                VFXPoolManager.Instance?.PlayProjectileExplosion(transform.position);
                AudioManager.Instance?.PlayProjectileExplode();
                OnDestroyedByLaser?.Invoke();
            }

            gameObject.SetActive(false);
        }

        private void StartFlightAudio()
        {
            if (flightAudioSource == null || flightAudioSource.clip == null)
                return;

            if (!flightAudioSource.isPlaying)
                flightAudioSource.Play();
        }

        private void StopFlightAudio()
        {
            if (flightAudioSource == null)
                return;

            audioPausedByOverlay = false;
            if (flightAudioSource.isPlaying)
                flightAudioSource.Stop();
        }

        private void UpdateFlightAudioPauseState()
        {
            if (flightAudioSource == null || isResolved)
                return;

            if (Time.timeScale <= 0f)
            {
                if (flightAudioSource.isPlaying)
                {
                    flightAudioSource.Pause();
                    audioPausedByOverlay = true;
                }
                return;
            }

            if (audioPausedByOverlay)
            {
                flightAudioSource.UnPause();
                audioPausedByOverlay = false;
            }
        }
    }
}
