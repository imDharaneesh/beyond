using UnityEngine;

namespace Beyond.Player
{
    public class ProceduralBodyAnimator : MonoBehaviour
    {
        [SerializeField] Transform leftLeg, rightLeg, leftArm, rightArm, torso;
        [SerializeField] float strideFrequency = 1.6f;
        [SerializeField] float legSwing = 32f;
        [SerializeField] float armSwing = 26f;

        PlayerMotor motor;
        float phase;
        float amount;

        public void Configure(Transform lLeg, Transform rLeg, Transform lArm, Transform rArm, Transform body)
        {
            leftLeg = lLeg;
            rightLeg = rLeg;
            leftArm = lArm;
            rightArm = rArm;
            torso = body;
        }

        void Awake() => motor = GetComponentInParent<PlayerMotor>();

        void LateUpdate()
        {
            float speed = motor != null ? motor.PlanarVelocity.magnitude : 0f;
            bool grounded = motor == null || motor.IsGrounded;
            float target = grounded ? Mathf.Clamp01(speed / 3f) : 0.35f;
            amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            phase += speed * strideFrequency * Time.deltaTime * Mathf.PI;

            float s = Mathf.Sin(phase) * amount;
            Swing(leftLeg, s * legSwing);
            Swing(rightLeg, -s * legSwing);
            Swing(leftArm, -s * armSwing);
            Swing(rightArm, s * armSwing);

            if (torso != null)
            {
                float lean = Mathf.Clamp(speed * 1.6f, 0f, 10f);
                float bob = Mathf.Abs(Mathf.Cos(phase)) * 0.035f * amount;
                torso.localRotation = Quaternion.Euler(lean, 0f, 0f);
                torso.localPosition = new Vector3(0f, bob, 0f);
            }
        }

        static void Swing(Transform t, float angle)
        {
            if (t != null) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
