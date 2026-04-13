using System.Collections;
using UnityEngine;
using LightVsDecay.Logic;
using LightVsDecay.UI.TechTree;
using LightVsDecay.UI.Equipment;

namespace LightVsDecay.UI.Tutorial
{
    public class MainSceneTutorialDirector : MonoBehaviour
    {
        private enum TechTutorialStep
        {
            None,
            SelectNode,
            UpgradeNode,
            CloseDetail,
            BackToMain,
            Completed
        }

        private enum EquipmentTutorialStep
        {
            None,
            SelectItem,
            EquipItem,
            BackToMain,
            Completed
        }

        [Header("General")]
        [SerializeField] private TutorialSpotlightOverlay spotlightOverlay;

        [Header("Tech Tree")]
        [SerializeField] private TechTreePanel techTreePanel;
        [SerializeField] private TechTreeNodeDetailPanel techTreeDetailPanel;
        [SerializeField] private string firstTechNodeId = "node_fp_cap1";

        [Header("Equipment")]
        [SerializeField] private EquipmentPanel equipmentPanel;
        [SerializeField] private ItemInfoPanel itemInfoPanel;

        private TechTutorialStep _techStep = TechTutorialStep.None;
        private EquipmentTutorialStep _equipmentStep = EquipmentTutorialStep.None;

        private void OnEnable()
        {
            MainSceneUIManager.OnStateChanged += OnMainSceneStateChanged;
            TopAreaController.BackClicked += OnBackClicked;
            TechTreeNodeDetailPanel.DetailShown += OnTechDetailShown;
            TechTreeNodeDetailPanel.DetailClosed += OnTechDetailClosed;
            TechTreeNodeDetailPanel.NodeUpgraded += OnTechNodeUpgraded;
            ItemInfoPanel.InfoShown += OnEquipmentInfoShown;
            ItemInfoPanel.Equipped += OnEquipmentEquipped;
            ItemInfoPanel.InfoClosed += OnEquipmentInfoClosed;
        }

        private void OnDisable()
        {
            MainSceneUIManager.OnStateChanged -= OnMainSceneStateChanged;
            TopAreaController.BackClicked -= OnBackClicked;
            TechTreeNodeDetailPanel.DetailShown -= OnTechDetailShown;
            TechTreeNodeDetailPanel.DetailClosed -= OnTechDetailClosed;
            TechTreeNodeDetailPanel.NodeUpgraded -= OnTechNodeUpgraded;
            ItemInfoPanel.InfoShown -= OnEquipmentInfoShown;
            ItemInfoPanel.Equipped -= OnEquipmentEquipped;
            ItemInfoPanel.InfoClosed -= OnEquipmentInfoClosed;
        }

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

            spotlightOverlay?.Hide();
        }

        private void TryStartTechTutorial()
        {
            if (spotlightOverlay == null || techTreePanel == null || ProgressManager.Instance == null)
            {
                return;
            }

            var unlockManager = SystemUnlockManager.Instance;
            var meta = ProgressManager.Instance.Meta;
            if (unlockManager == null || !unlockManager.IsTechTreeUnlocked || meta.hasViewedTechTreeTutorial)
            {
                return;
            }

            if (_techStep == TechTutorialStep.None)
            {
                _techStep = TechTutorialStep.SelectNode;
            }

            StartCoroutine(RefreshTechTutorialOverlay());
        }

        private IEnumerator RefreshTechTutorialOverlay()
        {
            yield return null;

            if (_techStep == TechTutorialStep.SelectNode)
            {
                var node = techTreePanel.GetNodeUIById(firstTechNodeId);
                if (node != null)
                {
                    spotlightOverlay.Show(node.RectTransform, "点击第一个火力节点，开始第一次科技强化。");
                }

                yield break;
            }

            if (_techStep == TechTutorialStep.BackToMain && TopAreaController.Instance != null)
            {
                spotlightOverlay.Show(TopAreaController.Instance.BackButtonRect, "点击返回按钮，回到主页面。");
            }
        }

