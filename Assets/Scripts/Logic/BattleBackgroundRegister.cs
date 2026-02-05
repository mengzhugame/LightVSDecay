// ============================================================
// BattleBackgroundRegister.cs
// 文件位置: Assets/Scripts/Logic/BattleBackgroundRegister.cs
// 用途：自动注册战斗背景到 GameManager（挂载在背景物体上）
// ============================================================

using UnityEngine;

namespace LightVsDecay.Logic
{
    /// <summary>
    /// 战斗背景注册器
    /// 挂载在 GameScene 的背景物体上
    /// 自动将 SpriteRenderer 注册到 GameManager
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BattleBackgroundRegister : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            // 设置 Tag 便于查找
            if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
            {
                // 如果 Tag 未设置，可以在这里处理
                // 但建议在编辑器中手动设置 Tag 为 "BattleBackground"
            }
        }
        
        private void Start()
        {
            // 注册到 GameManager
            if (GameManager.Instance != null && spriteRenderer != null)
            {
                GameManager.Instance.SetBattleBackground(spriteRenderer);
                Debug.Log("[BattleBackgroundRegister] 背景已注册到 GameManager");
            }
        }
    }
}