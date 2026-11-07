namespace Beyond.Interaction
{
    public interface IInteractable
    {
        string Prompt { get; }

        string Detail { get; }

        bool CanInteract(Interactor interactor);
        void Interact(Interactor interactor);
    }
}
