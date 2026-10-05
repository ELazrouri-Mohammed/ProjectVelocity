using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Third-person orbit camera built for speed: raw, immediate rotation, near-zero follow lag,
    /// collision pull-in and a field of view that widens with speed. Optional (Camera Tuning): a portrait field of view that
    /// keeps enough width on tall phones, auto follow behind your direction of travel when you aren't turning it yourself,
    /// looking down while falling fast, a wider frame on the tether, a slight roll on walls and small impulses. Never cinematic:
    /// nothing takes the camera away from the player.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class VelocityCamera : MonoBehaviour
    {
        // How quickly the camera eases back out after a wall stops blocking it (s).
        const float DistanceRecoverLag = 0.12f;

        [Tooltip("What the camera follows (the player root).")]
        [SerializeField] Transform target;

        [Tooltip("Motor whose speed drives the dynamic field of view.")]
        [SerializeField] VelocityMotor targetMotor;

        [Tooltip("Camera tuning asset. Changes made to it in Play Mode are kept when you stop playing.")]
        [SerializeField] CameraTuning tuning;

        Camera cam;
        CameraTuning fallbackTuning;
        float yaw;
        float pitch;
        Vector3 pivot;
        float distance;
        float fovVelocity;
        bool placed;
        float lastLookTime = -10f;
        float roll;
        float extraDistance;
        Vector3 impulse;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public VelocityMotor TargetMotor
        {
            get => targetMotor;
            set => targetMotor = value;
        }

        public CameraTuning Tuning
        {
            get => tuning;
            set => tuning = value;
        }

        /// <summary>Horizontal facing of the camera; movement input is relative to this.</summary>
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        CameraTuning Settings
        {
            get
            {
                if (tuning != null)
                    return tuning;
                if (fallbackTuning == null)
                    fallbackTuning = ScriptableObject.CreateInstance<CameraTuning>();
                return fallbackTuning;
            }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (!placed)
                SnapBehindTarget(target != null ? target.eulerAngles.y : transform.eulerAngles.y);
        }

        void OnDestroy()
        {
            if (fallbackTuning != null)
                Destroy(fallbackTuning);
        }

        /// <summary>
        /// Applies look input. Pointer deltas are scaled by sensitivity, stick rates by turn speed and frame time;
        /// <paramref name="degrees"/> (touch drag, already scaled by its own sensitivity) is applied as is.
        /// </summary>
        public void AddLookInput(Vector2 pointerDelta, Vector2 stickRate, Vector2 degrees, float deltaTime)
        {
            CameraTuning t = Settings;
            degrees += pointerDelta * t.lookSensitivity + stickRate * (t.stickLookSpeed * deltaTime);
            if (degrees.sqrMagnitude > 1e-6f)
                lastLookTime = Time.unscaledTime;
            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = Mathf.Clamp(pitch + (t.invertY ? degrees.y : -degrees.y), t.minPitch, t.maxPitch);
        }

        /// <summary>
        /// Tiny impact feedback: widens the field of view by <paramref name="degrees"/> at once, and the usual speed smoothing
        /// eases it back. Never moves or turns the camera.
        /// </summary>
        public void KickFieldOfView(float degrees)
        {
            if (cam != null && degrees != 0f)
                cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + degrees, 1f, 179f);
        }

        /// <summary>A small positional kick (m, world space) that settles quickly. Landings, slams. Use sparingly.</summary>
        public void AddImpulse(Vector3 offset)
        {
            impulse = Vector3.ClampMagnitude(impulse + offset, 0.6f);
        }

        /// <summary>Instantly places the camera behind the target, facing the given yaw.</summary>
        public void SnapBehindTarget(float facingYaw)
        {
            CameraTuning t = Settings;
            yaw = facingYaw;
            pitch = Mathf.Clamp(t.startPitch, t.minPitch, t.maxPitch);
            if (target != null)
                pivot = target.position + Vector3.up * t.pivotHeight;
            distance = t.distance;
            fovVelocity = 0f;
            roll = 0f;
            extraDistance = 0f;
            impulse = Vector3.zero;
            if (cam != null)
                cam.fieldOfView = TargetFov(t, 0f);
            placed = true;
            PlaceCamera(t, 0f, true);
        }

        void LateUpdate()
        {
            if (target == null)
                return;

            CameraTuning t = Settings;
            float dt = Time.deltaTime;

            Vector3 desiredPivot = target.position + Vector3.up * t.pivotHeight;
            float horizontal = Damp(t.followLag, dt);
            float vertical = Damp(t.verticalFollowLag, dt);
            pivot = new Vector3(
                Mathf.Lerp(pivot.x, desiredPivot.x, horizontal),
                Mathf.Lerp(pivot.y, desiredPivot.y, vertical),
                Mathf.Lerp(pivot.z, desiredPivot.z, horizontal));

            if (cam != null)
            {
                // On a wall, in a target pull or a lunge, upward speed counts too, so a climb or a steep pull doesn't narrow the view.
                float speed = targetMotor == null ? 0f
                    : targetMotor.IsWallRunning || targetMotor.IsTargetPulling || targetMotor.IsLunging || targetMotor.IsTethering ? targetMotor.Velocity.magnitude
                    : targetMotor.Speed;
                float speed01 = Mathf.InverseLerp(t.fovMinSpeed, t.fovMaxSpeed, speed);
                float targetFov = TargetFov(t, speed01);
                cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, t.fovSmoothTime);
            }

            AutoFollow(t, dt);

            // Presentation extras: wider on the tether, a slight roll on walls, impulses settling.
            bool swinging = targetMotor != null && targetMotor.IsTethering;
            extraDistance = Mathf.Lerp(extraDistance, swinging ? t.tetherDistanceBoost : 0f, 1f - Mathf.Exp(-4f * dt));
            float wantedRoll = 0f;
            if (targetMotor != null && targetMotor.IsWallRunning && t.wallRoll > 0f)
            {
                Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                wantedRoll = -Mathf.Sign(Vector3.Dot(right, targetMotor.WallNormal)) * t.wallRoll;
            }
            roll = Mathf.Lerp(roll, wantedRoll, 1f - Mathf.Exp(-6f * dt));
            impulse = Vector3.Lerp(impulse, Vector3.zero, 1f - Mathf.Exp(-dt / t.impulseRecover));

            PlaceCamera(t, dt, false);
        }

        /// <summary>The field of view for this speed: the vertical values, or on a portrait screen the portrait width.</summary>
        float TargetFov(CameraTuning t, float speed01)
        {
            float vertical = Mathf.Lerp(t.fovMin, t.fovMax, speed01);
            if (cam == null || t.portraitHorizontalFovMin <= 0f || cam.aspect >= 1f)
                return vertical;
            float horizontal = Mathf.Lerp(t.portraitHorizontalFovMin, Mathf.Max(t.portraitHorizontalFovMin, t.portraitHorizontalFovMax), speed01);
            float fromWidth = 2f * Mathf.Atan(Mathf.Tan(horizontal * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.1f, cam.aspect)) * Mathf.Rad2Deg;
            return Mathf.Min(Mathf.Max(vertical, fromWidth), t.portraitVerticalFovLimit);
        }

        /// <summary>When the player isn't turning the camera, swing it round behind the direction of travel and level it out.</summary>
        void AutoFollow(CameraTuning t, float dt)
        {
            if (t.autoFollowStrength <= 0f || targetMotor == null || dt <= 0f)
                return;
            if (Time.unscaledTime - lastLookTime < t.autoFollowDelay)
                return;

            Vector3 velocity = targetMotor.PlanarVelocity;
            float speed = velocity.magnitude;
            if (speed < t.autoFollowMinSpeed)
                return;
            float strength = t.autoFollowStrength * Mathf.InverseLerp(t.autoFollowMinSpeed, Mathf.Max(t.autoFollowMinSpeed + 0.1f, t.autoFollowFullSpeed), speed);
            float blend = 1f - Mathf.Exp(-strength * dt);

            float travelYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(yaw, travelYaw);
            if (Mathf.Abs(delta) <= t.autoFollowMaxAngle)
                yaw = Mathf.Repeat(yaw + delta * blend, 360f);

            float wantedPitch = t.autoFollowPitch;
            float fall = -targetMotor.Velocity.y;
            if (!targetMotor.IsGrounded && fall > 10f)
                wantedPitch += Mathf.Min((fall - 10f) * t.fallLookDown, t.maxFallLookDown);
            pitch = Mathf.Clamp(Mathf.Lerp(pitch, wantedPitch, blend * 0.6f), t.minPitch, t.maxPitch);
        }

        void PlaceCamera(CameraTuning t, float dt, bool instant)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float full = t.distance + extraDistance;
            float wanted = full;
            if (t.collisionRadius > 0f &&
                Physics.SphereCast(pivot, t.collisionRadius, back, out RaycastHit hit, full,
                    t.collisionLayers, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(hit.distance, t.minDistance);
            }

            // Pull in immediately so walls never block the view; ease back out smoothly.
            distance = instant || wanted < distance ? wanted : Mathf.Lerp(distance, wanted, Damp(DistanceRecoverLag, dt));
            transform.SetPositionAndRotation(pivot + back * distance + impulse, roll != 0f ? rotation * Quaternion.Euler(0f, 0f, roll) : rotation);
        }

        static float Damp(float lag, float dt)
        {
            return lag <= 0f ? 1f : 1f - Mathf.Exp(-dt / lag);
        }
    }
}
