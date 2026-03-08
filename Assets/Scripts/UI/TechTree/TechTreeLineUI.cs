// ============================================================
// TechTreeLineUI.cs
// 文件位置: Assets/Scripts/UI/TechTree/TechTreeLineUI.cs
// 用途：科技树节点连接线的状态控制脚本
//
// 使用方法：
//   将此脚本挂载到每条 Line Image 上（如 Line02、Line03...）
//   在 Inspector 中填入 sourceNodeId（该线段起始端的节点 nodeId）
//   准备两张 Sprite：灰色（未解锁）和彩色（已解锁）
//
// 逻辑：
//   sourceNodeId 对应的节点 level >= 1 → 显示彩色 Sprite
//   sourceNodeId 对应的节点 level == 0 → 显示灰色 Sprite
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using LightVsDecay.Logic.TechTree;

namespace LightVsDecay.UI.TechTree
{
    [RequireComponent(typeof(Image))]
    public class TechTreeLineUI : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("起始节点 ID（该线段从哪个节点流出）")]
        [SerializeField] private string sourceNodeId;

        [Header("Sprite 配置")]
        [SerializeField] private Sprite lockedSprite;    // 灰色线段（节点未解锁时）
        [SerializeField] private Sprite unlockedSprite;  // 彩色线段（节点已解锁时）

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private Image _image;

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 刷新
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>根据源节点解锁状态刷新线段外观</summary>
        public void Refresh()
        {
            if (_image == null) _image = GetComponent<Image>();
            if (string.IsNullOrEmpty(sourceNodeId)) return;

            bool unlocked = TechTreeManager.Instance != null
                            && TechTreeManager.Instance.IsUnlocked(sourceNodeId);

            if (unlocked)
            {
                if (unlockedSprite != null) _image.sprite = unlockedSprite;
            }
            else
            {
                if (lockedSprite != null) _image.sprite = lockedSprite;
            }
        }
    }
}