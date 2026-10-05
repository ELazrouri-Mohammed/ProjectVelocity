using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A small pool of real physics debris: chunks that break off transforming architecture and dead heavies, tumble, bounce
    /// off the world and fade. Spectacle only: debris lives on the Ignore Raycast layer, which the movement, camera and targeting
    /// probes all skip, and collisions between that layer and itself (the player) are turned off, so a falling chunk can never
    /// block, carry or trip the player. Critical geometry stays deterministic. Oldest chunk is reused when the pool is full.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebrisPool : MonoBehaviour
    {
        // Built-in "Ignore Raycast" layer, shared with the player (see the class summary).
        const int DebrisLayer = 2;

        [Tooltip("Debris chunks (each a renderer with a box collider and a rigidbody). Hidden while unused.")]
        [SerializeField] Rigidbody[] chunks;

        [Tooltip("Seconds a chunk lives, shrinking away over the last part.")]
        [SerializeField, Min(0.2f)] float lifetime = 3.2f;

        [SerializeField, Min(0f)] float shrinkTime = 0.6f;

        float[] ages;
        bool[] live;
        Vector3[] baseScales;
        int next;

        public void SetChunks(Rigidbody[] bodies)
        {
            chunks = bodies;
        }

        void Awake()
        {
            Physics.IgnoreLayerCollision(DebrisLayer, DebrisLayer, true);
            int count = chunks != null ? chunks.Length : 0;
            ages = new float[count];
            live = new bool[count];
            baseScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                if (chunks[i] == null)
                    continue;
                chunks[i].gameObject.layer = DebrisLayer;
                baseScales[i] = chunks[i].transform.localScale;
                chunks[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Throws a chunk of <paramref name="size"/> (m) from <paramref name="position"/>.</summary>
        public void Spawn(Vector3 position, Vector3 velocity, float size)
        {
            if (chunks == null || chunks.Length == 0)
                return;
            int slot = -1;
            for (int k = 0; k < chunks.Length; k++)
            {
                int i = (next + k) % chunks.Length;
                if (chunks[i] != null && !live[i])
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
                slot = next; // all busy: reuse the oldest
            next = (slot + 1) % chunks.Length;

            Rigidbody body = chunks[slot];
            if (body == null)
                return;
            Transform t = body.transform;
            t.SetPositionAndRotation(position, Random.rotation);
            baseScales[slot] = Vector3.one * size;
            t.localScale = baseScales[slot];
            body.gameObject.SetActive(true);
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = velocity;
#else
            body.velocity = velocity;
#endif
            body.angularVelocity = Random.insideUnitSphere * 8f;
            ages[slot] = 0f;
            live[slot] = true;
        }

        public void ClearAll()
        {
            if (chunks == null || live == null)
                return;
            for (int i = 0; i < chunks.Length; i++)
            {
                live[i] = false;
                if (chunks[i] != null)
                    chunks[i].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (chunks == null || live == null)
                return;
            float dt = Time.deltaTime;
            for (int i = 0; i < chunks.Length; i++)
            {
                if (!live[i])
                    continue;
                ages[i] += dt;
                float left = lifetime - ages[i];
                if (left <= 0f || chunks[i].position.y < -400f)
                {
                    live[i] = false;
                    chunks[i].gameObject.SetActive(false);
                    continue;
                }
                if (left < shrinkTime)
                    chunks[i].transform.localScale = baseScales[i] * (left / shrinkTime);
            }
        }
    }
}
