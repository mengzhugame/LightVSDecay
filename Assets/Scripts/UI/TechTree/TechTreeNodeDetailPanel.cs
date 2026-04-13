// ============================================================
// TechTreeNodeDetailPanel.cs
// 文件位置: Assets/Scripts/UI/TechTree/TechTreeNodeDetailPanel.cs
// 版本：v3（完全匹配实际 UI 设计）
//
// UI 层级：
//   TechTreeNodeDetailPanel (TechTreeNodeDetailPanel脚本)
//   ├── Background (Image半透明黑底, Button → 点击关闭)
//   └── PanelBody
//       ├── CloseButton (X按钮)
//       ├── HeaderArea
//       │   ├── BranchFrameImage (Image - 四色底框)
//       │   ├── IconImage        (Image - 节点图标)
//       │   └── NameText         (TMP - 节点名称)
//       ├── StatsArea
//       │   ├── LeftColumn
//       │   │   ├── LevelLeftText  (TMP - "Lv.20")
//       │   │   └── StatLeftText   (TMP - 当前属性，白色)
//       │   └── RightColumn
//       │       ├── LevelRightText (TMP - "Lv.21"，绿色)
//       │       └── StatRightText  (TMP - 下一级属性，绿色)
//       ├── DescText  (TMP - "当达到Lv.50解锁下一个技能"，末端节点或满级时隐藏)
//       └── UpgradeButton (Button)
//           └── UpgradeButtonText (TMP - "升级  🪙  +1000")
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LightVsDecay.Data.SO;
using LightVsDecay.Logic;
using LightVsDecay.Logic.TechTree;
using LightVsDecay.Audio;
using LightVsDecay.Core;
using System;

namespace LightVsDecay.UI.TechTree
{
    public class TechTreeNodeDetailPanel : MonoBehaviour
    {
        public static event Action<TechTreeNodeData> DetailShown;
        public static event Action<TechTreeNodeData> DetailClosed;
        public static event Action<TechTreeNodeData, int> NodeUpgraded;
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("关闭按钮")]
        [SerializeField] private Button backgroundButton;
        [SerializeField] private Button closeButton;

        [Header("Header 区域")]
        [SerializeField] private Image           branchFrameImage;
        [SerializeField] private Image           iconImage;
        [SerializeField] private TextMeshProUGUI nameText;

        [Header("分支底框 Sprite（顺序对应 TechBranch 枚举: 0红/1绿/2黄/3蓝）")]
        [SerializeField] private Sprite[] branchFrameSprites = new Sprite[4];

        [Header("左列（当前等级）")]
        [SerializeField] private TextMeshProUGUI levelLeftText;
        [SerializeField] private TextMeshProUGUI statLeftText;

        [Header("右列（下一级，绿色）")]
        [SerializeField] private TextMeshProUGUI levelRightText;
        [SerializeField] private TextMeshProUGUI statRightText;

        [Header("底部解锁提示")]
        [SerializeField] private TextMeshProUGUI descText;

        [Header("升级按钮")]
        [SerializeField] private Button          upgradeButton;
        [SerializeField] private TextMeshProUGUI upgradeButtonText;
        
        [Header("升级按钮内子元素（用于独立置灰）")]
        [Tooltip("按钮内'升级'静态标签文字")]
        [SerializeField] private TextMeshProUGUI upgradeLabelText;
        [Tooltip("按钮内金币图标 Image")]
        [SerializeField] private Image goldIconImage;
        
        [Header("按钮颜色")]
        [SerializeField] private Color buttonNormalColor     = new Color(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Color buttonDisabledColor   = new Color(0.35f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color costNormalColor       = Color.white;
        [SerializeField] private Color costInsufficientColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("长按连升")]
        [SerializeField] private float longPressDelay      = 0.5f;
        [SerializeField] private float autoUpgradeInterval = 0.12f;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private TechTreeNodeData _data;
        private TechTreePanel    _parent;
        private Coroutine        _longPressCoroutine;
        private Image            _buttonImage;
        public RectTransform UpgradeButtonRect => upgradeButton != null ? upgradeButton.GetComponent<RectTransform>() : null;
        public RectTransform CloseButtonRect => closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        public TechTreeNodeData CurrentNodeData => _data;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            if (closeButton      != null) closeButton.onClick.AddListener(Close);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Close);

