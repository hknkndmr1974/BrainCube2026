using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pastel tema için yuvarlak köşeli karo mesh'lerini üretir (Assets/Art/PastelTiles).
/// Mesh'lerin plaka üst yüzü y = 0'dadır.
/// Submesh sırası: 0 = plaka üstü, 1 = plaka yanları, 2 = buton/iç yüzey üstü, 3 = buton kenarı.
/// </summary>
public static class PastelTileMeshBuilder
{
    private const string Folder = "Assets/Art/PastelTiles";

    private const float TileSize = 0.92f;
    private const float TileHeight = 0.24f;
    private const float TileRadius = 0.08f;
    private const int ArcSteps = 5;
    private const int RoundSegments = 40;

    private static readonly float[] TargetRingRadii = { 0.41f, 0.33f, 0.25f, 0.17f, 0.09f };
    private static readonly float[] TargetRingHeights = { 0.02f, 0.035f, 0.05f, 0.065f, 0.085f };
    private const float TargetBevel = 0.015f;

    private const float SoftButtonRadius = 0.32f;
    private const float SoftButtonHeight = 0.13f;
    private const float SoftButtonBevel = 0.05f;

    private const float HardButtonSize = 0.68f;
    private const float HardButtonHeight = 0.17f;
    private const float HardButtonRadius = 0.07f;
    private const float HardInsetSize = 0.40f;

    private const float SplitButtonSize = 0.31f;
    private const float SplitButtonGap = 0.09f;
    private const float SplitButtonHeight = 0.14f;
    private const float SplitButtonRadius = 0.05f;

    private const float ArrowHeight = 0.07f;
    private const float ArrowShaftHalfWidth = 0.10f;
    private const float ArrowTail = -0.32f;
    private const float ArrowNeck = 0.02f;
    private const float ArrowTip = 0.34f;
    private const float ArrowHeadHalfWidth = 0.28f;

    private const float PortalInnerRadius = 0.17f;
    private const float PortalOuterRadius = 0.34f;
    private const float PortalHeight = 0.09f;

    private const int PlankCount = 4;
    private const float PlankGap = 0.04f;
    private const float PlankThickness = 0.09f;
    private const float BeamSize = 0.11f;
    private const float BeamOffset = 0.30f;

    [MenuItem("BrainCube/Build Pastel Tile Meshes")]
    public static void BuildAll()
    {
        Save(BuildSlab(), Folder + "/PT_SlabMesh.asset");
        Save(BuildGoal(), Folder + "/PT_GoalMesh.asset");
        Save(BuildSoftSwitch(), Folder + "/PT_SoftSwitchMesh.asset");
        Save(BuildHardSwitch(), Folder + "/PT_HardSwitchMesh.asset");
        Save(BuildSplitSwitch(), Folder + "/PT_SplitSwitchMesh.asset");
        Save(BuildConveyor(), Folder + "/PT_ConveyorMesh.asset");
        Save(BuildTeleport(), Folder + "/PT_TeleportMesh.asset");
        Save(BuildBridge(), Folder + "/PT_BridgeMesh.asset");
        AssetDatabase.SaveAssets();
    }

    /// <summary>Ok +Z yönünü gösterir; karo yönüne göre çalışma anında döndürülür.</summary>
    private static Mesh BuildConveyor()
    {
        var data = new MeshData(4);
        AddTileBase(data);
        AddExtrudedConvex(data, new[]
        {
            new Vector2(-ArrowShaftHalfWidth, ArrowTail), new Vector2(ArrowShaftHalfWidth, ArrowTail),
            new Vector2(ArrowShaftHalfWidth, ArrowNeck + 0.01f), new Vector2(-ArrowShaftHalfWidth, ArrowNeck + 0.01f)
        }, ArrowHeight, 2, 3);
        AddExtrudedConvex(data, new[]
        {
            new Vector2(-ArrowHeadHalfWidth, ArrowNeck), new Vector2(ArrowHeadHalfWidth, ArrowNeck), new Vector2(0f, ArrowTip)
        }, ArrowHeight, 2, 3);
        return data.ToMesh("PT_ConveyorMesh");
    }

