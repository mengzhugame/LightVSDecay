using System.Collections;
using UnityEngine;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic;
using LightVsDecay.UI.TechTree;
using LightVsDecay.UI.Equipment;

namespace LightVsDecay.UI.Tutorial
{
    /// <summary>
    /// 主界面新手引导控制器
    ///
    /// 负责：
    ///   1. 进入主界面时检测解锁提示（科技树/装备系统），调用 TipsPanelController 显示
    ///   2. 进入科技树/装备界面时，驱动配置文件驱动的引导步骤：
    ///      SDF 挖孔遮罩（TutorialSpotlightOverlay）+ 手指特效 Prefab 同时显示
    ///
    /// 配置：每个引导步骤绑定一个 TutorialStepConfigSO 资产（Inspector 中赋值）
    /// </summary>
    public class MainSceneTutorialDirector : MonoBehaviour
    {
        // ── 科技树引导状态 ────────────────────────────────────────────────
        private enum TechTutorialStep { None, SelectNode, UpgradeNode, CloseDetail, BackToMain, Completed }

        // ── 装备引导状态 ──────────────────────────────────────────────────
        private enum EquipmentTutorialStep { None, SelectItem, EquipItem, BackToMain, Completed }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("SDF 挖孔遮罩")]
        [SerializeField] private TutorialSpotlightOverlay spotlightOverlay;

        [Header("科技树引导配置（4步）")]
        [SerializeField] private TutorialStepConfigSO techSelectNodeConfig;
        [SerializeField] private TutorialStepConfigSO techUpgradeNodeConfig;
        [SerializeField] private TutorialStepConfigSO techCloseDetailConfig;
        [SerializeField] private TutorialStepConfigSO techBackToMainConfig;

        [Header("科技树面板引用")]
        [SerializeField] private TechTreePanel techTreePanel;
        [SerializeField] private TechTreeNodeDetailPanel techTreeDetailPanel;
        [SerializeField] private string firstTechNodeId = "node_fp_cap1";

        [Header("装备引导配置（3步）")]
        [SerializeField] private TutorialStepConfigSO equipSelectItemConfig;
        [SerializeField] private TutorialStepConfigSO equipEquipItemConfig;
        [SerializeField] private TutorialStepConfigSO equipBackToMainConfig;

        [Header("装备面板引用")]
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private ItemInfoPanel itemInfoPanel;

        [Header("调试 - 手指位置微调")]
        [Tooltip("开启后可在 Play 模式下实时拖动 debugFingerOffset，找到合适值后填入对应 TutorialStepConfigSO.localPosition")]
        [SerializeField] private bool debugFingerPos = false;
        [Tooltip("手指偏移调试值（像素），找好后复制到 TutorialStepConfigSO.localPosition")]
        [SerializeField] private Vector2 debugFingerOffset = Vector2.zero;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private TechTutorialStep      _techStep      = TechTutorialStep.None;
        private EquipmentTutorialStep _equipmentStep = EquipmentTutorialStep.None;
        private GameObject            _currentFingerObject;
        private RectTransform         _currentFingerTarget;  // 手指跟踪目标（每帧更新位置用）
        private Vector2               _currentFingerOffset;  // config.localPosition 缓存

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Start()
        {
            // 延迟 0.5s 等 TipsPanelController 和 SystemUnlockManager 完成初始化
            StartCoroutine(CheckUnlockTipsDelayed());
        }

        private void Update()
        {
            // 每帧跟踪手指目标位置：
            //   正常模式 → 用 _currentFingerOffset（config.localPosition）
            //   调试模式 → 改用 debugFingerOffset，方便运行时微调
            if (_currentFingerObject == null || _currentFingerTarget == null) return;
            Vector2 offset = debugFingerPos ? debugFingerOffset : _currentFingerOffset;
            ApplyFingerPosition(_currentFingerObject.transform as RectTransform, _currentFingerTarget, offset);
        }

