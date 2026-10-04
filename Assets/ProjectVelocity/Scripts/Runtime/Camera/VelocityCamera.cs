using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Third-person orbit camera built for speed: raw, immediate rotation, near-zero follow lag,
    /// collision pull-in and a field of view that widens with speed.
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

        /// <summary>Applies look input. Pointer deltas are scaled by sensitivity, stick rates by turn speed and frame time.</summary>
        public void AddLookInput(Vector2 pointerDelta, Vector2 stickRate, float deltaTime)
        {
            CameraTuning t = Settings;
            Vector2 degrees = pointerDelta * t.lookSensitivity + stickRate * (t.stickLookSpeed * deltaTime);
            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = Mathf.Clamp(pitch + (t.invertY ? degrees.y : -degrees.y), t.minPitch, t.maxPitch);
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
            if (cam != null)
                cam.fieldOfView = t.fovMin;
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
                float speed = targetMotor != null ? targetMotor.Speed : 0f;
                float speed01 = Mathf.InverseLerp(t.fovMinSpeed, t.fovMaxSpeed, speed);
                float targetFov = Mathf.Lerp(t.fovMin, t.fovMax, speed01);
                cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, t.fovSmoothTime);
            }

            PlaceCamera(t, dt, false);
        }

        void PlaceCamera(CameraTuning t, float dt, bool instant)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = t.distance;
            if (t.collisionRadius > 0f &&
                Physics.SphereCast(pivot, t.collisionRadius, back, out RaycastHit hit, t.distance,
                    t.collisionLayers, QueryTriggerInteraction.Ignore))
            {
                wanted = Mathf.Max(hit.distance, t.minDistance);
            }

            // Pull in immediately so walls never block the view; ease back out smoothly.
            distance = instant || wanted < distance ? wanted : Mathf.Lerp(distance, wanted, Damp(DistanceRecoverLag, dt));
            transform.SetPositionAndRotation(pivot + back * distance, rotation);
        }

        static float Damp(float lag, float dt)
        {
            return lag <= 0f ? 1f : 1f - Mathf.Exp(-dt / lag);
        }
    }
}
