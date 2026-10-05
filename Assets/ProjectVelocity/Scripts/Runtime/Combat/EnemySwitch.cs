using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// An energy node in the architecture: cut it or hit it with the pulse and it sets off a reality stage (a bridge drops into
    /// place, a gate opens, the world rearranges). It's a <see cref="CombatEnemy"/> that never attacks, so the soft targeting,
    /// the lunge and the pulse all work on it; it comes back with its segment on a checkpoint restart.
    /// </summary>
    [RequireComponent(typeof(CombatEnemy))]
    [DisallowMultipleComponent]
    public sealed class EnemySwitch : MonoBehaviour
    {
        [Tooltip("The stage it sets off.")]
        [SerializeField] RealityTransformSequence sequence;

        [Tooltip("Spun slowly while armed (visual only).")]
        [SerializeField] Transform spinner;

        [SerializeField] float spinSpeed = 90f;

        CombatEnemy body;

        public RealityTransformSequence Sequence
        {
            get => sequence;
            set => sequence = value;
        }

        public Transform Spinner
        {
            get => spinner;
            set => spinner = value;
        }

        void Awake()
        {
            body = GetComponent<CombatEnemy>();
        }

        void OnEnable()
        {
            body.Died += OnDied;
        }

        void OnDisable()
        {
            body.Died -= OnDied;
        }

        void Update()
        {
            if (spinner != null && body.IsAlive)
                spinner.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        void OnDied(CombatEnemy enemy, Vector3 attackerVelocity)
        {
            if (sequence != null)
                sequence.Play();
        }
    }
}
