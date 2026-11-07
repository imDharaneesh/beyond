using System;
using System.Collections.Generic;
using Beyond.Core;
using Beyond.Items;
using UnityEngine;

namespace Beyond.Player
{
    public class Wardrobe : MonoBehaviour
    {
        [Serializable]
        public class Garment
        {
            public ItemDefinition item;
            public GameObject[] show;
            public GameObject[] hide;
        }

        [SerializeField] List<Garment> garments = new List<Garment>();

        Inventory inventory;
        readonly HashSet<ItemDefinition> worn = new HashSet<ItemDefinition>();

        public event Action<ItemDefinition> Equipped;

        public void Configure(List<Garment> list) => garments = list;

        public bool IsWearing(ItemDefinition item) => worn.Contains(item);

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            foreach (var g in garments)
                foreach (var go in g.show)
                    if (go != null) go.SetActive(false);
        }

        void OnEnable()
        {
            if (inventory != null) inventory.ItemAdded += OnItemAdded;
        }

        void OnDisable()
        {
            if (inventory != null) inventory.ItemAdded -= OnItemAdded;
        }

        void OnItemAdded(ItemDefinition item, int amount)
        {
            foreach (var g in garments)
                if (g.item == item) Equip(g);
        }

        void Equip(Garment g)
        {
            if (!worn.Add(g.item)) return;
            foreach (var go in g.show)
                if (go != null) go.SetActive(true);
            foreach (var go in g.hide)
                if (go != null) go.SetActive(false);
            Messages.Post($"Now wearing: {g.item.DisplayName}");
            Equipped?.Invoke(g.item);
        }
    }
}
