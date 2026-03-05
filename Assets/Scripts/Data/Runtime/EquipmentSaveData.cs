// ============================================================
// EquipmentSaveData.cs
// 文件位置: Assets/Scripts/Data/Runtime/EquipmentSaveData.cs
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using LightVsDecay.Data.SO;

namespace LightVsDecay.Data.Runtime
{
    /// <summary>背包堆叠格（同equipmentId+同rarity合为一格，无等级概念）</summary>
    [System.Serializable]
    public class InventoryStack
    {
        public string     equipmentId;
        public ItemRarity rarity;
        public int        count;

        public InventoryStack() { }
        public InventoryStack(string id, ItemRarity r, int c = 1)
        { equipmentId = id; rarity = r; count = c; }
    }

    /// <summary>装备槽存档（含升级等级，0=空槽）</summary>
    [System.Serializable]
    public class EquippedSlotData
    {
        public string     equipmentId  = "";
        public ItemRarity rarity       = ItemRarity.Common;
        public int        upgradeLevel = 0;

        public bool IsEmpty => string.IsNullOrEmpty(equipmentId) || upgradeLevel == 0;

        public static EquippedSlotData Empty()              => new EquippedSlotData();
        public static EquippedSlotData Create(string id, ItemRarity r)
            => new EquippedSlotData { equipmentId = id, rarity = r, upgradeLevel = 1 };
    }

    [System.Serializable]
    public class EquipmentSaveData
    {
        public const string KEY_INV  = "Equip_Inv_v2";
        public const string KEY_SLOT = "Equip_Slot_v2";
        public const string KEY_BP   = "Equip_BP";

        public List<InventoryStack> inventory  = new List<InventoryStack>();
        public EquippedSlotData[]   slots      = new EquippedSlotData[3]
            { EquippedSlotData.Empty(), EquippedSlotData.Empty(), EquippedSlotData.Empty() };
        public int blueprints = 0;

        public void Save()
        {
            PlayerPrefs.SetString(KEY_INV,  JsonUtility.ToJson(new InvWrap  { items = inventory }));
            PlayerPrefs.SetString(KEY_SLOT, JsonUtility.ToJson(new SlotWrap { slots = slots     }));
            PlayerPrefs.SetInt   (KEY_BP,   blueprints);
            PlayerPrefs.Save();
        }

        public static EquipmentSaveData Load()
        {
            var d = new EquipmentSaveData();
            try { var w = JsonUtility.FromJson<InvWrap>(PlayerPrefs.GetString(KEY_INV, ""));
                  if (w?.items != null) d.inventory = w.items; } catch { }
            try { var w = JsonUtility.FromJson<SlotWrap>(PlayerPrefs.GetString(KEY_SLOT, ""));
                  if (w?.slots != null && w.slots.Length == 3) d.slots = w.slots; } catch { }
            d.blueprints = PlayerPrefs.GetInt(KEY_BP, 0);
            return d;
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(KEY_INV);
            PlayerPrefs.DeleteKey(KEY_SLOT);
            PlayerPrefs.DeleteKey(KEY_BP);
            PlayerPrefs.Save();
        }

        [System.Serializable] private class InvWrap  { public List<InventoryStack> items; }
        [System.Serializable] private class SlotWrap { public EquippedSlotData[]   slots; }
    }
}