using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Base class for anything that can drive the player: desktop keyboard/mouse or mobile touch controls.
    /// Swap the component and nothing else changes.
    /// </summary>
    public abstract class VelocityInputSource : MonoBehaviour
    {
        /// <summary>Called once per frame by the player controller.</summary>
        public abstract PlayerIntent ReadIntent();

        /// <summary>One-line control reminder for the debug HUD, or null when the controls explain themselves.</summary>
        public virtual string ControlsHint => null;
    }
}
