using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

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
            foreach (var b in fishSprites.Values) b?.Dispose();
            foreach (var b in paleSprites.Values) b?.Dispose();
            fishSprites.Clear();
            paleSprites.Clear();
            base.Release();
        }

        // ---- Peixes a partir de imagens (opcional no pacote) ----
        // fish_betta_<0..3>.png (uma por cor) ou fish_betta.png, fish_corydora.png, fish_rasbora.png, fish_neon.png
        // Peixe de lado, cabeça para a DIREITA, fundo transparente, recortado rente.

        readonly Dictionary<string, Bitmap> fishSprites = new Dictionary<string, Bitmap>();
        readonly Dictionary<string, Bitmap> paleSprites = new Dictionary<string, Bitmap>();

        Bitmap FishBitmap(Fish f, bool sad)
        {
            string key = f.Sp == Species.Betta ? "betta_" + f.Variant : f.Sp.ToString().ToLowerInvariant();
            if (!fishSprites.TryGetValue(key, out var bmp))
            {
                bmp = f.Sp == Species.Betta
                    ? LoadPng($"fish_betta_{f.Variant}.png") ?? LoadPng("fish_betta.png")
                    : LoadPng($"fish_{key}.png");
                fishSprites[key] = bmp; // guarda null também, para não procurar o arquivo todo quadro
            }
            if (bmp == null || !sad) return bmp;
            if (!paleSprites.TryGetValue(key, out var pale))
            {
                pale = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppPArgb);
                using var g = Graphics.FromImage(pale);
                using var ia = new ImageAttributes();
                const float a = 0.55f, r = 0.3f * a, gr = 0.59f * a, b = 0.11f * a, k = 1 - a;
                ia.SetColorMatrix(new ColorMatrix(new[]
                {
                    new[] { r + k, r, r, 0, 0 },
                    new[] { gr, gr + k, gr, 0, 0 },
                    new[] { b, b, b + k, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 },
                }));
                g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
                paleSprites[key] = pale;
            }
            return pale;
        }

        protected override bool DrawFishSprite(Graphics g, Fish f, float n, bool sad)
        {
            var bmp = FishBitmap(f, sad);
            if (bmp == null) return false;

            float len = f.Len * (f.Sp == Species.Betta ? 1.2f : 1.1f);
            float k = len / bmp.Width, h = bmp.Height * k, x0 = -len * 0.5f;
            const int N = 16;
            float sw = bmp.Width / (float)N;

            // Ondulação: a cauda balança mais, a cabeça quase nada.
            float Wave(float u)
            {
                float amp = (1 - u) * (1 - u) * f.Len * 0.09f;
                return M.Sin(f.Phase * 1.3f - u * 3.2f) * amp;
            }

            using var ia = new ImageAttributes();
            ia.SetColorMatrix(PixelRenderer.NightMatrix(n));
            ia.SetWrapMode(WrapMode.TileFlipXY); // evita linhas nas emendas das fatias
            for (int i = 0; i < N; i++)
            {
                float u0 = i / (float)N, u1 = (i + 1) / (float)N;
                float xa = x0 + i * sw * k, xb = x0 + (i + 1) * sw * k + 0.3f;
                float ya = -h / 2 + Wave(u0), yb = -h / 2 + Wave(u1);
                // Cada fatia vira um paralelogramo que encosta na próxima: o corpo dobra sem rasgar.
                var dest = new[] { new PointF(xa, ya), new PointF(xb, yb), new PointF(xa, ya + h) };
                g.DrawImage(bmp, dest, new RectangleF(i * sw, 0, sw, bmp.Height), GraphicsUnit.Pixel, ia);
            }
            return true;
        }

        protected override float RayStrength(World w) => 0.8f + 0.1f * w.State.TankLevel;

        protected override void DrawDepthFog(Graphics g, World w, float n, float dirt)
        {
            var c = Col.Night(Col.Lerp(Color.FromArgb(60, 135, 185), Color.FromArgb(80, 100, 60), dirt * 0.5f), n);
            using var b = new SolidBrush(Color.FromArgb(35, c));
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
            if (causticBrushes == null || w.Night >= 0.9f) return;
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
