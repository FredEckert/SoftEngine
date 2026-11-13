using System.Numerics;

namespace SoftEngine.Core;

/// <summary>
/// Orientation stored the Jed Margolin way: three orthonormal unit vectors,
/// expressed in world coordinates. No Euler angles anywhere.
///   X = the object's "right", Y = its "up", Z = its "back" (right-handed; -Z is forward).
/// Rotations are applied incrementally to the vectors themselves, and the set is
/// re-orthonormalized so floating-point error can't make them drift ("Is 1.0000?").
/// </summary>
public sealed class Frame
{
    public Vector3 X = Vector3.UnitX;
    public Vector3 Y = Vector3.UnitY;
    public Vector3 Z = Vector3.UnitZ;

    public void Reset()
    {
        X = Vector3.UnitX;
        Y = Vector3.UnitY;
        Z = Vector3.UnitZ;
    }

    /// <summary>Rotate all three unit vectors about a world-space axis.</summary>
    public void Rotate(Vector3 axis, float angle)
    {
        if (angle == 0f) return;
        axis = Vector3.Normalize(axis);
        X = Rodrigues(X, axis, angle);
        Y = Rodrigues(Y, axis, angle);
        Z = Rodrigues(Z, axis, angle);
    }

    /// <summary>Rotate about the object's OWN axes (pilot-style yaw / pitch / roll).</summary>
    public void Yaw(float angle) => Rotate(Y, angle);
    public void Pitch(float angle) => Rotate(X, angle);
    public void Roll(float angle) => Rotate(Z, angle);

    /// <summary>Gram-Schmidt: keep Z's direction, rebuild X and Y exactly perpendicular, unit length.</summary>
    public void Orthonormalize()
    {
        Z = Vector3.Normalize(Z);
        X = Vector3.Normalize(Vector3.Cross(Y, Z));   // right-handed: Y x Z = X
        Y = Vector3.Cross(Z, X);                       // already unit length
    }

    /// <summary>
    /// Object-to-world matrix. System.Numerics uses row vectors (v * M), so each
    /// unit vector is a ROW, and the translation is the bottom row.
    /// </summary>
    public Matrix4x4 ToMatrix(Vector3 position) => new(
        X.X, X.Y, X.Z, 0f,
        Y.X, Y.Y, Y.Z, 0f,
        Z.X, Z.Y, Z.Z, 0f,
        position.X, position.Y, position.Z, 1f);

    /// <summary>Rodrigues' rotation formula: rotate v about unit axis k by angle a.</summary>
    public static Vector3 Rodrigues(Vector3 v, Vector3 k, float a)
    {
        float c = MathF.Cos(a), s = MathF.Sin(a);
        return v * c + Vector3.Cross(k, v) * s + k * (Vector3.Dot(k, v) * (1f - c));
    }
}
