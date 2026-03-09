// ============================================================
// SettlementPanel.cs
// 文件位置: Assets/Scripts/UI/Panels/SettlementPanel.cs
//
// 统计区（InfoPanel）字段对应：
//   wavesText       → 通关波次 "8/8" 或 "5/8"
//   killCountText   → 击杀数
//   maxHitCountText → 最高连击数
//   totalDamageText → 总伤害（千分位大数字）
//
// RewardGridRoot 使用 GridLayoutGroup（非 HorizontalLayoutGroup）
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Logic;
using LightVsDecay.Logic.BattleReward;
using LightVsDecay.Logic.Equipment;
using LightVsDecay.Data.SO;
using LightVsDecay.VFX.PostProcess;

namespace LightVsDecay.UI.Panels
{
    public class SettlementPanel : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 标题 & 皇冠
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("标题")]
        [SerializeField] private GameObject victoryTitle;
        [SerializeField] private GameObject defeatTitle;
        [SerializeField] private GameObject crownIcon;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 统计数字（对应 InfoPanel 下各行的值文本）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("统计数字文本")]
        [Tooltip("通关波次，显示如 '8/8' 或 '5/8'")]
        [SerializeField] private TextMeshProUGUI wavesText;

        [Tooltip("击杀数")]
        [SerializeField] private TextMeshProUGUI killCountText;

        [Tooltip("最高连击数")]
        [SerializeField] private TextMeshProUGUI maxHitCountText;

        [Tooltip("总伤害，显示如 '12,450,800'")]
        [SerializeField] private TextMeshProUGUI totalDamageText;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("底部按钮")]
        [SerializeField] private Button doubleReceivedButton;
        [SerializeField] private Button returnButton;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 奖励网格
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("奖励网格（GridLayoutGroup）")]
        [Tooltip("RewardGridRoot，挂 GridLayoutGroup + ContentSizeFitter")]
        [SerializeField] private Transform rewardGridRoot;

        [Tooltip("单个奖励卡片预制体（SettlementRewardItemUI）")]
        [SerializeField] private SettlementRewardItemUI rewardItemPrefab;

        [Tooltip("卡片逐个出现的间隔（秒）")]
        [SerializeField] private float rewardCardInterval = 0.1f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector — 奖励配置 & 动画
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("奖励配置")]
        [SerializeField] private BattleRewardConfig rewardConfig;

        [Header("动画参数")]
        [SerializeField] private float fadeInDuration     = 0.3f;
        [SerializeField] private float numberAnimDuration = 1.2f;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private bool                isVictory = false;
        private Core.SettlementData settlementData;
        private CanvasGroup         canvasGroup;

        private BattleRewardResult  _pendingReward;
        private bool                _rewardsApplied = false;

        private readonly List<SettlementRewardItemUI> _rewardCards = new List<SettlementRewardItemUI>();

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            Time.timeScale = 0f;
            if (ScreenEffectController.Instance != null)
                ScreenEffectController.Instance.StopAllEffects();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共入口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public void Show(bool victory)
        {
            isVictory       = victory;
            _rewardsApplied = false;

            settlementData = ProgressManager.Instance != null
                ? ProgressManager.Instance.GetSettlementData()
                : new Core.SettlementData();

            _pendingReward = CalculateRewards(victory);

            NotifySystemUnlocks(victory);

            SetupButtons();
            StartCoroutine(ShowContentCoroutine());
        }

        public void ResetPanel()
        {
            StopAllCoroutines();
            ClearRewardCards();
            if (canvasGroup != null) canvasGroup.alpha = 1f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励计算
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private BattleRewardResult CalculateRewards(bool victory)
        {
            if (rewardConfig == null) return new BattleRewardResult();

            var ctx = new BattleContext
            {
                bossKilled   = victory,
                isPerfect    = settlementData.isPerfect,
                totalKills   = settlementData.killCount,
                chapterIndex = GameManager.Instance != null ? GameManager.Instance.CurrentChapterIndex : 0,
                difficulty   = GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty   : 1,
                wavesCleared = settlementData.wavesCleared,
            };

            return BattleRewardCalculator.Calculate(rewardConfig, ctx);
        }

        private void NotifySystemUnlocks(bool victory)
        {
            if (SystemUnlockManager.Instance == null) return;
            int ch   = GameManager.Instance != null ? GameManager.Instance.CurrentChapterIndex : 0;
            int diff = GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty   : 1;
            SystemUnlockManager.Instance.CheckAndProcessUnlocks(ch, diff, victory);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 显示流程
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ShowContentCoroutine()
        {
            if (victoryTitle != null) victoryTitle.SetActive(isVictory);
            if (defeatTitle  != null) defeatTitle.SetActive(!isVictory);
            if (crownIcon    != null) crownIcon.SetActive(isVictory && settlementData.isPerfect);

            // 面板淡入
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                float e = 0f;
                while (e < fadeInDuration)
                {
                    e += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(e / fadeInDuration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            // 数字滚动
            yield return StartCoroutine(AnimateNumbersCoroutine());

            // 奖励卡片逐个弹出
            yield return new WaitForSecondsRealtime(0.15f);
            yield return StartCoroutine(ShowRewardsCoroutine());
        }

        private IEnumerator AnimateNumbersCoroutine()
        {
            float elapsed = 0f;

            int   targetKill     = settlementData.killCount;
            int   targetMaxHit   = settlementData.maxHitCount;
            long  targetDamage   = settlementData.totalDamage;
            // 波次是固定值，直接设置
            if (wavesText != null) wavesText.text = settlementData.WavesText;

            while (elapsed < numberAnimDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t     = elapsed / numberAnimDuration;
                float easeT = 1f - Mathf.Pow(1f - t, 3f);   // EaseOutCubic

                if (killCountText   != null)
                    killCountText.text   = Mathf.RoundToInt(Mathf.Lerp(0, targetKill,   easeT)).ToString();

                if (maxHitCountText != null)
                    maxHitCountText.text = Mathf.RoundToInt(Mathf.Lerp(0, targetMaxHit, easeT)).ToString();

                if (totalDamageText != null)
                {
                    long cur = (long)Mathf.Lerp(0, targetDamage, easeT);
                    totalDamageText.text = cur.ToString("N0");
                }

                yield return null;
            }

            // 最终精确值
            if (killCountText   != null) killCountText.text   = targetKill.ToString();
            if (maxHitCountText != null) maxHitCountText.text = targetMaxHit.ToString();
            if (totalDamageText != null) totalDamageText.text = targetDamage.ToString("N0");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励卡片
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private IEnumerator ShowRewardsCoroutine()
        {
            ClearRewardCards();

            // ── 金币始终作为第一张卡片 ──
            int coins = settlementData.coinsEarned;
            if (coins > 0)
            {
                var coinDrop = new BattleDropItem
                {
                    type  = DropItemType.Coin,
                    count = coins,
                };
                var coinCard = SpawnRewardCard(coinDrop);
                if (coinCard != null)
                {
                    coinCard.PlayAppearAnimation();
                    _rewardCards.Add(coinCard);
                }
                yield return new WaitForSecondsRealtime(rewardCardInterval);
            }

            // ── 图纸 & 装备掉落 ──
            if (_pendingReward != null && !_pendingReward.IsEmpty)
            {
                foreach (var drop in _pendingReward.drops)
                {
                    if (drop == null) continue;
                    var card = SpawnRewardCard(drop);
                    if (card != null)
                    {
                        card.PlayAppearAnimation();
                        _rewardCards.Add(card);
                    }
                    yield return new WaitForSecondsRealtime(rewardCardInterval);
                }
            }
        }

        private SettlementRewardItemUI SpawnRewardCard(BattleDropItem drop)
        {
            if (rewardItemPrefab == null || rewardGridRoot == null) return null;
            var card = Instantiate(rewardItemPrefab, rewardGridRoot);
            card.Bind(drop);
            return card;
        }

        private void ClearRewardCards()
        {
            foreach (var c in _rewardCards)
                if (c != null) Destroy(c.gameObject);
            _rewardCards.Clear();

            if (rewardGridRoot != null)
                foreach (Transform child in rewardGridRoot)
                    Destroy(child.gameObject);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 奖励写入（点击按钮时执行，只执行一次）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void ApplyRewardsToManagers(bool doubleCoins)
        {
            if (_rewardsApplied) return;
            _rewardsApplied = true;

            int coins = settlementData.coinsEarned * (doubleCoins ? 2 : 1);
            if (ProgressManager.Instance != null)
                ProgressManager.Instance.AddGoldCoins(coins);
            else
                PlayerPrefs.SetInt("PlayerGoldCoins", PlayerPrefs.GetInt("PlayerGoldCoins", 0) + coins);

            if (_pendingReward == null || _pendingReward.IsEmpty) return;

            var eqMgr = EquipmentManager.Instance;
            foreach (var drop in _pendingReward.drops)
            {
                if (drop == null) continue;
                switch (drop.type)
                {
                    case DropItemType.Blueprint:
                        eqMgr?.AddBlueprints(drop.count);
                        break;
                    case DropItemType.Equipment:
                        eqMgr?.AddToInventory(drop.equipmentId, drop.rarity, drop.count);
                        break;
                }
            }
            PlayerPrefs.Save();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 按钮
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetupButtons()
        {
            if (doubleReceivedButton != null)
            {
                doubleReceivedButton.onClick.RemoveAllListeners();
                doubleReceivedButton.onClick.AddListener(() =>
                {
                    ApplyRewardsToManagers(true);
                    ReturnToMainMenu();
                });
            }
            if (returnButton != null)
            {
                returnButton.onClick.RemoveAllListeners();
                returnButton.onClick.AddListener(() =>
                {
                    ApplyRewardsToManagers(false);
                    ReturnToMainMenu();
                });
            }
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
                GameManager.Instance.LoadMainMenu();
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene");
        }
    }
}