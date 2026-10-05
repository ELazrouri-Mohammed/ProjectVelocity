using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Architecture that moves forever on a fixed, learnable rhythm: a crusher that slams and lifts, a wall that sweeps across
    /// a bridge, a platform that shuttles over a gap, a bridge that turns. Moved by its transform (colliders in children, no
    /// physics), on its own clock, so its timing never depends on the frame rate and is the same on every attempt.
    /// Ping Pong: hold at A, warn, move to B, hold, move back. Rotate: turn steadily around an axis.
    /// A player standing on it is carried; one it moves into is shoved, crushed if pinned, and killed if it's a hazard (see
    /// <see cref="KinematicResolver"/>). Runs before the player each frame.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class LoopingMotion : MonoBehaviour, IKinematicMover
    {
        public enum MotionMode
        {
            PingPong,
            Rotate,
        }

        const float TrembleRate = 16f;

        [SerializeField] MotionMode mode = MotionMode.PingPong;

        [Header("Ping Pong (local to the parent)")]
        [SerializeField] Vector3 positionA;
        [SerializeField] Vector3 rotationA;
        [SerializeField] Vector3 positionB;
        [SerializeField] Vector3 rotationB;

        [Tooltip("Seconds resting at A, moving to B, resting at B, moving back to A.")]
        [SerializeField, Min(0f)] float holdA = 1f;
        [SerializeField, Min(0.01f)] float moveToB = 0.4f;
        [SerializeField, Min(0f)] float holdB = 0.6f;
        [SerializeField, Min(0.01f)] float moveToA = 1f;

        [Tooltip("Shape of the move to B (time 0-1 → pose 0-1). The way back is a plain ease.")]
        [SerializeField] AnimationCurve easeToB = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("The last seconds of the rest at A are a warning: the seams glow and it trembles.")]
        [SerializeField, Min(0f)] float anticipation = 0.45f;

        [SerializeField, Min(0f)] float tremble = 0.08f;

        [Header("Rotate")]
        [SerializeField] Vector3 rotationAxis = Vector3.up;
        [SerializeField] float degreesPerSecond = 30f;

        [Header("Timing")]
        [Tooltip("Seconds added to its clock, so several loops can run out of step.")]
        [SerializeField] float phaseOffset;

        [Header("Danger")]
        [Tooltip("A hazard: touching it while it moves kills (Ping Pong: only on the move to B, its strike).")]
        [SerializeField] bool lethal;

        [SerializeField] VelocityPlayerController player;

        [Header("Look")]
        [SerializeField] Renderer[] accentRenderers;
        [SerializeField] Material restMaterial;
        [SerializeField] Material warningMaterial;
        [SerializeField] Material movingMaterial;

        float clock;
        bool moving;
        bool towardB;
        BoxCollider[] boxes;
        CharacterController body;
        VelocityMotor motor;
        Material shownMaterial;
        Matrix4x4 beforeWorldToLocal = Matrix4x4.identity;
        Matrix4x4 afterLocalToWorld = Matrix4x4.identity;
        Quaternion baseRotation = Quaternion.identity;
        bool wasWarning;
        bool wasMovingToB;
        readonly IKinematicMover[] self = new IKinematicMover[1];
        readonly bool[] selfMoved = { true };

        /// <summary>Raised when the warning before a strike starts.</summary>
        public event Action<LoopingMotion> Warned;
        /// <summary>Raised when it starts moving toward B (its strike).</summary>
        public event Action<LoopingMotion> Struck;
        /// <summary>Raised when it arrives at B.</summary>
        public event Action<LoopingMotion> Landed;

        public BoxCollider[] Boxes
        {
            get
            {
                if (boxes == null)
                    boxes = GetComponentsInChildren<BoxCollider>(true);
                return boxes;
            }
        }

        public bool IsLethal => lethal && (mode == MotionMode.Rotate || towardB);
        public bool IsMoving => moving;

        public float Period => mode == MotionMode.PingPong ? holdA + moveToB + holdB + moveToA : 0f;

        public Vector3 CarryPoint(Vector3 point)
        {
            return afterLocalToWorld.MultiplyPoint3x4(beforeWorldToLocal.MultiplyPoint3x4(point));
        }

        /// <summary>Sets up a ping-pong move (used by the scene builder).</summary>
        public void ConfigurePingPong(Vector3 fromPosition, Vector3 fromRotation, Vector3 toPosition, Vector3 toRotation,
            float restAtA, float toB, float restAtB, float backToA, AnimationCurve curve, float offset)
        {
            mode = MotionMode.PingPong;
            positionA = fromPosition;
            rotationA = fromRotation;
            positionB = toPosition;
            rotationB = toRotation;
            holdA = restAtA;
            moveToB = Mathf.Max(0.01f, toB);
            holdB = restAtB;
            moveToA = Mathf.Max(0.01f, backToA);
            easeToB = curve;
            phaseOffset = offset;
        }

        /// <summary>Sets up a steady rotation (used by the scene builder).</summary>
        public void ConfigureRotate(Vector3 axis, float speed, float offset)
        {
            mode = MotionMode.Rotate;
            rotationAxis = axis;
            degreesPerSecond = speed;
            phaseOffset = offset;
        }

        public void SetDanger(bool isLethal, float warning, float shake)
        {
            lethal = isLethal;
            anticipation = warning;
            tremble = shake;
        }

        public void SetLook(Renderer[] accents, Material rest, Material warning, Material movingLook)
        {
            accentRenderers = accents;
            restMaterial = rest;
            warningMaterial = warning;
            movingMaterial = movingLook;
        }

        public void SetPlayer(VelocityPlayerController target)
        {
            player = target;
            body = null;
        }

        void Awake()
        {
            boxes = GetComponentsInChildren<BoxCollider>(true);
            baseRotation = transform.localRotation;
            self[0] = this;
            clock = phaseOffset;
            if (player == null)
                player = FindAnyObjectByType<VelocityPlayerController>();
            Apply(true);
        }

        void Update()
        {
            clock += Mathf.Min(Time.deltaTime, 0.05f);
            if (!Apply(false))
                return;
            Physics.SyncTransforms();
            if (player == null)
                return;
            if (body == null)
            {
                body = player.GetComponent<CharacterController>();
                motor = player.GetComponent<VelocityMotor>();
            }
            KinematicResolver.Resolve(player, body, motor, self, selfMoved, 1);
        }

        /// <summary>Puts it where its clock says. Returns true when the transform changed.</summary>
        bool Apply(bool force)
        {
            beforeWorldToLocal = transform.worldToLocalMatrix;
            if (mode == MotionMode.Rotate)
            {
                moving = degreesPerSecond != 0f;
                towardB = false;
                transform.localRotation = baseRotation * Quaternion.AngleAxis(Mathf.Repeat(clock * degreesPerSecond, 360f), rotationAxis);
                afterLocalToWorld = transform.localToWorldMatrix;
                SetMaterial(movingMaterial != null ? movingMaterial : restMaterial);
                return moving || force;
            }

            float period = Period;
            float t = Mathf.Repeat(clock, Mathf.Max(0.01f, period));
            float pose;
            Vector3 shake = Vector3.zero;
            bool warning = false;
            towardB = false;
            moving = false;
            if (t < holdA)
            {
                pose = 0f;
                warning = t >= holdA - anticipation;
                if (warning && tremble > 0f)
                {
                    Vector3 travel = positionB - positionA;
                    Vector3 direction = travel.sqrMagnitude > 1e-4f ? travel.normalized : Vector3.right;
                    shake = direction * (Mathf.Sin(t * TrembleRate * 2f * Mathf.PI) * tremble);
                }
            }
            else if (t < holdA + moveToB)
            {
                float u = (t - holdA) / moveToB;
                pose = easeToB != null && easeToB.length > 0 ? easeToB.Evaluate(u) : u;
                moving = true;
                towardB = true;
            }
            else if (t < holdA + moveToB + holdB)
            {
                pose = 1f;
            }
            else
            {
                float u = (t - holdA - moveToB - holdB) / moveToA;
                pose = 1f - u * u * (3f - 2f * u);
                moving = true;
            }

            if (warning && !wasWarning)
                Warned?.Invoke(this);
            if (towardB && !wasMovingToB)
                Struck?.Invoke(this);
            if (!towardB && wasMovingToB)
                Landed?.Invoke(this);
            wasWarning = warning;
            wasMovingToB = towardB;

            SetMaterial(warning ? warningMaterial : moving ? movingMaterial : restMaterial);

            Vector3 position = Vector3.LerpUnclamped(positionA, positionB, pose) + shake;
            Quaternion rotation = Quaternion.Euler(Vector3.LerpUnclamped(rotationA, rotationB, pose));
            transform.GetLocalPositionAndRotation(out Vector3 oldPosition, out Quaternion oldRotation);
            if (!force && oldPosition == position && oldRotation == rotation)
                return false;
            transform.SetLocalPositionAndRotation(position, rotation);
            afterLocalToWorld = transform.localToWorldMatrix;
            return true;
        }

        void SetMaterial(Material material)
        {
            if (material == null || material == shownMaterial || accentRenderers == null)
                return;
            foreach (Renderer r in accentRenderers)
            {
                if (r != null)
                    r.sharedMaterial = material;
            }
            shownMaterial = material;
        }
    }
}