        private void OnTechDetailShown(Data.SO.TechTreeNodeData nodeData)
        {
            if ((_techStep != TechTutorialStep.SelectNode && _techStep != TechTutorialStep.UpgradeNode)
                || nodeData == null
                || nodeData.nodeId != firstTechNodeId)
            {
                return;
            }

            _techStep = TechTutorialStep.UpgradeNode;
            spotlightOverlay?.Show(techTreeDetailPanel != null ? techTreeDetailPanel.UpgradeButtonRect : null, "点击升级按钮，强化这个节点。");
        }

        private void OnTechNodeUpgraded(Data.SO.TechTreeNodeData _, int __)
        {
            if (_techStep != TechTutorialStep.UpgradeNode)
            {
                return;
            }

            _techStep = TechTutorialStep.CloseDetail;
            spotlightOverlay?.Show(techTreeDetailPanel != null ? techTreeDetailPanel.CloseButtonRect : null, "升级完成后，关闭当前窗口。");
        }

        private void OnTechDetailClosed(Data.SO.TechTreeNodeData _)
        {
            if (_techStep != TechTutorialStep.CloseDetail)
            {
                return;
            }

            _techStep = TechTutorialStep.BackToMain;
            if (TopAreaController.Instance != null)
            {
                spotlightOverlay?.Show(TopAreaController.Instance.BackButtonRect, "点击返回按钮，回到主页面。");
            }
        }

        private void TryStartEquipmentTutorial()
        {
            if (spotlightOverlay == null || equipmentPanel == null || ProgressManager.Instance == null)
            {
                return;
            }

            var unlockManager = SystemUnlockManager.Instance;
            var meta = ProgressManager.Instance.Meta;
            if (unlockManager == null || !unlockManager.IsEquipmentUnlocked || meta.hasViewedEquipmentTutorial)
            {
                return;
            }

            if (_equipmentStep == EquipmentTutorialStep.None)
            {
                _equipmentStep = EquipmentTutorialStep.SelectItem;
            }

            StartCoroutine(RefreshEquipmentTutorialOverlay());
        }

        private IEnumerator RefreshEquipmentTutorialOverlay()
        {
            yield return null;

            if (_equipmentStep == EquipmentTutorialStep.SelectItem)
            {
                var item = equipmentPanel.GetBestEquippableItemUI();
                if (item != null)
                {
                    spotlightOverlay.Show(item.RectTransform, "点击背包里品质最高、当前最值得装备的物品。");
                }

                yield break;
            }

            if (_equipmentStep == EquipmentTutorialStep.BackToMain && TopAreaController.Instance != null)
            {
                spotlightOverlay.Show(TopAreaController.Instance.BackButtonRect, "装备完成后，点击返回按钮回到主页面。");
            }
        }

        private void OnEquipmentInfoShown(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep != EquipmentTutorialStep.SelectItem && _equipmentStep != EquipmentTutorialStep.EquipItem)
            {
                return;
            }

            _equipmentStep = EquipmentTutorialStep.EquipItem;
            spotlightOverlay?.Show(itemInfoPanel != null ? itemInfoPanel.EquipButtonRect : null, "点击装备按钮，装上这件装备。");
        }

        private void OnEquipmentEquipped(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep != EquipmentTutorialStep.EquipItem)
            {
                return;
            }

            _equipmentStep = EquipmentTutorialStep.BackToMain;
            StartCoroutine(RefreshEquipmentTutorialOverlay());
        }

        private void OnEquipmentInfoClosed(Data.Runtime.InventoryStack _)
        {
            if (_equipmentStep == EquipmentTutorialStep.SelectItem)
            {
                StartCoroutine(RefreshEquipmentTutorialOverlay());
            }
        }

        private void OnBackClicked()
        {
            if (ProgressManager.Instance == null)
            {
                return;
            }

            if (_techStep == TechTutorialStep.BackToMain)
            {
                _techStep = TechTutorialStep.Completed;
                ProgressManager.Instance.Meta.hasViewedTechTreeTutorial = true;
                ProgressManager.Instance.Meta.Save();
                spotlightOverlay?.Hide();
            }

            if (_equipmentStep == EquipmentTutorialStep.BackToMain)
            {
                _equipmentStep = EquipmentTutorialStep.Completed;
                ProgressManager.Instance.Meta.hasViewedEquipmentTutorial = true;
                ProgressManager.Instance.Meta.Save();
                spotlightOverlay?.Hide();
            }
        }
    }
}