    /// <summary>Submesh 4 = halkanın içindeki parlayan portal yüzeyi.</summary>
    private static Mesh BuildTeleport()
    {
        var data = new MeshData(5);
        AddTileBase(data);
        AddRing(data, PortalInnerRadius, PortalOuterRadius, PortalHeight);
        AddDisc(data, 4, PortalInnerRadius + 0.01f, 0.004f);
        return data.ToMesh("PT_TeleportMesh");
    }

    /// <summary>Plaka yerine aralıklı enine tahtalar ve altında iki kiriş.
    /// Submesh: 0 = tahta üstü, 1 = tahta yanları, 2 = kirişler.</summary>
    private static Mesh BuildBridge()
    {
        var data = new MeshData(3);
        float plankDepth = (TileSize - PlankGap * (PlankCount - 1)) / PlankCount;
        for (int i = 0; i < PlankCount; i++)
        {
            float z = -TileSize * 0.5f + plankDepth * 0.5f + i * (plankDepth + PlankGap);
            AddRoundedBox(data, new Vector3(TileSize, PlankThickness, plankDepth), 0.03f,
                new Vector3(0f, -PlankThickness * 0.5f, z), 0, 1);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            AddRoundedBox(data, new Vector3(BeamSize, BeamSize, TileSize), 0.03f,
                new Vector3(side * BeamOffset, -PlankThickness - BeamSize * 0.5f + 0.01f, 0f), 2, 2);
        }

        return data.ToMesh("PT_BridgeMesh");
    }

    private static Mesh BuildSlab()
    {
        var data = new MeshData(2);
        AddTileBase(data);
        return data.ToMesh("PT_SlabMesh");
    }

    /// <summary>Hedef tahtası: içe doğru yükselen halkalar.
    /// Submesh: 2 = dıştan tek sıradaki halkalar ve merkez, 3 = aradaki halkalar, 4 = halka kenarları.</summary>
    private static Mesh BuildGoal()
    {
        var data = new MeshData(5);
        AddTileBase(data);
        for (int i = 0; i < TargetRingRadii.Length; i++)
        {
            AddRoundButton(data, Vector3.zero, TargetRingRadii[i], TargetRingHeights[i], TargetBevel, i % 2 == 0 ? 2 : 3, 4);
        }

        return data.ToMesh("PT_GoalMesh");
    }

    private static Mesh BuildSoftSwitch()
    {
        var data = new MeshData(4);
        AddTileBase(data);
        AddRoundButton(data, Vector3.zero, SoftButtonRadius, SoftButtonHeight, SoftButtonBevel);
        return data.ToMesh("PT_SoftSwitchMesh");
    }

    private static Mesh BuildHardSwitch()
    {
        var data = new MeshData(4);
        AddTileBase(data);
        Vector3 size = new Vector3(HardButtonSize, HardButtonHeight, HardButtonSize);
        AddRoundedBox(data, size, HardButtonRadius, new Vector3(0f, HardButtonHeight * 0.5f, 0f), 2, 3);
        AddRoundedSquare(data, 3, HardInsetSize, HardInsetSize * 0.2f, HardButtonHeight + 0.003f);
        return data.ToMesh("PT_HardSwitchMesh");
    }

    private static Mesh BuildSplitSwitch()
    {
        var data = new MeshData(4);
        AddTileBase(data);
        Vector3 size = new Vector3(SplitButtonSize, SplitButtonHeight, SplitButtonSize);
        float offset = (SplitButtonSize + SplitButtonGap) * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            AddRoundedBox(data, size, SplitButtonRadius, new Vector3(side * offset, SplitButtonHeight * 0.5f, 0f), 2, 3);
        }

