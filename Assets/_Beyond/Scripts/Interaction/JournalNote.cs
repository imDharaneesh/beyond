using System;
using UnityEngine;

namespace Beyond.Interaction
{
    public class JournalNote : MonoBehaviour, IInteractable
    {
        [SerializeField] string title;
        [SerializeField, TextArea(4, 12)] string body;

        public static event Action<JournalNote> Opened;

        public string Title => title;
        public string Body => body;
        public bool HasBeenRead { get; private set; }
        public string Prompt => $"Read \"{title}\"";
        public string Detail => HasBeenRead ? "Already read" : null;

        public void Configure(string noteTitle, string noteBody)
        {
            title = noteTitle;
            body = noteBody;
        }

        public bool CanInteract(Interactor interactor) => true;

        public void Interact(Interactor interactor)
        {
            HasBeenRead = true;
            Opened?.Invoke(this);
        }
    }
}
