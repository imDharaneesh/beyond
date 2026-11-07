using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beyond.Player
{
    public enum CameraMode { FirstPerson, ThirdPerson }

    [DefaultExecutionOrder(50)]
    public class PlayerCameraRig : MonoBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [SerializeField] CameraMode mode = CameraMode.ThirdPerson;
        [SerializeField] float minPitch = -75f;
        [SerializeField] float maxPitch = 80f;

        [Header("Third person")]
        [SerializeField] float pivotHeight = 1.6f;
        [SerializeField] Vector3 shoulderOffset = new Vector3(0.5f, 0.15f, -3.0f);
        [SerializeField] float collisionRadius = 0.2f;
        [SerializeField] float collisionRecoverSpeed = 6f;

        [Header("Blending")]
        [SerializeField] float modeBlendSpeed = 5f;
        [SerializeField] Renderer[] hideInFirstPerson;

        PlayerMotor motor;
        PlayerInputReader input;
        float blend;
        float currentDistance;
        bool bodyHidden;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public CameraMode Mode => mode;
        public Transform CameraTransform => cameraTransform;
        public event Action<CameraMode> ModeChanged;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            input = GetComponent<PlayerInputReader>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (hideInFirstPerson == null || hideInFirstPerson.Length == 0)
                hideInFirstPerson = GetComponentsInChildren<Renderer>(true);

            Yaw = transform.eulerAngles.y;
            Pitch = 10f;
            blend = mode == CameraMode.ThirdPerson ? 1f : 0f;
            currentDistance = shoulderOffset.magnitude;
            SetBodyHidden(mode == CameraMode.FirstPerson);
        }

        public void SetMode(CameraMode newMode)
        {
            if (newMode == mode) return;
            mode = newMode;
            ModeChanged?.Invoke(mode);
        }

        void LateUpdate()
        {
            if (cameraTransform == null) return;
            float dt = Time.deltaTime;

            if (input.ToggleCameraPressed)
                SetMode(mode == CameraMode.FirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson);

            Vector2 look = input.Look;
            Yaw += look.x;
            Pitch = Mathf.Clamp(Pitch - look.y, minPitch, maxPitch);

            blend = Mathf.MoveTowards(blend, mode == CameraMode.ThirdPerson ? 1f : 0f, modeBlendSpeed * dt);
            float t = blend * blend * (3f - 2f * blend);
            SetBodyHidden(blend < 0.35f);

            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            Vector3 up = Vector3.up;
            Vector3 eye = transform.position + up * motor.EyeHeight;

            float crouchScale = motor.CurrentHeight / 1.8f;
            Vector3 pivot = transform.position + up * (pivotHeight * crouchScale);
            Vector3 desired = pivot + rotation * shoulderOffset;
            Vector3 toCam = desired - pivot;
            float maxDist = toCam.magnitude;
            Vector3 dir = toCam / maxDist;

            float allowed = maxDist;
            if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit, maxDist,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0.3f, hit.distance);

            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.MoveTowards(currentDistance, allowed, collisionRecoverSpeed * dt);
            Vector3 thirdPos = pivot + dir * currentDistance;

            cameraTransform.SetPositionAndRotation(Vector3.Lerp(eye, thirdPos, t), rotation);
        }

        void SetBodyHidden(bool hidden)
        {
            if (hidden == bodyHidden || hideInFirstPerson == null) return;
            bodyHidden = hidden;
            foreach (var r in hideInFirstPerson)
                if (r != null) r.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }
}
