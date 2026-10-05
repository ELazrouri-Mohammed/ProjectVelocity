using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The tether's look: a physical-looking cable drawn with a line renderer. Purely visual: the swing itself is the motor's
    /// controlled arc. The cable is a short verlet chain pinned to the hand and the anchor: it shoots out when you connect, sags
    /// and ripples when slack, snaps straight when taut, and whips back to the anchor and fades when you let go.
    /// Allocation-free.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class TetherRopeVisual : MonoBehaviour
    {
        enum Phase
        {
            Hidden,
            Firing,
            Attached,
            Retracting,
        }

        [SerializeField] VelocityMotor motor;
        [SerializeField] TetherController tether;

        [Tooltip("Points along the cable.")]
        [SerializeField, Range(4, 32)] int points = 18;

        [Tooltip("Gravity on the cable's slack (m/s²).")]
        [SerializeField] float gravity = 30f;

        [Tooltip("Velocity kept per frame (0-1): lower = the cable settles faster.")]
        [SerializeField, Range(0f, 1f)] float damping = 0.94f;

        [Tooltip("Constraint passes per frame: more = stiffer.")]
        [SerializeField, Range(1, 12)] int iterations = 6;

        [Tooltip("Seconds the cable takes to whip back and fade after letting go.")]
        [SerializeField, Min(0.01f)] float retractTime = 0.3f;

        [Tooltip("Sideways kick (m) given to the middle of the cable when it connects, so it ripples into place.")]
        [SerializeField] float connectRipple = 1.2f;

        [SerializeField] float width = 0.07f;

        LineRenderer line;
        Vector3[] current;
        Vector3[] previous;
        Phase phase = Phase.Hidden;
        float phaseTime;
        TetherAnchor anchor;
        Vector3 anchorPoint;
        Vector3 releaseHand;
        float slackLength;

        public VelocityMotor Motor
        {
            get => motor;
            set => motor = value;
        }

        public TetherController Tether
        {
            get => tether;
            set => tether = value;
        }

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            current = new Vector3[points];
            previous = new Vector3[points];
            line.positionCount = points;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        void OnEnable()
        {
            if (motor != null)
            {
                motor.TetherAttached += OnAttached;
                motor.TetherReleased += OnReleased;
            }
        }

        void OnDisable()
        {
            if (motor != null)
            {
                motor.TetherAttached -= OnAttached;
                motor.TetherReleased -= OnReleased;
            }
            phase = Phase.Hidden;
            if (line != null)
                line.enabled = false;
        }

        void OnAttached(TetherAnchor target)
        {
            anchor = target;
            anchorPoint = target.Position;
            phase = Phase.Firing;
            phaseTime = 0f;
            Vector3 hand = HandPosition();
            slackLength = Vector3.Distance(hand, anchorPoint) * 1.08f;

            // Start bunched at the hand with a sideways ripple, so the cable visibly shoots out and settles.
            Vector3 side = Vector3.Cross((anchorPoint - hand).normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-4f)
                side = Vector3.right;
            side.Normalize();
            for (int i = 0; i < points; i++)
            {
                float u = i / (float)(points - 1);
                Vector3 p = Vector3.Lerp(hand, anchorPoint, u) + side * (Mathf.Sin(u * Mathf.PI) * connectRipple);
                current[i] = p;
                previous[i] = p;
            }
            line.enabled = true;
        }

        void OnReleased(TetherAnchor target, VelocityMotor.TetherExit exit)
        {
            if (phase == Phase.Hidden)
                return;
            phase = Phase.Retracting;
            phaseTime = 0f;
            releaseHand = HandPosition();
        }

        void LateUpdate()
        {
            if (phase == Phase.Hidden)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;
            phaseTime += dt;
            if (anchor != null)
                anchorPoint = anchor.Position;

            Vector3 hand = HandPosition();
            float fireTime = tether != null ? tether.Settings.fireTime : 0.07f;
            float alpha = 1f;

            switch (phase)
            {
                case Phase.Firing:
                {
                    // The tip flies out from the hand; the rest of the cable trails it.
                    float u = fireTime > 0f ? Mathf.Clamp01(phaseTime / fireTime) : 1f;
                    Vector3 tip = Vector3.Lerp(hand, anchorPoint, 1f - (1f - u) * (1f - u));
                    Simulate(hand, tip, Vector3.Distance(hand, tip) * 1.05f, dt);
                    if (u >= 1f)
                        phase = Phase.Attached;
                    break;
                }
                case Phase.Attached:
                {
                    // Taut when the motor's rope is at full stretch, a little slack otherwise.
                    float distance = Vector3.Distance(hand, anchorPoint);
                    float rope = motor != null && motor.IsTethering ? motor.RopeLength : distance;
                    slackLength = Mathf.Lerp(slackLength, Mathf.Max(distance, rope * 1.02f), 1f - Mathf.Exp(-20f * dt));
                    Simulate(hand, anchorPoint, slackLength, dt);
                    break;
                }
                case Phase.Retracting:
                {
                    // Let go at the hand: the free end whips back toward the anchor as the cable reels in.
                    float u = Mathf.Clamp01(phaseTime / retractTime);
                    Vector3 free = Vector3.Lerp(releaseHand, anchorPoint, u * u);
                    Simulate(free, anchorPoint, Mathf.Max(0.2f, Vector3.Distance(releaseHand, anchorPoint) * (1f - u)), dt, pinStart: u < 0.05f);
                    alpha = 1f - u;
                    if (u >= 1f)
                    {
                        phase = Phase.Hidden;
                        line.enabled = false;
                        anchor = null;
                        return;
                    }
                    break;
                }
            }

            line.widthMultiplier = width * Mathf.Lerp(0.3f, 1f, alpha);
            line.SetPositions(current);
        }

        void Simulate(Vector3 start, Vector3 end, float length, float dt, bool pinStart = true)
        {
            int n = points;
            float segment = length / (n - 1);
            Vector3 g = Vector3.down * (gravity * dt * dt);
            for (int i = 1; i < n - 1; i++)
            {
                Vector3 velocity = (current[i] - previous[i]) * damping;
                previous[i] = current[i];
                current[i] += velocity + g;
            }
            if (!pinStart)
            {
                Vector3 velocity = (current[0] - previous[0]) * damping;
                previous[0] = current[0];
                current[0] += velocity + g;
            }

            for (int k = 0; k < iterations; k++)
            {
                if (pinStart)
                    current[0] = start;
                current[n - 1] = end;
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 delta = current[i + 1] - current[i];
                    float d = delta.magnitude;
                    if (d < 1e-5f)
                        continue;
                    float error = (d - segment) / d;
                    bool firstPinned = i == 0 && pinStart;
                    bool lastPinned = i + 1 == n - 1;
                    if (firstPinned && lastPinned)
                        continue;
                    if (firstPinned)
                        current[i + 1] -= delta * error;
                    else if (lastPinned)
                        current[i] += delta * error;
                    else
                    {
                        current[i] += delta * (error * 0.5f);
                        current[i + 1] -= delta * (error * 0.5f);
                    }
                }
            }
            if (pinStart)
                current[0] = start;
            current[n - 1] = end;
        }

        Vector3 HandPosition()
        {
            if (tether != null)
                return tether.HandPosition;
            return motor != null ? motor.BodyCenter : transform.position;
        }
    }
}
