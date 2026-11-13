using System.Numerics;

namespace SoftEngine.Core;

/// <summary>
/// Draws a mesh's edges. Everything is RIGHT-HANDED and consistent:
///   Matrix4x4.CreateLookAt                  -> view space, camera looks down -Z
///   Matrix4x4.CreatePerspectiveFieldOfView  -> clip space, w = -z (positive in front)
/// Mixing LookAtLH with PerspectiveFovRH (as the 2013 engine did) makes w negative for
/// everything in front of the camera, and the divide by w flips the image.
///
/// Pipeline per edge:
///   model -> world -> view (one matrix)
///   clip the segment against the near plane IN VIEW SPACE (before any divide)
///   project each end, divide by w, map NDC to pixels
///   2D clip + Bresenham in FrameBuffer.DrawLine
/// </summary>
public sealed class WireframeRenderer
{
    private Vector3[] _viewSpace = Array.Empty<Vector3>();

    public void Draw(FrameBuffer fb, Mesh mesh, Matrix4x4 world, Matrix4x4 view,
                     Matrix4x4 projection, float near, uint argb)
    {
        var worldView = world * view;   // row vectors: v * World * View

        if (_viewSpace.Length < mesh.Vertices.Length)
            _viewSpace = new Vector3[mesh.Vertices.Length];
        for (int i = 0; i < mesh.Vertices.Length; i++)
            _viewSpace[i] = Vector3.Transform(mesh.Vertices[i], worldView);

        foreach (var (a, b) in mesh.Edges)
        {
            var p = _viewSpace[a];
            var q = _viewSpace[b];

            if (!ClipToNearPlane(ref p, ref q, near))
                continue;

            var sp = ProjectToScreen(p, projection, fb.Width, fb.Height);
            var sq = ProjectToScreen(q, projection, fb.Width, fb.Height);
            fb.DrawLine(sp.X, sp.Y, sq.X, sq.Y, argb);
        }
    }

    /// <summary>
    /// Visible region is z &lt;= -near (camera looks down -Z).
    /// Returns false if the whole segment is behind the near plane.
    /// </summary>
    public static bool ClipToNearPlane(ref Vector3 p, ref Vector3 q, float near)
    {
        float plane = -near;
        bool pIn = p.Z <= plane;
        bool qIn = q.Z <= plane;

        if (pIn && qIn) return true;
        if (!pIn && !qIn) return false;

        // Exactly one end is in front: slide the other one onto the plane.
        if (!pIn) p = Vector3.Lerp(p, q, (plane - p.Z) / (q.Z - p.Z));
        else      q = Vector3.Lerp(q, p, (plane - q.Z) / (p.Z - q.Z));
        return true;
    }

    /// <summary>
    /// View-space point (must be in front of the near plane) to pixel coordinates.
    /// NDC x,y run -1..1 with +y up; screen y runs down, hence (1 - y).
    /// </summary>
    public static Vector2 ProjectToScreen(Vector3 viewPos, Matrix4x4 projection, int width, int height)
    {
        var clip = Vector4.Transform(new Vector4(viewPos, 1f), projection);
        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        return new Vector2((ndcX + 1f) * 0.5f * width,
                           (1f - ndcY) * 0.5f * height);
    }
}
