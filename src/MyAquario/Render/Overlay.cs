using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace MyAquario
{
    /// <summary>
    /// Selo de "aquário maior" (dentro do aquário) e o painel de informações: status ao passar o mouse
    /// e avisos. O painel é desenhado na InfoWindow, FORA do aquário, para não tapar os peixes.
    /// </summary>
    static class Overlay
    {
        const float InfoWidth = 160; // em unidades de mundo (multiplica pela escala)

        static Font font, bold;
        static float fontScale;
        static Bitmap measureBmp;
        static Graphics measureG;

        public static RectangleF BadgeRect(World w, float s) => new RectangleF((w.TankW - 22) * s, 3 * s, 10 * s, 10 * s);

        static void EnsureFonts(float s)
        {
            if (font != null && Math.Abs(fontScale - s) < 0.01f) return;
            font?.Dispose();
            bold?.Dispose();
            font = new Font("Segoe UI", 6.2f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            bold = new Font("Segoe UI", 6.4f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            fontScale = s;
        }

        static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        // ---- Dentro do aquário: só o selo dourado ----

        public static void Draw(Graphics g, World w, float s, bool upgrade)
        {
            if (!upgrade) return;
            var st = g.Save();
            Setup(g);
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
            g.Restore(st);
        }

        // ---- Fora do aquário: painel de informações ----

        public static bool HasToast(World w) => w.ToastTime > 0 && !string.IsNullOrEmpty(w.ToastText);

        public static Size InfoSize(World w, float s, bool status, bool toast)
        {
            EnsureFonts(s);
            if (measureG == null)
            {
                measureBmp = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);
                measureG = Graphics.FromImage(measureBmp);
                Setup(measureG);
            }
            float width = InfoWidth * s, h = 0;
            if (toast) h += ToastHeight(measureG, w, s, width);
            if (status) h += (h > 0 ? 4 * s : 0) + StatusHeight(measureG, s);
            return new Size((int)Math.Ceiling(width) + 1, (int)Math.Ceiling(h) + 1);
        }

        public static void DrawInfo(Graphics g, World w, float s, bool status, bool toast)
        {
            EnsureFonts(s);
            var st = g.Save();
            Setup(g);
            float width = InfoWidth * s, y = 0;
            if (toast) y += DrawToast(g, w, s, width) + 4 * s;
            if (status) DrawStatus(g, w, s, width, y);
            g.Restore(st);
        }

        static float ToastHeight(Graphics g, World w, float s, float width) =>
            g.MeasureString(w.ToastText, font, (int)(width - 12 * s)).Height + 8 * s;

        static float DrawToast(Graphics g, World w, float s, float width)
        {
            float a = Math.Min(1, w.ToastTime / 0.4f);
            float h = ToastHeight(g, w, s, width);
            var r = new RectangleF(0, 0, width, h);
            using (var path = Shapes.RoundRect(r, 4 * s))
            using (var b = new SolidBrush(Color.FromArgb((int)(215 * a), 15, 25, 40)))
                g.FillPath(b, path);
            using var tb = new SolidBrush(Color.FromArgb((int)(255 * a), 255, 255, 255));
            g.DrawString(w.ToastText, font, tb, new RectangleF(6 * s, 4 * s, width - 12 * s, h - 6 * s));
            return h;
        }

        static float LineHeight(Graphics g) => font.GetHeight(g) * 1.15f;
        static float StatusHeight(Graphics g, float s) => 8 * s + LineHeight(g) * 6;

        static void DrawStatus(Graphics g, World w, float s, float width, float top)
        {
            var st = w.State;
            var next = Tanks.Next(st.TankLevel);
            float line = LineHeight(g), pad = 4 * s;
            var r = new RectangleF(0, top, width, StatusHeight(g, s));

            using (var path = Shapes.RoundRect(r, 4 * s))
            using (var b = new SolidBrush(Color.FromArgb(225, 12, 22, 36)))
            using (var edge = new Pen(Color.FromArgb(90, 63, 198, 255), 1))
            {
                g.FillPath(b, path);
                g.DrawPath(edge, path);
            }

            using var white = new SolidBrush(Color.White);
            using var soft = new SolidBrush(Color.FromArgb(190, 210, 225, 240));
            float x = r.X + pad, y = r.Y + pad;
            g.DrawString($"{st.BettaName} está {Care.Mood(st)}", bold, white, x, y);
            y += line;
            g.DrawString($"{w.Def.Name} · {w.Def.Liters} L", font, soft, x, y);
            y += line;

            float labelW = 34 * s, barW = width - pad * 2 - labelW;
            // Fome: barra cheia e verde = alimentado; esvaziando e ficando vermelha = hora de comer.
            double fed = 100 - st.Hunger;
            var hungerColor = fed > 60 ? Color.FromArgb(120, 210, 90)
                : fed > 30 ? Color.FromArgb(250, 200, 60) : Color.FromArgb(240, 80, 70);
            Bar(g, "Fome", fed, hungerColor, x, y, labelW, barW, line, s, soft);
            y += line;
            Bar(g, "Água", st.Cleanliness, st.Cleanliness < 25 ? Color.FromArgb(240, 80, 70) : Color.FromArgb(80, 180, 240),
                x, y, labelW, barW, line, s, soft);
            y += line;
            Bar(g, "Humor", st.Happiness, st.Happiness < 25 ? Color.FromArgb(240, 80, 70) : Color.FromArgb(250, 140, 180),
                x, y, labelW, barW, line, s, soft);
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
            float rightW = 0;
            if (right != null)
            {
                rightW = g.MeasureString(right, font).Width + 2 * s;
                barW -= rightW;
                g.DrawString(right, font, text, x + labelW + barW + 2 * s, y);
            }
            float bh = line * 0.45f, by = y + (line - bh) / 2;
            var bg = new RectangleF(x + labelW, by, barW, bh);
            using (var p = Shapes.RoundRect(bg, bh / 2))
            using (var b = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                g.FillPath(b, p);
            float fw = (float)(barW * M.Clamp(value, 0, 100) / 100);
            if (fw > 1)
                using (var p = Shapes.RoundRect(new RectangleF(bg.X, by, fw, bh), bh / 2))
                using (var b = new SolidBrush(c))
                    g.FillPath(b, p);
        }
    }
}
