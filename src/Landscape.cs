using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

internal static class Landscape
{
    // Upper sky, horizon, light tint, and terrain brightness for each time of day.
    private static readonly int[] Sky = { 0x276197, 0x267EC0, 0x534281, 0x102043 };
    private static readonly int[] Horizon = { 0xF9CEA0, 0xACEBD9, 0xF6A172, 0x465887 };
    private static readonly int[] Light = { 0xFFD7A4, 0xFFFFFF, 0xFFBDA2, 0x6481B6 };
    private static readonly int[] Meadow = { 0x5D9B83, 0x5EA65E, 0xB68349, 0xB6D5E0 };
    private static readonly int[] Near = { 0x277464, 0x29764E, 0x8D5034, 0x79A6B6 };
    private static readonly int[] Path = { 0xB6C88B, 0xA6C87F, 0xDBAA69, 0xD9E9EE };
    private static readonly int[] Leaves = { 0x8CCC80, 0x69B75B, 0xF4A33A, 0x8B604E };

    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint action, uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool enabled, uint flags);

    public static bool MotionEnabled()
    {
        bool enabled;
        const uint getClientAreaAnimation = 0x1042;
        return !SystemParametersInfo(getClientAreaAnimation, 0, out enabled, 0) || enabled;
    }

    public static void Draw(Graphics g, long elapsed, bool demo, int season, bool animate)
    {
        double lighting = SceneMotion.Lighting(elapsed, demo);
        if (!animate) lighting = Math.Floor(lighting);
        Color tint = Palette(Light, lighting);
        Rectangle view = new Rectangle(647, 100, 455, 467);

        Block(g, 634, 87, 481, 493, Rgb(0x243B56));
        GraphicsState original = g.Save();
        g.SetClip(view, CombineMode.Intersect);
        using (var sky = new LinearGradientBrush(view, Palette(Sky, lighting),
                                                  Palette(Horizon, lighting), LinearGradientMode.Vertical))
            g.FillRectangle(sky, view);

        DrawSkyDetails(g, lighting);
        Shape(g, Lit(0x5F93AA, tint), 647, 368, 647, 320, 728, 254, 796, 318, 862, 247,
              956, 333, 1023, 264, 1102, 322, 1102, 420);
        Color peaks = Lit(season == 3 ? 0xF0F7FF : 0x8FBDC0, tint);
        Shape(g, peaks, 695, 283, 728, 254, 760, 285, 728, 275);
        Shape(g, peaks, 829, 285, 862, 247, 900, 281, 863, 271);
        Shape(g, peaks, 1000, 288, 1023, 264, 1052, 285, 1027, 276);
        Shape(g, Lit(Meadow[season], tint), 647, 353, 733, 333, 808, 370, 879, 344,
              984, 367, 1102, 327, 1102, 515, 647, 515);
        Shape(g, Lit(Near[season], tint), 647, 414, 741, 376, 853, 418, 960, 377,
              1102, 414, 1102, 567, 647, 567);
        Shape(g, Lit(Path[season], tint), 894, 392, 954, 397, 881, 449, 976, 567,
              814, 567, 790, 474);
        DrawSeason(g, season, tint);

        Block(g, 866, 100, 10, 467, Rgb(0x243B56));
        Block(g, 647, 325, 455, 10, Rgb(0x243B56));
        DrawHero(g, elapsed, tint, animate);
        g.Restore(original);
        Block(g, 632, 554, 487, 26, Rgb(0x345372));
    }

    private static void DrawSkyDetails(Graphics g, double lighting)
    {
        float sunX = (float)(716 + 160 * Math.Min(2, lighting));
        float sunY = (float)(170 - 45 * Math.Sin(Math.PI * Math.Min(2, lighting) / 2));
        if (lighting < 3)
            Block(g, sunX, sunY, 42, 42, Color.FromArgb((int)(255 * Math.Min(1, 3 - lighting)), Rgb(0xFFE493)));
        if (lighting > 2)
        {
            Color moon = Color.FromArgb((int)(255 * (lighting - 2)), Rgb(0xE3EFFF));
            Block(g, 966, 143, 34, 34, moon);
            Block(g, 701, 147, 5, 5, moon);
            Block(g, 815, 194, 4, 4, moon);
            Block(g, 915, 145, 5, 5, moon);
            Block(g, 1057, 183, 4, 4, moon);
        }
        Color cloud = Color.FromArgb((int)(175 - 39 * lighting), Rgb(0xDEF8F0));
        Block(g, 678, 225, 94, 13, cloud);
        Block(g, 699, 211, 53, 14, cloud);
        Block(g, 846, 199, 73, 10, cloud);
        Block(g, 1025, 215, 52, 11, cloud);
    }

    private static void DrawSeason(Graphics g, int season, Color tint)
    {
        DrawTree(g, 719, 417, season, tint);
        DrawTree(g, 1018, 401, season, tint);
        DrawTree(g, 1073, 445, season, tint);
        Color detail = Lit(new[] { 0xF6A4CD, 0xFFE380, 0xF8A449, 0xEDF8FF }[season], tint);
        for (int i = 0; i < 10; i++)
        {
            int x = 674 + (i * 97) % 412;
            int y = 430 + (i * 19) % 76;
            if (season <= 1)
            {
                Block(g, x + 2, y + 4, 2, 7, Lit(0x416F53, tint));
                Block(g, x, y, 6, 6, detail);
            }
            else
                Block(g, x, y, season == 3 ? 12 : 7, 4, detail);
        }
    }

    private static void DrawTree(Graphics g, int x, int y, int season, Color tint)
    {
        Color wood = Lit(0x885A42, tint);
        Block(g, x - 4, y - 31, 8, 31, wood);
        if (season == 3)
        {
            Block(g, x - 19, y - 41, 38, 5, wood);
            Block(g, x - 19, y - 51, 5, 13, wood);
            Block(g, x + 14, y - 52, 5, 13, wood);
            Block(g, x - 4, y - 60, 8, 33, wood);
            Block(g, x - 23, y - 54, 14, 5, Lit(0xF0F7FF, tint));
            Block(g, x - 8, y - 63, 16, 5, Lit(0xF0F7FF, tint));
        }
        else
        {
            Color crown = Lit(Leaves[season], tint);
            Block(g, x - 23, y - 54, 46, 24, crown);
            Block(g, x - 15, y - 65, 30, 13, crown);
            if (season == 0)
            {
                Block(g, x - 16, y - 51, 6, 6, Lit(0xF6A4CD, tint));
                Block(g, x + 10, y - 44, 6, 6, Lit(0xF6A4CD, tint));
            }
        }
    }

    private static void DrawHero(Graphics g, long elapsed, Color tint, bool animate)
    {
        float x = (float)(723 + 303 * (animate ? SceneMotion.Position(elapsed) : 0.5));
        float stride = animate ? (float)Math.Sin(2 * Math.PI * elapsed / 1100.0) : 0;
        Block(g, x - 57, 541, 114, 9, Color.FromArgb(105, Rgb(0x101C2C)));

        GraphicsState state = g.Save();
        g.TranslateTransform(x, 542 - Math.Abs(stride) * 3);
        if (animate && !SceneMotion.FacingRight(elapsed)) g.ScaleTransform(-1, 1);
        DrawLeg(g, -16 - stride * 22, -9 * Math.Max(0, -stride), Lit(0x233F69, tint));
        DrawLeg(g, 10 + stride * 22, -9 * Math.Max(0, stride), Lit(0x345787, tint));
        DrawArm(g, -45 + stride * 13, Lit(0x8AC24A, tint), tint);
        Block(g, -34, -101, 74, 60, Lit(0x74A83F, tint));
        Block(g, -34, -101, 65, 52, Lit(0xB9F46F, tint));
        DrawArm(g, 39 - stride * 13, Lit(0x9CD84F, tint), tint);
        Block(g, -24, -157, 57, 56, Lit(0xD98C4A, tint));
        Block(g, -15, -157, 48, 51, Lit(0xFFC879, tint));
        Block(g, -29, -161, 65, 15, Lit(0x263657, tint));
        Block(g, -29, -149, 18, 16, Lit(0x263657, tint));
        Block(g, 5, -135, 5, 7, Rgb(0x263657));
        Block(g, 24, -135, 5, 7, Rgb(0x263657));
        Block(g, 16, -120, 11, 4, Lit(0x654139, tint));
        Block(g, -7, -83, 24, 5, Lit(0x305743, tint));
        Block(g, 2, -93, 5, 25, Lit(0x305743, tint));
        g.Restore(state);
    }

    private static void DrawLeg(Graphics g, float x, float lift, Color color)
    {
        Block(g, x - 10, -47 + lift, 23, 39, color);
        Block(g, x - 10, -13 + lift, 29, 13, Rgb(0x14263D));
    }

    private static void DrawArm(Graphics g, float x, Color sleeve, Color tint)
    {
        Block(g, x - 8, -94, 19, 40, sleeve);
        Block(g, x - 8, -62, 19, 15, Lit(0xE7A85F, tint));
    }

    private static Color Palette(int[] colors, double phase)
    {
        int current = (int)phase;
        Color a = Rgb(colors[current]);
        Color b = Rgb(colors[Math.Min(current + 1, colors.Length - 1)]);
        double fraction = phase - current;
        return Color.FromArgb((int)(a.R + (b.R - a.R) * fraction),
                              (int)(a.G + (b.G - a.G) * fraction),
                              (int)(a.B + (b.B - a.B) * fraction));
    }

    private static Color Lit(int rgb, Color tint)
    {
        Color color = Rgb(rgb);
        return Color.FromArgb(color.R * tint.R / 255, color.G * tint.G / 255, color.B * tint.B / 255);
    }

    private static Color Rgb(int rgb)
    {
        return Color.FromArgb(unchecked((int)0xFF000000) | rgb);
    }

    private static void Block(Graphics g, float x, float y, float width, float height, Color color)
    {
        using (var brush = new SolidBrush(color)) g.FillRectangle(brush, x, y, width, height);
    }

    private static void Shape(Graphics g, Color color, params int[] coordinates)
    {
        var points = new Point[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Point(coordinates[2 * i], coordinates[2 * i + 1]);
        using (var brush = new SolidBrush(color)) g.FillPolygon(brush, points);
    }
}
