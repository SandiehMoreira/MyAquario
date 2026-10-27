using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MyAquario
{
    /// <summary>Textos por cima do aquário: status (ao passar o mouse), avisos e selo de "aquário maior".</summary>
    static class Overlay
    {
        static Font font, bold;
        static float fontScale;

        public static RectangleF BadgeRect(World w, float s) => new RectangleF((w.W - 22) * s, 3 * s, 10 * s, 10 * s);

        static void EnsureFonts(float s)
        {
            if (font != null && Math.Abs(fontScale - s) < 0.01f) return;
            font?.Dispose();
            bold?.Dispose();
            font = new Font("Segoe UI", 6.2f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            bold = new Font("Segoe UI", 6.4f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            fontScale = s;
        }

        public static void Draw(Graphics g, World w, float s, bool hover, bool upgrade)
        {
            EnsureFonts(s);
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (upgrade) DrawBadge(g, w, s);
            if (w.ToastTime > 0 && !string.IsNullOrEmpty(w.ToastText)) DrawToast(g, w, s);
            if (hover) DrawStatus(g, w, s);

            g.Restore(st);
        }

        static void DrawBadge(Graphics g, World w, float s)
        {
            var r = BadgeRect(w, s);
            float pulse = 0.5f + 0.5f * M.Sin(w.Time * 4);
            using (var glow = new SolidBrush(Color.FromArgb((int)(80 * pulse), 255, 210, 60)))
                g.FillEllipse(glow, RectangleF.Inflate(r, 2 * s, 2 * s));
            using (var b = new SolidBrush(Color.FromArgb(255, 200, 50))) g.FillEllipse(b, r);
            float cx = r.X + r.Width / 2, u = r.Width / 10;
            using var arrow = new SolidBrush(Color.White);
            g.FillPolygon(arrow, new[]
            {
                new PointF(cx, r.Y + 2 * u), new PointF(cx + 3 * u, r.Y + 5 * u), new PointF(cx + 1.2f * u, r.Y + 5 * u),
                new PointF(cx + 1.2f * u, r.Y + 8 * u), new PointF(cx - 1.2f * u, r.Y + 8 * u), new PointF(cx - 1.2f * u, r.Y + 5 * u),
                new PointF(cx - 3 * u, r.Y + 5 * u),
            });
        }

        static void DrawToast(Graphics g, World w, float s)
        {
            float a = Math.Min(1, w.ToastTime / 0.4f);
            float maxW = w.Water.Width * s - 12 * s;
            var size = g.MeasureString(w.ToastText, font, (int)maxW);
            var r = new RectangleF(w.Water.Left * s + (w.Water.Width * s - size.Width) / 2 - 4 * s, w.SurfaceY * s + 4 * s,
                size.Width + 8 * s, size.Height + 4 * s);
            using (var path = Shapes.RoundRect(r, 4 * s))
            using (var b = new SolidBrush(Color.FromArgb((int)(185 * a), 15, 25, 40)))
                g.FillPath(b, path);
            using var tb = new SolidBrush(Color.FromArgb((int)(255 * a), 255, 255, 255));
            g.DrawString(w.ToastText, font, tb, new RectangleF(r.X + 4 * s, r.Y + 2 * s, size.Width + 1, size.Height + 1));
        }

        static void DrawStatus(Graphics g, World w, float s)
        {
            var st = w.State;
            var next = Tanks.Next(st.TankLevel);
            float line = font.GetHeight(g) * 1.12f;
            float pad = 4 * s;
            float width = Math.Min(w.Water.Width - 8, 150) * s;
            float height = pad * 2 + line * 6;
            var r = new RectangleF(w.Water.Left * s + 4 * s, w.Water.Bottom * s - height - 3 * s, width, height);
            if (r.Top < w.SurfaceY * s) r.Y = w.SurfaceY * s;

            using (var path = Shapes.RoundRect(r, 4 * s))
            using (var b = new SolidBrush(Color.FromArgb(195, 12, 22, 36)))
                g.FillPath(b, path);

            using var white = new SolidBrush(Color.White);
            using var soft = new SolidBrush(Color.FromArgb(190, 210, 225, 240));
            float x = r.X + pad, y = r.Y + pad;
            g.DrawString($"{st.BettaName} está {Care.Mood(st)}", bold, white, x, y);
            y += line;
            g.DrawString($"{w.Def.Name} · {w.Def.Liters} L", font, soft, x, y);
            y += line;

            float labelW = 34 * s, barW = width - pad * 2 - labelW;
            Bar(g, "Barriga", 100 - st.Hunger, Color.FromArgb(120, 210, 90), x, y, labelW, barW, line, s, soft);
            y += line;
            Bar(g, "Água", st.Cleanliness, Color.FromArgb(80, 180, 240), x, y, labelW, barW, line, s, soft);
            y += line;
            Bar(g, "Humor", st.Happiness, Color.FromArgb(250, 140, 180), x, y, labelW, barW, line, s, soft);
            y += line;

            if (next != null)
            {
                double pct = Math.Min(100, st.CarePoints / next.PointsNeeded * 100);
                Bar(g, "Cuidado", pct, Color.FromArgb(255, 200, 60), x, y, labelW, barW, line, s, soft,
                    $"{Math.Floor(st.CarePoints)}/{next.PointsNeeded}");
            }
            else g.DrawString($"Cuidado: {Math.Floor(st.CarePoints)} · aquário máximo!", font, soft, x, y);
        }

        static void Bar(Graphics g, string label, double value, Color c, float x, float y, float labelW, float barW,
            float line, float s, Brush text, string right = null)
        {
            g.DrawString(label, font, text, x, y);
            float bh = line * 0.45f, by = y + (line - bh) / 2;
            var bg = new RectangleF(x + labelW, by, barW, bh);
            using (var p = Shapes.RoundRect(bg, bh / 2))
            using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillPath(b, p);
            float fw = (float)(barW * M.Clamp(value, 0, 100) / 100);
            if (fw > 1)
                using (var p = Shapes.RoundRect(new RectangleF(bg.X, by, fw, bh), bh / 2))
                using (var b = new SolidBrush(value < 25 ? Color.FromArgb(240, 80, 70) : c))
                    g.FillPath(b, p);
            if (right != null)
            {
                var sz = g.MeasureString(right, font);
                using var tb = new SolidBrush(Color.White);
                g.DrawString(right, font, tb, bg.Right - sz.Width - 2 * s, y);
            }
        }
    }
}
