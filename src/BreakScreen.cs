using System;
using System.Drawing;
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
    private readonly int season;
    private readonly bool animate;
    private long sceneElapsed;
    private const string Instruction = "Подойди к этому окну и посмотри вдаль";
    private Font soundFont;
    private bool allowClose;
    private int seconds = -1;

    public BreakScreen(bool sound, bool demo, int season)
    {
        this.demo = demo;
        this.season = season;
        animate = Landscape.MotionEnabled();
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

    public void ShowFrame(int remaining, long elapsed)
    {
        sceneElapsed = elapsed;
        if (remaining == seconds)
        {
            if (animate) InvalidateScene();
            return;
        }
        bool shouldTick = seconds >= 0 && remaining > 0 && SoundEnabled;
        seconds = remaining;
        Invalidate();
        if (shouldTick) player.Play();
    }

    private void InvalidateScene()
    {
        float s = ScaleFactor;
        int left = (int)((ClientSize.Width - 1200 * s) / 2 + 634 * s);
        int top = (int)((ClientSize.Height - 700 * s) / 2 + 87 * s);
        Invalidate(new Rectangle(left, top, (int)Math.Ceiling(482 * s), (int)Math.Ceiling(494 * s)));
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

        Landscape.Draw(g, sceneElapsed, demo, season, animate);
        Label(g, "РЕЖИМ 01   /   СМОТРИМ ВДАЛЬ", 637, 609, 15, 0x829AB5);
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

    private static Color ColorOf(int rgb)
    {
        return Color.FromArgb(unchecked((int)0xFF000000) | rgb);
    }

    private static void Block(Graphics g, float x, float y, float width, float height, int color)
    {
        using (var brush = new SolidBrush(ColorOf(color))) g.FillRectangle(brush, x, y, width, height);
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