        private void OnEnable()
        {
            MainSceneUIManager.OnStateChanged      += OnMainSceneStateChanged;
            TopAreaController.BackClicked          += OnBackClicked;
            TechTreeNodeDetailPanel.DetailShown    += OnTechDetailShown;
            TechTreeNodeDetailPanel.DetailClosed   += OnTechDetailClosed;
            TechTreeNodeDetailPanel.NodeUpgraded   += OnTechNodeUpgraded;
            ItemInfoPanel.InfoShown                += OnEquipmentInfoShown;
            ItemInfoPanel.Equipped                 += OnEquipmentEquipped;
            ItemInfoPanel.InfoClosed               += OnEquipmentInfoClosed;
        }

        private void OnDisable()
        {
            MainSceneUIManager.OnStateChanged      -= OnMainSceneStateChanged;
            TopAreaController.BackClicked          -= OnBackClicked;
            TechTreeNodeDetailPanel.DetailShown    -= OnTechDetailShown;
            TechTreeNodeDetailPanel.DetailClosed   -= OnTechDetailClosed;
            TechTreeNodeDetailPanel.NodeUpgraded   -= OnTechNodeUpgraded;
            ItemInfoPanel.InfoShown                -= OnEquipmentInfoShown;
            ItemInfoPanel.Equipped                 -= OnEquipmentEquipped;
            ItemInfoPanel.InfoClosed               -= OnEquipmentInfoClosed;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 解锁提示（TipsPanel）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator CheckUnlockTipsDelayed()
        {
            yield return new WaitForSeconds(0.5f);

            var meta       = ProgressManager.Instance?.Meta;
            var unlockMgr  = SystemUnlockManager.Instance;
            var tipsCtrl   = TipsPanelController.Instance;

            if (meta == null || unlockMgr == null || tipsCtrl == null) yield break;

            if (unlockMgr.IsTechTreeUnlocked && !meta.hasShownTechTreeUnlockTip)
            {
                meta.hasShownTechTreeUnlockTip = true;
                meta.Save();
                tipsCtrl.ShowTip("科技树系统已解锁！永久强化你的战力！");
            }

            if (unlockMgr.IsEquipmentUnlocked && !meta.hasShownEquipmentUnlockTip)
            {
                meta.hasShownEquipmentUnlockTip = true;
                meta.Save();
                tipsCtrl.ShowTip("装备系统已解锁！强化你的光棱塔！");
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 界面状态切换
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnMainSceneStateChanged(MainSceneState state)
        {
            if (state == MainSceneState.KeJi)
            {
                TryStartTechTutorial();
                return;
            }

            if (state == MainSceneState.ZhuangBei)
            {
                TryStartEquipmentTutorial();
                return;
            }

            ClearStep();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 科技树引导（4步状态机）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void TryStartTechTutorial()
        {
            if (spotlightOverlay == null || techTreePanel == null || ProgressManager.Instance == null)
                return;

            var unlockMgr = SystemUnlockManager.Instance;
            var meta      = ProgressManager.Instance.Meta;
            if (unlockMgr == null || !unlockMgr.IsTechTreeUnlocked || meta.hasViewedTechTreeTutorial)
                return;

            if (_techStep == TechTutorialStep.None)
                _techStep = TechTutorialStep.SelectNode;

            StartCoroutine(RefreshTechTutorialStep());
        }

        private IEnumerator RefreshTechTutorialStep()
        {
            yield return null; // 等一帧让面板完全显示

            if (_techStep == TechTutorialStep.SelectNode)
            {
                var node = techTreePanel.GetNodeUIById(firstTechNodeId);
                if (node != null)
                    ShowStep(techSelectNodeConfig, node.RectTransform);
                yield break;
            }

            if (_techStep == TechTutorialStep.BackToMain && TopAreaController.Instance != null)
            {
                ShowStep(techBackToMainConfig, TopAreaController.Instance.BackButtonRect);
            }
        }

        private void OnTechDetailShown(Data.SO.TechTreeNodeData nodeData)
        {
            if ((_techStep != TechTutorialStep.SelectNode && _techStep != TechTutorialStep.UpgradeNode)
                || nodeData == null || nodeData.nodeId != firstTechNodeId)
                return;

            _techStep = TechTutorialStep.UpgradeNode;
            StartCoroutine(ShowTechUpgradeStep());
        }

        private IEnumerator ShowTechUpgradeStep()
        {
            // 等一帧，让 Canvas 布局系统完成 layout rebuild，
            // 确保 UpgradeButtonRect.GetWorldCorners() 返回正确的屏幕位置
            yield return null;
            if (techTreeDetailPanel != null)
                ShowStep(techUpgradeNodeConfig, techTreeDetailPanel.UpgradeButtonRect);
        }

        private void OnTechNodeUpgraded(Data.SO.TechTreeNodeData _, int __)
        {
            if (_techStep != TechTutorialStep.UpgradeNode) return;

            _techStep = TechTutorialStep.CloseDetail;
            if (techTreeDetailPanel != null)
                ShowStep(techCloseDetailConfig, techTreeDetailPanel.CloseButtonRect);
        }

        private void OnTechDetailClosed(Data.SO.TechTreeNodeData _)
        {
            if (_techStep != TechTutorialStep.CloseDetail) return;

            _techStep = TechTutorialStep.BackToMain;
            if (TopAreaController.Instance != null)
                ShowStep(techBackToMainConfig, TopAreaController.Instance.BackButtonRect);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 装备引导（3步状态机）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void TryStartEquipmentTutorial()
        {
            if (spotlightOverlay == null || equipmentPanel == null || ProgressManager.Instance == null)
                return;

            var unlockMgr = SystemUnlockManager.Instance;
            var meta      = ProgressManager.Instance.Meta;
            if (unlockMgr == null || !unlockMgr.IsEquipmentUnlocked || meta.hasViewedEquipmentTutorial)
                return;

            if (_equipmentStep == EquipmentTutorialStep.None)
                _equipmentStep = EquipmentTutorialStep.SelectItem;

            StartCoroutine(RefreshEquipmentTutorialStep());
        }

        private IEnumerator RefreshEquipmentTutorialStep()
        {
            yield return null;

            if (_equipmentStep == EquipmentTutorialStep.SelectItem)
            {
                var item = equipmentPanel.GetBestEquippableItemUI();
                if (item != null)
                    ShowStep(equipSelectItemConfig, item.RectTransform);
                yield break;
            }

            if (_equipmentStep == EquipmentTutorialStep.BackToMain && TopAreaController.Instance != null)
            {
                ShowStep(equipBackToMainConfig, TopAreaController.Instance.BackButtonRect);
            }
        }

        private void OnEquipmentInfoShown(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep != EquipmentTutorialStep.SelectItem
                && _equipmentStep != EquipmentTutorialStep.EquipItem)
                return;

            _equipmentStep = EquipmentTutorialStep.EquipItem;
            StartCoroutine(ShowEquipmentEquipStep());
        }

        private IEnumerator ShowEquipmentEquipStep()
        {
            yield return null; // 同上，等一帧确保装备面板布局完成
            if (itemInfoPanel != null)
                ShowStep(equipEquipItemConfig, itemInfoPanel.EquipButtonRect);
        }

        private void OnEquipmentEquipped(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep != EquipmentTutorialStep.EquipItem) return;

            _equipmentStep = EquipmentTutorialStep.BackToMain;
            StartCoroutine(RefreshEquipmentTutorialStep());
        }

        private void OnEquipmentInfoClosed(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep == EquipmentTutorialStep.SelectItem)
                StartCoroutine(RefreshEquipmentTutorialStep());
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 返回按钮：完成引导
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnBackClicked()
        {
            if (ProgressManager.Instance == null) return;

            if (_techStep == TechTutorialStep.BackToMain)
            {
                _techStep = TechTutorialStep.Completed;
                ProgressManager.Instance.Meta.hasViewedTechTreeTutorial = true;
                ProgressManager.Instance.Meta.Save();
                ClearStep();
            }

            if (_equipmentStep == EquipmentTutorialStep.BackToMain)
            {
                _equipmentStep = EquipmentTutorialStep.Completed;
                ProgressManager.Instance.Meta.hasViewedEquipmentTutorial = true;
                ProgressManager.Instance.Meta.Save();
                ClearStep();
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 通用引导显示 / 隐藏
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>
        /// 显示一个引导步骤：SDF 挖孔遮罩 + 手指特效 Prefab
        /// </summary>
        private void ShowStep(TutorialStepConfigSO config, RectTransform target)
        {
            if (target == null) return;

            // 1. SDF 挖孔遮罩
            if (config != null && config.useSpotlightOverlay && spotlightOverlay != null)
            {
                spotlightOverlay.Show(target, config.overlayMessage, config.holePadding,
                    config.cornerRadius, config.holeShape);
            }
            else if (spotlightOverlay != null)
            {
                spotlightOverlay.Hide();
            }

            // 2. 手指特效 Prefab
            DestroyFingerObject();
            if (config != null && !string.IsNullOrEmpty(config.prefabPath))
            {
                SpawnFingerPrefab(config, target);
            }
        }

        /// <summary>
        /// 隐藏当前引导步骤（挖孔遮罩 + 手指 Prefab）
        /// </summary>
        private void ClearStep()
        {
            spotlightOverlay?.Hide();
            DestroyFingerObject();
        }

        private void SpawnFingerPrefab(TutorialStepConfigSO config, RectTransform target)
        {
            GameObject prefab = Resources.Load<GameObject>(config.prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[MainSceneTutorialDirector] 找不到 Prefab: {config.prefabPath}");
                return;
            }

            _currentFingerObject = Instantiate(prefab);
            _currentFingerObject.name = $"Tutorial_{config.stepID}";

            // 引导特效不应拦截点击，统一关闭所有 Graphic 的 raycastTarget
            DisableRaycastTargets(_currentFingerObject);

            // 父节点：优先用 config.parentPath，否则挂到自身节点（与 SDF 遮罩同层，保证渲染层级正确）
            Transform parent = string.IsNullOrEmpty(config.parentPath)
                ? this.transform
                : (FindByPath(config.parentPath) ?? this.transform);

            _currentFingerObject.transform.SetParent(parent, false);

            var rect = _currentFingerObject.transform as RectTransform;
            if (rect != null)
            {
                rect.localScale = config.localScale;
                ApplyFingerPosition(rect, target, (Vector2)config.localPosition);
            }

            // 缓存目标和偏移，供 Update() 每帧跟踪
            _currentFingerTarget = target;
            _currentFingerOffset = (Vector2)config.localPosition;
        }

        /// <summary>
        /// 将手指 Prefab 的 anchoredPosition 对齐到 target 的视觉中心（用 GetWorldCorners 取包围盒，
        /// 避免锚点/pivot 偏移导致 target.position 不在视觉中心的情况），再叠加 offset。
        /// </summary>
        private void ApplyFingerPosition(RectTransform fingerRect, RectTransform target, Vector2 offset)
        {
            if (fingerRect == null || target == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCam  = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            // 用 GetWorldCorners 取四个角，算出包围盒世界中心（不受 pivot 影响）
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);

            Transform parent = fingerRect.parent;
            if (parent is RectTransform parentRect
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       parentRect, screenPos, uiCam, out Vector2 localPos))
            {
                fingerRect.anchoredPosition = localPos + offset;
            }
            else
            {
                fingerRect.localPosition = (Vector3)offset;
            }
        }

        private static void DisableRaycastTargets(GameObject root)
        {
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
        }

        private void DestroyFingerObject()
        {
            if (_currentFingerObject != null)
            {
                Destroy(_currentFingerObject);
                _currentFingerObject = null;
            }
        }

        private static Transform FindByPath(string path)
        {
            var obj = GameObject.Find(path);
            return obj != null ? obj.transform : null;
        }
    }
}
