using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Something the tether can hook onto. <see cref="TetherTargeting"/> picks the one the player most likely means and the LINK
    /// button attaches to it; the swing itself lives in VelocityMotor.Tether.cs.
    /// A Swing anchor is a pivot to arc around; a Zip anchor reels you straight in and lets go with your speed; an Enemy Strike
    /// anchor sits on a light enemy: the rope zips you onto it and the blade cuts it down on arrival.
    /// It has no collider. The anchor point is this object's position, so it can ride on moving architecture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TetherAnchor : MonoBehaviour
    {
        public enum AnchorKind
        {
            Swing,
            Zip,
            EnemyStrike,
        }

        static readonly List<TetherAnchor> active = new List<TetherAnchor>();

        [SerializeField] AnchorKind kind = AnchorKind.Swing;

        [Tooltip("Scales Range (Tether Tuning) for this anchor. 1.5 = selectable from half as far again.")]
        [SerializeField, Min(0f)] float rangeMultiplier = 1f;

        [Tooltip("Seconds after you let go of this anchor before it can be selected again.")]
        [SerializeField, Min(0f)] float cooldown = 0.6f;

        [Tooltip("Enemy Strike only: the enemy it rides on. The anchor is only usable while it's alive.")]
        [SerializeField] CombatEnemy strikeTarget;

        [Header("Look")]
        [Tooltip("Renderers that switch material when selected.")]
        [SerializeField] Renderer[] stateRenderers;
        [SerializeField] Material idleMaterial;
        [SerializeField] Material selectedMaterial;

        [Tooltip("Turned to face the camera every frame (the ring).")]
        [SerializeField] Transform faceCamera;

        [Tooltip("Grown and pulsed while selected.")]
        [SerializeField] Transform pulseRoot;

        [SerializeField, Min(0f)] float selectedScale = 1.35f;
        [SerializeField, Min(0f)] float pulseRate = 3.5f;

        float readyTime;
        bool selected;
        bool attached;
        Material shownMaterial;
        Vector3 pulseBaseScale = Vector3.one;
        float scale = 1f;
        Transform viewer;

        /// <summary>Every enabled anchor. Small enough to scan each frame.</summary>
        public static IReadOnlyList<TetherAnchor> Active => active;

        public AnchorKind Kind
        {
            get => kind;
            set => kind = value;
        }

        /// <summary>The point the rope hooks onto.</summary>
        public Vector3 Position => transform.position;

        public float RangeMultiplier
        {
            get => rangeMultiplier;
            set => rangeMultiplier = Mathf.Max(0f, value);
        }

        public float Cooldown
        {
            get => cooldown;
            set => cooldown = Mathf.Max(0f, value);
        }

        public CombatEnemy StrikeTarget
        {
            get => strikeTarget;
            set => strikeTarget = value;
        }

        /// <summary>Zip and strike anchors reel you straight in instead of swinging.</summary>
        public bool PullsIn => kind != AnchorKind.Swing;

        /// <summary>False while cooling down after use, or (strike anchors) while its enemy is down.</summary>
        public bool IsReady => Time.time >= readyTime && (kind != AnchorKind.EnemyStrike || (strikeTarget != null && strikeTarget.IsAlive));

        public bool IsSelected => selected;

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
        }

        /// <summary>Wires up the look (used by the scene builders).</summary>
        public void SetLook(Renderer[] renderers, Material idle, Material selectedLook, Transform ring, Transform pulse)
        {
            stateRenderers = renderers;
            idleMaterial = idle;
            selectedMaterial = selectedLook;
            faceCamera = ring;
            pulseRoot = pulse;
            if (pulseRoot != null)
                pulseBaseScale = pulseRoot.localScale;
        }

        /// <summary>Set by <see cref="TetherTargeting"/>.</summary>
        public void SetSelected(bool value)
        {
            selected = value;
        }

        /// <summary>Set by the motor while the rope is hooked onto it.</summary>
        public void SetAttached(bool value)
        {
            attached = value;
            if (!value)
                readyTime = Time.time + cooldown;
        }

        /// <summary>Ready again at once (on respawn).</summary>
        public void ResetCooldown()
        {
            readyTime = 0f;
            attached = false;
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
            bool lit = selected || attached;
            Material look = lit ? selectedMaterial : idleMaterial;
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
                float goal = !IsReady && !attached ? 0.7f
                    : lit ? selectedScale * (1f + 0.08f * Mathf.Sin(Time.time * pulseRate * 2f * Mathf.PI)) : 1f;
                scale = Mathf.Lerp(scale, goal, 1f - Mathf.Exp(-18f * Time.deltaTime));
                pulseRoot.localScale = pulseBaseScale * scale;
            }

            if (faceCamera != null)
            {
                if (viewer == null && Camera.main != null)
                    viewer = Camera.main.transform;
                if (viewer != null)
                {
                    Vector3 toCamera = viewer.position - faceCamera.position;
                    if (toCamera.sqrMagnitude > 1e-4f)
                        faceCamera.rotation = Quaternion.LookRotation(toCamera);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.8f);
        }
    }
}
