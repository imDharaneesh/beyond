using System;
using System.Collections.Generic;
using System.Text;
using Beyond.Items;
using UnityEngine;

namespace Beyond.Interaction
{
    public class RepairableDevice : MonoBehaviour, IInteractable
    {
        [SerializeField] string deviceName = "Device";
        [SerializeField] List<ItemStack> requirements = new List<ItemStack>();
        [SerializeField] GameObject[] enableWhenRepaired;
        [SerializeField] GameObject[] disableWhenRepaired;

        public bool IsRepaired { get; private set; }
        public IReadOnlyList<ItemStack> Requirements => requirements;
        public string DeviceName => deviceName;
        public event Action Repaired;

        Inventory lastInventory;

        public string Prompt => IsRepaired ? $"{deviceName} (working)" : $"Repair {deviceName}";
        public string Detail => IsRepaired ? null : MissingText(lastInventory);

        public void Configure(string displayName, List<ItemStack> needs, GameObject[] enable, GameObject[] disable)
        {
            deviceName = displayName;
            requirements = needs;
            enableWhenRepaired = enable;
            disableWhenRepaired = disable;
        }

        public bool CanInteract(Interactor interactor)
        {
            lastInventory = interactor.Inventory;
            if (IsRepaired || lastInventory == null) return false;
            foreach (var r in requirements)
                if (!lastInventory.Has(r.item, r.amount)) return false;
            return true;
        }

        public void Interact(Interactor interactor)
        {
            foreach (var r in requirements)
                interactor.Inventory.Remove(r.item, r.amount);
            IsRepaired = true;
            if (enableWhenRepaired != null)
                foreach (var go in enableWhenRepaired)
                    if (go != null) go.SetActive(true);
            if (disableWhenRepaired != null)
                foreach (var go in disableWhenRepaired)
                    if (go != null) go.SetActive(false);
            Repaired?.Invoke();
        }

        public string MissingText(Inventory inventory)
        {
            var sb = new StringBuilder("Needs: ");
            for (int i = 0; i < requirements.Count; i++)
            {
                var r = requirements[i];
                int have = inventory != null ? Mathf.Min(inventory.Count(r.item), r.amount) : 0;
                if (i > 0) sb.Append("   ");
                sb.Append($"{r.item.DisplayName} {have}/{r.amount}");
            }
            return sb.ToString();
        }
    }
}
