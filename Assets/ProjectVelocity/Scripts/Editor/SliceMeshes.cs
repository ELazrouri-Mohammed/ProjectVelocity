using System.Collections.Generic;
using UnityEngine;

namespace ProjectVelocity.EditorTools
{
    /// <summary>
    /// Procedural meshes for the vertical slice: faceted lofts (a polygon swept through scaled rings, flat shaded for a crisp,
    /// stylised read at phone size), hanging cloth strips, flat rings and discs. Small and deterministic: the same call always
    /// makes the same mesh, so the hero, enemies and details can later be swapped for authored models without touching gameplay.
    /// </summary>
    static class SliceMeshes
    {
        /// <summary>One cross-section of a loft: its position along the axis, its size and its offset.</summary>
        public struct Ring
        {
            public float At;
            public Vector2 Scale;
            public Vector2 Offset;

            public Ring(float at, float sizeA, float sizeB, float offsetA = 0f, float offsetB = 0f)
            {
                At = at;
                Scale = new Vector2(sizeA, sizeB);
                Offset = new Vector2(offsetA, offsetB);
            }
        }

        /// <summary>A regular polygon of unit radius (first point at <paramref name="startDegrees"/>).</summary>
        public static Vector2[] Polygon(int sides, float startDegrees = 0f)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (startDegrees + i * 360f / sides) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return points;
        }

        /// <summary>
        /// Sweeps <paramref name="profile"/> (unit-sized, in the plane across the axis) through <paramref name="rings"/> along
        /// local axis <paramref name="axis"/> (0 = X, 1 = Y, 2 = Z), flat shaded, optionally capped at both ends. Ring sizes
        /// scale the profile's two coordinates: for Y they map to (X, Z), for Z to (X, Y), for X to (Y, Z).
        /// </summary>
        public static Mesh Loft(string name, Vector2[] profile, Ring[] rings, int axis, bool capStart = true, bool capEnd = true)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            int n = profile.Length;

            for (int r = 0; r < rings.Length - 1; r++)
            {
                for (int j = 0; j < n; j++)
                {
                    int k = (j + 1) % n;
                    Vector3 a = Point(profile[j], rings[r], axis);
                    Vector3 b = Point(profile[k], rings[r], axis);
                    Vector3 c = Point(profile[k], rings[r + 1], axis);
                    Vector3 d = Point(profile[j], rings[r + 1], axis);
                    Vector3 centre = (Centre(rings[r], axis) + Centre(rings[r + 1], axis)) * 0.5f;
                    Vector3 outward = (a + b + c + d) * 0.25f - centre;
                    Vector3 normal = Vector3.Cross(b - a, d - a);
                    if (normal.sqrMagnitude < 1e-10f)
                        normal = Vector3.Cross(c - b, a - b);
                    if (normal.sqrMagnitude < 1e-10f)
                        normal = outward;
                    normal.Normalize();
                    if (Vector3.Dot(normal, outward) < 0f)
                        normal = -normal;
                    float u0 = (float)j / n;
                    float u1 = (float)(j + 1) / n;
                    AddQuad(vertices, normals, uvs, triangles, a, b, c, d, normal,
                        new Vector2(u0, r), new Vector2(u1, r), new Vector2(u1, r + 1), new Vector2(u0, r + 1));
                }
            }

            Vector3 axisDir = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
            if (capStart)
                AddCap(vertices, normals, uvs, triangles, profile, rings[0], axis, -axisDir);
            if (capEnd)
                AddCap(vertices, normals, uvs, triangles, profile, rings[rings.Length - 1], axis, axisDir);

