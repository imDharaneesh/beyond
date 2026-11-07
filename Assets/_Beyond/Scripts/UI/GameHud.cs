using System.Collections.Generic;
using System.Text;
using Beyond.Core;
using Beyond.Interaction;
using Beyond.Items;
using Beyond.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Beyond.UI
{
    [DefaultExecutionOrder(100)]
    public class GameHud : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0.04f, 0.06f, 0.08f, 0.82f);
        static readonly Color Accent = new Color(1f, 0.72f, 0.3f, 1f);
        static readonly Color TextColor = new Color(0.92f, 0.94f, 0.96f, 1f);
        static readonly Color Muted = new Color(0.65f, 0.7f, 0.75f, 1f);

        const float ToastDuration = 3.5f;

        PlayerInputReader input;
        PlayerMotor motor;
        PlayerCameraRig rig;
        Interactor interactor;
        Inventory inventory;

        Font font;
        Image crosshair;
        Text promptText, promptDetail, objectiveTitle, objectiveProgress, toastText, modeText, helpText;
        RectTransform staminaFill;
        GameObject staminaRoot, inventoryPanel, notePanel, pausePanel, helpPanel;
        Text inventoryText, noteTitle, noteBody;

        readonly List<(string text, float time)> toasts = new List<(string, float)>();
        bool inventoryOpen, noteOpen, paused, helpVisible = true;
        int noteOpenedFrame;
        float helpAutoHide = 12f;

        void Awake()
        {
            var player = GameObject.FindWithTag("Player");
            input = player.GetComponent<PlayerInputReader>();
            motor = player.GetComponent<PlayerMotor>();
            rig = player.GetComponent<PlayerCameraRig>();
            interactor = player.GetComponent<Interactor>();
            inventory = player.GetComponent<Inventory>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void OnEnable()
        {
            Objectives.Changed += RefreshObjective;
            Messages.Posted += Toast;
            JournalNote.Opened += OpenNote;
            if (inventory != null)
            {
                inventory.ItemAdded += OnItemAdded;
                inventory.Changed += RefreshInventory;
            }
        }

        void OnDisable()
        {
            Objectives.Changed -= RefreshObjective;
            Messages.Posted -= Toast;
            JournalNote.Opened -= OpenNote;
            if (inventory != null)
            {
                inventory.ItemAdded -= OnItemAdded;
                inventory.Changed -= RefreshInventory;
            }
            Time.timeScale = 1f;
        }

        void Start()
        {
            RefreshObjective();
            RefreshInventory();
            ApplyUiState();
        }

        void Update()
        {
            HandleMenus();
            UpdatePrompt();
            UpdateStamina();
            UpdateToasts();

            modeText.text = rig.Mode == CameraMode.FirstPerson ? "1st person  [V]" : "3rd person  [V]";

            if (helpVisible && helpAutoHide > 0f)
            {
                helpAutoHide -= Time.unscaledDeltaTime;
                if (helpAutoHide <= 0f) SetHelp(false);
            }
        }

        void HandleMenus()
        {
            if (input.PausePressed)
            {
                if (noteOpen) CloseNote();
                else if (inventoryOpen) inventoryOpen = false;
                else paused = !paused;
                ApplyUiState();
            }

            if (paused) return;

            if (noteOpen && noteOpenedFrame != Time.frameCount && (input.InteractPressed || input.InventoryPressed))
            {
                CloseNote();
                return;
            }

            if (input.InventoryPressed && !noteOpen)
            {
                inventoryOpen = !inventoryOpen;
                ApplyUiState();
            }

            if (input.HelpPressed) SetHelp(!helpVisible);

            if (!AnyMenuOpen && Cursor.lockState != CursorLockMode.Locked &&
                UnityEngine.InputSystem.Mouse.current != null &&
                UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                ApplyUiState();
        }

        bool AnyMenuOpen => inventoryOpen || noteOpen || paused;

        void ApplyUiState()
        {
            inventoryPanel.SetActive(inventoryOpen);
            notePanel.SetActive(noteOpen);
            pausePanel.SetActive(paused);
            input.GameplayBlocked = AnyMenuOpen;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = AnyMenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = AnyMenuOpen;
            crosshair.enabled = !AnyMenuOpen;
        }

        void SetHelp(bool visible)
        {
            helpVisible = visible;
            helpPanel.SetActive(visible);
            if (!visible) helpAutoHide = 0f;
        }

        void OpenNote(JournalNote note)
        {
            noteTitle.text = note.Title;
            noteBody.text = note.Body;
            noteOpen = true;
            inventoryOpen = false;
            noteOpenedFrame = Time.frameCount;
            ApplyUiState();
        }

        void CloseNote()
        {
            noteOpen = false;
            ApplyUiState();
        }

        void UpdatePrompt()
        {
            var target = interactor.Current;
            if (target == null || AnyMenuOpen)
            {
                promptText.text = "";
                promptDetail.text = "";
                crosshair.color = new Color(1f, 1f, 1f, 0.55f);
                return;
            }

            bool usable = target.CanInteract(interactor);
            promptText.text = usable ? $"<color=#FFB84D>[E]</color>  {target.Prompt}" : target.Prompt;
            promptDetail.text = target.Detail ?? "";
            crosshair.color = usable ? Accent : new Color(1f, 1f, 1f, 0.8f);
        }

        void UpdateStamina()
        {
            float s = motor.Stamina01;
            staminaFill.anchorMax = new Vector2(s, 1f);
            staminaRoot.SetActive(s < 0.999f);
        }

        void Toast(string text)
        {
            toasts.Add((text, Time.unscaledTime));
            if (toasts.Count > 5) toasts.RemoveAt(0);
        }

        void OnItemAdded(ItemDefinition item, int amount) =>
            Toast(amount > 1 ? $"+{amount}  {item.DisplayName}" : $"+  {item.DisplayName}");

        void UpdateToasts()
        {
            toasts.RemoveAll(t => Time.unscaledTime - t.time > ToastDuration);
            var sb = new StringBuilder();
            foreach (var t in toasts) sb.AppendLine(t.text);
            toastText.text = sb.ToString();
        }

        void RefreshObjective()
        {
            objectiveTitle.text = string.IsNullOrEmpty(Objectives.Title) ? "" : Objectives.Title.ToUpperInvariant();
            objectiveProgress.text = Objectives.Progress;
        }

        void RefreshInventory()
        {
            if (inventory == null) return;
            var sb = new StringBuilder();
            foreach (ItemCategory cat in System.Enum.GetValues(typeof(ItemCategory)))
            {
                bool header = false;
                foreach (var s in inventory.Stacks)
                {
                    if (s.item.Category != cat) continue;
                    if (!header)
                    {
                        sb.AppendLine($"<color=#FFB84D><b>{CategoryLabel(cat)}</b></color>");
                        header = true;
                    }
                    sb.AppendLine($"   {s.item.DisplayName}  <color=#9AA6B2>x{s.amount}</color>");
                    sb.AppendLine($"   <size=15><color=#7F8A96>{s.item.Description}</color></size>");
                }
                if (header) sb.AppendLine();
            }
            inventoryText.text = sb.Length == 0 ? "<color=#9AA6B2>Your backpack is empty.</color>" : sb.ToString();
        }

        static string CategoryLabel(ItemCategory c) => c switch
        {
            ItemCategory.Resource => "RESOURCES",
            ItemCategory.Component => "COMPONENTS",
            ItemCategory.Tool => "TOOLS",
            ItemCategory.Clothing => "CLOTHING",
            _ => "ARTIFACTS",
        };

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.GetComponent<RectTransform>();

            crosshair = NewImage(root, "Crosshair", Color.white);
            Place(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));

            promptText = NewText(root, "Prompt", 26, TextAnchor.UpperCenter, TextColor);
            Place(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(900, 40));
            promptDetail = NewText(root, "PromptDetail", 18, TextAnchor.UpperCenter, Muted);
            Place(promptDetail.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -78), new Vector2(900, 60));

            objectiveTitle = NewText(root, "ObjectiveTitle", 22, TextAnchor.UpperLeft, Accent);
            objectiveTitle.fontStyle = FontStyle.Bold;
            PlaceCorner(objectiveTitle.rectTransform, new Vector2(0, 1), new Vector2(48, -40), new Vector2(800, 34));
            objectiveProgress = NewText(root, "ObjectiveProgress", 19, TextAnchor.UpperLeft, TextColor);
            PlaceCorner(objectiveProgress.rectTransform, new Vector2(0, 1), new Vector2(48, -74), new Vector2(800, 140));

            toastText = NewText(root, "Toasts", 22, TextAnchor.LowerRight, TextColor);
            PlaceCorner(toastText.rectTransform, new Vector2(1, 0), new Vector2(-48, 140), new Vector2(700, 220));

            modeText = NewText(root, "CameraMode", 16, TextAnchor.LowerRight, Muted);
            PlaceCorner(modeText.rectTransform, new Vector2(1, 0), new Vector2(-48, 40), new Vector2(400, 26));

            var staminaBg = NewImage(root, "Stamina", new Color(0f, 0f, 0f, 0.45f));
            Place(staminaBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 70), new Vector2(320, 8));
            staminaRoot = staminaBg.gameObject;
            var fill = NewImage(staminaBg.rectTransform, "Fill", new Color(0.55f, 0.85f, 0.6f, 0.9f));
            staminaFill = fill.rectTransform;
            staminaFill.anchorMin = Vector2.zero;
            staminaFill.anchorMax = Vector2.one;
            staminaFill.offsetMin = staminaFill.offsetMax = Vector2.zero;

            helpPanel = NewPanel(root, "Help", new Vector2(0, 0), new Vector2(48, 40), new Vector2(380, 300), true).gameObject;
            helpText = NewText(helpPanel.transform as RectTransform, "Text", 17, TextAnchor.UpperLeft, TextColor);
            Fill(helpText.rectTransform, 18);
            helpText.text =
                "<color=#FFB84D><b>CONTROLS</b></color>\n" +
                "WASD  move        Shift  sprint\n" +
                "Space  jump        C  crouch\n" +
                "Mouse  look        V  1st / 3rd person\n" +
                "E  interact / pick up\n" +
                "Tab  inventory    Esc  pause\n" +
                "H  show / hide this help\n\n" +
                "<color=#9AA6B2>Gamepad supported. Click the game view if the mouse is free.</color>";

            var inv = NewPanel(root, "Inventory", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 640), false);
            inventoryPanel = inv.gameObject;
            var invTitle = NewText(inv, "Title", 30, TextAnchor.UpperLeft, Accent);
            invTitle.fontStyle = FontStyle.Bold;
            invTitle.text = "BACKPACK";
            PlaceTop(invTitle.rectTransform, 28, 50);
            inventoryText = NewText(inv, "Items", 20, TextAnchor.UpperLeft, TextColor);
            Fill(inventoryText.rectTransform, 28);
            inventoryText.rectTransform.offsetMax = new Vector2(-28, -90);
            var invHint = NewText(inv, "Hint", 16, TextAnchor.LowerRight, Muted);
            invHint.text = "Tab / Esc to close";
            Fill(invHint.rectTransform, 20);

            var note = NewPanel(root, "Note", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 600), false);
            note.GetComponent<Image>().color = new Color(0.93f, 0.9f, 0.82f, 0.97f);
            notePanel = note.gameObject;
            noteTitle = NewText(note, "Title", 30, TextAnchor.UpperLeft, new Color(0.15f, 0.12f, 0.08f));
            noteTitle.fontStyle = FontStyle.Bold;
            PlaceTop(noteTitle.rectTransform, 40, 60);
            noteBody = NewText(note, "Body", 21, TextAnchor.UpperLeft, new Color(0.2f, 0.17f, 0.12f));
            Fill(noteBody.rectTransform, 40);
            noteBody.rectTransform.offsetMax = new Vector2(-40, -110);
            noteBody.lineSpacing = 1.15f;
            var noteHint = NewText(note, "Hint", 16, TextAnchor.LowerRight, new Color(0.4f, 0.35f, 0.28f));
            noteHint.text = "E / Esc to close";
            Fill(noteHint.rectTransform, 22);

            var pause = NewImage(root, "Pause", new Color(0f, 0f, 0f, 0.6f));
            Fill(pause.rectTransform, 0);
            pausePanel = pause.gameObject;
            var pauseText = NewText(pause.rectTransform, "Text", 34, TextAnchor.MiddleCenter, TextColor);
            Fill(pauseText.rectTransform, 0);
            pauseText.text = "<b>PAUSED</b>\n\n<size=22>Esc to resume</size>";

            inventoryPanel.SetActive(false);
            notePanel.SetActive(false);
            pausePanel.SetActive(false);
        }

        Text NewText(RectTransform parent, string name, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        static Image NewImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform NewPanel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, bool corner)
        {
            var img = NewImage(parent, name, PanelColor);
            if (corner) PlaceCorner(img.rectTransform, anchor, pos, size);
            else Place(img.rectTransform, anchor, pos, size);
            return img.rectTransform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void PlaceCorner(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void PlaceTop(RectTransform rt, float margin, float height)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(margin, -margin - height);
            rt.offsetMax = new Vector2(-margin, -margin);
        }

        static void Fill(RectTransform rt, float margin)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
