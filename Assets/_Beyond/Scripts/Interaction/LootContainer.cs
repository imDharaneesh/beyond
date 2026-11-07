using System.Collections.Generic;
using Beyond.Items;
using UnityEngine;

namespace Beyond.Interaction
{
    public class LootContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] string containerName = "Supply Crate";
        [SerializeField] List<ItemStack> contents = new List<ItemStack>();
        [SerializeField] Transform lid;
        [SerializeField] Vector3 lidOpenEuler = new Vector3(110f, 0f, 0f);

        bool opened;

        public bool IsOpened => opened;
        public string ContainerName => containerName;
        Quaternion lidClosed;
        float lidT;

        public string Prompt => opened ? $"{containerName} (empty)" : $"Open {containerName}";
        public string Detail => null;

        public void Configure(string displayName, Transform lidTransform, List<ItemStack> items)
        {
            containerName = displayName;
            lid = lidTransform;
            contents = items;
        }

        void Awake()
        {
            if (lid != null) lidClosed = lid.localRotation;
        }

        public bool CanInteract(Interactor interactor) => !opened && interactor.Inventory != null;

        public void Interact(Interactor interactor)
        {
            opened = true;
            foreach (var stack in contents)
                interactor.Inventory.Add(stack.item, stack.amount);
            contents.Clear();
        }

        void Update()
        {
            if (!opened || lid == null || lidT >= 1f) return;
            lidT = Mathf.Min(1f, lidT + Time.deltaTime * 2.5f);
            float eased = 1f - (1f - lidT) * (1f - lidT);
            lid.localRotation = lidClosed * Quaternion.Euler(lidOpenEuler * eased);
        }
    }
}
