using UnityEngine;

namespace Beyond.Player
{
    [DefaultExecutionOrder(40)]
    public class HumanoidLocomotion : MonoBehaviour
    {
        [SerializeField] float armDown = 34f;
        [SerializeField] float legSwing = 30f;
        [SerializeField] float kneeBend = 50f;
        [SerializeField] float armSwing = 24f;
        [SerializeField] float stridePerMetre = 1.35f;
        [SerializeField] float bob = 0.035f;

        Animator animator;
        PlayerMotor motor;
        Transform hips, spine, chest, neck, head;
        Transform lUpperArm, rUpperArm, lLowerArm, rLowerArm, lHand, rHand;
        Transform lUpperLeg, rUpperLeg, lLowerLeg, rLowerLeg, lFoot, rFoot;
        Transform[] all;
        Quaternion[] bind;
        Vector3 hipsBind;
        float phase, amount, crouch, air, breath;

        void Awake()
        {
            animator = GetComponent<Animator>();
            motor = GetComponentInParent<PlayerMotor>();
            if (animator == null || !animator.isHuman) { enabled = false; return; }
            hips = Bone(HumanBodyBones.Hips);
            spine = Bone(HumanBodyBones.Spine);
            chest = Bone(HumanBodyBones.Chest) ?? spine;
            neck = Bone(HumanBodyBones.Neck);
            head = Bone(HumanBodyBones.Head);
            lUpperArm = Bone(HumanBodyBones.LeftUpperArm);
            rUpperArm = Bone(HumanBodyBones.RightUpperArm);
            lLowerArm = Bone(HumanBodyBones.LeftLowerArm);
            rLowerArm = Bone(HumanBodyBones.RightLowerArm);
            lHand = Bone(HumanBodyBones.LeftHand);
            rHand = Bone(HumanBodyBones.RightHand);
            lUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            rUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
            lLowerLeg = Bone(HumanBodyBones.LeftLowerLeg);
            rLowerLeg = Bone(HumanBodyBones.RightLowerLeg);
            lFoot = Bone(HumanBodyBones.LeftFoot);
            rFoot = Bone(HumanBodyBones.RightFoot);
            all = new[] { hips, spine, chest, neck, head, lUpperArm, rUpperArm, lLowerArm, rLowerArm, lHand, rHand,
                lUpperLeg, rUpperLeg, lLowerLeg, rLowerLeg, lFoot, rFoot };
            bind = new Quaternion[all.Length];
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) bind[i] = all[i].localRotation;
            hipsBind = hips.localPosition;
            animator.enabled = false;
        }

        Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null) all[i].localRotation = bind[i];
            hips.localPosition = hipsBind;

            Vector3 velocity = motor != null ? motor.PlanarVelocity : Vector3.zero;
            float speed = velocity.magnitude;
            bool grounded = motor == null || motor.IsGrounded;
            float targetAmount = Mathf.Clamp01(speed / 3.2f) * (speed > 4.5f ? 1.25f : 1f);
            amount = Mathf.Lerp(amount, grounded ? targetAmount : 0f, 1f - Mathf.Exp(-8f * dt));
            air = Mathf.Lerp(air, grounded ? 0f : 1f, 1f - Mathf.Exp(-10f * dt));
            float crouchTarget = motor != null && motor.IsCrouching ? 1f : 0f;
            crouch = Mathf.Lerp(crouch, crouchTarget, 1f - Mathf.Exp(-8f * dt));
            phase += speed * stridePerMetre * dt * Mathf.PI;
            breath += dt * 1.7f;

            Transform root = transform;
            Vector3 right = root.right, fwd = root.forward, up = root.up;

            float s = Mathf.Sin(phase) * amount;
            float lean = Mathf.Clamp(speed * 1.6f, 0f, 9f) + crouch * 18f;
            Rot(spine, right, lean * 0.6f);
            Rot(spine, up, s * 5f);
            Rot(chest, right, lean * 0.4f + Mathf.Sin(breath) * 1.2f);
            Rot(chest, up, -s * 7f);
            Rot(neck, right, -lean * 0.5f);

            float bobOffset = -Mathf.Abs(Mathf.Cos(phase)) * bob * amount - crouch * 0.42f - air * 0.05f;
            hips.position += up * bobOffset;

            LowerArm(lUpperArm, lLowerArm, fwd);
            LowerArm(rUpperArm, rLowerArm, fwd);
            float elbow = 14f + 22f * amount + 30f * crouch;
            Rot(lUpperArm, right, s * armSwing - crouch * 10f);
            Rot(rUpperArm, right, -s * armSwing - crouch * 10f);
            Rot(lLowerArm, right, -elbow);
            Rot(rLowerArm, right, -elbow);

            float lKnee = Mathf.Max(0f, Mathf.Sin(phase + 0.7f)) * kneeBend * amount;
            float rKnee = Mathf.Max(0f, Mathf.Sin(phase + Mathf.PI + 0.7f)) * kneeBend * amount;
            float crouchHip = crouch * 70f + air * 25f;
            float crouchKnee = crouch * 105f + air * 45f;
            Rot(lUpperLeg, right, -s * legSwing - crouchHip);
            Rot(rUpperLeg, right, s * legSwing - crouchHip);
            Rot(lLowerLeg, right, lKnee + crouchKnee);
            Rot(rLowerLeg, right, rKnee + crouchKnee);
            Rot(lFoot, right, -lKnee * 0.25f - crouch * 35f);
            Rot(rFoot, right, -rKnee * 0.25f - crouch * 35f);
        }

        void LowerArm(Transform upper, Transform lower, Vector3 axis)
        {
            if (upper == null || lower == null) return;
            Vector3 dir = lower.position - upper.position;
            float plus = Vector3.Dot(Quaternion.AngleAxis(armDown, axis) * dir, transform.up);
            float minus = Vector3.Dot(Quaternion.AngleAxis(-armDown, axis) * dir, transform.up);
            Rot(upper, axis, plus < minus ? armDown : -armDown);
        }

        static void Rot(Transform t, Vector3 axis, float angle)
        {
            if (t != null && angle != 0f) t.rotation = Quaternion.AngleAxis(angle, axis) * t.rotation;
        }
    }
}
