using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Media;
using System.Windows.Forms;

internal sealed class BreakScreen : Form
{
    private readonly Button soundButton;
    private readonly MemoryStream tickWave = CreateTick();
    private readonly SoundPlayer player;
    private readonly bool demo;
    private const string Instruction = "Подойди к этому окну и посмотри вдаль";
    private Font soundFont;
    private bool allowClose;
    private int seconds = -1;

    public BreakScreen(bool sound, bool demo)
    {
        this.demo = demo;
        SoundEnabled = sound;
        Text = "Программа для тренировки зрения";
        AccessibleName = Text;
        AccessibleDescription = Instruction;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen.Bounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        BackColor = ColorOf(0x080F24);
        soundButton = new Button
        {
            BackColor = ColorOf(0x182C4C),
            ForeColor = ColorOf(0xBAF66B),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false
        };
        soundButton.FlatAppearance.BorderColor = ColorOf(0x345A7C);
        soundButton.Click += delegate
        {
            SoundEnabled = !SoundEnabled;
            if (!SoundEnabled) player.Stop();
            UpdateButton();
        };
        Controls.Add(soundButton);
        Resize += delegate { PlaceButton(); };
        PlaceButton();
        UpdateButton();

        player = new SoundPlayer(tickWave);
        player.Load();
    }

    public bool SoundEnabled { get; private set; }

    public void ShowSeconds(int remaining)
    {
        if (remaining == seconds) return;
        bool shouldTick = seconds >= 0 && remaining > 0 && SoundEnabled;
        seconds = remaining;
        Invalidate();
        if (shouldTick) player.Play();
    }

