using UnityEngine;
using UnityEngine.InputSystem;

namespace Beyond.Player
{
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] float mouseSensitivity = 0.1f;
        [SerializeField] float gamepadLookSpeed = 160f;
        [SerializeField] bool invertY;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CrouchPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool ToggleCameraPressed { get; private set; }
        public bool InventoryPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool HelpPressed { get; private set; }

        public bool GameplayBlocked { get; set; }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool sprint = false, jump = false, crouch = false, interact = false;
            bool toggleCam = false, inventory = false, pause = false, help = false;

            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

                sprint |= kb.leftShiftKey.isPressed;
                jump |= kb.spaceKey.wasPressedThisFrame;
                crouch |= kb.cKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame;
                interact |= kb.eKey.wasPressedThisFrame;
                toggleCam |= kb.vKey.wasPressedThisFrame;
                inventory |= kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame;
                pause |= kb.escapeKey.wasPressedThisFrame;
                help |= kb.f1Key.wasPressedThisFrame || kb.hKey.wasPressedThisFrame;
            }

            if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                look += mouse.delta.ReadValue() * mouseSensitivity;

            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                look += pad.rightStick.ReadValue() * (gamepadLookSpeed * Time.unscaledDeltaTime);
                sprint |= pad.leftStickButton.isPressed;
                jump |= pad.buttonSouth.wasPressedThisFrame;
                crouch |= pad.buttonEast.wasPressedThisFrame;
                interact |= pad.buttonWest.wasPressedThisFrame;
                toggleCam |= pad.rightStickButton.wasPressedThisFrame;
                inventory |= pad.buttonNorth.wasPressedThisFrame;
                pause |= pad.startButton.wasPressedThisFrame;
                help |= pad.selectButton.wasPressedThisFrame;
            }

            if (invertY) look.y = -look.y;

            Move = GameplayBlocked ? Vector2.zero : Vector2.ClampMagnitude(move, 1f);
            Look = GameplayBlocked ? Vector2.zero : look;
            SprintHeld = sprint && !GameplayBlocked;
            JumpPressed = jump && !GameplayBlocked;
            CrouchPressed = crouch && !GameplayBlocked;
            ToggleCameraPressed = toggleCam && !GameplayBlocked;
            InteractPressed = interact;
            InventoryPressed = inventory;
            PausePressed = pause;
            HelpPressed = help;
        }
    }
}
