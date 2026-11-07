using UnityEngine;

namespace Beyond.Player
{
    [RequireComponent(typeof(Animator))]
    public class MocapLocomotion : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int CrouchId = Animator.StringToHash("Crouch");
        static readonly int JumpId = Animator.StringToHash("Jump");

        [SerializeField] float speedDamping = 0.12f;
        [SerializeField] float footHeight = 0.085f;
        [SerializeField] float footReach = 0.45f;
        [SerializeField] float pelvisSmoothing = 12f;
        [SerializeField] float rootClearance = 0.0f;

        [SerializeField] float fingerRelax = 0.7f;
        [SerializeField] float lookBodyWeight = 0.3f;
        [SerializeField] float lookHeadWeight = 0.85f;

        static readonly HumanBodyBones[] Fingers =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
        };

        [SerializeField, HideInInspector] Transform[] fingerBones;
        [SerializeField, HideInInspector] Quaternion[] fingerRest;

        Animator animator;
        PlayerMotor motor;
        Transform body;
        float pelvisOffset;
        float rootOffset;
        float lWeight, rWeight;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            motor = GetComponentInParent<PlayerMotor>();
            body = transform.parent != null ? transform.parent : transform;
            if (fingerBones == null || fingerBones.Length != Fingers.Length) CaptureFingerRest();
        }

        public void CaptureFingerRest()
        {
            if (animator == null) animator = GetComponent<Animator>();
            fingerBones = new Transform[Fingers.Length];
            fingerRest = new Quaternion[Fingers.Length];
            for (int i = 0; i < Fingers.Length; i++)
            {
                fingerBones[i] = animator.GetBoneTransform(Fingers[i]);
                if (fingerBones[i] != null) fingerRest[i] = fingerBones[i].localRotation;
            }
        }

        void LateUpdate()
        {
            if (fingerBones == null) return;
            for (int i = 0; i < fingerBones.Length; i++)
                if (fingerBones[i] != null)
                    fingerBones[i].localRotation = Quaternion.Slerp(fingerBones[i].localRotation, fingerRest[i], fingerRelax);
        }

        void OnEnable()
        {
            if (motor == null) motor = GetComponentInParent<PlayerMotor>();
            if (motor != null) motor.Jumped += OnJumped;
        }

        void OnDisable()
        {
            if (motor != null) motor.Jumped -= OnJumped;
        }

        void OnJumped() => animator.SetTrigger(JumpId);

        bool Grounded(out RaycastHit hit)
        {
            bool cast = Physics.Raycast(body.position + Vector3.up * 0.6f, Vector3.down, out hit, 1.6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return (cast && hit.distance < 0.85f) || (motor != null && motor.IsGrounded);
        }

        void Update()
        {
            if (motor == null) return;
            float dt = Time.deltaTime;
            bool grounded = Grounded(out RaycastHit hit);
            animator.SetFloat(SpeedId, motor.PlanarVelocity.magnitude, speedDamping, dt);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(CrouchId, motor.IsCrouching);

            float target = 0f;
            if (grounded && hit.collider != null)
                target = Mathf.Clamp(hit.point.y - body.position.y + rootClearance, -0.3f, 0.05f);
            rootOffset = Mathf.Lerp(rootOffset, target, 1f - Mathf.Exp(-18f * dt));
            transform.localPosition = new Vector3(0f, rootOffset, 0f);
        }

        void OnAnimatorIK(int layerIndex)
        {
            bool grounded = Grounded(out _);
            float dt = Time.deltaTime;
            float lDelta = SolveFoot(AvatarIKGoal.LeftFoot, grounded, ref lWeight, dt);
            float rDelta = SolveFoot(AvatarIKGoal.RightFoot, grounded, ref rWeight, dt);
            float wanted = grounded ? Mathf.Min(0f, Mathf.Min(lDelta, rDelta)) : 0f;
            pelvisOffset = Mathf.Lerp(pelvisOffset, wanted, 1f - Mathf.Exp(-pelvisSmoothing * dt));
            animator.bodyPosition += Vector3.up * pelvisOffset;

            Vector3 look = body.position + Vector3.up * 1.62f + body.forward * 12f;
            animator.SetLookAtWeight(1f, lookBodyWeight, lookHeadWeight, 0f, 0.55f);
            animator.SetLookAtPosition(look);
        }

        float SolveFoot(AvatarIKGoal goal, bool grounded, ref float weight, float dt)
        {
            Vector3 foot = animator.GetIKPosition(goal);
            Quaternion rot = animator.GetIKRotation(goal);
            float planted = 0f;
            float delta = 0f;
            if (grounded && Physics.Raycast(foot + Vector3.up * footReach, Vector3.down, out RaycastHit hit, footReach * 2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                float above = foot.y - (hit.point.y + footHeight);
                planted = Mathf.Clamp01(1f - above / 0.18f);
                delta = hit.point.y + footHeight - foot.y;
                Vector3 target = new Vector3(foot.x, Mathf.Max(foot.y, hit.point.y + footHeight) + (delta < 0f ? delta * planted : 0f), foot.z);
                Quaternion tilt = Quaternion.FromToRotation(Vector3.up, hit.normal);
                animator.SetIKPosition(goal, target);
                animator.SetIKRotation(goal, Quaternion.Slerp(rot, tilt * rot, planted));
            }
            weight = Mathf.Lerp(weight, grounded ? planted : 0f, 1f - Mathf.Exp(-20f * dt));
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight * 0.8f);
            return delta;
        }
    }
}
