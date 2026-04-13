using TMPro;
using UnityEngine;

namespace LightVsDecay.UI.Tutorial
{
    public class DroneChoiceTipsPresenter : MonoBehaviour
    {
        [System.Serializable]
        private class DroneTipItem
        {
            public GameObject root;
            public TextMeshProUGUI descriptionText;
            [TextArea(2, 4)] public string description;
        }

        [Header("Top Tips")]
        [SerializeField] private TextMeshProUGUI topTipsText;
        [TextArea(1, 3)]
        [SerializeField] private string topTipsMessage = "根据当前局势选择无人机：补给偏稳定，问号偏随机，契约偏高风险高收益。";

        [Header("Descriptions")]
        [SerializeField] private DroneTipItem[] tipItems = new DroneTipItem[3];

        private void OnEnable()
        {
            RefreshTips();
        }

        public void RefreshTips()
        {
            if (topTipsText != null)
            {
                topTipsText.text = topTipsMessage;
            }

            if (tipItems == null)
            {
                return;
            }

            foreach (var item in tipItems)
            {
                if (item == null || item.descriptionText == null)
                {
                    continue;
                }

                item.descriptionText.text = item.description;
                if (item.root != null)
                {
                    item.root.SetActive(true);
                }
            }
        }
    }
}
