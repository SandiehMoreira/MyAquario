using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MyAquario
{
    /// <summary>
    /// Gráfico Básico: pixel art. Desenha numa imagem pequena (metade da resolução do mundo)
    /// e amplia sem suavizar. Tudo gerado por código, nada para baixar.
    /// </summary>
    sealed class PixelRenderer : IRenderer
    {
        public Tier Tier => Tier.Basico;
        public int Fps => 20;

        Bitmap low;
        Graphics lg;
        Bitmap gravel;
        int gravelKey = -1;
        readonly SolidBrush br = new SolidBrush(Color.White);
        readonly Dictionary<int, Bitmap> sprites = new Dictionary<int, Bitmap>();

        static int L(float v) => (int)Math.Floor(v / 2f);

        void Px(Color c, int x, int y, int w = 1, int h = 1)
        {
            if (c.A == 0 || w <= 0 || h <= 0) return;
            br.Color = c;
            lg.FillRectangle(br, x, y, w, h);
        }

        public void Render(Graphics g, World w, float scale)
        {
            int lw = (int)Math.Ceiling(w.W / 2), lh = (int)Math.Ceiling(w.H / 2);
            if (low == null || low.Width != lw || low.Height != lh)
            {
                lg?.Dispose();
                low?.Dispose();
                low = new Bitmap(lw, lh, PixelFormat.Format32bppPArgb);
                lg = Graphics.FromImage(low);
                lg.SmoothingMode = SmoothingMode.None;
                lg.InterpolationMode = InterpolationMode.NearestNeighbor;
                lg.PixelOffsetMode = PixelOffsetMode.Half;
            }
            lg.Clear(Color.Transparent);
            DrawScene(w);

            var st = g.Save();
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.DrawImage(low, new Rectangle(0, 0, (int)Math.Round(lw * 2 * scale), (int)Math.Round(lh * 2 * scale)));
            g.Restore(st);
        }

        void DrawScene(World w)
        {
            float n = w.Night, dirt = WorldLook.Dirt(w);
            int gx = L(w.Glass.Left), gy = L(w.Glass.Top), gr = L(w.Glass.Right), gb = L(w.Glass.Bottom);
            int wl = L(w.Water.Left), wr = L(w.Water.Right), surf = L(w.SurfaceY), grav = L(w.GravelTop), wb = L(w.Water.Bottom);
            int lw = low.Width, lh = low.Height;

            // Móvel de madeira
            Px(Color.FromArgb(92, 60, 38), 0, gb, lw, lh - gb);
            Px(Color.FromArgb(130, 90, 55), 0, gb, lw, 1);
            Px(Color.FromArgb(60, 38, 25), 0, lh - 1, lw, 1);

            // Tampa com luminária
            int lidTop = L(5);
            Px(Color.FromArgb(52, 58, 66), 0, lidTop, lw, gy - lidTop);
            Px(Color.FromArgb(84, 92, 102), 0, lidTop, lw, 1);
            if (n < 1) Px(Color.FromArgb((int)(220 * (1 - n)), 255, 236, 170), gx + 2, gy - 1, gr - gx - 4, 1);

            // Vidro e ar acima da água
            Px(Color.FromArgb(150, 170, 215, 225), gx, gy, gr - gx, gb - gy);
            Px(Color.FromArgb(90, 210, 235, 245), wl, gy, wr - wl, surf - gy);

            // Água em faixas (degradê pixelado)
            var top = Color.FromArgb(92, 170, 215);
            var bot = Color.FromArgb(38, 98, 160);
            var dirty = Color.FromArgb(95, 120, 70);
            for (int b = 0; b < 4; b++)
            {
                int y0 = surf + (grav - surf) * b / 4, y1 = surf + (grav - surf) * (b + 1) / 4;
                var c = Col.Lerp(top, bot, b / 3f);
                c = Col.Lerp(c, dirty, dirt * 0.55f);
                Px(Col.Night(c, n), wl, y0, wr - wl, y1 - y0 + 1);
            }
            Px(Col.Night(Col.Lerp(bot, dirty, dirt * 0.55f), n), wl, grav, wr - wl, wb - grav);

            // Brilho na superfície
            int ww = Math.Max(1, wr - wl);
            for (int k = 0; k < 6; k++)
            {
                int x = wl + (int)(w.Time * 6 + k * 23) % ww;
                Px(Col.Night(Color.FromArgb(180, 225, 245), n), x, surf + 1, Math.Min(3, wr - x), 1);
            }

            foreach (var p in w.Plants) if (!p.Front) DrawPlant(w, p, grav, n);
            foreach (var d in w.Decor) DrawDecor(d, grav, n);

            // Cascalho
            DrawGravel(w, wl, wr, grav, wb, n);

            if (w.HasAirstone)
            {
                var a = w.Airstone;
                Px(Col.Night(Color.FromArgb(120, 120, 125), n), L(a.X) - 2, L(a.Y), 4, 1);
                Px(Color.FromArgb(60, 220, 230, 235), L(a.X), surf - 1, 1, L(a.Y) - surf + 1);
            }

            foreach (var p in w.Plants) if (p.Front) DrawPlant(w, p, grav, n);

            foreach (var p in w.Pellets)
                Px(Col.Night(Color.FromArgb(150, 82, 40), n), L(p.X), L(p.Y));

            bool sad = WorldLook.Sad(w);
            foreach (var f in w.Fish)
            {
                int frame = ((int)(f.Phase / Math.PI) & 1);
                var spr = Sprite(f.Sp, f.Variant, frame, f.Facing < 0, sad);
                lg.DrawImage(spr, L(f.X) - spr.Width / 2, L(f.Y) - spr.Height / 2, spr.Width, spr.Height);
            }

            var bub = Color.FromArgb(200, 210, 240, 250);
            foreach (var b in w.Bubbles)
            {
                int x = L(b.X), y = L(b.Y);
                if (b.R > 1.6f) { Px(bub, x - 1, y); Px(bub, x + 1, y); Px(bub, x, y - 1); Px(bub, x, y + 1); }
                else Px(bub, x, y);
            }

            // Ninho de bolhas: o betta faz quando está bem feliz
            if (w.State.Happiness > 78)
            {
                int count = (int)(w.Nest.Count * (w.State.Happiness - 78) / 22);
                for (int i = 0; i < count && i < w.Nest.Count; i++)
                    Px(Color.FromArgb(220, 240, 250, 255), L(w.Nest[i].X), L(w.Nest[i].Y));
            }

            if (dirt > 0)
            {
                var alg = Color.FromArgb((int)(dirt * 200), 50, 110, 40);
                for (int i = 0; i < w.Algae.Count; i++)
                {
                    int x = L(w.Algae[i].X), y = L(w.Algae[i].Y);
                    Px(alg, x, y, 2, 1);
                    if (i % 2 == 0) Px(alg, x + 1, y + 1);
                }
            }

            if (w.CleanFx > 0)
            {
                var sp = Color.FromArgb((int)(255 * Math.Min(1, w.CleanFx)), 255, 255, 255);
                for (int i = 0; i < 8; i++)
                {
                    int x = wl + (i * 37 + 11) % ww, y = surf + 3 + (i * 23) % Math.Max(1, grav - surf - 4);
                    Px(sp, x, y); Px(sp, x - 1, y); Px(sp, x + 1, y); Px(sp, x, y - 1); Px(sp, x, y + 1);
                }
            }

            if (n > 0) Px(Color.FromArgb((int)(n * 110), 10, 15, 45), gx, gy, gr - gx, gb - gy);

            Px(Color.FromArgb(180, 210, 240, 250), wl, surf, wr - wl, 1);
            var edge = Color.FromArgb(200, 215, 240, 245);
            Px(edge, gx, gy, 1, gb - gy);
            Px(edge, gr - 1, gy, 1, gb - gy);
            Px(Color.FromArgb(60, 255, 255, 255), gx + 2, gy + 2, 1, gb - gy - 4);
        }

        static readonly Color[][] Greens =
        {
            new[] { Color.FromArgb(60, 150, 70), Color.FromArgb(40, 120, 55) },
            new[] { Color.FromArgb(80, 170, 75), Color.FromArgb(50, 135, 60) },
            new[] { Color.FromArgb(95, 180, 85), Color.FromArgb(60, 140, 65) },
        };

        void DrawPlant(World w, Plant p, int grav, float n)
        {
            var pal = Greens[p.Shade % 3];
            int bx = L(p.X), h = Math.Max(2, L(p.H));
            switch (p.Kind)
            {
                case 0: // Valisnéria: fitas compridas
                    for (int b = 0; b < p.Blades; b++)
                    {
                        int bh = (int)(h * (0.7f + 0.3f * ((b * 37) % 10) / 10f));
                        var c = Col.Night(pal[b % 2], n);
                        for (int j = 0; j < bh; j++)
                        {
                            float t = j / (float)bh;
                            float sway = M.Sin(w.Time * 1.2f + p.Phase + b * 0.7f) * t * t * 3;
                            int x = bx + (b - p.Blades / 2) + (int)Math.Round(sway + (b - p.Blades / 2) * t * 1.5f);
                            Px(c, x, grav - j);
                        }
                    }
                    break;
                case 1: // Anúbia: folhas largas baixinhas
                    for (int b = 0; b < p.Blades; b++)
                    {
                        int cx = bx + (b - p.Blades / 2) * 3, cy = grav - 3 - (b % 2) * 2;
                        br.Color = Col.Night(Color.FromArgb(30, 100, 50), n);
                        lg.FillEllipse(br, cx - 3, cy - 2, 6, 4);
                        Px(Col.Night(Color.FromArgb(70, 150, 80), n), cx - 1, cy, 3, 1);
                    }
                    break;
                default: // Espada-amazônica
                    for (int b = 0; b < p.Blades; b++)
                    {
                        float lean = (b - (p.Blades - 1) / 2f) * 0.8f;
                        var c = Col.Night(pal[b % 2], n);
                        int bh = (int)(h * (0.75f + 0.25f * ((b * 13) % 7) / 7f));
                        for (int j = 0; j < bh; j++)
                        {
                            float t = j / (float)bh;
                            float sway = M.Sin(w.Time * 0.9f + p.Phase + b) * t * 1.5f;
                            int x = bx + (int)Math.Round(lean * t * 4 + sway);
                            Px(c, x, grav - j, t > 0.4f ? 2 : 1, 1);
                        }
                    }
                    break;
            }
        }

        void DrawDecor(Decor d, int grav, float n)
        {
            if (d.Kind == 1)
            {
                using var pen = new Pen(Col.Night(Color.FromArgb(105, 70, 42), n), 2);
                int x = L(d.X), hw = L(d.W) / 2, h = L(d.H);
                lg.DrawLine(pen, x - hw, grav, x + hw, grav - h / 2);
                lg.DrawLine(pen, x, grav - h / 4, x - hw / 3, grav - h);
                pen.Width = 1;
                lg.DrawLine(pen, x + hw / 3, grav - h / 3, x + hw / 2, grav - h);
                return;
            }
            var baseC = d.Shade switch { 0 => Color.FromArgb(120, 120, 128), 1 => Color.FromArgb(100, 96, 92), _ => Color.FromArgb(140, 132, 120) };
            int rw = Math.Max(3, L(d.W)), rh = Math.Max(2, L(d.H));
            int rx = L(d.X) - rw / 2, ry = grav - rh;
            br.Color = Col.Night(baseC, n);
            lg.FillEllipse(br, rx, ry, rw, rh * 2);
            Px(Col.Night(Col.Light(baseC, 0.3f), n), rx + rw / 4, ry + 1, Math.Max(1, rw / 4), 1);
            Px(Col.Night(Col.Shade(baseC, 0.7f), n), rx + rw * 2 / 3, ry + rh / 2, Math.Max(1, rw / 4), 1);
        }

        void DrawGravel(World w, int wl, int wr, int grav, int wb, float n)
        {
            int gw = wr - wl, gh = wb - grav + 1;
            int key = gw * 1000 + gh + w.State.TankLevel * 1000000;
            if (gravel == null || gravelKey != key)
            {
                gravel?.Dispose();
                gravel = new Bitmap(Math.Max(1, gw), Math.Max(1, gh), PixelFormat.Format32bppArgb);
                var pal = new[] { Color.FromArgb(190, 160, 110), Color.FromArgb(160, 130, 90), Color.FromArgb(120, 100, 70), Color.FromArgb(210, 190, 140) };
                var r = new Random(7 + w.State.TankLevel);
                for (int y = 0; y < gh; y++)
                    for (int x = 0; x < gw; x++)
                    {
                        if (y == 0 && r.Next(3) == 0) continue; // borda irregular
                        var c = pal[r.Next(pal.Length)];
                        if (y > gh / 2) c = Col.Shade(c, 0.85f);
                        gravel.SetPixel(x, y, c);
                    }
                gravelKey = key;
            }
            if (n <= 0) lg.DrawImageUnscaled(gravel, wl, grav);
            else
            {
                using var ia = new ImageAttributes();
                ia.SetColorMatrix(NightMatrix(n));
                lg.DrawImage(gravel, new Rectangle(wl, grav, gravel.Width, gravel.Height), 0, 0, gravel.Width, gravel.Height, GraphicsUnit.Pixel, ia);
            }
        }

        // Mesmo efeito de Col.Night, para imagens inteiras.
        public static ColorMatrix NightMatrix(float n, float alpha = 1f)
        {
            float day = 1 - n;
            return new ColorMatrix(new[]
            {
                new float[] { day + n * 0.25f, 0, 0, 0, 0 },
                new float[] { 0, day + n * 0.33f, 0, 0, 0 },
                new float[] { 0, 0, day + n * 0.55f, 0, 0 },
                new float[] { 0, 0, 0, alpha, 0 },
                new float[] { 0, 0, 0, 0, 1 },
            });
        }

        // ---- Sprites ----

        static readonly string[][] BettaFrames =
        {
            new[]
            {
                "..TT............",
                ".TTTT..DDD......",
                "TTTTT.DDDDD.....",
                ".TTTTDBBBBBB....",
                "..TTTBBBBBBBBB..",
                "..TTBBBbbbbBBEB.",
                "..TTTBBBBBBBBBB.",
                ".TTTTTBBBBBBB...",
                "TTTTT.AAAAA.....",
                ".TTT...AAAA.....",
                "........AA......",
            },
            new[]
            {
                "................",
                "TT.....DDD......",
                "TTTT..DDDDD.....",
                "TTTTTDBBBBBB....",
                ".TTTTBBBBBBBBB..",
                "..TTBBBbbbbBBEB.",
                ".TTTTBBBBBBBBBB.",
                "TTTTTTBBBBBBB...",
                "TTTT..AAAAA.....",
                "TT.....AAAA.....",
                ".........AA.....",
            },
        };

        static readonly string[][] CoryFrames =
        {
            new[] { "....DD.....", "T.SSSSSSS..", "TTSsSsSSSES", "T.SSSSSSSSS", "...ww...ww." },
            new[] { "....DD.....", "..SSSSSSS..", "TTSsSsSSSES", "TTSSSSSSSSS", "...ww...ww." },
        };

        static readonly string[][] RasboraFrames =
        {
            new[] { "...OOOO...", "T.OOOKOOO.", "TTOOKKKOOE", "T.OKKKOOO.", "...OOOO..." },
            new[] { "...OOOO...", "TTOOOKOOO.", "T.OOKKKOOE", "TTOKKKOOO.", "...OOOO..." },
        };

        static readonly string[][] NeonFrames =
        {
            new[] { "..SSSS..", "T.BBBBBE", "TRRRWWW.", "..RRWW.." },
            new[] { "..SSSS..", "TTBBBBBE", ".RRRWWW.", "..RRWW.." },
        };

        static Dictionary<char, Color> Palette(Species sp, int variant)
        {
            var eye = Color.FromArgb(20, 20, 30);
            switch (sp)
            {
                case Species.Betta:
                    var main = WorldLook.BettaColors[variant % WorldLook.BettaColors.Length];
                    return new Dictionary<char, Color>
                    {
                        ['T'] = main, ['D'] = Col.Shade(main, 0.7f), ['A'] = Col.Shade(main, 0.7f),
                        ['B'] = Col.Light(main, 0.1f), ['b'] = Col.Light(main, 0.4f), ['E'] = eye,
                    };
                case Species.Corydora:
                    return new Dictionary<char, Color>
                    {
                        ['S'] = Color.FromArgb(170, 150, 120), ['s'] = Color.FromArgb(90, 80, 70), ['D'] = Color.FromArgb(120, 110, 95),
                        ['T'] = Color.FromArgb(150, 135, 110), ['w'] = Color.FromArgb(200, 190, 170), ['E'] = eye,
                    };
                case Species.Rasbora:
                    return new Dictionary<char, Color>
                    {
                        ['O'] = Color.FromArgb(230, 130, 70), ['K'] = Color.FromArgb(40, 30, 40),
                        ['T'] = Color.FromArgb(220, 110, 60), ['E'] = eye,
                    };
                default:
                    return new Dictionary<char, Color>
                    {
                        ['S'] = Color.FromArgb(180, 190, 200), ['B'] = Color.FromArgb(40, 170, 240), ['R'] = Color.FromArgb(220, 40, 50),
                        ['W'] = Color.FromArgb(230, 230, 235), ['T'] = Color.FromArgb(150, 170, 180, 190), ['E'] = eye,
                    };
            }
        }

        Bitmap Sprite(Species sp, int variant, int frame, bool flip, bool pale)
        {
            int key = (((int)sp * 8 + variant) * 2 + frame) * 4 + (flip ? 2 : 0) + (pale ? 1 : 0);
            if (sprites.TryGetValue(key, out var cached)) return cached;

            var rows = sp switch
            {
                Species.Betta => BettaFrames[frame],
                Species.Corydora => CoryFrames[frame],
                Species.Rasbora => RasboraFrames[frame],
                _ => NeonFrames[frame],
            };
            var pal = Palette(sp, variant);
            int h = rows.Length, wd = 0;
            foreach (var r in rows) wd = Math.Max(wd, r.Length);

            var bmp = new Bitmap(wd, h, PixelFormat.Format32bppArgb);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    if (!pal.TryGetValue(rows[y][x], out var c)) continue;
                    if (pale) c = Col.Desaturate(c, 0.55f);
                    bmp.SetPixel(flip ? wd - 1 - x : x, y, c);
                }
            sprites[key] = bmp;
            return bmp;
        }

        public void Release()
        {
            foreach (var b in sprites.Values) b.Dispose();
            sprites.Clear();
            gravel?.Dispose(); gravel = null; gravelKey = -1;
            lg?.Dispose(); lg = null;
            low?.Dispose(); low = null;
        }

        public void Dispose()
        {
            Release();
            br.Dispose();
        }
    }
}
