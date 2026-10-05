using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// Turns an object steadily (the distant megastructure's rings, the anomaly). Visual only: use it on things without
    /// colliders; architecture you can touch uses <see cref="LoopingMotion"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpinVisual : MonoBehaviour
    {
        [SerializeField] Vector3 axis = Vector3.up;
        [SerializeField] float degreesPerSecond = 10f;

        public void Configure(Vector3 spinAxis, float speed)
        {
            axis = spinAxis;
            degreesPerSecond = speed;
        }

        public float DegreesPerSecond
        {
            get => degreesPerSecond;
            set => degreesPerSecond = value;
        }

        void Update()
        {
            transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
