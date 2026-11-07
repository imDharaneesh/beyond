using System;
using UnityEngine;

namespace Beyond.World
{
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [SerializeField] string zoneId;

        public string ZoneId => zoneId;
        public bool Entered { get; private set; }
        public event Action<ZoneTrigger> PlayerEntered;

        public void Configure(string id) => zoneId = id;

        void OnTriggerEnter(Collider other)
        {
            if (Entered || !other.CompareTag("Player")) return;
            Entered = true;
            PlayerEntered?.Invoke(this);
        }
    }
}