            if (upgradeButton != null)
            {
                _buttonImage = upgradeButton.GetComponent<Image>();

                var et = upgradeButton.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                         ?? upgradeButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

                AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerDown, _ => OnUpgradeDown());
                AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerUp,   _ => OnUpgradeUp());
                AddTrigger(et, UnityEngine.EventSystems.EventTriggerType.PointerExit, _ => OnUpgradeUp());
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公开接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Show(TechTreeNodeData data, TechTreePanel parent)
        {
            _data   = data;
            _parent = parent;
            gameObject.SetActive(true);
            Refresh();
            DetailShown?.Invoke(_data);
        }

        public void Close()
        {
            OnUpgradeUp();
            DetailClosed?.Invoke(_data);
            gameObject.SetActive(false);
            _parent?.OnDetailPanelClosed();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新全部显示
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Refresh()
        {
            if (_data == null) return;

            var  mgr   = TechTreeManager.Instance;
            int  curLv = mgr != null ? mgr.GetNodeLevel(_data.nodeId) : 0;
            bool isMax = curLv >= _data.maxLevel;
            int  cost  = _data.GetUpgradeCost(curLv);

            RefreshHeader();
            RefreshStats(curLv, isMax);
            RefreshDescText(curLv, isMax);
            RefreshUpgradeButton(curLv, isMax, cost);
        }

        // ── Header ──────────────────────────────────────────

        private void RefreshHeader()
        {
            // 分支底框
            if (branchFrameImage != null)
            {
                int idx = (int)_data.branch;
                if (idx >= 0 && idx < branchFrameSprites.Length && branchFrameSprites[idx] != null)
                    branchFrameImage.sprite = branchFrameSprites[idx];
            }

            // 图标
            if (iconImage != null && _data.icon != null)
                iconImage.sprite = _data.icon;

            // 名称
            if (nameText != null)
                nameText.text = _data.displayName;
        }

        // ── 属性对比 ─────────────────────────────────────────

        private void RefreshStats(int curLv, bool isMax)
        {
            // Lv.0 显示"未激活"状态；Lv.1+ 左列=当前，右列=下一级
            if (levelLeftText != null)
                levelLeftText.text = curLv == 0
                    ? "<color=#888888>未激活</color>"
                    : $"Lv.{curLv}";

            if (statLeftText != null)
                statLeftText.text = curLv == 0
                    ? "<color=#888888>—</color>"
                    : BuildStatText(curLv);

            int nextLv = curLv + 1;
            bool rightIsMax = isMax || nextLv > _data.maxLevel;

            if (levelRightText != null)
                levelRightText.text = rightIsMax
                    ? "<color=#FFD700>MAX</color>"
                    : $"<color=#66FF66>Lv.{nextLv}</color>";

            if (statRightText != null)
                statRightText.text = rightIsMax
                    ? "<color=#888888>已满级</color>"
                    : $"<color=#66FF66>{BuildStatText(nextLv)}</color>";
        }
        // ── 解锁提示 ─────────────────────────────────────────

        private void RefreshDescText(int curLv, bool isMax)
        {
            if (descText == null) return;

            // 查找是否存在以本节点为前置的后续节点
            var db = TechTreeManager.Instance?.Database;
            int unlockLv = GetNextNodeUnlockLevel(db);

            if (isMax || unlockLv <= 0)
            {
                // 满级 或 末端节点（无后续）
                descText.gameObject.SetActive(false);
                return;
            }

            descText.gameObject.SetActive(true);

            if (curLv >= unlockLv)
                descText.text = "<color=#66FF66>下一个技能已解锁！</color>";
            else
                descText.text = $"当达到 <color=#FFD700>Lv.{unlockLv}</color> 解锁下一个技能";
        }

        /// <summary>
        /// 获取解锁后续节点所需的等级。
        /// 当前规则：升到 Lv.1 即满足前置（返回 1）。
        /// 如需改为"升到满级才解锁"，将返回值改为 _data.maxLevel。
        /// </summary>
        private int GetNextNodeUnlockLevel(TechTreeDatabase db)
        {
            if (db == null) return 0;
            foreach (var node in db.AllNodes)
            {
                if (node == null) continue;
                if (node.prerequisiteNodeIds != null
                    && node.prerequisiteNodeIds.Contains(_data.nodeId))
                {
                    // ★ 返回后续节点配置的前置最低等级（例如 Lv.50）
                    return Mathf.Max(1, node.prerequisiteMinLevel);
                }
            }
            return 0;
        }

        // ── 升级按钮 ─────────────────────────────────────────

        private void RefreshUpgradeButton(int curLv, bool isMax, int cost)
        {
            if (upgradeButton == null) return;

            if (isMax)
            {
                upgradeButton.gameObject.SetActive(false);
                return;
            }

            upgradeButton.gameObject.SetActive(true);

            bool prereqOk   = TechTreeManager.Instance != null
                              && TechTreeManager.Instance.CanUpgrade(_data.nodeId);
            bool canAfford  = ProgressManager.Instance != null
                              && ProgressManager.Instance.GoldCoins >= cost;
            bool canUpgrade = prereqOk && canAfford;

            // 按钮交互 + 背景色
            upgradeButton.interactable = canUpgrade;
            if (_buttonImage != null)
                _buttonImage.color = canUpgrade ? buttonNormalColor : buttonDisabledColor;

            // "升级" 标签文字：可升级=白，否则=灰
            if (upgradeLabelText != null)
                upgradeLabelText.color = canUpgrade ? costNormalColor : Color.gray;

            // 金币数字：前置不满足=灰，金币不足=红，正常=白
            if (upgradeButtonText != null)
            {
                if (!prereqOk)
                {
                    upgradeButtonText.text  = $"{cost:N0}";
                    upgradeButtonText.color = Color.gray;
                }
                else
                {
                    upgradeButtonText.text  = $"{cost:N0}";
                    upgradeButtonText.color = canAfford ? costNormalColor : costInsufficientColor;
                }
            }

            // 金币图标：可升级=白，否则=灰
            if (goldIconImage != null)
                goldIconImage.color = canUpgrade ? Color.white : Color.gray;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 属性文本构建
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private string BuildStatText(int level)
        {
            if (level <= 0) return "—";
            float val = _data.GetTotalEffect(level);

            return _data.effectType switch
            {
                TechEffectType.AttackBonus            => $"攻击力\n+{val:F0}",
                TechEffectType.AttackBonusPercent     => $"攻击力\n+{val:F0}%",
                TechEffectType.CritRate               => $"暴击率\n+{val:F1}%",
                TechEffectType.CritDamage             => $"暴击伤害\n+{val:F0}%",
                TechEffectType.OverloadLaserWidth     => "大招激光\n+50%",
                TechEffectType.EliteDamageBonus       => $"精英伤害\n+{val:F0}%",
                TechEffectType.PenetrationDecayReduce => $"穿透衰减\n-{val:F0}%",
                TechEffectType.MaxHP                  => $"生命上限\n+{val:F0}",
                TechEffectType.MaxHPPercent           => $"生命上限\n+{val:F0}%",
                TechEffectType.ShieldRecoveryRate     => $"护盾恢复\n+{val:F0}%",
                TechEffectType.CollisionDamageReduce  => $"碰撞减伤\n{val:F0}%",
                TechEffectType.ReviveCount            => $"复活次数\n+{val:F0}",
                TechEffectType.HealOnOverload         => "大招回血\n20%",
                TechEffectType.ShieldBurstOnBreak     => "护盾脉冲\n已解锁",
                TechEffectType.CoinDropRate           => $"金币掉落\n+{val:F0}%",
                TechEffectType.OfflineIncome          => $"离线收益\n+{val:F0}%",
                TechEffectType.ShopFreeRefresh        => $"免费刷新\n+{val:F0}次",
                TechEffectType.FirstWaveCoinDouble    => "首波金币\n翻倍",
                TechEffectType.BountyHunter           => "赏金宝箱\n已解锁",
                TechEffectType.ChargeRate             => $"充能速度\n+{val:F0}%",
                TechEffectType.KnockbackDistance      => $"击退距离\n+{val:F0}%",
                TechEffectType.KillCooldownReduction  => $"击杀减冷\n+{val:F2}s",
                TechEffectType.BuffDurationBonus      => $"增益时长\n+{val:F0}%",
                TechEffectType.SkillRerollCount       => $"免费刷新\n+{val:F0}次",
                TechEffectType.OverloadStun           => "大招震晕\n1.5s",
                _                                     => $"+{val}",
            };
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 升级逻辑（长按连升）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnUpgradeDown()
        {
            // ★ EventTrigger 不检查 interactable，必须手动拦截
            if (upgradeButton != null && !upgradeButton.interactable) return;
            TryUpgrade();
            if (_longPressCoroutine != null) StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = StartCoroutine(LongPressRoutine());
        }

        private void OnUpgradeUp()
        {
            if (_longPressCoroutine == null) return;
            StopCoroutine(_longPressCoroutine);
            _longPressCoroutine = null;
        }

        private IEnumerator LongPressRoutine()
        {
            yield return new WaitForSecondsRealtime(longPressDelay);
            while (true)
            {
                if (!TryUpgrade()) yield break;
                yield return new WaitForSecondsRealtime(autoUpgradeInterval);
            }
        }

        private bool TryUpgrade()
        {
            if (_data == null || TechTreeManager.Instance == null) return false;

            var result = TechTreeManager.Instance.TryUpgradeNode(_data.nodeId);

            if (result == TechUpgradeResult.Success)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayButtonClick();

                int newLevel = TechTreeManager.Instance.GetNodeLevel(_data.nodeId);
                Refresh();
                _parent?.RefreshAll();
                TopAreaController.RefreshIfExists();   // ★ 刷新顶部金币显示
                NodeUpgraded?.Invoke(_data, newLevel);
                return true;
            }

            if (showDebugInfo)
                GameLogger.Log($"[TechDetailPanel] 升级失败: {result}");

            return false;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 工具
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static void AddTrigger(
            UnityEngine.EventSystems.EventTrigger et,
            UnityEngine.EventSystems.EventTriggerType type,
            UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> action)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            et.triggers.Add(entry);
        }
    }
}