        return data.ToMesh("PT_SplitSwitchMesh");
    }

    private static void AddTileBase(MeshData data)
    {
        Vector3 size = new Vector3(TileSize, TileHeight, TileSize);
        AddRoundedBox(data, size, TileRadius, new Vector3(0f, -TileHeight * 0.5f, 0f), 0, 1);
    }

    private static void Save(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return;
        }

        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
    }

    /// <summary>Yuvarlak köşeli kutu; üste bakan yüzler topSub'a, diğerleri sideSub'a gider.</summary>
    private static void AddRoundedBox(MeshData data, Vector3 size, float radius, Vector3 center, int topSub, int sideSub)
    {
        Vector3 half = size * 0.5f;
        Vector3 inner = half - Vector3.one * radius;

        Vector3[] faceNormals = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        foreach (Vector3 n in faceNormals)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            float[] us = AxisSamples(Vector3.Scale(half, Abs(u)).magnitude, radius);
            float[] vs = AxisSamples(Vector3.Scale(half, Abs(v)).magnitude, radius);
            float faceDist = Vector3.Scale(half, Abs(n)).magnitude;

            int start = data.Vertices.Count;
            for (int j = 0; j < vs.Length; j++)
            {
                for (int i = 0; i < us.Length; i++)
                {
                    Vector3 p = n * faceDist + u * us[i] + v * vs[j];
                    Vector3 clamped = new Vector3(
                        Mathf.Clamp(p.x, -inner.x, inner.x),
                        Mathf.Clamp(p.y, -inner.y, inner.y),
                        Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 dir = (p - clamped).normalized;
                    Vector3 pos = clamped + dir * radius + center;
                    data.Add(pos, dir, new Vector2(pos.x + 0.5f, pos.z + 0.5f));
                }
            }

            for (int j = 0; j < vs.Length - 1; j++)
            {
                for (int i = 0; i < us.Length - 1; i++)
                {
                    int a = start + j * us.Length + i;
                    int b = a + 1;
                    int c = a + us.Length + 1;
                    int d = a + us.Length;
                    Vector3 avg = (data.Normals[a] + data.Normals[b] + data.Normals[c] + data.Normals[d]).normalized;
                    int sub = avg.y > 0.95f ? topSub : sideSub;
                    data.AddTriangle(sub, avg, a, b, c);
                    data.AddTriangle(sub, avg, a, c, d);
                }
            }
        }
    }

    /// <summary>Kenarı yuvarlatılmış silindir buton; üst kapak topSub, kenar sideSub numaralı submesh'e gider.</summary>
    private static void AddRoundButton(MeshData data, Vector3 center, float radius, float height, float bevel,
        int topSub = 2, int sideSub = 3)
    {
        var profile = new List<Vector2>();
        var profileNormals = new List<Vector2>();
        profile.Add(new Vector2(0f, height));
        profileNormals.Add(Vector2.up);
        for (int s = 0; s <= ArcSteps; s++)
        {
            float a = s * 90f / ArcSteps * Mathf.Deg2Rad;
            profile.Add(new Vector2(radius - bevel + Mathf.Sin(a) * bevel, height - bevel + Mathf.Cos(a) * bevel));
            profileNormals.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
        }

        profile.Add(new Vector2(radius, 0f));
        profileNormals.Add(Vector2.right);

        int rings = profile.Count;
        int start = data.Vertices.Count;
        for (int seg = 0; seg <= RoundSegments; seg++)
        {
            float angle = seg * 360f / RoundSegments * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int k = 0; k < rings; k++)
            {
                Vector3 pos = center + radial * profile[k].x + Vector3.up * profile[k].y;
                Vector3 normal = (radial * profileNormals[k].x + Vector3.up * profileNormals[k].y).normalized;
                data.Add(pos, normal, new Vector2(pos.x + 0.5f, pos.z + 0.5f));
            }
        }

        for (int seg = 0; seg < RoundSegments; seg++)
        {
            for (int k = 0; k < rings - 1; k++)
            {
                int a = start + seg * rings + k;
                int b = start + (seg + 1) * rings + k;
                int c = b + 1;
                int d = a + 1;
                Vector3 avg = (data.Normals[a] + data.Normals[b] + data.Normals[c] + data.Normals[d]).normalized;
                int sub = avg.y > 0.95f ? topSub : sideSub;
                data.AddTriangle(sub, avg, a, b, c);
                data.AddTriangle(sub, avg, a, c, d);
            }
        }
    }

    /// <summary>Dışbükey çokgeni plaka üstünden yukarı çıkarır; üst yüz topSub, yanlar sideSub.</summary>
    private static void AddExtrudedConvex(MeshData data, Vector2[] polygon, float height, int topSub, int sideSub)
    {
        int center = data.Vertices.Count;
        Vector2 mid = Vector2.zero;
        foreach (Vector2 p in polygon)
        {
            mid += p;
        }

        mid /= polygon.Length;
        data.Add(new Vector3(mid.x, height, mid.y), Vector3.up, mid);
        int ring = data.Vertices.Count;
        foreach (Vector2 p in polygon)
        {
            data.Add(new Vector3(p.x, height, p.y), Vector3.up, p);
        }

        for (int i = 0; i < polygon.Length; i++)
        {
            data.AddTriangle(topSub, Vector3.up, center, ring + i, ring + (i + 1) % polygon.Length);
        }

        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Length];
            Vector2 edge = (b - a).normalized;
            Vector3 normal = new Vector3(edge.y, 0f, -edge.x);
            if (Vector2.Dot(new Vector2(normal.x, normal.z), (a + b) * 0.5f - mid) < 0f)
            {
                normal = -normal;
            }

            int s = data.Vertices.Count;
            data.Add(new Vector3(a.x, 0f, a.y), normal, a);
            data.Add(new Vector3(b.x, 0f, b.y), normal, b);
            data.Add(new Vector3(b.x, height, b.y), normal, b);
            data.Add(new Vector3(a.x, height, a.y), normal, a);
            data.AddTriangle(sideSub, normal, s, s + 1, s + 2);
            data.AddTriangle(sideSub, normal, s, s + 2, s + 3);
        }
    }

    /// <summary>Kenarları yuvarlak halka (portal çerçevesi); üstü 2, kenarları 3 numaralı submesh.</summary>
    private static void AddRing(MeshData data, float innerRadius, float outerRadius, float height)
    {
        float tube = (outerRadius - innerRadius) * 0.5f;
        float bevel = Mathf.Min(tube, height) * 0.9f;
        var profile = new List<Vector2>();
        var profileNormals = new List<Vector2>();

        profile.Add(new Vector2(innerRadius, 0f));
        profileNormals.Add(Vector2.left);
        for (int s = 0; s <= ArcSteps; s++)
        {
            float a = (-90f + s * 90f / ArcSteps) * Mathf.Deg2Rad;
            profile.Add(new Vector2(innerRadius + bevel + Mathf.Sin(a) * bevel, height - bevel + Mathf.Cos(a) * bevel));
            profileNormals.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
        }

        for (int s = 0; s <= ArcSteps; s++)
        {
            float a = (s * 90f / ArcSteps) * Mathf.Deg2Rad;
            profile.Add(new Vector2(outerRadius - bevel + Mathf.Sin(a) * bevel, height - bevel + Mathf.Cos(a) * bevel));
            profileNormals.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)));
        }

        profile.Add(new Vector2(outerRadius, 0f));
        profileNormals.Add(Vector2.right);
        Lathe(data, Vector3.zero, profile, profileNormals);
    }

    private static void AddDisc(MeshData data, int sub, float radius, float lift)
    {
        int center = data.Vertices.Count;
        data.Add(new Vector3(0f, lift, 0f), Vector3.up, new Vector2(0.5f, 0.5f));
        int ring = data.Vertices.Count;
        for (int seg = 0; seg < RoundSegments; seg++)
        {
            float angle = seg * 360f / RoundSegments * Mathf.Deg2Rad;
            data.Add(new Vector3(Mathf.Cos(angle) * radius, lift, Mathf.Sin(angle) * radius), Vector3.up, Vector2.zero);
        }

        for (int seg = 0; seg < RoundSegments; seg++)
        {
            data.AddTriangle(sub, Vector3.up, center, ring + seg, ring + (seg + 1) % RoundSegments);
        }
    }

    /// <summary>Profili Y ekseni etrafında döndürür; üste bakan yüzler 2, diğerleri 3 numaralı submesh.</summary>
    private static void Lathe(MeshData data, Vector3 center, List<Vector2> profile, List<Vector2> profileNormals)
    {
        int rings = profile.Count;
        int start = data.Vertices.Count;
        for (int seg = 0; seg <= RoundSegments; seg++)
        {
            float angle = seg * 360f / RoundSegments * Mathf.Deg2Rad;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (int k = 0; k < rings; k++)
            {
                Vector3 pos = center + radial * profile[k].x + Vector3.up * profile[k].y;
                Vector3 normal = (radial * profileNormals[k].x + Vector3.up * profileNormals[k].y).normalized;
                data.Add(pos, normal, new Vector2(pos.x + 0.5f, pos.z + 0.5f));
            }
        }

        for (int seg = 0; seg < RoundSegments; seg++)
        {
            for (int k = 0; k < rings - 1; k++)
            {
                int a = start + seg * rings + k;
                int b = start + (seg + 1) * rings + k;
                int c = b + 1;
                int d = a + 1;
                Vector3 avg = (data.Normals[a] + data.Normals[b] + data.Normals[c] + data.Normals[d]).normalized;
                int sub = avg.y > 0.95f ? 2 : 3;
                data.AddTriangle(sub, avg, a, b, c);
                data.AddTriangle(sub, avg, a, c, d);
            }
        }
    }

    private static void AddRoundedSquare(MeshData data, int sub, float size, float radius, float lift)
    {
        float innerHalf = size * 0.5f - radius;
        int center = data.Vertices.Count;
        data.Add(new Vector3(0f, lift, 0f), Vector3.up, new Vector2(0.5f, 0.5f));

        Vector2[] corners = { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) };
        int ring = data.Vertices.Count;
        for (int c = 0; c < 4; c++)
        {
            for (int s = 0; s <= ArcSteps; s++)
            {
                float a = (c * 90f + s * 90f / ArcSteps) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(corners[c].x * innerHalf + Mathf.Cos(a) * radius, lift,
                    corners[c].y * innerHalf + Mathf.Sin(a) * radius);
                data.Add(p, Vector3.up, new Vector2(p.x / size + 0.5f, p.z / size + 0.5f));
            }
        }

        int count = data.Vertices.Count - ring;
        for (int i = 0; i < count; i++)
        {
            data.AddTriangle(sub, Vector3.up, center, ring + i, ring + (i + 1) % count);
        }
    }

    /// <summary>Kenarlarda yay boyunca eşit açılı, ortada tek parça örnekler.</summary>
    private static float[] AxisSamples(float halfLength, float radius)
    {
        float innerHalf = halfLength - radius;
        var samples = new List<float>();
        for (int i = ArcSteps; i >= 0; i--)
        {
            samples.Add(-innerHalf - radius * Mathf.Tan(i * 45f / ArcSteps * Mathf.Deg2Rad));
        }

        for (int i = 0; i <= ArcSteps; i++)
        {
            samples.Add(innerHalf + radius * Mathf.Tan(i * 45f / ArcSteps * Mathf.Deg2Rad));
        }

        return samples.ToArray();
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] submeshes;

        public MeshData(int submeshCount)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++)
            {
                submeshes[i] = new List<int>();
            }
        }

        public void Add(Vector3 position, Vector3 normal, Vector2 uv)
        {
            Vertices.Add(position);
            Normals.Add(normal);
            uvs.Add(uv);
        }

        public void AddTriangle(int sub, Vector3 outward, int a, int b, int c)
        {
            Vector3 cross = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
            if (cross.sqrMagnitude < 1e-12f)
            {
                return;
            }

            List<int> target = submeshes[sub];
            if (Vector3.Dot(cross, outward) >= 0f)
            {
                target.Add(a); target.Add(b); target.Add(c);
            }
            else
            {
                target.Add(a); target.Add(c); target.Add(b);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
