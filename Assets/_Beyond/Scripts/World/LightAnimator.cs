using UnityEngine;

namespace Beyond.World
{
    public class LightAnimator : MonoBehaviour
    {
        public enum Mode { Blink, Flicker }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Mode mode;
        [SerializeField] Light target;
        [SerializeField] Renderer emissive;
        [SerializeField] Color emissionColor = Color.red;
        [SerializeField] float rate = 1f;

        float baseIntensity;
        float seed;
        MaterialPropertyBlock block;

        public void Configure(Mode animMode, Light light, Renderer glow, Color glowColor, float speed)
        {
            mode = animMode;
            target = light;
            emissive = glow;
            emissionColor = glowColor;
            rate = speed;
        }

        void Awake()
        {
            if (target != null) baseIntensity = target.intensity;
            block = new MaterialPropertyBlock();
            seed = Random.value * 100f;
        }

        void Update()
        {
            float k = mode == Mode.Blink
                ? (Mathf.Repeat(Time.time * rate + seed, 1f) < 0.2f ? 1f : 0.03f)
                : 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * rate * 7f + seed, seed);

            if (target != null) target.intensity = baseIntensity * k;
            if (emissive != null)
            {
                emissive.GetPropertyBlock(block);
                block.SetColor(EmissionId, emissionColor * k);
                emissive.SetPropertyBlock(block);
            }
        }
    }
}
