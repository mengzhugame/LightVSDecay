// ============================================================
// BattleTutorialDirector.cs
// 文件位置: Assets/Scripts/UI/Tutorial/BattleTutorialDirector.cs
//
// 用途：战斗场景内的新手引导
//   Step 1 — 激光操控引导：首次进入战斗，显示手指+左右弧线箭头 Prefab，
//             检测到玩家在屏幕上拖动即结束。
//   Step 2 — 大招引导：首次大招就绪前约10秒（充能≥83%），
//             屏幕顶部涌入15只0经验小怪，大招就绪时在按钮旁显示手指 Prefab，
//             玩家点击大招（状态变 Active）即结束。
//
// 注意：两个引导均不使用 SpotlightOverlay 挖孔遮罩（战斗内不阻挡操作）。
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.UI.Tutorial
{
    public class BattleTutorialDirector : MonoBehaviour
    {
        [Header("激光操控引导")]
        [SerializeField] private TutorialStepConfigSO laserConfig;
        [Tooltip("判定为有效拖动的最小滑动量（像素/帧），防止误触")]
        [SerializeField] private float swipeThreshold = 8f;

        [Header("大招引导")]
        [SerializeField] private TutorialStepConfigSO overloadConfig;
        [Tooltip("大招按钮 RectTransform，用于定位手指 Prefab")]
        [SerializeField] private RectTransform overloadButtonRect;

        [Header("大招前置小怪（首次大招引导专用）")]
        [Tooltip("触发涌入的大招充能进度阈值（0~1，默认0.83≈50秒）")]
        [SerializeField] private float preUltimateSpawnThreshold = 0.83f;
        [Tooltip("涌入的小怪数量")]
        [SerializeField] private int preUltimateSpawnCount = 15;
        [Tooltip("相邻两只小怪的生成间隔（秒），产生动感涌入效果")]
        [SerializeField] private float spawnInterval = 0.05f;

        // 运行时
        private GameObject _currentPrefab;
        private bool _shouldShowLaserTutorial;
        private bool _shouldShowOverloadTutorial;
        private Coroutine _preUltimateSpawnCoroutine;
        private WaitForSeconds _spawnIntervalWait;

        // ─── 生命周期 ─────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // 等一帧，确保 ProgressManager 和 GameEvents 已初始化
            yield return null;

            // 缓存 WaitForSeconds（规范：协程中禁止每帧 new WaitForSeconds）
            _spawnIntervalWait = new WaitForSeconds(spawnInterval);

            var meta = ProgressManager.Instance?.Meta;
            if (meta == null) yield break;

            // 在 Start 时缓存状态，避免被 OverloadManager.ShowReadyBubble 修改后读到错误值
            _shouldShowLaserTutorial    = !meta.hasSeenLaserTutorial;
            _shouldShowOverloadTutorial = !meta.hasSeenOverloadTutorial;

            if (_shouldShowLaserTutorial)
            {
                SpawnPrefab(laserConfig);
            }

            // 首次大招引导：提前在充能约83%时涌入小怪，营造割草爽感
            if (_shouldShowOverloadTutorial)
            {
                _preUltimateSpawnCoroutine = StartCoroutine(PreUltimateSpawnRoutine());
            }
        }

        private void OnEnable()
        {
            GameEvents.OnOverloadStateChanged += OnOverloadStateChanged;
            GameEvents.OnGameVictory           += OnGameEnd;
            GameEvents.OnGameDefeat            += OnGameEnd;
        }

        private void OnDisable()
        {
            GameEvents.OnOverloadStateChanged -= OnOverloadStateChanged;
            GameEvents.OnGameVictory           -= OnGameEnd;
            GameEvents.OnGameDefeat            -= OnGameEnd;
        }

        // ─── 每帧检测拖动 ─────────────────────────────────────────────────

        private void Update()
        {
            if (!_shouldShowLaserTutorial) return;

            bool swipeDetected = false;

            // 鼠标拖动：编辑器 / PC Standalone / 安卓模拟器（模拟器发送的是鼠标事件而非 touch）
            if (Input.GetMouseButton(0) && Mathf.Abs(Input.GetAxis("Mouse X")) > 0.05f)
                swipeDetected = true;

            // 触摸滑动：真机（同时保留，两者互不干扰）
            if (!swipeDetected && Input.touchCount > 0
                && Input.GetTouch(0).deltaPosition.magnitude > swipeThreshold)
                swipeDetected = true;

            if (swipeDetected)
                CompleteLaserTutorial();
        }

        // ─── 大招状态变化 ─────────────────────────────────────────────────

        private void OnOverloadStateChanged(OverloadState state)
        {
            if (!_shouldShowOverloadTutorial) return;

            if (state == OverloadState.Ready)
            {
                // 大招就绪：切换为大招引导 Prefab
                DestroyCurrent();
                SpawnOverloadPrefab();
            }
            else if (state == OverloadState.Active)
            {
                // 玩家点击了大招，引导完成
                CompleteOverloadTutorial();
            }
        }

        // ─── 大招前置小怪涌入 ─────────────────────────────────────────────

        private IEnumerator PreUltimateSpawnRoutine()
        {
            // 等待 OverloadManager 初始化（可能在同一帧的其他 Awake/Start 中）
            while (OverloadManager.Instance == null) yield return null;

            // 等到大招充能进度达到阈值（约50秒）
            while (OverloadManager.Instance.ChargeProgress < preUltimateSpawnThreshold)
                yield return null;

            int count = preUltimateSpawnCount;

            // 通知 WaveManager 追加计数，防止前置小怪击杀导致当前波次提前完成
            WaveManager.Instance?.AddBonusEnemyCount(count);

            // 计算屏幕顶部的世界坐标生成区域
            Camera cam = Camera.main;
            float spawnY = cam.ViewportToWorldPoint(new Vector3(0.5f, 1.05f, cam.nearClipPlane)).y;
            float leftX  = cam.ViewportToWorldPoint(new Vector3(0.08f, 0f, cam.nearClipPlane)).x;
            float rightX = cam.ViewportToWorldPoint(new Vector3(0.92f, 0f, cam.nearClipPlane)).x;

            // 均匀分布 + 随机抖动，分批生成产生"涌入"动感
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = Mathf.Lerp(leftX, rightX, t) + Random.Range(-0.4f, 0.4f);
                Vector3 spawnPos = new Vector3(x, spawnY, 0f);

                var enemy = EnemyPoolManager.Instance?.Spawn(EnemyType.Slime, spawnPos);
                if (enemy != null)
                    enemy.SetXPReward(0); // 0经验，不影响升级节奏；金币保持正常

                yield return _spawnIntervalWait;
            }

            _preUltimateSpawnCoroutine = null;
        }

        // ─── 完成逻辑 ─────────────────────────────────────────────────────

        private void CompleteLaserTutorial()
        {
            _shouldShowLaserTutorial = false;
            DestroyCurrent();
            SaveFlag(ref ProgressManager.Instance.Meta.hasSeenLaserTutorial);
        }

        private void CompleteOverloadTutorial()
        {
            _shouldShowOverloadTutorial = false;
            DestroyCurrent();
            SaveFlag(ref ProgressManager.Instance.Meta.hasSeenOverloadTutorial);
        }

        private void SaveFlag(ref bool flag)
        {
            if (ProgressManager.Instance == null) return;
            flag = true;
            ProgressManager.Instance.Meta.Save();
        }

        private void OnGameEnd()
        {
            _shouldShowLaserTutorial    = false;
            _shouldShowOverloadTutorial = false;
            DestroyCurrent();

            if (_preUltimateSpawnCoroutine != null)
            {
                StopCoroutine(_preUltimateSpawnCoroutine);
                _preUltimateSpawnCoroutine = null;
            }
        }

        // ─── Prefab 管理 ──────────────────────────────────────────────────

        private void SpawnPrefab(TutorialStepConfigSO config)
        {
            if (config == null || string.IsNullOrEmpty(config.prefabPath)) return;

            DestroyCurrent();

            GameObject prefab = Resources.Load<GameObject>(config.prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[BattleTutorialDirector] 找不到 Prefab: {config.prefabPath}");
                return;
            }

            _currentPrefab = Instantiate(prefab);
            _currentPrefab.name = $"Tutorial_{config.stepID}";
            DisableRaycastTargets(_currentPrefab);

            Transform parent = string.IsNullOrEmpty(config.parentPath)
                ? FindRootCanvas()
                : FindByPath(config.parentPath);

            if (parent != null)
                _currentPrefab.transform.SetParent(parent, false);

            var rect = _currentPrefab.transform as RectTransform;
            if (rect != null)
            {
                rect.localPosition = config.localPosition;
                rect.localScale    = config.localScale;
            }
        }

        private void SpawnOverloadPrefab()
        {
            if (overloadConfig == null || overloadButtonRect == null) return;

            GameObject prefab = Resources.Load<GameObject>(overloadConfig.prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[BattleTutorialDirector] 找不到大招 Prefab: {overloadConfig.prefabPath}");
                return;
            }

            _currentPrefab = Instantiate(prefab);
            _currentPrefab.name = $"Tutorial_{overloadConfig.stepID}";
            DisableRaycastTargets(_currentPrefab);

            // 挂到大招按钮的父节点，定位在按钮上方
            Transform parent = overloadButtonRect.parent ?? FindRootCanvas();
            _currentPrefab.transform.SetParent(parent, false);

            var rect = _currentPrefab.transform as RectTransform;
            if (rect != null)
            {
                rect.anchoredPosition = overloadButtonRect.anchoredPosition
                                        + (Vector2)overloadConfig.localPosition;
                rect.localScale       = overloadConfig.localScale;
            }
        }

        private static void DisableRaycastTargets(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
        }

        private void DestroyCurrent()
        {
            if (_currentPrefab != null)
            {
                Destroy(_currentPrefab);
                _currentPrefab = null;
            }
        }

        // ─── 辅助 ────────────────────────────────────────────────────────

        private static Transform FindByPath(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null ? obj.transform : null;
        }

        private static Transform FindRootCanvas()
        {
            var canvas = FindObjectOfType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }
    }
}
