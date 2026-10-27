using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MyAquario
{
    /// <summary>
    /// Gráfico Normal: 2D liso estilo cartoon, com contorno. Usa as texturas do pacote "normal".
    /// O UltraRenderer herda daqui e liga os efeitos realistas.
    /// </summary>
    class CartoonRenderer : IRenderer
    {
        public virtual Tier Tier => Tier.Normal;
        public virtual int Fps => 30;
        protected virtual bool Realistic => false;
        protected virtual string BackdropFile => "backdrop.png";
        protected virtual string GravelFile => "gravel.png";

        protected readonly string PackDir;
        protected Bitmap Backdrop, GravelTex;
        bool loaded;

        public CartoonRenderer(string packDir) { PackDir = packDir; }

        protected Bitmap LoadPng(string name)
        {
            var p = Path.Combine(PackDir, name);
            if (!File.Exists(p)) return null;
            try
            {
                // Lê para a memória para não travar o arquivo (o pacote pode ser apagado).
                using var fs = File.OpenRead(p);
                using var img = Image.FromStream(fs);
                var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                return bmp;
            }
            catch (Exception ex) { Log.Error(ex); return null; }
        }

        protected virtual void Load()
        {
            Backdrop = LoadPng(BackdropFile);
            GravelTex = LoadPng(GravelFile);
        }

        public virtual void Release()
        {
            Backdrop?.Dispose(); Backdrop = null;
            GravelTex?.Dispose(); GravelTex = null;
            loaded = false;
        }

        public void Dispose() => Release();

        public void Render(Graphics g, World w, float s)
        {
            if (!loaded) { Load(); loaded = true; }
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = Realistic ? InterpolationMode.HighQualityBilinear : InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(s, s);

            float n = w.Night, dirt = WorldLook.Dirt(w);
            DrawStand(g, w);
            DrawLid(g, w, n);

            g.SetClip(w.Glass);
            DrawWater(g, w, n, dirt);
            DrawBackdrop(g, w, n);
            DrawLightRays(g, w, n);
            foreach (var p in w.Plants) if (!p.Front) DrawPlant(g, w, p, n, true);
            foreach (var d in w.Decor) DrawDecor(g, w, d, n);
            DrawDepthFog(g, w, n, dirt);
            DrawGravel(g, w, n);
            DrawShadows(g, w, n);
            if (w.HasAirstone) DrawAirstone(g, w, n);
            foreach (var p in w.Plants) if (p.Front) DrawPlant(g, w, p, n, false);
            DrawPellets(g, w, n);
            bool sad = WorldLook.Sad(w);
            foreach (var f in w.Fish) DrawFish(g, f, n, sad);
            DrawBubbles(g, w, n);
            DrawParticles(g, w, n);
            DrawCaustics(g, w, n);
            DrawAlgae(g, w, dirt);
            if (n > 0)
                using (var b = new SolidBrush(Color.FromArgb((int)(n * 100), 8, 12, 40)))
                    g.FillRectangle(b, w.Glass);
            DrawCleanFx(g, w);
            DrawSurface(g, w, n);
            g.ResetClip();

            DrawGlass(g, w);
            g.Restore(st);
        }

        // ---- Móvel, tampa e vidro ----

        void DrawStand(Graphics g, World w)
        {
            var r = RectangleF.FromLTRB(1, w.Glass.Bottom - 1, w.W - 1, w.H - 1);
            using var path = Shapes.RoundRect(r, 2);
            using var b = new LinearGradientBrush(r, Color.FromArgb(140, 95, 60), Color.FromArgb(88, 56, 34), 90f);
            g.FillPath(b, path);
            using var hl = new Pen(Color.FromArgb(120, 255, 220, 180), 0.6f);
            g.DrawLine(hl, r.Left + 2, r.Top + 1, r.Right - 2, r.Top + 1);
            if (!Realistic) using (var o = new Pen(Color.FromArgb(160, 50, 30, 18), 0.7f)) g.DrawPath(o, path);
        }

        void DrawLid(Graphics g, World w, float n)
        {
            var r = RectangleF.FromLTRB(1, 5, w.W - 1, w.Glass.Top + 1);
            using var path = Shapes.RoundRect(r, 2);
            using var b = new LinearGradientBrush(r, Color.FromArgb(80, 86, 96), Color.FromArgb(40, 44, 52), 90f);
            g.FillPath(b, path);
            // LED da luminária
            var led = n < 0.5f ? Color.FromArgb(120, 255, 120) : Color.FromArgb(70, 120, 255);
            using (var lb = new SolidBrush(led)) g.FillEllipse(lb, w.W - 8, 7, 2, 2);
            if (n < 1)
            {
                var lr = new RectangleF(w.Glass.Left + 2, w.Glass.Top - 0.5f, w.Glass.Width - 4, 1.5f);
                using var lamp = new SolidBrush(Color.FromArgb((int)(230 * (1 - n)), 255, 240, 190));
                g.FillRectangle(lamp, lr);
            }
            if (!Realistic) using (var o = new Pen(Color.FromArgb(160, 20, 22, 28), 0.7f)) g.DrawPath(o, path);
        }

        void DrawGlass(Graphics g, World w)
        {
            using var path = Shapes.RoundRect(w.Glass, 1.5f);
            using (var pen = new Pen(Color.FromArgb(200, 205, 238, 248), 1.2f)) g.DrawPath(pen, path);
            // Reflexo diagonal no vidro
            using var refl = new GraphicsPath();
            float x = w.Glass.Left + w.Glass.Width * 0.08f, t = w.Glass.Top + 2, bt = w.Glass.Bottom - 2;
            refl.AddPolygon(new[] { new PointF(x, t), new PointF(x + 7, t), new PointF(x + 2, bt), new PointF(x - 5, bt) });
            g.SetClip(w.Glass);
            using (var rb = new SolidBrush(Color.FromArgb(Realistic ? 18 : 26, 255, 255, 255))) g.FillPath(rb, refl);
            g.ResetClip();
        }

        // ---- Água e fundo ----

        void DrawWater(Graphics g, World w, float n, float dirt)
        {
            var top = Color.FromArgb(120, 205, 235);
            var bot = Realistic ? Color.FromArgb(18, 70, 120) : Color.FromArgb(28, 100, 165);
            top = Col.Night(Col.Lerp(top, Color.FromArgb(130, 150, 90), dirt * 0.5f), n);
            bot = Col.Night(Col.Lerp(bot, Color.FromArgb(70, 90, 50), dirt * 0.5f), n);
            var wr = RectangleF.FromLTRB(w.Water.Left, w.SurfaceY - 1, w.Water.Right, w.Water.Bottom + 1);
            using (var b = new LinearGradientBrush(wr, top, bot, 90f)) g.FillRectangle(b, w.Water);
            using (var air = new SolidBrush(Color.FromArgb(70, 220, 240, 250)))
                g.FillRectangle(air, RectangleF.FromLTRB(w.Glass.Left, w.Glass.Top, w.Glass.Right, w.SurfaceY));
        }

        void DrawBackdrop(Graphics g, World w, float n)
        {
            if (Backdrop == null) return;
            var dest = RectangleF.FromLTRB(w.Water.Left, w.SurfaceY + (w.GravelTop - w.SurfaceY) * 0.15f, w.Water.Right, w.GravelTop + 3);
            using var ia = new ImageAttributes();
            ia.SetColorMatrix(PixelRenderer.NightMatrix(n, 0.9f));
            g.DrawImage(Backdrop, new[] { dest.Location, new PointF(dest.Right, dest.Top), new PointF(dest.Left, dest.Bottom) },
                new RectangleF(0, 0, Backdrop.Width, Backdrop.Height), GraphicsUnit.Pixel, ia);
        }

        void DrawGravel(Graphics g, World w, float n)
        {
            using var path = new GraphicsPath();
            var pts = new System.Collections.Generic.List<PointF>();
            for (float x = w.Water.Left - 2; x <= w.Water.Right + 2; x += 3)
                pts.Add(new PointF(x, w.GravelTop + M.Sin(x * 0.15f) * 1.2f + M.Sin(x * 0.37f) * 0.6f));
            pts.Add(new PointF(w.Water.Right + 2, w.Water.Bottom + 2));
            pts.Add(new PointF(w.Water.Left - 2, w.Water.Bottom + 2));
            path.AddPolygon(pts.ToArray());

            if (GravelTex != null)
            {
                using var tb = new TextureBrush(GravelTex, WrapMode.Tile);
                float k = (w.Water.Bottom - w.GravelTop + 4) / GravelTex.Height;
                tb.TranslateTransform(w.Water.Left, w.GravelTop - 2);
                tb.ScaleTransform(k, k);
                g.FillPath(tb, path);
            }
            else using (var sb = new SolidBrush(Color.FromArgb(180, 150, 105))) g.FillPath(sb, path);

            if (n > 0) using (var nb = new SolidBrush(Color.FromArgb((int)(n * 150), 10, 20, 50))) g.FillPath(nb, path);
            using var edge = new Pen(Color.FromArgb(Realistic ? 40 : 90, 255, 240, 200), 0.6f);
            g.DrawLines(edge, pts.GetRange(0, pts.Count - 2).ToArray());
        }

        void DrawAirstone(Graphics g, World w, float n)
        {
            var a = w.Airstone;
            using (var tube = new Pen(Color.FromArgb(70, 230, 240, 245), 0.8f)) g.DrawLine(tube, a.X + 2, a.Y, a.X + 2, w.Glass.Top);
            using var b = new SolidBrush(Col.Night(Color.FromArgb(125, 125, 130), n));
            using var path = Shapes.RoundRect(new RectangleF(a.X - 3, a.Y - 1.5f, 6, 3), 1.2f);
            g.FillPath(b, path);
        }

        // ---- Plantas e decoração ----

        static readonly Color[] LeafColors = { Color.FromArgb(70, 170, 80), Color.FromArgb(50, 145, 70), Color.FromArgb(95, 185, 85) };

        void DrawPlant(Graphics g, World w, Plant p, float n, bool back)
        {
            var baseC = LeafColors[p.Shade % 3];
            if (back) baseC = Col.Shade(baseC, 0.85f);
            var fill = Col.Night(baseC, n);
            using var b = new SolidBrush(fill);
            using var o = new Pen(Col.Night(Color.FromArgb(150, 25, 80, 40), n), 0.5f);
            using var vein = new Pen(Col.Night(Col.Alpha(Col.Light(baseC, 0.35f), 160), n), 0.35f);
            float gy = w.GravelTop + 2;

            if (p.Kind == 0)
            {
                // Valisnéria: fitas que balançam
                for (int bl = 0; bl < p.Blades; bl++)
                {
                    float h = p.H * (0.7f + 0.3f * ((bl * 37) % 10) / 10f);
                    float x0 = p.X + (bl - p.Blades / 2f) * 1.8f, lean = (bl - p.Blades / 2f) * 0.25f;
                    const int N = 9;
                    var left = new PointF[N + 1];
                    var right = new PointF[N + 1];
                    for (int j = 0; j <= N; j++)
                    {
                        float t = j / (float)N;
                        float x = x0 + lean * t * h * 0.15f + M.Sin(w.Time * 1.1f + p.Phase + bl * 0.6f - j * 0.25f) * (float)Math.Pow(t, 1.5) * 4;
                        float y = gy - h * t;
                        float half = M.Lerp(1.2f, 0.15f, t);
                        left[j] = new PointF(x - half, y);
                        right[N - j] = new PointF(x + half, y);
                    }
                    using var path = new GraphicsPath();
                    path.AddLines(left);
                    path.AddLines(right);
                    path.CloseFigure();
                    using (var lb = new SolidBrush(Col.Night(bl % 2 == 0 ? baseC : Col.Shade(baseC, 0.85f), n))) g.FillPath(lb, path);
                    if (!Realistic) g.DrawPath(o, path);
                }
                return;
            }

            bool anubias = p.Kind == 1;
            for (int bl = 0; bl < p.Blades; bl++)
            {
                float spread = anubias ? 0.5f : 0.32f;
                float ang = -90 + (bl - (p.Blades - 1) / 2f) * spread * 57.3f + M.Sin(w.Time * 0.8f + p.Phase + bl) * (anubias ? 2 : 4);
                float stem = anubias ? 4 + (bl % 2) * 2 : p.H * (0.45f + 0.15f * ((bl * 13) % 7) / 7f);
                float leafL = anubias ? 9 : p.H * 0.45f, leafW = anubias ? 5 : 4;

                var st = g.Save();
                g.TranslateTransform(p.X, gy);
                g.RotateTransform(ang);
                using (var sp = new Pen(Col.Night(Col.Shade(baseC, 0.75f), n), 0.6f)) g.DrawLine(sp, 0, 0, stem, 0);
                var leaf = new RectangleF(stem - 1, -leafW / 2, leafL, leafW);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(leaf);
                    using var lgb = new LinearGradientBrush(new RectangleF(leaf.X, leaf.Y - 0.5f, leaf.Width, leaf.Height + 1),
                        Col.Night(Col.Light(baseC, Realistic ? 0.1f : 0.2f), n), fill, 90f);
                    g.FillPath(lgb, path);
                    if (!Realistic) g.DrawPath(o, path);
                }
                g.DrawLine(vein, stem, 0, stem + leafL - 1, 0);
                g.Restore(st);
            }
        }

        void DrawDecor(Graphics g, World w, Decor d, float n)
        {
            if (d.Kind == 1)
            {
                // Tronco
                float y = w.GravelTop + 1;
                var dark = Col.Night(Color.FromArgb(70, 45, 28), n);
                var wood = Col.Night(Color.FromArgb(120, 82, 50), n);
                void Branch(float x1, float y1, float x2, float y2, float cx, float cy, float width)
                {
                    using var path = new GraphicsPath();
                    path.AddBezier(x1, y1, cx, cy, cx, cy, x2, y2);
                    using (var pd = new Pen(dark, width + (Realistic ? 0.4f : 1.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pd, path);
                    using (var pw = new Pen(wood, width) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawPath(pw, path);
                }
                Branch(d.X - d.W / 2, y, d.X + d.W / 2, y - d.H * 0.4f, d.X, y - d.H * 0.1f, 3.5f);
                Branch(d.X - d.W * 0.05f, y - d.H * 0.15f, d.X - d.W * 0.25f, y - d.H, d.X - d.W * 0.05f, y - d.H * 0.6f, 2.2f);
                Branch(d.X + d.W * 0.2f, y - d.H * 0.3f, d.X + d.W * 0.32f, y - d.H * 0.85f, d.X + d.W * 0.3f, y - d.H * 0.5f, 1.5f);
                return;
            }

            var c = d.Shade switch { 0 => Color.FromArgb(130, 130, 138), 1 => Color.FromArgb(108, 102, 96), _ => Color.FromArgb(150, 140, 125) };
            var r = new RectangleF(d.X - d.W / 2, w.GravelTop - d.H, d.W, d.H * 1.7f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using var b = new LinearGradientBrush(r, Col.Night(Col.Light(c, 0.25f), n), Col.Night(Col.Shade(c, 0.6f), n), 60f);
                g.FillPath(b, path);
                if (!Realistic) using (var o = new Pen(Col.Night(Color.FromArgb(160, 50, 50, 55), n), 0.6f)) g.DrawPath(o, path);
            }
            using var hl = new SolidBrush(Color.FromArgb((int)(70 * (1 - n)), 255, 255, 255));
            g.FillEllipse(hl, r.Left + r.Width * 0.22f, r.Top + 1.5f, r.Width * 0.3f, r.Height * 0.15f);
        }

        // ---- Peixes ----

        void DrawFish(Graphics g, Fish f, float n, bool sad)
        {
            var st = g.Save();
            float face = f.Facing;
            if (Math.Abs(face) < 0.2f) face = face < 0 ? -0.2f : 0.2f;
            g.TranslateTransform(f.X, f.Y);
            float tilt = M.Clamp(f.Vy * 0.025f, -0.35f, 0.35f) * Math.Sign(face);
            g.RotateTransform(tilt * 57.3f);
            g.ScaleTransform(face, 1);

            if (DrawFishSprite(g, f, n, sad))
            {
                g.Restore(st);
                return;
            }

            switch (f.Sp)
            {
                case Species.Betta:
                    DrawBetta(g, f, Mood(WorldLook.BettaColors[f.Variant % WorldLook.BettaColors.Length], sad, n));
                    break;
                case Species.Corydora:
                    DrawSmallFish(g, f, Mood(Color.FromArgb(185, 165, 130), sad, n), 0.24f, n, sad);
                    break;
                case Species.Rasbora:
                    DrawSmallFish(g, f, Mood(Color.FromArgb(235, 140, 80), sad, n), 0.21f, n, sad);
                    break;
                default:
                    DrawSmallFish(g, f, Mood(Color.FromArgb(190, 200, 212), sad, n), 0.17f, n, sad);
                    break;
            }
            g.Restore(st);
        }

        static Color Mood(Color c, bool sad, float n) => Col.Night(sad ? Col.Desaturate(c, 0.55f) : c, n);

        Pen OutlinePen(Color main) => new Pen(Color.FromArgb(170, Col.Shade(main, 0.45f)), 0.55f);

        void DrawBetta(Graphics g, Fish f, Color main)
        {
            float L = f.Len, t = f.Phase;
            float wv = M.Sin(t) * L * 0.06f, wv2 = M.Sin(t - 1.2f) * L * 0.08f;
            using var outline = OutlinePen(main);
            float bx = -L * 0.22f;

            // Cauda grande em leque
            using (var p = new GraphicsPath())
            {
                p.AddBezier(bx, -L * 0.08f, bx - L * 0.25f, -L * 0.42f + wv, bx - L * 0.62f, -L * 0.40f + wv2, bx - L * 0.62f, wv2 * 0.5f);
                p.AddBezier(bx - L * 0.62f, wv2 * 0.5f, bx - L * 0.62f, L * 0.40f + wv2, bx - L * 0.25f, L * 0.42f + wv, bx, L * 0.08f);
                p.CloseFigure();
                using var b = new LinearGradientBrush(new RectangleF(bx - L * 0.66f, -L * 0.45f, L * 0.68f, L * 0.9f),
                    Col.Alpha(Col.Light(main, 0.25f), Realistic ? 150 : 200), Col.Shade(main, 0.75f), 0f);
                g.FillPath(b, p);
                using var rays = new Pen(Col.Alpha(Col.Shade(main, 0.55f), Realistic ? 140 : 100), 0.35f);
                var clip = g.Save();
                g.SetClip(p, CombineMode.Intersect);
                for (int i = -3; i <= 3; i++)
                {
                    float a = i / 3f;
                    g.DrawLine(rays, bx, a * L * 0.05f, bx - L * 0.62f, a * L * 0.42f + wv2 * (0.5f + 0.5f * Math.Abs(a)));
                }
                g.Restore(clip);
                if (!Realistic) g.DrawPath(outline, p);
            }

            using (var finBrush = new SolidBrush(Col.Alpha(Col.Shade(main, 0.8f), Realistic ? 170 : 220)))
            {
                // Dorsal
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(L * 0.05f, -L * 0.13f, -L * 0.1f, -L * 0.35f + wv, -L * 0.32f, -L * 0.32f + wv2, -L * 0.3f, -L * 0.08f);
                    p.CloseFigure();
                    g.FillPath(finBrush, p);
                    if (!Realistic) g.DrawPath(outline, p);
                }
                // Anal (longa, embaixo)
                using (var p = new GraphicsPath())
                {
                    p.AddBezier(L * 0.12f, L * 0.1f, 0, L * 0.42f + wv, -L * 0.3f, L * 0.45f + wv2, -L * 0.32f, L * 0.1f);
                    p.CloseFigure();
                    g.FillPath(finBrush, p);
                    if (!Realistic) g.DrawPath(outline, p);
                }
            }

            // Corpo
            using (var body = new GraphicsPath())
            {
                body.AddBezier(L * 0.32f, 0, L * 0.30f, -L * 0.15f, -L * 0.05f, -L * 0.17f, -L * 0.3f, -L * 0.06f);
                body.AddBezier(-L * 0.3f, -L * 0.06f, -L * 0.34f, 0, -L * 0.34f, 0, -L * 0.3f, L * 0.06f);
                body.AddBezier(-L * 0.3f, L * 0.06f, -L * 0.05f, L * 0.17f, L * 0.30f, L * 0.15f, L * 0.32f, 0);
                body.CloseFigure();
                using var b = new LinearGradientBrush(new RectangleF(-L * 0.35f, -L * 0.18f, L * 0.7f, L * 0.36f),
                    Col.Light(main, 0.25f), Col.Shade(main, 0.7f), 90f);
                g.FillPath(b, body);
                if (Realistic) DrawSheen(g, body, L, L * 0.17f);
                else g.DrawPath(outline, body);
            }

            // Nadadeira peitoral batendo
            var st = g.Save();
            g.TranslateTransform(L * 0.1f, L * 0.05f);
            g.RotateTransform(20 + M.Sin(t * 2.2f) * 25);
            using (var pb = new SolidBrush(Col.Alpha(Col.Light(main, 0.35f), 170))) g.FillEllipse(pb, -L * 0.12f, -L * 0.03f, L * 0.13f, L * 0.06f);
            g.Restore(st);

            DrawEye(g, L * 0.2f, -L * 0.03f, L * 0.055f);
        }

        void DrawSmallFish(Graphics g, Fish f, Color main, float depth, float n, bool sad)
        {
            float L = f.Len, t = f.Phase, hh = L * depth;
            float wv = M.Sin(t * 1.6f) * L * 0.1f;
            using var outline = OutlinePen(main);

            using (var tail = new GraphicsPath())
            {
                tail.AddBezier(-L * 0.30f, -L * 0.03f, -L * 0.40f, -L * 0.08f, -L * 0.50f, -L * 0.20f + wv, -L * 0.55f, -L * 0.22f + wv);
                tail.AddLine(-L * 0.55f, -L * 0.22f + wv, -L * 0.47f, wv);
                tail.AddLine(-L * 0.47f, wv, -L * 0.55f, L * 0.22f + wv);
                tail.AddBezier(-L * 0.55f, L * 0.22f + wv, -L * 0.50f, L * 0.20f + wv, -L * 0.40f, L * 0.08f, -L * 0.30f, L * 0.03f);
                tail.CloseFigure();
                using var tb = new SolidBrush(Col.Alpha(Col.Shade(main, 0.85f), f.Sp == Species.Neon ? 150 : 220));
                g.FillPath(tb, tail);
                if (!Realistic) g.DrawPath(outline, tail);
            }
            using (var dorsal = new GraphicsPath())
            {
                dorsal.AddPolygon(new[] { new PointF(L * 0.1f, -hh * 0.85f), new PointF(-L * 0.05f, -hh * 1.6f), new PointF(-L * 0.12f, -hh * 0.75f) });
                using var db = new SolidBrush(Col.Alpha(Col.Shade(main, 0.8f), 200));
                g.FillPath(db, dorsal);
            }

            using var body = new GraphicsPath();
            body.AddBezier(L * 0.5f, 0, L * 0.45f, -hh, -L * 0.1f, -hh * 1.1f, -L * 0.34f, 0);
            body.AddBezier(-L * 0.34f, 0, -L * 0.1f, hh * 1.1f, L * 0.45f, hh, L * 0.5f, 0);
            body.CloseFigure();
            using (var b = new LinearGradientBrush(new RectangleF(-L * 0.35f, -hh - 0.5f, L * 0.86f, hh * 2 + 1),
                Col.Light(main, 0.25f), Col.Shade(main, 0.75f), 90f))
                g.FillPath(b, body);

            // Desenhos de cada espécie, recortados no formato do corpo
            var st = g.Save();
            g.SetClip(body, CombineMode.Intersect);
            switch (f.Sp)
            {
                case Species.Corydora:
                    using (var sb = new SolidBrush(Col.Night(Color.FromArgb(150, 70, 60, 50), n)))
                        for (int i = 0; i < 6; i++)
                            g.FillEllipse(sb, -L * 0.25f + i * L * 0.11f, -hh * 0.5f + (i % 2) * hh * 0.5f, L * 0.06f, L * 0.06f);
                    break;
                case Species.Rasbora:
                    using (var kb = new SolidBrush(Col.Night(Color.FromArgb(35, 25, 35), n)))
                        g.FillPolygon(kb, new[] { new PointF(-L * 0.28f, -hh * 0.05f), new PointF(L * 0.05f, -hh * 0.65f), new PointF(L * 0.05f, hh * 0.55f), new PointF(-L * 0.28f, hh * 0.25f) });
                    break;
                case Species.Neon:
                    using (var rb = new SolidBrush(Mood(Color.FromArgb(225, 45, 60), sad, n)))
                        g.FillRectangle(rb, -L * 0.36f, hh * 0.05f, L * 0.48f, hh);
                    using (var wb = new SolidBrush(Mood(Color.FromArgb(235, 235, 240), sad, n)))
                        g.FillRectangle(wb, L * 0.12f, hh * 0.05f, L * 0.4f, hh);
                    using (var bb = new SolidBrush(Mood(Color.FromArgb(40, 185, 250), sad, n)))
                        g.FillRectangle(bb, -L * 0.36f, -hh * 0.4f, L * 0.82f, hh * 0.42f);
                    break;
            }
            g.Restore(st);

            if (Realistic) DrawSheen(g, body, L, hh);
            else g.DrawPath(outline, body);

            if (f.Sp == Species.Corydora)
                using (var wp = new Pen(Color.FromArgb(160, 90, 80, 70), 0.3f))
                {
                    g.DrawLine(wp, L * 0.45f, hh * 0.2f, L * 0.55f, hh * 0.55f);
                    g.DrawLine(wp, L * 0.42f, hh * 0.25f, L * 0.48f, hh * 0.65f);
                }

            DrawEye(g, L * 0.3f, -hh * 0.2f, L * 0.075f);
        }

        // Brilho realista por cima do corpo (só no Ultra)
        static void DrawSheen(Graphics g, GraphicsPath body, float L, float hh)
        {
            var st = g.Save();
            g.SetClip(body, CombineMode.Intersect);
            using (var sheen = new LinearGradientBrush(new RectangleF(-L * 0.4f, -hh * 1.1f, L, hh * 1.2f),
                Color.FromArgb(95, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                g.FillEllipse(sheen, -L * 0.25f, -hh * 1.05f, L * 0.7f, hh * 0.9f);
            using (var gill = new Pen(Color.FromArgb(60, 0, 0, 0), 0.35f))
                g.DrawArc(gill, L * 0.08f, -hh * 0.6f, L * 0.12f, hh * 1.2f, -70, 140);
            g.Restore(st);
        }

        static void DrawEye(Graphics g, float x, float y, float r)
        {
            using (var w = new SolidBrush(Color.FromArgb(245, 250, 255))) g.FillEllipse(w, x - r, y - r, r * 2, r * 2);
            float pr = r * 0.62f;
            using (var b = new SolidBrush(Color.FromArgb(20, 20, 30))) g.FillEllipse(b, x - pr + r * 0.2f, y - pr, pr * 2, pr * 2);
            float gr = r * 0.25f;
            using (var s = new SolidBrush(Color.White)) g.FillEllipse(s, x + r * 0.05f, y - r * 0.5f, gr * 2, gr * 2);
        }

        // ---- Comida, bolhas, sujeira, superfície ----

        void DrawPellets(Graphics g, World w, float n)
        {
            using var b = new SolidBrush(Col.Night(Color.FromArgb(150, 85, 40), n));
            using var hl = new SolidBrush(Col.Night(Color.FromArgb(210, 140, 80), n));
            foreach (var p in w.Pellets)
            {
                g.FillEllipse(b, p.X - 1.1f, p.Y - 1.1f, 2.2f, 2.2f);
                g.FillEllipse(hl, p.X - 0.7f, p.Y - 0.8f, 0.8f, 0.8f);
            }
        }

        void DrawBubbles(Graphics g, World w, float n)
        {
            using var pen = new Pen(Color.FromArgb(170, 235, 248, 255), 0.4f);
            using var fill = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
            using var dot = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            foreach (var b in w.Bubbles) Bubble(g, b.X, b.Y, b.R, pen, fill, dot);

            if (w.State.Happiness > 78)
            {
                int count = (int)(w.Nest.Count * (w.State.Happiness - 78) / 22);
                for (int i = 0; i < count && i < w.Nest.Count; i++)
                    Bubble(g, w.Nest[i].X, w.Nest[i].Y, 0.9f + (i % 3) * 0.25f, pen, fill, dot);
            }
        }

        static void Bubble(Graphics g, float x, float y, float r, Pen pen, Brush fill, Brush dot)
        {
            g.FillEllipse(fill, x - r, y - r, r * 2, r * 2);
            g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
            g.FillEllipse(dot, x - r * 0.5f, y - r * 0.6f, r * 0.5f, r * 0.5f);
        }

        void DrawAlgae(Graphics g, World w, float dirt)
        {
            if (dirt <= 0) return;
            using var b = new SolidBrush(Color.FromArgb((int)(dirt * 150), 60, 115, 45));
            for (int i = 0; i < w.Algae.Count; i++)
            {
                float s = 2.5f + (i * 7 % 5);
                g.FillEllipse(b, w.Algae[i].X - s / 2, w.Algae[i].Y - s / 2, s, s * 0.8f);
            }
        }

        void DrawCleanFx(Graphics g, World w)
        {
            if (w.CleanFx <= 0) return;
            int a = (int)(255 * Math.Min(1, w.CleanFx));
            using var pen = new Pen(Color.FromArgb(a, 255, 255, 255), 0.6f);
            for (int i = 0; i < 10; i++)
            {
                float x = w.Water.Left + 5 + (i * 37 + 11) % Math.Max(1, (int)w.Water.Width - 10);
                float y = w.SurfaceY + 6 + (i * 23) % Math.Max(1, (int)(w.GravelTop - w.SurfaceY - 10));
                float s = 2 + (1 - w.CleanFx / 1.2f) * 2;
                g.DrawLine(pen, x - s, y, x + s, y);
                g.DrawLine(pen, x, y - s, x, y + s);
            }
        }

        void DrawSurface(Graphics g, World w, float n)
        {
            var pts = new System.Collections.Generic.List<PointF>();
            for (float x = w.Water.Left; x <= w.Water.Right; x += 3)
                pts.Add(new PointF(x, w.SurfaceY + M.Sin(x * 0.12f + w.Time * 2) * 0.6f));
            using (var band = new SolidBrush(Color.FromArgb((int)(28 * (1 - n * 0.6f)), 255, 255, 255)))
                g.FillRectangle(band, w.Water.Left, w.SurfaceY, w.Water.Width, 3);
            using var pen = new Pen(Color.FromArgb(130, 230, 248, 255), 0.8f);
            g.DrawLines(pen, pts.ToArray());
        }

        // ---- Extras ligados só no Ultra ----

        /// <summary>Desenha o peixe a partir de uma imagem do pacote. Retorna false para usar o desenho por código.</summary>
        protected virtual bool DrawFishSprite(Graphics g, Fish f, float n, bool sad) => false;
        protected virtual void DrawLightRays(Graphics g, World w, float n) { }
        protected virtual void DrawDepthFog(Graphics g, World w, float n, float dirt) { }
        protected virtual void DrawShadows(Graphics g, World w, float n) { }
        protected virtual void DrawParticles(Graphics g, World w, float n) { }
        protected virtual void DrawCaustics(Graphics g, World w, float n) { }
    }
}
