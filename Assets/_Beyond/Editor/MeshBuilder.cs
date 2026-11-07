using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beyond.EditorTools
{
    public class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;

        public MeshBuilder(int subMeshCount)
        {
            triangles = new List<int>[subMeshCount];
            for (int i = 0; i < subMeshCount; i++) triangles[i] = new List<int>();
        }

        public int Vertex(Vector3 position, Vector2 uv)
        {
            vertices.Add(position);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        public void Triangle(int subMesh, int a, int b, int c, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            var list = triangles[subMesh];
            list.Add(a);
            if (Vector3.Dot(n, outward) >= 0f) { list.Add(b); list.Add(c); }
            else { list.Add(c); list.Add(b); }
        }

        public void Quad(int subMesh, int a, int b, int c, int d, Vector3 outward)
        {
            Triangle(subMesh, a, b, c, outward);
            Triangle(subMesh, a, c, d, outward);
        }

        public void Cylinder(int subMesh, Vector3 basePos, float height, float r0, float r1, int sides, float uvScale = 1f)
        {
            int start = vertices.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? r0 : r1;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vertex(basePos + new Vector3(Mathf.Cos(a) * r, ring * height, Mathf.Sin(a) * r),
                        new Vector2(i / (float)sides * uvScale, ring * height * uvScale * 0.5f));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < sides; i++)
            {
                int a = start + i, b = a + 1, c = a + stride + 1, d = a + stride;
                float ang = (i + 0.5f) / sides * Mathf.PI * 2f;
                Quad(subMesh, a, b, c, d, new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)));
            }
        }

        public void Cone(int subMesh, Vector3 basePos, float radius, float height, int sides, float droop, float jitter, System.Random rng)
        {
            int apex = Vertex(basePos + Vector3.up * height, new Vector2(0.5f, 1f));
            int centre = Vertex(basePos + Vector3.up * (droop * 0.5f), new Vector2(0.5f, 0f));
            int ringStart = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                float r = radius * (1f - jitter + (float)rng.NextDouble() * jitter * 2f);
                float y = -droop * (0.6f + (float)rng.NextDouble() * 0.8f);
                Vertex(basePos + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), new Vector2(i / (float)sides, 0f));
            }
            int underStart = vertices.Count;
            for (int i = 0; i < sides; i++) Vertex(vertices[ringStart + i], uvs[ringStart + i]);
            for (int i = 0; i < sides; i++)
            {
                int a = ringStart + i, b = ringStart + (i + 1) % sides;
                float ang = (i + 0.5f) / sides * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(ang), radius / Mathf.Max(0.01f, height), Mathf.Sin(ang));
                Triangle(subMesh, apex, a, b, side);
                Triangle(subMesh, centre, underStart + i, underStart + (i + 1) % sides, Vector3.down);
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
