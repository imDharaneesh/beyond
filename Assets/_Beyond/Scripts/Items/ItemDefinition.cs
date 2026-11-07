using UnityEngine;

namespace Beyond.Items
{
    public enum ItemCategory { Resource, Component, Tool, Artifact, Clothing }

    [CreateAssetMenu(menuName = "Beyond/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, TextArea(2, 5)] string description;
        [SerializeField] ItemCategory category;
        [SerializeField] Color tint = Color.white;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public ItemCategory Category => category;
        public Color Tint => tint;

        public void Configure(string itemId, string name, string desc, ItemCategory cat, Color color)
        {
            id = itemId;
            displayName = name;
            description = desc;
            category = cat;
            tint = color;
        }
    }
}
