using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Ads;
using LightVsDecay.Core;
using LightVsDecay.Logic;
using LightVsDecay.Logic.Enemy;
using LightVsDecay.Logic.Player;

namespace LightVsDecay.UI.Panels
{
    public class RevivePanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button quitButton;
        [SerializeField] private Button reviveButton;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private TextMeshProUGUI reviveButtonText;

        [Header("Copy")]
        [SerializeField] private string defaultTitle = "濒死状态";
        [SerializeField] private string reviveButtonLabel = "看视频复活";
        [SerializeField] private string exhaustedButtonLabel = "今日已达上限";
        [SerializeField] private string reviveDescription = "<color=#DF1D1D>光棱塔被击破，</color><color=#00BFFF>核心完整性至关重要，必须进行重建！</color>";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        private void Awake()
        {
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (reviveButton != null)
            {
                reviveButton.onClick.RemoveAllListeners();
                reviveButton.onClick.AddListener(OnReviveClicked);
            }
        }

        private void OnEnable()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            if (titleText != null)
            {
                titleText.text = defaultTitle;
            }

            if (infoText != null)
            {
                infoText.text = BuildInfoText();
            }

            bool canRevive = AdManager.Instance.CanOfferRevive(GetCurrentWave());
            if (reviveButton != null)
            {
                reviveButton.interactable = canRevive;
            }

            if (reviveButtonText != null)
            {
                reviveButtonText.text = canRevive ? reviveButtonLabel : exhaustedButtonLabel;
            }
        }

        private string BuildInfoText()
        {
            string content = $"{reviveDescription}\n\n当前波次: <color=#FFD166>{GetCurrentWave()}</color>";
            BossHealth bossHealth = FindObjectOfType<BossHealth>();
            if (bossHealth != null && bossHealth.MaxHealth > 0f)
            {
                float hpPercent = bossHealth.CurrentHealth / bossHealth.MaxHealth;
                content += $"\nBoss 剩余 HP: <color=#FF6B6B>{hpPercent:P0}</color>";
            }

            return content;
        }

        private int GetCurrentWave()
        {
            return WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0;
        }

        private void OnQuitClicked()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideRevivePanel();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.Defeat();
            }
            else if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowSettlementPanel(false);
            }
        }

        private void OnReviveClicked()
        {
            AnalyticsManager.LogScene(AnalyticsSceneIds.AdClickRevive);
            AdManager.Instance.ShowRewardedAd(AdType.Revive, RevivePlayer, RefreshView);
        }

        private void RevivePlayer()
        {
            TurretHealth turretHealth = FindObjectOfType<TurretHealth>();
            ShieldController shieldController = FindObjectOfType<ShieldController>();

            turretHealth?.RestoreHealthPercent(1f);
            shieldController?.RestoreShieldPercent(0.5f);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideRevivePanel();
            }

            if (showDebugInfo)
            {
                GameLogger.Log("[RevivePanel] 玩家已通过广告复活");
            }
        }
    }
}
