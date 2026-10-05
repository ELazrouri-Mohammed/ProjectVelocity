using System;
using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// The ranged pulse: a compact energy bolt that homes in on the enemy (or switch) it was fired at. Not a shooter weapon:
    /// one charge with a cooldown (a kill recharges it), for finishing a far enemy, hitting one in the air, or setting off a
    /// switch while you keep moving. Bolts are a small fixed pool of objects moved by hand (no physics, no allocation).
    /// <see cref="CombatController"/> fires it and applies the hit when it lands.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PulseLauncher : MonoBehaviour
    {
        // A bolt that loses its target flies on for this long (s) before fading.
        const float LostTargetLife = 0.4f;
        // Longest a bolt can fly (s).
        const float MaxLife = 1.5f;

        [Tooltip("Bolt objects (each with its renderer and trail as children). Hidden while unused.")]
        [SerializeField] Transform[] bolts;

        [Tooltip("Layers that stop a bolt. The player itself sits on Ignore Raycast.")]
        [SerializeField] LayerMask blockingLayers = ~(1 << 2);

        CombatEnemy[] targets;
        Vector3[] velocities;
        float[] ages;
        bool[] flying;
        CombatController[] owners;
        TrailRenderer[] trails;

        /// <summary>Raised where a bolt ends: on an enemy (true) or against the world (false).</summary>
        public event Action<Vector3, bool> Impact;

        /// <summary>Hooks up the bolts (used by the scene builder).</summary>
        public void SetBolts(Transform[] boltObjects)
        {
            bolts = boltObjects;
        }

        void Awake()
        {
            int count = bolts != null ? bolts.Length : 0;
            targets = new CombatEnemy[count];
            velocities = new Vector3[count];
            ages = new float[count];
            flying = new bool[count];
            owners = new CombatController[count];
            trails = new TrailRenderer[count];
            for (int i = 0; i < count; i++)
            {
                if (bolts[i] == null)
                    continue;
                trails[i] = bolts[i].GetComponentInChildren<TrailRenderer>(true);
                bolts[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Fires a bolt from <paramref name="origin"/> at <paramref name="target"/>.</summary>
        public void Fire(Vector3 origin, CombatEnemy target, float speed, CombatController owner)
        {
            if (bolts == null || target == null)
                return;
            int slot = -1;
            for (int i = 0; i < bolts.Length; i++)
            {
                if (bolts[i] != null && !flying[i])
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
                return;

            Transform bolt = bolts[slot];
            bolt.position = origin;
            bolt.gameObject.SetActive(true);
            if (trails[slot] != null)
                trails[slot].Clear();
            Vector3 to = target.HurtboxCenter - origin;
            velocities[slot] = (to.sqrMagnitude > 1e-4f ? to.normalized : transform.forward) * speed;
            targets[slot] = target;
            owners[slot] = owner;
            ages[slot] = 0f;
            flying[slot] = true;
        }

        /// <summary>Removes every bolt in flight (on respawn).</summary>
        public void Clear()
        {
            if (bolts == null || flying == null)
                return;
            for (int i = 0; i < bolts.Length; i++)
                End(i);
        }

        void Update()
        {
            if (bolts == null || flying == null)
                return;
            float dt = Time.deltaTime;
            for (int i = 0; i < bolts.Length; i++)
            {
                if (!flying[i])
                    continue;
                ages[i] += dt;
                Transform bolt = bolts[i];
                Vector3 position = bolt.position;
                float speed = velocities[i].magnitude;
                CombatEnemy target = targets[i];

                if (target != null && target.IsAlive)
                {
                    Vector3 to = target.HurtboxCenter - position;
                    float distance = to.magnitude;
                    if (distance <= speed * dt + target.HurtboxRadius)
                    {
                        Vector3 direction = velocities[i] / Mathf.Max(speed, 1e-3f);
                        Impact?.Invoke(target.HurtboxCenter, true);
                        if (owners[i] != null)
                            owners[i].PulseLanded(target, direction);
                        End(i);
                        continue;
                    }
                    velocities[i] = to / distance * speed; // homes in: it always arrives
                }
                else if (target != null)
                {
                    targets[i] = null;
                    ages[i] = Mathf.Max(ages[i], MaxLife - LostTargetLife);
                }

                Vector3 step = velocities[i] * dt;
                if (Physics.Raycast(position, step, out RaycastHit hit, step.magnitude, blockingLayers, QueryTriggerInteraction.Ignore))
                {
                    Impact?.Invoke(hit.point, false);
                    End(i);
                    continue;
                }
                bolt.position = position + step;
                if (ages[i] >= MaxLife)
                    End(i);
            }
        }

        void End(int i)
        {
            flying[i] = false;
            targets[i] = null;
            owners[i] = null;
            if (bolts[i] != null)
                bolts[i].gameObject.SetActive(false);
        }
    }
}
