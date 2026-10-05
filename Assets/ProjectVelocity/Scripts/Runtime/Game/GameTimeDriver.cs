using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>Runs <see cref="GameTime"/> each frame, first. One per scene that wants hit-stop and slow motion.</summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameTimeDriver : MonoBehaviour
    {
        void OnEnable()
        {
            GameTime.Active = true;
        }

        void OnDisable()
        {
            GameTime.Active = false;
            GameTime.Reset();
        }

        void Update()
        {
            GameTime.Tick(Time.unscaledDeltaTime);
        }
    }
}
