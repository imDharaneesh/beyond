using UnityEngine;

namespace Beyond.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Speeds (m/s)")]
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float sprintSpeed = 6.2f;
        [SerializeField] float crouchSpeed = 1.6f;
        [SerializeField] float acceleration = 10f;
        [SerializeField] float airControl = 0.3f;
        [SerializeField] float turnSpeed = 720f;

        [Header("Jump & gravity")]
        [SerializeField] float jumpHeight = 1.1f;
        [SerializeField] float gravity = -19.6f;

        [Header("Stance")]
        [SerializeField] float standingHeight = 1.8f;
        [SerializeField] float crouchHeight = 1.15f;
        [SerializeField] float stanceChangeSpeed = 6f;
        [SerializeField] Transform bodyVisual;

        [Header("Stamina")]
        [SerializeField] bool unlimitedStamina = true;
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float sprintDrain = 16f;
        [SerializeField] float staminaRegen = 12f;
        [SerializeField] float regenDelay = 1f;

        [Header("Safety")]
        [SerializeField] float killY = -50f;

        CharacterController controller;
        PlayerInputReader input;
        PlayerCameraRig rig;
        Vector3 planarVelocity;
        float verticalVelocity;
        float stamina;
        float regenTimer;
        bool exhausted;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public bool IsGrounded => controller.isGrounded;
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public float Stamina01 => stamina / maxStamina;
        public float CurrentHeight => controller.height;
        public float EyeHeight => controller.height - 0.14f;
        public Vector3 PlanarVelocity => planarVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
            rig = GetComponent<PlayerCameraRig>();
            stamina = maxStamina;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            HandleStance(dt);

            Quaternion yaw = Quaternion.Euler(0f, rig != null ? rig.Yaw : transform.eulerAngles.y, 0f);
            Vector2 move = input.Move;
            Vector3 wish = yaw * new Vector3(move.x, 0f, move.y);

            bool wantsSprint = input.SprintHeld && move.y > 0.1f && !IsCrouching && !exhausted;
            IsSprinting = wantsSprint && wish.sqrMagnitude > 0.01f;
            UpdateStamina(dt);

            float speed = IsCrouching ? crouchSpeed : IsSprinting ? sprintSpeed : walkSpeed;
            Vector3 target = wish * speed;
            float accel = controller.isGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.Lerp(planarVelocity, target, 1f - Mathf.Exp(-accel * dt));

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (input.JumpPressed && controller.isGrounded && !IsCrouching)
                verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            verticalVelocity += gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);

            UpdateFacing(wish, dt);

            if (transform.position.y < killY) Respawn();
        }

        void HandleStance(float dt)
        {
            if (input.CrouchPressed)
            {
                if (!IsCrouching) IsCrouching = true;
                else if (HasHeadroom()) IsCrouching = false;
            }

            float targetHeight = IsCrouching ? crouchHeight : standingHeight;
            float h = Mathf.MoveTowards(controller.height, targetHeight, stanceChangeSpeed * dt);
            controller.height = h;
            controller.center = new Vector3(0f, h * 0.5f, 0f);

            if (bodyVisual != null)
                bodyVisual.localScale = new Vector3(1f, h / standingHeight, 1f);
        }

        bool HasHeadroom()
        {
            float r = controller.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * (r + 0.05f);
            Vector3 top = transform.position + Vector3.up * (standingHeight - r);
            return !Physics.CheckCapsule(bottom, top, r, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        void UpdateStamina(float dt)
        {
            if (unlimitedStamina)
            {
                stamina = maxStamina;
                exhausted = false;
                return;
            }
            if (IsSprinting)
            {
                stamina = Mathf.Max(0f, stamina - sprintDrain * dt);
                regenTimer = regenDelay;
                if (stamina <= 0f) exhausted = true;
            }
            else
            {
                regenTimer -= dt;
                if (regenTimer <= 0f) stamina = Mathf.Min(maxStamina, stamina + staminaRegen * dt);
                if (exhausted && stamina > maxStamina * 0.3f) exhausted = false;
            }
        }

        void UpdateFacing(Vector3 wish, float dt)
        {
            if (rig != null && rig.Mode == CameraMode.FirstPerson)
            {
                transform.rotation = Quaternion.Euler(0f, rig.Yaw, 0f);
            }
            else if (wish.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * dt);
            }
        }

        public void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
