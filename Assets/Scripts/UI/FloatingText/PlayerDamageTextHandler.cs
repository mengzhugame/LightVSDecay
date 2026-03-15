// ============================================================
// PlayerDamageTextHandler.cs
// 文件位置: Assets/Scripts/UI/FloatingText/PlayerDamageTextHandler.cs
// 用途：玩家受击/恢复飘字事件处理器
// ============================================================

using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.UI.FloatingText
{
    /// <summary>
    /// 玩家受击飘字事件处理器
    /// 监听 GameEvents 并调用 FloatingTextManager
    /// 挂载到 GameScene 的 UI 管理器上
    /// </summary>
    public class PlayerDamageTextHandler : MonoBehaviour
    {
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnEnable()
        {
            GameEvents.OnPlayerHealthDamaged += OnHealthDamaged;
            GameEvents.OnPlayerShieldDamaged += OnShieldDamaged;
            GameEvents.OnPlayerHealthRestored += OnHealthRestored;
            GameEvents.OnPlayerShieldRestored += OnShieldRestored;
            
            if (showDebugInfo)
            {
                GameLogger.Log("[PlayerDamageTextHandler] 事件订阅完成");
            }
        }
        
        private void OnDisable()
        {
            GameEvents.OnPlayerHealthDamaged -= OnHealthDamaged;
            GameEvents.OnPlayerShieldDamaged -= OnShieldDamaged;
            GameEvents.OnPlayerHealthRestored -= OnHealthRestored;
            GameEvents.OnPlayerShieldRestored -= OnShieldRestored;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件回调
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void OnHealthDamaged(int damage, Vector3 position)
        {
            if (damage <= 0) return;
            
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowPlayerHealthDamage(position, damage);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[PlayerDamageTextHandler] 血量受伤飘字: -{damage}");
            }
        }
        
        private void OnShieldDamaged(int damage, Vector3 position)
        {
            if (damage <= 0) return;
            
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowPlayerShieldDamage(position, damage);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[PlayerDamageTextHandler] 护盾受伤飘字: -{damage}");
            }
        }
        
        private void OnHealthRestored(int amount, Vector3 position)
        {
            if (amount <= 0) return;
            
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowPlayerHealthRestore(position, amount);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[PlayerDamageTextHandler] 血量恢复飘字: +{amount}");
            }
        }
        
        private void OnShieldRestored(int amount, Vector3 position)
        {
            if (amount <= 0) return;
            
            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowPlayerShieldRestore(position, amount);
            }
            
            if (showDebugInfo)
            {
                GameLogger.Log($"[PlayerDamageTextHandler] 护盾恢复飘字: +{amount}");
            }
        }
    }
}