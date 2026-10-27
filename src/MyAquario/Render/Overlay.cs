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

        static Font font, bold, small, smallBold;
        static float fontScale;
        static Bitmap measureBmp;
        static Graphics measureG;

        public static RectangleF BadgeRect(World w, float s) => new RectangleF((w.TankW - 22) * s, 3 * s, 10 * s, 10 * s);

        static void EnsureFonts(float s)
        {
            if (font != null && Math.Abs(fontScale - s) < 0.01f) return;
            font?.Dispose();
            bold?.Dispose();
            small?.Dispose();
            smallBold?.Dispose();
            font = new Font("Segoe UI", 6.2f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            bold = new Font("Segoe UI", 6.4f * s, FontStyle.Bold, GraphicsUnit.Pixel);
            small = new Font("Segoe UI", 5.2f * s, FontStyle.Regular, GraphicsUnit.Pixel);
            smallBold = new Font("Segoe UI", 5.4f * s, FontStyle.Bold, GraphicsUnit.Pixel);
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
        static float RingSize(float s) => 26 * s;
        static float SmallLine(Graphics g) => small.GetHeight(g) * 1.05f;
        static float StatusHeight(Graphics g, float s) =>
            4 * s + LineHeight(g) * 2 + 4 * s + RingSize(s) + 3 * s + SmallLine(g) * 2 + 4 * s;

        static readonly Color Green = Color.FromArgb(120, 210, 90), Yellow = Color.FromArgb(250, 200, 60),
            Red = Color.FromArgb(240, 80, 70), Blue = Color.FromArgb(80, 180, 240), Pink = Color.FromArgb(250, 140, 180),
            Gold = Color.FromArgb(255, 200, 60);

        static Color Level(double v, Color good) => v > 60 ? good : v > 30 ? Yellow : Red;

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
            y += line + 4 * s;

            // Quatro anéis: a volta completa é 100%.
            double fed = 100 - st.Hunger;
            double care = next == null ? 100 : Math.Min(100, st.CarePoints / next.PointsNeeded * 100);
            string careText = next == null ? "máximo!" : $"{Math.Floor(st.CarePoints)}/{next.PointsNeeded}";
            float slot = (width - pad * 2) / 4, ring = RingSize(s);
            var items = new (string Name, double Value, Color Color, string Text, Action<Graphics, PointF, float, Color> Icon)[]
            {
                ("Fome", fed, Level(fed, Green), $"{Math.Round(fed)}%", IconFood),
                ("Água", st.Cleanliness, Level(st.Cleanliness, Blue), $"{Math.Round(st.Cleanliness)}%", IconDrop),
                ("Humor", st.Happiness, Level(st.Happiness, Pink), $"{Math.Round(st.Happiness)}%", (gg, c, sz, col) => IconFace(gg, c, sz, st.Happiness)),
                ("Cuidado", care, Gold, careText, IconStar),
            };

            var center = new StringFormat { Alignment = StringAlignment.Center };
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                float cx = x + slot * i + slot / 2, cy = y + ring / 2;
                // Vermelho pisca de leve para chamar atenção
                float pulse = it.Color == Red ? 0.6f + 0.4f * M.Sin(w.Time * 5) : 1;
                Ring(g, new PointF(cx, cy), ring, 3.2f * s, it.Value, Col.Alpha(it.Color, (int)(255 * pulse)));
                it.Icon(g, new PointF(cx, cy), ring * 0.42f, it.Color);
                g.DrawString(it.Name, small, soft, new RectangleF(cx - slot / 2, y + ring + 3 * s, slot, SmallLine(g) + 2), center);
                g.DrawString(it.Text, smallBold, white, new RectangleF(cx - slot / 2, y + ring + 3 * s + SmallLine(g), slot, SmallLine(g) + 2), center);
            }
        }

        static void Ring(Graphics g, PointF c, float size, float thick, double value, Color color)
        {
            var rect = new RectangleF(c.X - size / 2 + thick / 2, c.Y - size / 2 + thick / 2, size - thick, size - thick);
            using (var bg = new Pen(Color.FromArgb(55, 255, 255, 255), thick)) g.DrawEllipse(bg, rect);
            float sweep = (float)(360 * M.Clamp(value, 0, 100) / 100);
            if (sweep < 1) return;
            using var p = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (sweep >= 359.5f) g.DrawEllipse(p, rect);
            else g.DrawArc(p, rect, -90, sweep); // começa no topo, sentido horário
        }

        // ---- Desenhos do meio dos anéis (size = raio útil) ----

        // Potinho com ração
        static void IconFood(Graphics g, PointF c, float size, Color color)
        {
            float u = size / 6;
            using (var bowl = new GraphicsPath())
            {
                bowl.AddArc(c.X - 5 * u, c.Y - 4 * u, 10 * u, 9 * u, 0, 180);
                bowl.CloseFigure();
                using var bb = new SolidBrush(Color.FromArgb(235, 240, 245));
                g.FillPath(bb, bowl);
            }
            using var food = new SolidBrush(Color.FromArgb(200, 120, 60));
            g.FillEllipse(food, c.X - 3.6f * u, c.Y - 2.2f * u, 2.4f * u, 2.2f * u);
            g.FillEllipse(food, c.X - 1.2f * u, c.Y - 3.0f * u, 2.4f * u, 2.2f * u);
            g.FillEllipse(food, c.X + 1.2f * u, c.Y - 2.2f * u, 2.4f * u, 2.2f * u);
            g.FillEllipse(food, c.X - 0.2f * u, c.Y - 1.4f * u, 2.2f * u, 2.0f * u);
        }

        // Gota d'água
        static void IconDrop(Graphics g, PointF c, float size, Color color)
        {
            float u = size / 6;
            using var p = new GraphicsPath();
            p.AddBezier(c.X, c.Y - 5.5f * u, c.X + 1.5f * u, c.Y - 3f * u, c.X + 4f * u, c.Y - 0.5f * u, c.X + 4f * u, c.Y + 1.8f * u);
            p.AddArc(c.X - 4f * u, c.Y - 2.2f * u, 8f * u, 8f * u, 0, 180);
            p.AddBezier(c.X - 4f * u, c.Y + 1.8f * u, c.X - 4f * u, c.Y - 0.5f * u, c.X - 1.5f * u, c.Y - 3f * u, c.X, c.Y - 5.5f * u);
            using (var b = new SolidBrush(Color.FromArgb(110, 200, 250))) g.FillPath(b, p);
            using var shine = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            g.FillEllipse(shine, c.X - 2.2f * u, c.Y + 0.2f * u, 1.4f * u, 2.2f * u);
        }

        // Carinha: sorri, fica séria ou triste conforme o humor
        static void IconFace(Graphics g, PointF c, float size, double happiness)
        {
            float u = size / 6, rr = 5 * u;
            using (var face = new SolidBrush(Color.FromArgb(255, 215, 70))) g.FillEllipse(face, c.X - rr, c.Y - rr, rr * 2, rr * 2);
            using var dark = new SolidBrush(Color.FromArgb(70, 45, 20));
            g.FillEllipse(dark, c.X - 2.4f * u, c.Y - 2.2f * u, 1.4f * u, 1.8f * u);
            g.FillEllipse(dark, c.X + 1.0f * u, c.Y - 2.2f * u, 1.4f * u, 1.8f * u);
            using var mouth = new Pen(Color.FromArgb(70, 45, 20), 0.9f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float my = c.Y + 1.6f * u;
            if (happiness >= 60) g.DrawArc(mouth, c.X - 2.6f * u, my - 2.2f * u, 5.2f * u, 3.6f * u, 20, 140);   // sorriso
            else if (happiness >= 30) g.DrawLine(mouth, c.X - 2f * u, my + 0.6f * u, c.X + 2f * u, my + 0.6f * u); // sério
            else g.DrawArc(mouth, c.X - 2.6f * u, my, 5.2f * u, 3.6f * u, 200, 140);                             // triste
        }

        // Estrela
        static void IconStar(Graphics g, PointF c, float size, Color color)
        {
            float outer = size * 0.85f, inner = outer * 0.45f;
            var pts = new PointF[10];
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5;
                float rad = i % 2 == 0 ? outer : inner;
                pts[i] = new PointF(c.X + (float)Math.Cos(a) * rad, c.Y + (float)Math.Sin(a) * rad);
            }
            using var b = new SolidBrush(Gold);
            g.FillPolygon(b, pts);
        }
    }
}
