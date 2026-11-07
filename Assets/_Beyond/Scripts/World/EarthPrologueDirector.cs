using Beyond.Core;
using Beyond.Interaction;
using Beyond.Items;
using UnityEngine;

namespace Beyond.World
{
    public class EarthPrologueDirector : MonoBehaviour
    {
        enum Step { FindCamp, RepairRadio, ReadTransmission, Done }

        [SerializeField] ZoneTrigger campZone;
        [SerializeField] RepairableDevice radio;
        [SerializeField] JournalNote transmissionLog;

        Inventory inventory;
        Step step;

        public void Configure(ZoneTrigger zone, RepairableDevice campRadio, JournalNote log)
        {
            campZone = zone;
            radio = campRadio;
            transmissionLog = log;
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) inventory = player.GetComponent<Inventory>();
            if (inventory != null) inventory.Changed += RefreshProgress;

            campZone.PlayerEntered += _ => OnCampFound();
            radio.Repaired += OnRadioRepaired;
            JournalNote.Opened += OnNoteOpened;

            if (transmissionLog != null) transmissionLog.gameObject.SetActive(false);

            step = Step.FindCamp;
            Objectives.Set("Find the abandoned expedition camp", "Follow the dirt trail north-east", campZone.transform.position);
            Messages.Post("You wake at the trailhead in your underwear. Your gear must be somewhere nearby.");
        }

        void OnDestroy()
        {
            JournalNote.Opened -= OnNoteOpened;
            if (inventory != null) inventory.Changed -= RefreshProgress;
        }

        void OnCampFound()
        {
            if (step != Step.FindCamp) return;
            step = Step.RepairRadio;
            Messages.Post("Discovered: Meridian Expedition Camp");
            Objectives.Set("Repair the camp radio", "", radio.transform.position);
            RefreshProgress();
        }

        void RefreshProgress()
        {
            if (step == Step.RepairRadio)
                Objectives.SetProgress(radio.MissingText(inventory).Replace("Needs: ", "").Replace("   ", "\n"));
        }

        void OnRadioRepaired()
        {
            step = Step.ReadTransmission;
            Messages.Post("Radio repaired - a transmission log was recovered");
            if (transmissionLog != null) transmissionLog.gameObject.SetActive(true);
            Objectives.Set("Read the recovered transmission", "It's printing at the radio table", transmissionLog != null ? transmissionLog.transform.position : radio.transform.position);
        }

        void OnNoteOpened(JournalNote note)
        {
            if (step != Step.ReadTransmission || note != transmissionLog) return;
            step = Step.Done;
            Objectives.Set("Prologue complete", "The coordinates point to a sealed facility. (Next chunk coming soon)");
        }
    }
}
