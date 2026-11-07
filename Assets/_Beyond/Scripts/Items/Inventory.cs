using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beyond.Items
{
    [Serializable]
    public class ItemStack
    {
        public ItemDefinition item;
        public int amount;

        public ItemStack(ItemDefinition item, int amount)
        {
            this.item = item;
            this.amount = amount;
        }
    }

    public class Inventory : MonoBehaviour
    {
        [SerializeField] List<ItemStack> stacks = new List<ItemStack>();

        public IReadOnlyList<ItemStack> Stacks => stacks;
        public event Action Changed;
        public event Action<ItemDefinition, int> ItemAdded;

        public void Add(ItemDefinition item, int amount)
        {
            if (item == null || amount <= 0) return;
            var stack = Find(item);
            if (stack != null) stack.amount += amount;
            else stacks.Add(new ItemStack(item, amount));
            ItemAdded?.Invoke(item, amount);
            Changed?.Invoke();
        }

        public bool Remove(ItemDefinition item, int amount)
        {
            var stack = Find(item);
            if (stack == null || stack.amount < amount) return false;
            stack.amount -= amount;
            if (stack.amount == 0) stacks.Remove(stack);
            Changed?.Invoke();
            return true;
        }

        public int Count(ItemDefinition item) => Find(item)?.amount ?? 0;

        public bool Has(ItemDefinition item, int amount) => Count(item) >= amount;

        ItemStack Find(ItemDefinition item)
        {
            foreach (var s in stacks)
                if (s.item == item) return s;
            return null;
        }
    }
}
