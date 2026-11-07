using System;
using Beyond.Items;
using Beyond.Player;
using UnityEngine;

namespace Beyond.Interaction
{
    public class Interactor : MonoBehaviour
    {
        [SerializeField] float reach = 2.6f;
        [SerializeField] float nearbyRadius = 1.4f;
        [SerializeField] float chestHeight = 1.2f;

        PlayerInputReader input;
        PlayerCameraRig rig;
        readonly Collider[] overlap = new Collider[16];

        public Inventory Inventory { get; private set; }
        public IInteractable Current { get; private set; }
        public event Action<IInteractable> Interacted;

        Vector3 Chest => transform.position + Vector3.up * chestHeight;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            rig = GetComponent<PlayerCameraRig>();
            Inventory = GetComponent<Inventory>();
        }

        void Update()
        {
            Current = input.GameplayBlocked ? null : FindTarget();

            if (Current != null && input.InteractPressed && Current.CanInteract(this))
            {
                Current.Interact(this);
                Interacted?.Invoke(Current);
            }
        }

        IInteractable FindTarget()
        {
            Transform cam = rig != null ? rig.CameraTransform : null;
            if (cam != null)
            {
                float camToChest = Vector3.Distance(cam.position, Chest);
                if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, camToChest + reach,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    var target = hit.collider.GetComponentInParent<IInteractable>();
                    if (target != null && Vector3.Distance(hit.point, Chest) <= reach) return target;
                }
            }

            int count = Physics.OverlapSphereNonAlloc(Chest, nearbyRadius, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestDot = 0.3f;
            Vector3 forward = cam != null ? cam.forward : transform.forward;
            for (int i = 0; i < count; i++)
            {
                var candidate = overlap[i].GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                Vector3 to = (overlap[i].bounds.center - Chest).normalized;
                float dot = Vector3.Dot(forward, to);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
