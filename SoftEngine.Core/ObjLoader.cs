using System.Globalization;
using System.Numerics;

namespace SoftEngine.Core;

/// <summary>
/// Minimal Wavefront .obj reader for wireframes.
/// Understands: v, f, l, o. Ignores vt, vn, mtllib, usemtl, s, g and anything else.
/// Face/line tokens may be "7", "7/2", "7//3" or "7/2/3"; negative (relative) indices work.
/// Each face becomes a closed outline; shared edges are stored once.
/// </summary>
public static class ObjLoader
{
    public static Mesh Load(string path)
    {
        var vertices = new List<Vector3>();
        var edges = new HashSet<(int, int)>();
        string name = Path.GetFileNameWithoutExtension(path);
        int lineNo = 0;

        foreach (var raw in File.ReadLines(path))
        {
            lineNo++;
            var line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            switch (parts[0])
            {
                case "v":
                    if (parts.Length < 4)
                        throw new FormatException($"{path}({lineNo}): 'v' needs 3 coordinates");
                    vertices.Add(new Vector3(ParseFloat(parts[1]), ParseFloat(parts[2]), ParseFloat(parts[3])));
                    break;

                case "f":
                    AddOutline(parts, vertices.Count, edges, closed: true, path, lineNo);
                    break;

                case "l":
                    AddOutline(parts, vertices.Count, edges, closed: false, path, lineNo);
                    break;

                case "o":
                    if (parts.Length > 1) name = parts[1];
                    break;
            }
        }

        return new Mesh(name, vertices.ToArray(), edges.ToArray());
    }

    private static void AddOutline(string[] parts, int vertexCount, HashSet<(int, int)> edges,
                                   bool closed, string path, int lineNo)
    {
        int n = parts.Length - 1;
        if (n < 2) return;

        var idx = new int[n];
        for (int k = 0; k < n; k++)
            idx[k] = ParseIndex(parts[k + 1], vertexCount, path, lineNo);

        int segments = closed ? n : n - 1;
        for (int k = 0; k < segments; k++)
        {
            int a = idx[k];
            int b = idx[(k + 1) % n];
            if (a == b) continue;
            edges.Add(a < b ? (a, b) : (b, a));   // canonical order => shared edges dedupe
        }
    }

    private static int ParseIndex(string token, int vertexCount, string path, int lineNo)
    {
        int slash = token.IndexOf('/');
        var s = slash >= 0 ? token[..slash] : token;
        int i = int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
        int index = i > 0 ? i - 1 : vertexCount + i;     // OBJ is 1-based; negatives count back
        if (i == 0 || index < 0 || index >= vertexCount)
            throw new FormatException($"{path}({lineNo}): vertex index {i} out of range (have {vertexCount})");
        return index;
    }

    private static float ParseFloat(string s) =>
        float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
