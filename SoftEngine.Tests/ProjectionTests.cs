using System.Numerics;
using SoftEngine.Core;
using Xunit;

namespace SoftEngine.Tests;

/// <summary>
/// These are the tests that would have caught the 2013 engine's LH/RH mix-up:
/// with the camera on +Z looking at the origin, +X must land on the right half
/// of the screen and +Y on the upper half.
/// </summary>
public class ProjectionTests
{
    private const int W = 800, H = 600;
    private static readonly Matrix4x4 View =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 10), Vector3.Zero, Vector3.UnitY);
    private static readonly Matrix4x4 Proj =
        Matrix4x4.CreatePerspectiveFieldOfView(0.78f, (float)W / H, 0.1f, 100f);

    private static Vector2 Screen(Vector3 world) =>
        WireframeRenderer.ProjectToScreen(Vector3.Transform(world, View), Proj, W, H);

    [Fact]
    public void Origin_LandsAtScreenCenter()
    {
        var s = Screen(Vector3.Zero);
        Assert.Equal(W / 2f, s.X, 3);
        Assert.Equal(H / 2f, s.Y, 3);
    }

    [Fact]
    public void PlusX_IsRight_PlusY_IsUp()
    {
        Assert.True(Screen(Vector3.UnitX).X > W / 2f);
        Assert.True(Screen(Vector3.UnitY).Y < H / 2f);   // screen y runs down
    }

    [Fact]
    public void FartherPoints_AreCloserToCenter()
    {
        var near = Screen(new Vector3(1, 0, 0));
        var far = Screen(new Vector3(1, 0, -10));
        Assert.True(near.X > far.X);
        Assert.True(far.X > W / 2f);
    }

    [Fact]
    public void ClipW_IsPositiveInFrontOfCamera()
    {
        var viewPos = Vector3.Transform(Vector3.Zero, View);           // z = -10
        var clip = Vector4.Transform(new Vector4(viewPos, 1f), Proj);
        Assert.True(clip.W > 0f);
    }

    [Fact]
    public void NearClip_SegmentBehindCamera_IsRejected()
    {
        var p = new Vector3(0, 0, 1);
        var q = new Vector3(1, 0, 5);
        Assert.False(WireframeRenderer.ClipToNearPlane(ref p, ref q, 0.1f));
    }

    [Fact]
    public void NearClip_CrossingSegment_IsCutAtThePlane()
    {
        var p = new Vector3(0, 0, -5);
        var q = new Vector3(0, 0, 5);
        Assert.True(WireframeRenderer.ClipToNearPlane(ref p, ref q, 0.1f));
        Assert.Equal(-5f, p.Z, 4);
        Assert.Equal(-0.1f, q.Z, 4);
    }

    [Fact]
    public void Frame_StaysOrthonormal_AfterManyRotations()
    {
        var f = new Frame();
        for (int i = 0; i < 100_000; i++)
        {
            f.Yaw(0.013f);
            f.Pitch(0.007f);
            f.Roll(-0.011f);
            f.Orthonormalize();
        }
        Assert.Equal(1f, f.X.Length(), 4);
        Assert.Equal(1f, f.Y.Length(), 4);
        Assert.Equal(1f, f.Z.Length(), 4);
        Assert.Equal(0f, Vector3.Dot(f.X, f.Y), 4);
        Assert.Equal(0f, Vector3.Dot(f.Y, f.Z), 4);
        // Still right-handed: X x Y = Z
        Assert.Equal(1f, Vector3.Dot(Vector3.Cross(f.X, f.Y), f.Z), 4);
    }
}
