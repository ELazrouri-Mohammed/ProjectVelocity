using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The slice's colour script: the air changes as you progress (a warm morning haze at the causeway, cooler and stranger
    /// deeper in, a bright pale gold at the crown). Each zone starts at a distance along the route (world Z) and sets the fog,
    /// the sky tint, the ambient light and the sun; the player's position blends between neighbouring zones. A few material
    /// and render-setting writes per frame, nothing else. The skybox is copied at start so the asset is never modified.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtmosphereZones : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            [Tooltip("Where (world Z) this zone is fully in effect.")]
            public float startZ;
            public Color fog;
            public float fogDensity;
            public Color skyTint;
            public float skyExposure;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public Color sun;
            public float sunIntensity;
        }

        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] Transform player;
        [SerializeField] Light sun;

        [Tooltip("Zones in route order (increasing Start Z).")]
        [SerializeField] Zone[] zones = new Zone[0];

        [Tooltip("Distance (m) over which one zone blends into the next, ending at the next zone's Start Z.")]
        [SerializeField, Min(1f)] float blendDistance = 140f;

        Material sky;

        public void Configure(Transform target, Light sunLight, Zone[] palette, float blend)
        {
            player = target;
            sun = sunLight;
            zones = palette;
            blendDistance = blend;
        }

        void Start()
        {
            if (RenderSettings.skybox != null)
            {
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
            }
            Apply();
        }

        void OnDestroy()
        {
            if (sky != null)
                Destroy(sky);
        }

        void LateUpdate()
        {
            Apply();
        }

        void Apply()
        {
            if (zones == null || zones.Length == 0 || player == null)
                return;
            float z = player.position.z;
            int i = 0;
            while (i < zones.Length - 1 && z >= zones[i + 1].startZ)
                i++;
            Zone a = zones[i];
            Zone b = i < zones.Length - 1 ? zones[i + 1] : a;
            float t = i < zones.Length - 1 ? Mathf.Clamp01((z - (b.startZ - blendDistance)) / blendDistance) : 0f;
            t = t * t * (3f - 2f * t);

            RenderSettings.fogColor = Color.Lerp(a.fog, b.fog, t);
            RenderSettings.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);
            RenderSettings.ambientSkyColor = Color.Lerp(a.ambientSky, b.ambientSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(a.ambientGround, b.ambientGround, t);
            if (sun != null)
            {
                sun.color = Color.Lerp(a.sun, b.sun, t);
                sun.intensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            }
            if (sky != null)
            {
                if (sky.HasProperty(TintId))
                    sky.SetColor(TintId, Color.Lerp(a.skyTint, b.skyTint, t));
                if (sky.HasProperty(ExposureId))
                    sky.SetFloat(ExposureId, Mathf.Lerp(a.skyExposure, b.skyExposure, t));
            }
        }
    }
}