            return Build(name, vertices, normals, uvs, triangles);
        }

        /// <summary>A tapered prism along -Y from the origin (a limb hanging from its joint).</summary>
        public static Mesh Limb(string name, int sides, float length, float topWidth, float topDepth, float bottomWidth, float bottomDepth,
            float midBulge = 1f)
        {
            Vector2[] profile = Polygon(sides, 90f / sides);
            var rings = new[]
            {
                new Ring(0f, topWidth * 0.5f, topDepth * 0.5f),
                new Ring(-length * 0.35f, Mathf.Lerp(topWidth, bottomWidth, 0.35f) * 0.5f * midBulge, Mathf.Lerp(topDepth, bottomDepth, 0.35f) * 0.5f * midBulge),
                new Ring(-length, bottomWidth * 0.5f, bottomDepth * 0.5f),
            };
            return Loft(name, profile, rings, 1);
        }

        /// <summary>A straight box between two corners, flat shaded (for small details that shouldn't share the grid UVs).</summary>
        public static Mesh Block(string name, Vector3 size)
        {
            Vector2[] square = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
            var rings = new[] { new Ring(-size.y * 0.5f, size.x * 0.5f, size.z * 0.5f), new Ring(size.y * 0.5f, size.x * 0.5f, size.z * 0.5f) };
            return Loft(name, square, rings, 1);
        }

        /// <summary>A cloth strip hanging from the origin along -Y, in the XY plane, subdivided so it can sway.</summary>
        public static Mesh Strip(string name, float width, float height, int columns, int rows, float tipNotch = 0.2f)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float v = (float)r / rows;
                for (int c = 0; c <= columns; c++)
                {
                    float u = (float)c / columns;
                    float y = -height * v;
                    // The bottom edge is cut into a shallow point, like a hanging banner.
                    if (r == rows)
                        y += height * tipNotch * Mathf.Abs(u - 0.5f) * 2f;
                    vertices.Add(new Vector3((u - 0.5f) * width, y, 0f));
                    normals.Add(Vector3.back);
                    uvs.Add(new Vector2(u, 1f - v));
                }
            }
            int stride = columns + 1;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int a = r * stride + c;
                    int b = a + 1;
                    int d = a + stride;
                    int e = d + 1;
                    // Both sides: the material is double-sided anyway, but this keeps it visible either way.
                    triangles.AddRange(new[] { a, b, e, a, e, d });
                }
            }
            return Build(name, vertices, normals, uvs, triangles);
        }

        /// <summary>A flat annulus in the XZ plane facing up: <paramref name="inner"/>-<paramref name="outer"/> as fractions of radius 1.</summary>
        public static Mesh FlatRing(string name, float inner, float outer, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(d * inner);
                vertices.Add(d * outer);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2((float)i / segments, 0f));
                uvs.Add(new Vector2((float)i / segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 3;
                int d = a + 2;
                AddTriangle(triangles, vertices, a, c, b, Vector3.up);
                AddTriangle(triangles, vertices, a, d, c, Vector3.up);
            }
            return Build(name, vertices, normals, uvs, triangles);
        }

        static Vector3 Point(Vector2 p, Ring ring, int axis)
        {
            float a = p.x * ring.Scale.x + ring.Offset.x;
            float b = p.y * ring.Scale.y + ring.Offset.y;
            switch (axis)
            {
                case 0: return new Vector3(ring.At, a, b);
                case 1: return new Vector3(a, ring.At, b);
                default: return new Vector3(a, b, ring.At);
            }
        }

        static Vector3 Centre(Ring ring, int axis)
        {
            return Point(Vector2.zero, ring, axis);
        }

        static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector2[] profile,
            Ring ring, int axis, Vector3 normal)
        {
            if (ring.Scale.x * ring.Scale.y <= 1e-8f)
                return;
            int centre = vertices.Count;
            vertices.Add(Centre(ring, axis));
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j < profile.Length; j++)
            {
                vertices.Add(Point(profile[j], ring, axis));
                normals.Add(normal);
                uvs.Add(profile[j] * 0.5f + new Vector2(0.5f, 0.5f));
            }
            for (int j = 0; j < profile.Length; j++)
            {
                int a = centre + 1 + j;
                int b = centre + 1 + (j + 1) % profile.Length;
                AddTriangle(triangles, vertices, centre, a, b, normal);
            }
        }

        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(ua);
            uvs.Add(ub);
            uvs.Add(uc);
            uvs.Add(ud);
            AddTriangle(triangles, vertices, i, i + 1, i + 2, normal);
            AddTriangle(triangles, vertices, i, i + 2, i + 3, normal);
        }

        // Unity treats clockwise triangles as front-facing; pick the winding that faces along the normal.
        static void AddTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 normal)
        {
            Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            triangles.Add(a);
            if (Vector3.Dot(face, normal) >= 0f)
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

        static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