    public void Dismiss()
    {
        allowClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!allowClose && (e.CloseReason == CloseReason.UserClosing ||
                            e.CloseReason == CloseReason.None))
            e.Cancel = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (player != null) player.Dispose();
            if (tickWave != null) tickWave.Dispose();
        }
        base.Dispose(disposing);
        if (disposing && soundFont != null) soundFont.Dispose();
    }

    private float ScaleFactor
    {
        get { return Math.Min(ClientSize.Width / 1200f, ClientSize.Height / 700f); }
    }

    private void PlaceButton()
    {
        float s = ScaleFactor;
        if (s <= 0) return;
        int x = (int)((ClientSize.Width - 1200 * s) / 2 + 83 * s);
        int y = (int)((ClientSize.Height - 700 * s) / 2 + 527 * s);
        soundButton.SetBounds(x, y, (int)(234 * s), (int)(48 * s));
        Font previous = soundFont;
        soundFont = new Font("Segoe UI", Math.Max(8, 19 * s), FontStyle.Bold, GraphicsUnit.Pixel);
        soundButton.Font = soundFont;
        if (previous != null) previous.Dispose();
    }

    private void UpdateButton()
    {
        soundButton.Text = SoundEnabled ? "Тиканье  ·  вкл." : "Тиканье  ·  выкл.";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        float s = ScaleFactor;
        if (s <= 0) return;
        g.TranslateTransform((ClientSize.Width - 1200 * s) / 2, (ClientSize.Height - 700 * s) / 2);
        g.ScaleTransform(s, s);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        Block(g, 42, 38, 1116, 624, 0x13223C);
        Block(g, 44, 40, 1112, 620, 0x0D1930);
        Block(g, 44, 40, 1112, 5, 0x4EDAFD);
        Block(g, 82, 76, 16, 16, 0xB9F46F);
        Label(g, demo ? "ДЕМО  /  ПЕРЕРЫВ" : "МИССИЯ  /  ПЕРЕРЫВ", 110, 72, 20, 0xBAF66B);
        Label(g, "Подойди к этому окну", 82, 123, 42, 0xF3F7FF);
        Label(g, "и посмотри вдаль", 82, 175, 42, 0xF3F7FF);
        Label(g, "ОСТАЛОСЬ", 84, 255, 19, 0x94B3CF);

        int left = Math.Max(0, seconds);
        string digits = string.Format(CultureInfo.InvariantCulture, "{0:00}{1:00}", left / 60, left % 60);
        DrawDigit(g, digits[0], 84);
        DrawDigit(g, digits[1], 202);
        Block(g, 333, 325, 18, 18, 0x55DFFF);
        Block(g, 333, 376, 18, 18, 0x55DFFF);
        DrawDigit(g, digits[2], 371);
        DrawDigit(g, digits[3], 489);
        Block(g, 84, 451, 507, 12, 0x243B56);
        Block(g, 84, 451, 507f * left / (demo ? 20 : 1200), 12, 0xBAF66B);
        Label(g, "Затем можно вернуться к своим делам", 84, 480, 18, 0xA6BCD2);
        Label(g, "40 МИН РАБОТЫ   /   20 МИН ПЕРЕРЫВА", 84, 609, 15, 0x829AB5);

        DrawView(g);
    }

    private static readonly int[] Segments =
    {
        0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F
    };

    private static void DrawDigit(Graphics g, char digit, int x)
    {
        int mask = Segments[digit - '0'];
        int[][] bars =
        {
            new[] { 16, 0, 69, 16 }, new[] { 85, 16, 16, 48 }, new[] { 85, 80, 16, 48 },
            new[] { 16, 128, 69, 16 }, new[] { 0, 80, 16, 48 }, new[] { 0, 16, 16, 48 },
            new[] { 16, 64, 69, 16 }
        };
        for (int i = 0; i < bars.Length; i++)
        {
            int[] b = bars[i];
            Block(g, x + b[0], 290 + b[1], b[2], b[3], (mask & (1 << i)) != 0 ? 0x55DFFF : 0x162944);
        }
    }

    private static void DrawView(Graphics g)
    {
        Block(g, 634, 87, 481, 493, 0x243B56);
        Block(g, 647, 100, 455, 467, 0x265480);
        using (var sky = new LinearGradientBrush(new Rectangle(647, 100, 455, 370),
                                                  ColorOf(0x276197), ColorOf(0x9FEBD9),
                                                  LinearGradientMode.Vertical))
            g.FillRectangle(sky, 647, 100, 455, 370);
        Block(g, 977, 145, 52, 52, 0xFFE493);
        Block(g, 678, 172, 94, 17, 0xC5F3E9);
        Block(g, 699, 157, 53, 17, 0xC5F3E9);
        Block(g, 845, 220, 76, 13, 0xA0E4E2);
        Shape(g, 0x497FA1, 647, 368, 647, 320, 728, 254, 796, 318, 862, 247, 956, 333, 1023, 264, 1102, 322, 1102, 420);
        Shape(g, 0x72AFB8, 701, 276, 728, 254, 754, 279, 730, 271);
        Shape(g, 0x5D9B83, 647, 353, 733, 333, 808, 370, 879, 344, 984, 367, 1102, 327, 1102, 515, 647, 515);
        Shape(g, 0x276B64, 647, 414, 741, 376, 853, 418, 960, 377, 1102, 414, 1102, 567, 647, 567);
        Shape(g, 0x79BB88, 894, 392, 954, 397, 881, 449, 976, 567, 814, 567, 790, 474);
        Block(g, 866, 100, 10, 467, 0x243B56);
        Block(g, 647, 325, 455, 10, 0x243B56);
        Block(g, 632, 554, 487, 26, 0x345372);

        // Original block character standing in front of the window.
        Block(g, 918, 549, 169, 14, 0x193248);
        Block(g, 955, 491, 40, 63, 0x233F69);
        Block(g, 1002, 491, 40, 63, 0x345787);
        Block(g, 950, 536, 46, 21, 0x14263D);
        Block(g, 1002, 536, 46, 21, 0x14263D);
        Block(g, 940, 419, 114, 82, 0x74A83F);
        Block(g, 940, 419, 100, 70, 0xB9F46F);
        Block(g, 908, 429, 29, 65, 0x9CD84F);
        Block(g, 908, 481, 29, 21, 0xE7A85F);
        Block(g, 1058, 429, 29, 65, 0x8AC24A);
        Block(g, 1058, 481, 29, 21, 0xE7A85F);
        Block(g, 954, 343, 90, 75, 0xD98C4A);
        Block(g, 954, 343, 77, 69, 0xFFC879);
        Block(g, 949, 337, 97, 20, 0x263657);
        Block(g, 949, 352, 19, 20, 0x263657);
        Block(g, 976, 372, 8, 10, 0x263657);
        Block(g, 1009, 372, 8, 10, 0x263657);
        Block(g, 990, 392, 17, 5, 0x654139);
        Block(g, 974, 439, 29, 6, 0x305743);
        Block(g, 986, 428, 6, 28, 0x305743);
        Label(g, "РЕЖИМ 01   /   СМОТРИМ ВДАЛЬ", 637, 609, 15, 0x829AB5);
    }

    private static Color ColorOf(int rgb)
    {
        return Color.FromArgb(unchecked((int)0xFF000000) | rgb);
    }

    private static void Block(Graphics g, float x, float y, float width, float height, int color)
    {
        using (var brush = new SolidBrush(ColorOf(color))) g.FillRectangle(brush, x, y, width, height);
    }

    private static void Shape(Graphics g, int color, params int[] coordinates)
    {
        var points = new Point[coordinates.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Point(coordinates[2 * i], coordinates[2 * i + 1]);
        using (var brush = new SolidBrush(ColorOf(color))) g.FillPolygon(brush, points);
    }

    private static void Label(Graphics g, string text, float x, float y, float size, int color)
    {
        using (var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(ColorOf(color)))
            g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
    }

    private static MemoryStream CreateTick()
    {
        const int rate = 22050;
        const int samples = 485;
        var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, System.Text.Encoding.ASCII, true))
        {
            writer.Write(0x46464952); // RIFF
            writer.Write(36 + samples * 2);
            writer.Write(0x45564157); // WAVE
            writer.Write(0x20746D66); // fmt
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(rate);
            writer.Write(rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(0x61746164); // data
            writer.Write(samples * 2);
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)rate;
                double envelope = Math.Min(1, i / 18.0) * Math.Exp(-t * 220) * (samples - i) / samples;
                writer.Write((short)(1150 * envelope * Math.Sin(2 * Math.PI * 1100 * t)));
            }
        }
        data.Position = 0;
        return data;
    }
}
