using UnityEngine;

namespace ProjectVelocity
{
    /// <summary>
    /// A long cloth ribbon streaming from the character (the scarf): a short verlet chain hung from an anchor point, drawn as a
    /// tapered strip that faces the camera so it always reads. It trails behind at speed, flutters, and swings with every
    /// direction change: secondary motion that sells the movement. Purely visual; the mesh lives in world space on this object,
    /// which should not be parented to anything that moves. Allocation-free after start.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RibbonTrail : MonoBehaviour
    {
        [Tooltip("Where the ribbon is tied on (the back of the neck).")]
        [SerializeField] Transform anchor;

        [SerializeField, Range(3, 24)] int segments = 12;
        [SerializeField, Min(0.1f)] float length = 1.9f;
        [SerializeField, Min(0.01f)] float rootWidth = 0.17f;
        [SerializeField, Min(0f)] float tipWidth = 0.04f;

        [SerializeField] float gravity = 7f;
        [SerializeField, Range(0f, 1f)] float damping = 0.9f;

        [Tooltip("Flutter strength (m/s²) of the wind noise.")]
        [SerializeField] float flutter = 18f;

        [SerializeField] float flutterRate = 9f;

        Vector3[] points;
        Vector3[] previous;
        Vector3[] vertices;
        Vector2[] uvs;
        int[] triangles;
        Mesh mesh;
        Transform view;
        float seed;
        bool initialised;

        public Transform Anchor
        {
            get => anchor;
            set
            {
                anchor = value;
                initialised = false;
            }
        }

        public void SetShape(int segmentCount, float ribbonLength, float widthAtRoot, float widthAtTip)
        {
            segments = Mathf.Clamp(segmentCount, 3, 24);
            length = ribbonLength;
            rootWidth = widthAtRoot;
            tipWidth = widthAtTip;
        }

        void Awake()
        {
            int n = segments + 1;
            points = new Vector3[n];
            previous = new Vector3[n];
            vertices = new Vector3[n * 2];
            uvs = new Vector2[n * 2];
            triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int k = i * 6;
                triangles[k] = v;
                triangles[k + 1] = v + 2;
                triangles[k + 2] = v + 1;
                triangles[k + 3] = v + 1;
                triangles[k + 4] = v + 2;
                triangles[k + 5] = v + 3;
            }
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)segments;
                uvs[i * 2] = new Vector2(0f, u);
                uvs[i * 2 + 1] = new Vector2(1f, u);
            }
            mesh = new Mesh { name = "Ribbon" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            GetComponent<MeshFilter>().sharedMesh = mesh;
            seed = Random.value * 100f;
        }

        void OnEnable()
        {
            initialised = false;
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        void LateUpdate()
        {
            if (anchor == null)
                return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f)
                return;
            int n = segments + 1;
            float segmentLength = length / segments;
            Vector3 root = anchor.position;

            if (!initialised)
            {
                Vector3 hang = -anchor.forward * 0.5f + Vector3.down;
                hang.Normalize();
                for (int i = 0; i < n; i++)
                {
                    points[i] = root + hang * (segmentLength * i);
                    previous[i] = points[i];
                }
                initialised = true;
            }

            // Integrate: inertia, gravity, a fluttering wind that grows toward the tip.
            float time = Time.time * flutterRate + seed;
            for (int i = 1; i < n; i++)
            {
                float tipward = i / (float)segments;
                Vector3 wind = new Vector3(Mathf.Sin(time + i * 0.9f), Mathf.Sin(time * 1.3f + i * 0.7f) * 0.6f, Mathf.Cos(time * 0.8f + i * 1.1f))
                               * (flutter * tipward);
                Vector3 velocity = (points[i] - previous[i]) * damping;
                previous[i] = points[i];
                points[i] += velocity + (Vector3.down * gravity + wind) * (dt * dt);
            }

            // Keep the links at length, root pinned to the anchor.
            for (int k = 0; k < 4; k++)
            {
                points[0] = root;
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 delta = points[i + 1] - points[i];
                    float d = delta.magnitude;
                    if (d < 1e-5f)
                        continue;
                    float error = (d - segmentLength) / d;
                    if (i == 0)
                        points[1] -= delta * error;
                    else
                    {
                        points[i] += delta * (error * 0.5f);
                        points[i + 1] -= delta * (error * 0.5f);
                    }
                }
            }
            points[0] = root;

            // A camera-facing strip: always reads, never edge-on.
            if (view == null && Camera.main != null)
                view = Camera.main.transform;
            Vector3 toView = view != null ? view.forward : Vector3.forward;
            Vector3 min = root;
            Vector3 max = root;
            for (int i = 0; i < n; i++)
            {
                Vector3 along = i < n - 1 ? points[i + 1] - points[i] : points[i] - points[i - 1];
                Vector3 side = Vector3.Cross(along, toView);
                side = side.sqrMagnitude > 1e-8f ? side.normalized : anchor.right;
                float width = Mathf.Lerp(rootWidth, tipWidth, i / (float)segments) * 0.5f;
                Vector3 a = points[i] - side * width;
                Vector3 b = points[i] + side * width;
                vertices[i * 2] = a;
                vertices[i * 2 + 1] = b;
                min = Vector3.Min(min, Vector3.Min(a, b));
                max = Vector3.Max(max, Vector3.Max(a, b));
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 0.2f);
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
