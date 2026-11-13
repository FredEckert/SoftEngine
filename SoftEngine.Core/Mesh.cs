using System.Numerics;

namespace SoftEngine.Core;

/// <summary>
/// A wireframe mesh: a vertex list plus a list of unique edges (index pairs).
/// Faces are only used while loading, to produce edges.
/// </summary>
public sealed class Mesh
{
    public string Name { get; }
    public Vector3[] Vertices { get; }
    public (int A, int B)[] Edges { get; }

    /// <summary>Center of the axis-aligned bounding box.</summary>
    public Vector3 Center { get; }

    /// <summary>Distance from Center to the farthest vertex.</summary>
    public float Radius { get; }

    public Mesh(string name, Vector3[] vertices, (int A, int B)[] edges)
    {
        Name = name;
        Vertices = vertices;
        Edges = edges;

        if (vertices.Length == 0)
        {
            Center = Vector3.Zero;
            Radius = 1f;
            return;
        }

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }
        Center = (min + max) * 0.5f;

        float r = 0f;
        foreach (var v in vertices)
            r = MathF.Max(r, Vector3.Distance(v, Center));
        Radius = r > 0f ? r : 1f;
    }
}
