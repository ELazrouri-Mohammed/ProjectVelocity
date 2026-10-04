using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A traversal target (propulsion node). Activating it while it's selected pulls the player through its centre at speed
    /// and launches them out the far side, reshaping their momentum instead of replacing it.
    /// Selection lives in <see cref="TraversalTargeting"/>, the propulsion in VelocityMotor.Targets.cs, and the shared feel
    /// values in Movement Tuning (Traversal Targets). The values here adjust this one target.
    /// It has no collider: the player flies straight through it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TraversalTarget : MonoBehaviour
    {
        static readonly List<TraversalTarget> active = new List<TraversalTarget>();

        [Header("This Target")]
        [Tooltip("Scales Target Detection Range (Movement Tuning) for this target. 2 = selectable from twice as far.")]
        [SerializeField, Min(0f)] float rangeMultiplier = 1f;

        [Tooltip("Scales Target Propulsion Speed (Movement Tuning) for this target.")]
        [SerializeField, Min(0f)] float strengthMultiplier = 1f;

        [Tooltip("Added to Target Upward Bias (Movement Tuning) when launching out of this target. Raise it for targets that redirect you upward.")]
        [SerializeField, Range(0f, 1f)] float extraUpwardBias;

        [Tooltip("Seconds after you leave this target before it can be selected again.")]
        [SerializeField, Min(0f)] float cooldown = 1.5f;

        [Header("Placeholder Look")]
        [Tooltip("Renderers that switch material with the target's state.")]
        [SerializeField] Renderer[] stateRenderers;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material selectedMaterial;
        [SerializeField] Material cooldownMaterial;

        [Tooltip("Turned to face the camera every frame (the ring around the core).")]
        [SerializeField] Transform faceCamera;

        [Tooltip("Grown and pulsed while selected.")]
        [SerializeField] Transform pulseRoot;

        [Tooltip("Size while selected, relative to idle.")]
        [SerializeField, Min(0f)] float selectedScale = 1.3f;

        [Tooltip("Pulse size while selected, as a share of the selected size.")]
        [SerializeField, Range(0f, 0.5f)] float pulseAmount = 0.1f;

        [Tooltip("Pulses per second while selected.")]
        [SerializeField, Min(0f)] float pulseRate = 3f;

        float readyTime;
        bool selected;
        Material shownMaterial;
        Vector3 pulseBaseScale = Vector3.one;
        float scale = 1f;

        /// <summary>Every enabled target. Small enough to scan each frame.</summary>
        public static IReadOnlyList<TraversalTarget> Active => active;

        /// <summary>World-space centre the player is pulled through.</summary>
        public Vector3 Position => transform.position;

        /// <summary>False while cooling down after use.</summary>
        public bool IsReady => Time.time >= readyTime;

        public bool IsSelected => selected;

        public float RangeMultiplier
        {
            get => rangeMultiplier;
            set => rangeMultiplier = Mathf.Max(0f, value);
        }

        public float StrengthMultiplier
        {
            get => strengthMultiplier;
            set => strengthMultiplier = Mathf.Max(0f, value);
        }

        public float ExtraUpwardBias
        {
            get => extraUpwardBias;
            set => extraUpwardBias = Mathf.Clamp01(value);
        }

        public float Cooldown
        {
            get => cooldown;
            set => cooldown = Mathf.Max(0f, value);
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>Wires up the placeholder look (used by the movement test builder).</summary>
        public void SetLook(Renderer[] renderers, Material idle, Material selectedLook, Material coolingDown, Transform ring, Transform pulse)
        {
            stateRenderers = renderers;
            idleMaterial = idle;
            selectedMaterial = selectedLook;
            cooldownMaterial = coolingDown;
            faceCamera = ring;
            pulseRoot = pulse;
        }

        /// <summary>Set by <see cref="TraversalTargeting"/>: this is the target the activate button will launch through.</summary>
        public void SetSelected(bool value)
        {
            selected = value;
        }

        /// <summary>Called by the motor when the player leaves this target.</summary>
        public void StartCooldown()
        {
            readyTime = Time.time + cooldown;
        }

        void Awake()
        {
            if (pulseRoot != null)
                pulseBaseScale = pulseRoot.localScale;
        }

        void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        void OnDisable()
        {
            active.Remove(this);
            selected = false;
        }

        void LateUpdate()
        {
            Material look = selected ? selectedMaterial : IsReady ? idleMaterial : cooldownMaterial;
            if (look != null && look != shownMaterial && stateRenderers != null)
            {
                foreach (Renderer r in stateRenderers)
                {
                    if (r != null)
                        r.sharedMaterial = look;
                }
                shownMaterial = look;
            }

            if (pulseRoot != null)
            {
                float goal = selected ? selectedScale * (1f + pulseAmount * Mathf.Sin(Time.time * pulseRate * 2f * Mathf.PI)) : 1f;
                scale = Mathf.Lerp(scale, goal, 1f - Mathf.Exp(-20f * Time.deltaTime));
                pulseRoot.localScale = pulseBaseScale * scale;
            }

            Camera view = Camera.main;
            if (faceCamera != null && view != null)
            {
                Vector3 toCamera = view.transform.position - faceCamera.position;
                if (toCamera.sqrMagnitude > 1e-4f)
                    faceCamera.rotation = Quaternion.LookRotation(toCamera);
            }
        }
    }
}
