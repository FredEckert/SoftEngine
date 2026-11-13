using SoftEngine.Core;
using Xunit;

namespace SoftEngine.Tests;

public class ObjLoaderTests
{
    private static string Write(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), $"softengine-{Guid.NewGuid():N}.obj");
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Cube_Has8Vertices_And12UniqueEdges()
    {
        var path = Write("""
            o cube
            v -1 -1 -1
            v  1 -1 -1
            v  1  1 -1
            v -1  1 -1
            v -1 -1  1
            v  1 -1  1
            v  1  1  1
            v -1  1  1
            f 1 2 3 4
            f 5 8 7 6
            f 1 5 6 2
            f 2 6 7 3
            f 3 7 8 4
            f 5 1 4 8
            """);
        var m = ObjLoader.Load(path);
        Assert.Equal("cube", m.Name);
        Assert.Equal(8, m.Vertices.Length);
        Assert.Equal(12, m.Edges.Length);
        Assert.Equal(MathF.Sqrt(3f), m.Radius, 4);
    }

    [Fact]
    public void SlashFormats_NegativeIndices_AndLines_Work()
    {
        var path = Write("""
            v 0 0 0
            v 1 0 0
            v 0 1 0
            vn 0 0 1
            f 1//1 2//1 3//1
            l -3 -1
            """);
        var m = ObjLoader.Load(path);
        Assert.Equal(3, m.Edges.Length);   // triangle; the 'l' duplicates edge 1-3
    }
}
