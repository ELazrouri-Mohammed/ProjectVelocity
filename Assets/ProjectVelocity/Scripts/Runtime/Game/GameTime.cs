using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The one place that touches <see cref="Time.timeScale"/>: short hit-stops (a kill freezes the world for a few hundredths
    /// of a second) and a slow-motion layer (death, the finish). Requests overlap safely: the slowest active one wins.
    /// Timers run on unscaled time. Without a <see cref="GameTimeDriver"/> in the scene, requests do nothing, so scenes that
    /// don't want it are untouched.
    /// </summary>
    public static class GameTime
    {
        static float hitStopTimer;
        static float hitStopScale = 1f;
        static float slowScale = 1f;
        static float slowTimer;

        /// <summary>Whether a driver is running (requests take effect).</summary>
        public static bool Active { get; internal set; }

        /// <summary>Freezes the world to <paramref name="scale"/> for <paramref name="seconds"/> of real time. Use sparingly.</summary>
        public static void HitStop(float seconds, float scale = 0.03f)
        {
            if (!Active || seconds <= 0f)
                return;
            if (hitStopTimer > 0f)
                hitStopScale = Mathf.Min(hitStopScale, scale);
            else
                hitStopScale = scale;
            hitStopTimer = Mathf.Max(hitStopTimer, seconds);
        }

        /// <summary>Runs the world at <paramref name="scale"/> for <paramref name="seconds"/> of real time (negative = until cleared).</summary>
        public static void SlowMotion(float scale, float seconds)
        {
            if (!Active)
                return;
            slowScale = Mathf.Clamp(scale, 0f, 1f);
            slowTimer = seconds;
        }

        public static void ClearSlowMotion()
        {
            slowScale = 1f;
            slowTimer = 0f;
        }

        public static void Reset()
        {
            hitStopTimer = 0f;
            hitStopScale = 1f;
            ClearSlowMotion();
            Time.timeScale = 1f;
        }

        internal static void Tick(float unscaledDelta)
        {
            if (hitStopTimer > 0f)
                hitStopTimer = Mathf.Max(0f, hitStopTimer - unscaledDelta);
            if (slowTimer > 0f)
            {
                slowTimer -= unscaledDelta;
                if (slowTimer <= 0f)
                    ClearSlowMotion();
            }
            float scale = slowScale;
            if (hitStopTimer > 0f)
                scale = Mathf.Min(scale, hitStopScale);
            Time.timeScale = scale;
        }

        // Survives play mode without a domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            hitStopTimer = 0f;
            hitStopScale = 1f;
            slowScale = 1f;
            slowTimer = 0f;
            Active = false;
        }
    }
}
