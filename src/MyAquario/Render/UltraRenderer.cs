using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MyAquario
{
    /// <summary>
    /// Gráfico Ultra: sem contornos, texturas HD, cáusticas (luz dançando no fundo),
    /// raios de luz, névoa de profundidade, sombras e partículas.
    /// </summary>
    sealed class UltraRenderer : CartoonRenderer
    {
        public override Tier Tier => Tier.Ultra;
        public override int Fps => 40;
        protected override bool Realistic => true;
        protected override string BackdropFile => "backdrop_hd.png";
        protected override string GravelFile => "gravel_hd.png";

        const int CausticFrames = 16;
        Bitmap[] caustics;
        TextureBrush[] causticBrushes;

        public UltraRenderer(string packDir) : base(packDir) { }

        protected override void Load()
        {
            base.Load();
            caustics = new Bitmap[CausticFrames];
            causticBrushes = new TextureBrush[CausticFrames];
            for (int i = 0; i < CausticFrames; i++)
            {
                caustics[i] = LoadPng($"caustics_{i:00}.png");
                if (caustics[i] != null) causticBrushes[i] = new TextureBrush(caustics[i], WrapMode.Tile);
            }
        }

        public override void Release()
        {
            if (causticBrushes != null) foreach (var b in causticBrushes) b?.Dispose();
            if (caustics != null) foreach (var b in caustics) b?.Dispose();
            causticBrushes = null;
            caustics = null;
            base.Release();
        }

        protected override void DrawLightRays(Graphics g, World w, float n)
        {
            if (n >= 1) return;
            int a = (int)(42 * (1 - n));
            float top = w.SurfaceY, bot = w.GravelTop;
            using var b = new LinearGradientBrush(new RectangleF(0, top - 1, 1, bot - top + 2),
                Color.FromArgb(a, 255, 250, 220), Color.FromArgb(0, 255, 250, 220), 90f);
            for (int i = 0; i < 5; i++)
            {
                float x = w.Water.Left + w.Water.Width * (0.08f + 0.2f * i) + M.Sin(w.Time * 0.2f + i * 1.7f) * 6;
                g.FillPolygon(b, new[]
                {
                    new PointF(x - 3, top), new PointF(x + 5, top),
                    new PointF(x + 20, bot), new PointF(x + 3, bot),
                });
            }
        }

        protected override void DrawDepthFog(Graphics g, World w, float n, float dirt)
        {
            var c = Col.Night(Col.Lerp(Color.FromArgb(40, 110, 160), Color.FromArgb(80, 100, 60), dirt * 0.5f), n);
            using var b = new SolidBrush(Color.FromArgb(55, c));
            g.FillRectangle(b, RectangleF.FromLTRB(w.Water.Left, w.SurfaceY, w.Water.Right, w.GravelTop + 2));
        }

        protected override void DrawShadows(Graphics g, World w, float n)
        {
            float span = w.GravelTop - w.SurfaceY;
            foreach (var f in w.Fish)
            {
                float near = 1 - M.Clamp((w.GravelTop - f.Y) / span, 0, 1);
                int a = (int)(70 * near * (1 - n * 0.7f));
                if (a < 4) continue;
                float sw = f.Len * (0.5f + 0.3f * (1 - near));
                using var b = new SolidBrush(Color.FromArgb(a, 0, 10, 20));
                g.FillEllipse(b, f.X - sw / 2, w.GravelTop, sw, 2.5f);
            }
        }

        protected override void DrawParticles(Graphics g, World w, float n)
        {
            using var b = new SolidBrush(Color.FromArgb((int)(70 * (1 - n * 0.6f)), 230, 240, 230));
            for (int i = 0; i < 28; i++)
            {
                float x = w.Water.Left + M.Frac(i * 0.618f + w.Time * 0.004f * (1 + i % 3)) * w.Water.Width;
                float y = w.SurfaceY + 3 + M.Frac(i * 0.37f + w.Time * 0.006f * (1 + i % 2)) * (w.GravelTop - w.SurfaceY - 3);
                float r = 0.25f + (i % 3) * 0.12f;
                g.FillEllipse(b, x - r, y - r, r * 2, r * 2);
            }
        }

        protected override void DrawCaustics(Graphics g, World w, float n)
        {
            if (causticBrushes == null || n >= 0.9f) return;
            var brush = causticBrushes[(int)(w.Time * 10) % CausticFrames];
            if (brush == null) return;
            // Luz dançando sobre o cascalho
            brush.ResetTransform();
            brush.TranslateTransform(w.Water.Left + w.Time * 1.5f, w.GravelTop);
            brush.ScaleTransform(0.16f, 0.16f);
            g.FillRectangle(brush, RectangleF.FromLTRB(w.Water.Left, w.GravelTop - 1, w.Water.Right, w.Water.Bottom));
        }
    }
}
