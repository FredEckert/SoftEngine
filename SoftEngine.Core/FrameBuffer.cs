namespace SoftEngine.Core;

/// <summary>
/// A plain 32-bit ARGB pixel buffer. Knows nothing about SDL, WPF or Avalonia;
/// the host just copies Pixels to the screen each frame.
/// </summary>
public sealed class FrameBuffer
{
    public int Width { get; }
    public int Height { get; }
    public uint[] Pixels { get; }

    public FrameBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
    }

    public void Clear(uint argb) => Array.Fill(Pixels, argb);

    public void PutPixel(int x, int y, uint argb)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
            Pixels[y * Width + x] = argb;
    }

    /// <summary>Draw a line given in (possibly off-screen) float pixel coordinates.</summary>
    public void DrawLine(float x0, float y0, float x1, float y1, uint argb)
    {
        if (!ClipLine(ref x0, ref y0, ref x1, ref y1, 0f, 0f, Width - 1, Height - 1))
            return;
        Bresenham((int)MathF.Round(x0), (int)MathF.Round(y0),
                  (int)MathF.Round(x1), (int)MathF.Round(y1), argb);
    }

    /// <summary>Integer Bresenham, all octants. Endpoints must already be on screen.</summary>
    private void Bresenham(int x0, int y0, int x1, int y1, uint argb)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            Pixels[y0 * Width + x0] = argb;
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // ---- Cohen–Sutherland 2D clipping against the screen rectangle ----

    private const int Left = 1, Right = 2, Top = 4, Bottom = 8;

    private static int OutCode(float x, float y, float xmin, float ymin, float xmax, float ymax)
    {
        int code = 0;
        if (x < xmin) code |= Left; else if (x > xmax) code |= Right;
        if (y < ymin) code |= Top; else if (y > ymax) code |= Bottom;
        return code;
    }

    public static bool ClipLine(ref float x0, ref float y0, ref float x1, ref float y1,
                                float xmin, float ymin, float xmax, float ymax)
    {
        int c0 = OutCode(x0, y0, xmin, ymin, xmax, ymax);
        int c1 = OutCode(x1, y1, xmin, ymin, xmax, ymax);

        while (true)
        {
            if ((c0 | c1) == 0) return true;     // both inside
            if ((c0 & c1) != 0) return false;    // both outside on the same side

            int c = c0 != 0 ? c0 : c1;
            float x, y;
            if ((c & Bottom) != 0) { x = x0 + (x1 - x0) * (ymax - y0) / (y1 - y0); y = ymax; }
            else if ((c & Top) != 0) { x = x0 + (x1 - x0) * (ymin - y0) / (y1 - y0); y = ymin; }
            else if ((c & Right) != 0) { y = y0 + (y1 - y0) * (xmax - x0) / (x1 - x0); x = xmax; }
            else { y = y0 + (y1 - y0) * (xmin - x0) / (x1 - x0); x = xmin; }

            if (c == c0) { x0 = x; y0 = y; c0 = OutCode(x0, y0, xmin, ymin, xmax, ymax); }
            else { x1 = x; y1 = y; c1 = OutCode(x1, y1, xmin, ymin, xmax, ymax); }
        }
    }
}
