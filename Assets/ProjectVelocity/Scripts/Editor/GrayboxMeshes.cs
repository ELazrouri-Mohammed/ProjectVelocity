using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Builds box and ramp meshes whose UVs are measured in metres, so the grid texture keeps
    /// the same scale on every surface. A consistent grid is what makes speed readable.
    /// </summary>
    static class GrayboxMeshes
    {
        /// <summary>World size (m) covered by one repeat of the grid texture.</summary>
        public const float TileSize = 4f;

        /// <summary>Box centred on its pivot. <paramref name="uvOrigin"/> is its world position, which lines the grid up across objects.</summary>
        public static Mesh Box(Vector3 size, Vector3 uvOrigin)
        {
            Vector3 h = size * 0.5f;
            var builder = new Builder(uvOrigin);

            // +Y / -Y: grid on x,z
            builder.Quad(new Vector3(-h.x, h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.up, UvPlane.XZ);
            builder.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), Vector3.down, UvPlane.XZ);
            // +X / -X: grid on z,y
            builder.Quad(new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Vector3.right, UvPlane.ZY);
            builder.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y, -h.z), Vector3.left, UvPlane.ZY);
            // +Z / -Z: grid on x,y
            builder.Quad(new Vector3(-h.x, -h.y, h.z), new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z), Vector3.forward, UvPlane.XY);
            builder.Quad(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z), new Vector3(-h.x, h.y, -h.z), Vector3.back, UvPlane.XY);

            return builder.ToMesh($"Box {size.x:0.#}x{size.y:0.#}x{size.z:0.#}");
        }

        /// <summary>
        /// Ramp (wedge). Pivot is the centre of its low edge; it rises along local +Z
        /// from height 0 to <paramref name="rise"/> over <paramref name="length"/>.
        /// </summary>
        public static Mesh Ramp(float width, float length, float rise)
        {
            float w = width * 0.5f;
            var builder = new Builder(Vector3.zero);

            Vector3 lowLeft = new Vector3(-w, 0f, 0f);
            Vector3 lowRight = new Vector3(w, 0f, 0f);
            Vector3 backLeft = new Vector3(-w, 0f, length);
            Vector3 backRight = new Vector3(w, 0f, length);
            Vector3 topLeft = new Vector3(-w, rise, length);
            Vector3 topRight = new Vector3(w, rise, length);

            Vector3 slopeNormal = new Vector3(0f, length, -rise).normalized;
            builder.Quad(lowLeft, lowRight, topRight, topLeft, slopeNormal, UvPlane.Slope);
            builder.Quad(lowLeft, lowRight, backRight, backLeft, Vector3.down, UvPlane.XZ);
            builder.Quad(backLeft, backRight, topRight, topLeft, Vector3.forward, UvPlane.XY);
            builder.Triangle(lowLeft, backLeft, topLeft, Vector3.left, UvPlane.ZY);
            builder.Triangle(lowRight, backRight, topRight, Vector3.right, UvPlane.ZY);

            return builder.ToMesh($"Ramp {width:0.#}x{length:0.#} rise {rise:0.#}");
        }

        /// <summary>
        /// Ring (torus) centred on its pivot, lying in the local XY plane so it faces along ±Z.
        /// <paramref name="radius"/> is to the middle of the tube.
        /// </summary>
        public static Mesh Torus(float radius, float tubeRadius, int segments, int sides)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float around = i * 2f * Mathf.PI / segments;
                var ringDir = new Vector3(Mathf.Cos(around), Mathf.Sin(around), 0f);
                for (int j = 0; j <= sides; j++)
                {
                    float tube = j * 2f * Mathf.PI / sides;
                    Vector3 normal = ringDir * Mathf.Cos(tube) + Vector3.forward * Mathf.Sin(tube);
                    vertices.Add(ringDir * radius + normal * tubeRadius);
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)i / segments, (float)j / sides));
                }
            }

            int stride = sides + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = i * stride + j;
                    int b = a + stride;
                    int c = b + 1;
                    int d = a + 1;

                    // Unity treats clockwise triangles as front-facing; pick the winding that faces outward.
                    Vector3 outward = normals[a] + normals[b] + normals[c] + normals[d];
                    Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    if (Vector3.Dot(face, outward) >= 0f)
                        triangles.AddRange(new[] { a, b, c, a, c, d });
                    else
                        triangles.AddRange(new[] { a, c, b, a, d, c });
                }
            }

            var mesh = new Mesh { name = $"Torus {radius:0.#} / {tubeRadius:0.##}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Octahedron (a double pyramid) centred on its pivot: <paramref name="radius"/> to its four side points,
        /// <paramref name="halfHeight"/> to its tips. Flat-shaded.
        /// </summary>
        public static Mesh Octahedron(float radius, float halfHeight)
        {
            var builder = new Builder(Vector3.zero);
            Vector3 top = Vector3.up * halfHeight;
            Vector3 bottom = Vector3.down * halfHeight;
            Vector3[] sides =
            {
                new Vector3(radius, 0f, 0f), new Vector3(0f, 0f, radius), new Vector3(-radius, 0f, 0f), new Vector3(0f, 0f, -radius),
            };

            for (int i = 0; i < sides.Length; i++)
            {
                Vector3 a = sides[i];
                Vector3 b = sides[(i + 1) % sides.Length];
                builder.Triangle(top, a, b, OutwardNormal(top, a, b), UvPlane.XZ);
                builder.Triangle(bottom, a, b, OutwardNormal(bottom, a, b), UvPlane.XZ);
            }

            return builder.ToMesh($"Octahedron {radius:0.##} / {halfHeight:0.##}");
        }

        /// <summary>
        /// Flat crescent in the local XZ plane, centred on +Z around the pivot: a placeholder slash arc. Widest in the middle,
        /// tapering to points at both ends, and double-sided so it shows from above and below.
        /// </summary>
        public static Mesh Crescent(float innerRadius, float outerRadius, float arcDegrees, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = (u - 0.5f) * arcDegrees * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                float inner = Mathf.Lerp(outerRadius, innerRadius, Mathf.Sin(u * Mathf.PI));
                vertices.Add(direction * inner);
                vertices.Add(direction * outerRadius);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 3;
                int d = a + 2;
                // Both windings: visible from either side whatever the material's culling.
                triangles.AddRange(new[] { a, b, c, a, c, d, a, c, b, a, d, c });
            }

            var mesh = new Mesh { name = $"Crescent {innerRadius:0.#}-{outerRadius:0.#} m {arcDegrees:0}°" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Normal of a triangle on a convex shape around the origin, pointing away from the origin.</summary>
        static Vector3 OutwardNormal(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            return Vector3.Dot(normal, a + b + c) < 0f ? -normal : normal;
        }

        enum UvPlane
        {
            XZ,
            ZY,
            XY,
            Slope,
        }

        sealed class Builder
        {
            readonly Vector3 uvOrigin;
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> triangles = new List<int>();

            public Builder(Vector3 uvOrigin)
            {
                this.uvOrigin = uvOrigin;
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, UvPlane plane)
            {
                int i = Add(a, normal, plane);
                Add(b, normal, plane);
                Add(c, normal, plane);
                Add(d, normal, plane);
                AddTriangle(i, i + 1, i + 2, normal);
                AddTriangle(i, i + 2, i + 3, normal);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, UvPlane plane)
            {
                int i = Add(a, normal, plane);
                Add(b, normal, plane);
                Add(c, normal, plane);
                AddTriangle(i, i + 1, i + 2, normal);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }

            int Add(Vector3 position, Vector3 normal, UvPlane plane)
            {
                Vector3 p = position + uvOrigin;
                Vector2 uv;
                switch (plane)
                {
                    case UvPlane.XZ: uv = new Vector2(p.x, p.z); break;
                    case UvPlane.ZY: uv = new Vector2(p.z, p.y); break;
                    case UvPlane.XY: uv = new Vector2(p.x, p.y); break;
                    default: uv = new Vector2(p.x, Mathf.Sqrt(p.z * p.z + p.y * p.y)); break;
                }

                vertices.Add(position);
                normals.Add(normal);
                uvs.Add(uv / TileSize);
                return vertices.Count - 1;
            }

            // Unity treats clockwise triangles as front-facing; pick the winding that faces along the normal.
            void AddTriangle(int a, int b, int c, Vector3 normal)
            {
                Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                triangles.Add(a);
                if (Vector3.Dot(faceNormal, normal) >= 0f)
                {
                    triangles.Add(b);
                    triangles.Add(c);
                }
                else
                {
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }
        }
    }
}
