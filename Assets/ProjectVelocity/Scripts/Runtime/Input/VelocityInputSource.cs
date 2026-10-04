using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Base class for anything that can drive the player: desktop keyboard/mouse now,
    /// mobile touch controls later. Swap the component and nothing else changes.
    /// </summary>
    public abstract class VelocityInputSource : MonoBehaviour
    {
        /// <summary>Called once per frame by the player controller.</summary>
        public abstract PlayerIntent ReadIntent();
    }
}
