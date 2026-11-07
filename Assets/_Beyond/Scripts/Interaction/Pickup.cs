using Beyond.Items;
using UnityEngine;

namespace Beyond.Interaction
{
    public class Pickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemDefinition item;
        [SerializeField] int amount = 1;

        public ItemDefinition Item => item;
        public string Prompt => amount > 1 ? $"Pick up {item.DisplayName} x{amount}" : $"Pick up {item.DisplayName}";
        public string Detail => item.Description;

        public void Configure(ItemDefinition definition, int count)
        {
            item = definition;
            amount = count;
        }

        public bool CanInteract(Interactor interactor) => item != null && interactor.Inventory != null;

        public void Interact(Interactor interactor)
        {
            interactor.Inventory.Add(item, amount);
            Destroy(gameObject);
        }
    }
}
