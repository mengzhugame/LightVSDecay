// ============================================================
// BattleTutorialDirector.cs
// 文件位置: Assets/Scripts/UI/Tutorial/BattleTutorialDirector.cs
//
// 用途：战斗场景内的新手引导
//   Step 1 — 激光操控引导：首次进入战斗，显示手指+左右弧线箭头 Prefab，
//             检测到玩家在屏幕上拖动即结束。
//   Step 2 — 大招引导：首次大招就绪，在大招按钮旁显示手指 Prefab，
//             玩家点击大招（状态变 Active）即结束。
//
// 注意：两个引导均不使用 SpotlightOverlay 挖孔遮罩（战斗内不阻挡操作）。
// ============================================================

using System.Collections;
using UnityEngine;
using LightVsDecay.Core;
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

        // 运行时
        private GameObject _currentPrefab;
        private bool _shouldShowLaserTutorial;
        private bool _shouldShowOverloadTutorial;

        // ─── 生命周期 ─────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // 等一帧，确保 ProgressManager 和 GameEvents 已初始化
            yield return null;

            var meta = ProgressManager.Instance?.Meta;
            if (meta == null) yield break;

            // 在 Start 时缓存状态，避免被 OverloadManager.ShowReadyBubble 修改后读到错误值
            _shouldShowLaserTutorial    = !meta.hasSeenLaserTutorial;
            _shouldShowOverloadTutorial = !meta.hasSeenOverloadTutorial;

            if (_shouldShowLaserTutorial)
            {
                SpawnPrefab(laserConfig);
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
