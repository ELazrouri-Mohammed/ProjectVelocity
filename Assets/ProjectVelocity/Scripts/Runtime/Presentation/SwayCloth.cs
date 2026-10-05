using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A hanging cloth banner that sways in the wind: the strip mesh's vertices are bent each frame, more toward the free end,
    /// only while it's on screen. A handful of these on monumental walls make a still world feel alive for almost nothing.
    /// The mesh must hang from this object's origin along -Y (as the scene builder makes it). Visual only, no collider.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SwayCloth : MonoBehaviour
    {
        [Tooltip("Sideways sway (m) at the free end.")]
        [SerializeField] float amplitude = 1.2f;

        [Tooltip("Billow (m) toward and away from the wall at the free end.")]
        [SerializeField] float billow = 0.8f;

        [SerializeField] float rate = 0.5f;

        [Tooltip("Ripples running down the cloth.")]
        [SerializeField] float ripple = 2.2f;

        MeshFilter filter;
        MeshRenderer meshRenderer;
        Mesh mesh;
        Vector3[] rest;
        Vector3[] bent;
        float height = 1f;
        float phase;

        void Awake()
        {
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null)
                return;
            mesh = Instantiate(filter.sharedMesh);
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            bent = new Vector3[rest.Length];
            float lowest = 0f;
            foreach (Vector3 v in rest)
                lowest = Mathf.Min(lowest, v.y);
            height = Mathf.Max(0.01f, -lowest);
            Vector3 p = transform.position;
            phase = p.x * 0.07f + p.z * 0.05f;
            Bounds b = mesh.bounds;
            b.Expand(new Vector3(amplitude * 2f, 0f, billow * 2f));
            mesh.bounds = b;
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        void Update()
        {
            if (mesh == null || !meshRenderer.isVisible)
                return;
            float t = Time.time * rate * Mathf.PI * 2f + phase;
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 v = rest[i];
                float down = Mathf.Clamp01(-v.y / height);
                float weight = down * down;
                float wave = t - down * ripple;
                v.x += Mathf.Sin(wave) * amplitude * weight;
                v.z += (Mathf.Sin(wave * 0.7f + 1.3f) * 0.5f + 0.5f) * billow * weight;
                bent[i] = v;
            }
            mesh.vertices = bent;
        }
    }
}
