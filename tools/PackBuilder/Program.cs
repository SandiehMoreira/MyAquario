using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;

// Gera os pacotes gráficos do MyAquario (pack-normal.zip e pack-ultra.zip) e o ícone do app.
// Uso: PackBuilder <pasta-saida> [caminho-do-app.ico]
static class Program
{
    const string Version = "0.1.0";

    static int Main(string[] args)
    {
        // PackBuilder --limpar <entrada.png> <saida.png>: tira fundo branco/xadrez "pintado" pela IA
        if (args.Length == 3 && args[0] == "--limpar")
        {
            if (Path.GetFullPath(args[1]).Equals(Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Recusado: a saída não pode ser o próprio arquivo de entrada.");
                return 1;
            }
            RemoveFakeBackground(args[1], args[2]);
            Console.WriteLine("Fundo removido: " + args[2]);
            return 0;
        }

        if (args.Length < 1 || args[0].StartsWith("-"))
        {
            Console.WriteLine("Uso: PackBuilder <pasta-saida> [app.ico] [pasta-arte]");
            Console.WriteLine("     PackBuilder --limpar <entrada.png> <saida.png>");
            return 1;
        }
        // Segurança: o 2º argumento é sobrescrito com o ícone, então só aceita um .ico.
        if (args.Length >= 2 && !args[1].EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Recusado: o ícone precisa terminar em .ico (" + args[1] + ")");
            return 1;
        }
        string outDir = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(outDir);

        BuildPack(outDir, "normal", "Pacote Normal (cartoon 2D)", dir =>
        {
            Save(NormalBackdrop(), Path.Combine(dir, "backdrop.png"));
            Save(NormalGravel(), Path.Combine(dir, "gravel.png"));
        });

        string artDir = args.Length >= 3 ? Path.GetFullPath(args[2]) : null;

        BuildPack(outDir, "ultra", "Pacote Ultra (realista)", dir =>
        {
            Save(UltraBackdrop(), Path.Combine(dir, "backdrop_hd.png"));
            Save(UltraGravel(), Path.Combine(dir, "gravel_hd.png"));
            for (int i = 0; i < 16; i++) Save(Caustic(i, 16), Path.Combine(dir, $"caustics_{i:00}.png"));
            CopyArt(artDir == null ? null : Path.Combine(artDir, "ultra"), dir);
        });

        if (args.Length >= 2)
        {
            WriteIcon(args[1]);
            Console.WriteLine("Ícone: " + args[1]);
        }
        return 0;
    }

    static void BuildPack(string outDir, string id, string name, Action<string> fill)
    {
        string dir = Path.Combine(outDir, "pack-" + id);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        fill(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"),
            $"{{\"id\":\"{id}\",\"name\":\"{name}\",\"version\":\"{Version}\"}}");
        string zip = Path.Combine(outDir, "pack-" + id + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, false);
        Console.WriteLine($"{zip}  ({new FileInfo(zip).Length / 1024} KB)");
    }

    // Arte feita à mão / por IA (art\ultra\*.png) substitui ou completa o que foi gerado por código.
    // Peixes (fish_*.png) são reduzidos para no máximo 512 px de largura: pesa menos e gasta menos memória.
    static void CopyArt(string artDir, string dir)
    {
        if (artDir == null || !Directory.Exists(artDir)) return;
        foreach (var file in Directory.GetFiles(artDir, "*.png"))
        {
            string name = Path.GetFileName(file).ToLowerInvariant();
            string dest = Path.Combine(dir, name);
            using var img = new Bitmap(file);
            bool fish = name.StartsWith("fish_");
            // Peixe: recorta a margem transparente, para o tamanho no aquário bater com o corpo.
            var src = fish ? OpaqueBounds(img) : new Rectangle(0, 0, img.Width, img.Height);
            int maxW = fish ? 512 : 2048;
            int w = Math.Min(maxW, src.Width), h = (int)Math.Round(src.Height * (w / (double)src.Width));
            using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, w, h), src, GraphicsUnit.Pixel);
            }
            bmp.Save(dest, ImageFormat.Png);
            Console.WriteLine($"  arte: {name} ({w}x{h})");
        }
    }

    static int[] ReadArgb(Bitmap b)
    {
        var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new int[b.Width * b.Height];
        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
        b.UnlockBits(data);
        return px;
    }

    static Rectangle OpaqueBounds(Bitmap b)
    {
        var px = ReadArgb(b);
        int w = b.Width, h = b.Height, x0 = w, x1 = -1, y0 = h, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if ((px[y * w + x] >>> 24) > 20)
                {
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
        if (x1 < 0) return new Rectangle(0, 0, w, h);
        const int pad = 4;
        x0 = Math.Max(0, x0 - pad); y0 = Math.Max(0, y0 - pad);
        x1 = Math.Min(w - 1, x1 + pad); y1 = Math.Min(h - 1, y1 + pad);
        return Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
    }

    // Fundo claro e sem cor (branco ou o xadrez cinza/branco que a IA desenha) ligado à borda da imagem
    // vira transparente. Perto do peixe, o quanto o pixel é "branco" vira transparência parcial,
    // para as nadadeiras finas não ficarem com contorno branco.
    static void RemoveFakeBackground(string input, string output)
    {
        using var src = new Bitmap(input);
        int w = src.Width, h = src.Height;
        var px = ReadArgb(src);

        bool IsBg(int c)
        {
            int r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            int mn = Math.Min(r, Math.Min(g, b)), mx = Math.Max(r, Math.Max(g, b));
            return mn > 218 && mx - mn < 16;
        }

        var bg = new bool[w * h];
        var queue = new Queue<int>();
        void Seed(int i) { if (!bg[i] && IsBg(px[i])) { bg[i] = true; queue.Enqueue(i); } }
        for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % w, y = i / w;
            if (x > 0) Seed(i - 1);
            if (x < w - 1) Seed(i + 1);
            if (y > 0) Seed(i - w);
            if (y < h - 1) Seed(i + w);
        }

        // Buracos fechados (xadrez preso entre nadadeiras): só se tiverem os DOIS tons do xadrez,
        // para não apagar uma parte branca do próprio peixe.
        var seen = new bool[w * h];
        var comp = new List<int>();
        for (int start = 0; start < px.Length; start++)
        {
            if (bg[start] || seen[start] || !IsBg(px[start])) continue;
            comp.Clear();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, y = i / w;
                comp.Add(i);
                foreach (int j in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < h - 1 ? i + w : -1 })
                    if (j >= 0 && !seen[j] && !bg[j] && IsBg(px[j])) { seen[j] = true; queue.Enqueue(j); }
            }
            if (comp.Count < 30) continue;
            int light = 0, dark = 0;
            foreach (int i in comp)
            {
                int c = px[i], mn = Math.Min((c >> 16) & 255, Math.Min((c >> 8) & 255, c & 255));
                if (mn >= 250) light++; else if (mn <= 246) dark++;
            }
            if (light > comp.Count / 5 && dark > comp.Count / 5)
                foreach (int i in comp) bg[i] = true;
        }

        // Distância (em pixels, até 3) de cada pixel do peixe até o fundo
        var dist = new byte[w * h];
        for (int i = 0; i < dist.Length; i++) dist[i] = bg[i] ? (byte)0 : (byte)255;
        for (int pass = 1; pass <= 3; pass++)
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (dist[i] != 255) continue;
                    if (dist[i - 1] == pass - 1 || dist[i + 1] == pass - 1 || dist[i - w] == pass - 1 || dist[i + w] == pass - 1)
                        dist[i] = (byte)pass;
                }

        var outPx = new int[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            if (bg[i]) { outPx[i] = 0; continue; }
            int c = px[i], r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            int a = 255;
            if (dist[i] <= 3)
            {
                int mn = Math.Min(r, Math.Min(g, b)), mx = Math.Max(r, Math.Max(g, b));
                float white = Math.Max(0, (mn - 150) / 105f) * Math.Max(0, 1 - (mx - mn) / 60f);
                float keep = 1 - white * (1 - (dist[i] - 1) / 3f);
                a = (int)(255 * Math.Max(0, Math.Min(1, keep)));
                if (a > 0 && a < 255)
                {
                    // tira o branco misturado na cor (desfaz a mistura com o fundo branco)
                    float k = a / 255f;
                    r = Clamp((int)((r - 255 * (1 - k)) / k));
                    g = Clamp((int)((g - 255 * (1 - k)) / k));
                    b = Clamp((int)((b - 255 * (1 - k)) / k));
                }
            }
            outPx[i] = (a << 24) | (r << 16) | (g << 8) | b;
        }

        using var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = dst.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        System.Runtime.InteropServices.Marshal.Copy(outPx, 0, data.Scan0, outPx.Length);
        dst.UnlockBits(data);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        dst.Save(output, ImageFormat.Png);
    }

    static void Save(Bitmap b, string path)
    {
        b.Save(path, ImageFormat.Png);
        b.Dispose();
    }

    // ---------- Ruído ----------

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)int.MaxValue;
        }
    }

    static float Smooth(float t) => t * t * (3 - 2 * t);

    // Ruído de valor; repete a cada "period" células no eixo X (0 = não repete)
    static float Noise(float x, float y, int seed, int period)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        float fx = Smooth(x - x0), fy = Smooth(y - y0);
        int xa = x0, xb = x0 + 1;
        if (period > 0) { xa = ((xa % period) + period) % period; xb = ((xb % period) + period) % period; }
        float a = Hash(xa, y0, seed), b = Hash(xb, y0, seed), c = Hash(xa, y0 + 1, seed), d = Hash(xb, y0 + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    static float Fbm(float x, float y, int seed, int octaves, int period)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(x, y, seed + i * 31, period) * amp;
            norm += amp;
            x *= 2; y *= 2; period *= 2;
            amp *= 0.5f;
        }
        return sum / norm;
    }

    // ---------- Pacote Normal (cartoon) ----------

    static Bitmap NormalBackdrop()
    {
        const int W = 512, H = 256;
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var colors = new[] { Color.FromArgb(70, 70, 150, 140), Color.FromArgb(100, 55, 135, 105), Color.FromArgb(140, 45, 120, 85) };
        var r = new Random(3);
        for (int k = 0; k < 3; k++)
        {
            var pts = new List<PointF>();
            float baseY = H * (0.35f + 0.17f * k);
            float ph = (float)r.NextDouble() * 6;
            for (int x = 0; x <= W; x += 8)
            {
                float y = baseY + (float)Math.Sin(x * Math.PI * 2 / W * (2 + k) + ph) * 14 + (float)Math.Sin(x * Math.PI * 2 / W * 7 + ph * 2) * 5;
                pts.Add(new PointF(x, y));
            }
            pts.Add(new PointF(W, H));
            pts.Add(new PointF(0, H));
            using var b = new SolidBrush(colors[k]);
            g.FillPolygon(b, pts.ToArray());
            // Moitas arredondadas no topo de cada camada
            for (int i = 0; i < 8; i++)
            {
                float x = (float)r.NextDouble() * W, rad = 14 + (float)r.NextDouble() * 20;
                float y = baseY - rad * 0.4f;
                g.FillEllipse(b, x - rad, y - rad * 0.6f, rad * 2, rad * 1.4f);
                if (x - rad < 0) g.FillEllipse(b, x + W - rad, y - rad * 0.6f, rad * 2, rad * 1.4f);
                if (x + rad > W) g.FillEllipse(b, x - W - rad, y - rad * 0.6f, rad * 2, rad * 1.4f);
            }
        }
        return bmp;
    }

    static Bitmap NormalGravel()
    {
        const int W = 256, H = 64;
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(176, 146, 104));
        var pal = new[]
        {
            Color.FromArgb(196, 166, 120), Color.FromArgb(160, 128, 92), Color.FromArgb(130, 108, 82),
            Color.FromArgb(214, 196, 152), Color.FromArgb(150, 140, 130),
        };
        var r = new Random(11);
        var stones = Enumerable.Range(0, 230).Select(_ => new
        {
            X = (float)r.NextDouble() * W, Y = (float)r.NextDouble() * (H + 8) - 4,
            Rx = 3 + (float)r.NextDouble() * 5, Ry = 2 + (float)r.NextDouble() * 3, C = pal[r.Next(pal.Length)],
        }).OrderBy(s => s.Y).ToList();
        using var outline = new Pen(Color.FromArgb(110, 80, 60, 40), 1f);
        using var hl = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
        foreach (var s in stones)
            foreach (var ox in new[] { -W, 0, W })
            {
                var rect = new RectangleF(s.X + ox - s.Rx, s.Y - s.Ry, s.Rx * 2, s.Ry * 2);
                if (rect.Right < 0 || rect.Left > W) continue;
                using var b = new SolidBrush(s.C);
                g.FillEllipse(b, rect);
                g.DrawEllipse(outline, rect);
                g.FillEllipse(hl, rect.X + rect.Width * 0.25f, rect.Y + rect.Height * 0.15f, rect.Width * 0.3f, rect.Height * 0.25f);
            }
        return bmp;
    }

    // ---------- Pacote Ultra (realista) ----------

    static Bitmap UltraBackdrop()
    {
        const int W = 1024, H = 512;
        var px = new float[W * H * 4]; // r,g,b,a (0..1, sem pré-multiplicar)

        // Parede de pedra ao fundo
        for (int x = 0; x < W; x++)
        {
            float ridge = H * (0.28f + 0.3f * Fbm(x / (float)W * 4, 0.5f, 5, 4, 4));
            for (int y = 0; y < H; y++)
            {
                if (y < ridge) continue;
                float n = Fbm(x / 90f, y / 90f, 9, 5, W / 90 > 0 ? (int)(W / 90f) : 0);
                float crack = Fbm(x / 30f, y / 30f, 21, 3, (int)(W / 30f));
                float v = n * 0.8f + crack * 0.2f;
                var c0 = new[] { 34f, 52f, 64f };
                var c1 = new[] { 84f, 102f, 108f };
                int i = (y * W + x) * 4;
                for (int k = 0; k < 3; k++) px[i + k] = (c0[k] + (c1[k] - c0[k]) * v) / 255f;
                float edge = Math.Min(1, (y - ridge) / 30f);
                float depth = 0.55f + 0.45f * (y / (float)H);
                px[i + 3] = edge * depth * 0.92f;
            }
        }

        // Silhuetas de plantas distantes
        var r = new Random(5);
        for (int p = 0; p < 70; p++)
        {
            float bx = (float)r.NextDouble() * W, h = 80 + (float)r.NextDouble() * 220, lean = ((float)r.NextDouble() - 0.5f) * 0.6f;
            float width = 2 + (float)r.NextDouble() * 3;
            for (int j = 0; j < h; j++)
            {
                float t = j / h;
                float x = bx + lean * j + (float)Math.Sin(t * 5 + p) * 6 * t;
                int y = H - 1 - j;
                float half = width * (1 - t * 0.8f);
                for (int dx = (int)-half; dx <= half; dx++)
                {
                    int xx = (((int)x + dx) % W + W) % W;
                    int i = (y * W + xx) * 4;
                    float a = 0.35f;
                    px[i + 0] = px[i + 0] * (1 - a) + 30 / 255f * a;
                    px[i + 1] = px[i + 1] * (1 - a) + 75 / 255f * a;
                    px[i + 2] = px[i + 2] * (1 - a) + 60 / 255f * a;
                    px[i + 3] = Math.Max(px[i + 3], 0.5f);
                }
            }
        }

        BoxBlur(px, W, H, 2);
        BoxBlur(px, W, H, 2);
        return ToBitmap(px, W, H);
    }

    static Bitmap UltraGravel()
    {
        const int W = 512, H = 128;
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(105, 92, 76));
        var pal = new[]
        {
            Color.FromArgb(196, 180, 160), Color.FromArgb(160, 145, 125), Color.FromArgb(130, 120, 110),
            Color.FromArgb(214, 204, 188), Color.FromArgb(110, 100, 90), Color.FromArgb(185, 155, 115),
        };
        var r = new Random(17);
        var stones = Enumerable.Range(0, 1400).Select(_ => new
        {
            X = (float)r.NextDouble() * W, Y = (float)r.NextDouble() * (H + 10) - 5,
            Rx = 3 + (float)r.NextDouble() * 6, Ry = 2 + (float)r.NextDouble() * 4,
            Rot = (float)r.NextDouble() * 180, C = pal[r.Next(pal.Length)],
        }).OrderBy(s => s.Y).ToList();

        using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        using var spec = new SolidBrush(Color.FromArgb(110, 255, 255, 255));
        foreach (var s in stones)
            foreach (var ox in new[] { -W, 0, W })
            {
                float cx = s.X + ox;
                if (cx + s.Rx * 2 < 0 || cx - s.Rx * 2 > W) continue;
                var st = g.Save();
                g.TranslateTransform(cx, s.Y);
                g.RotateTransform(s.Rot);
                g.FillEllipse(shadow, -s.Rx + 1.2f, -s.Ry + 1.8f, s.Rx * 2, s.Ry * 2);
                using (var path = new GraphicsPath())
                {
                    path.AddEllipse(-s.Rx, -s.Ry, s.Rx * 2, s.Ry * 2);
                    using var pg = new PathGradientBrush(path)
                    {
                        CenterColor = Lighten(s.C, 35),
                        SurroundColors = new[] { Lighten(s.C, -45) },
                        CenterPoint = new PointF(-s.Rx * 0.3f, -s.Ry * 0.35f),
                    };
                    g.FillPath(pg, path);
                }
                g.FillEllipse(spec, -s.Rx * 0.5f, -s.Ry * 0.6f, s.Rx * 0.4f, s.Ry * 0.3f);
                g.Restore(st);
            }
        return bmp;
    }

    static Color Lighten(Color c, int d) =>
        Color.FromArgb(Clamp(c.R + d), Clamp(c.G + d), Clamp(c.B + d));

    static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // Cáusticas: rede de luz no fundo, feita com Voronoi (F2 - F1).
    // Os pontos andam em círculos, então o quadro 16 emenda no quadro 0 (animação em loop).
    static Bitmap Caustic(int frame, int frames)
    {
        const int S = 256, N = 24;
        var r = new Random(42);
        var bx = new float[N]; var by = new float[N]; var rad = new float[N]; var ph = new float[N];
        for (int i = 0; i < N; i++)
        {
            bx[i] = (float)r.NextDouble() * S; by[i] = (float)r.NextDouble() * S;
            rad[i] = 10 + (float)r.NextDouble() * 22; ph[i] = (float)(r.NextDouble() * Math.PI * 2);
        }
        float t = frame / (float)frames * (float)Math.PI * 2;
        var px = new float[N]; var py = new float[N];
        for (int i = 0; i < N; i++)
        {
            px[i] = bx[i] + rad[i] * (float)Math.Cos(t + ph[i]);
            py[i] = by[i] + rad[i] * (float)Math.Sin(t + ph[i]);
        }

        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, S, S), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[S * S * 4];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float f1 = float.MaxValue, f2 = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    float dx = Math.Abs(x - px[i]) % S, dy = Math.Abs(y - py[i]) % S;
                    if (dx > S / 2) dx = S - dx;
                    if (dy > S / 2) dy = S - dy;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
                float v = Math.Max(0, 1 - (f2 - f1) / 9f);
                v = (float)Math.Pow(v, 3);
                int o = (y * S + x) * 4;
                buf[o] = 255; buf[o + 1] = 255; buf[o + 2] = 245;
                buf[o + 3] = (byte)(v * 105);
            }
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    static void BoxBlur(float[] px, int w, int h, int radius)
    {
        var tmp = new float[px.Length];
        int win = radius * 2 + 1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int k = 0; k < 4; k++)
                {
                    float s = 0;
                    for (int d = -radius; d <= radius; d++) s += px[(y * w + ((x + d) % w + w) % w) * 4 + k];
                    tmp[(y * w + x) * 4 + k] = s / win;
                }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int k = 0; k < 4; k++)
                {
                    float s = 0;
                    for (int d = -radius; d <= radius; d++) s += tmp[(Math.Max(0, Math.Min(h - 1, y + d)) * w + x) * 4 + k];
                    px[(y * w + x) * 4 + k] = s / win;
                }
    }

    static Bitmap ToBitmap(float[] px, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            buf[i * 4 + 0] = (byte)(Math.Min(1, px[i * 4 + 2]) * 255); // B
            buf[i * 4 + 1] = (byte)(Math.Min(1, px[i * 4 + 1]) * 255); // G
            buf[i * 4 + 2] = (byte)(Math.Min(1, px[i * 4 + 0]) * 255); // R
            buf[i * 4 + 3] = (byte)(Math.Min(1, px[i * 4 + 3]) * 255); // A
        }
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    // ---------- Ícone ----------

    static Bitmap IconImage(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(size / 256f, size / 256f);
        var rect = new RectangleF(8, 8, 240, 240);
        using (var path = RoundRect(rect, 48))
        {
            using var water = new LinearGradientBrush(rect, Color.FromArgb(110, 205, 240), Color.FromArgb(25, 95, 170), 90f);
            g.FillPath(water, path);
            g.SetClip(path);
            using (var sand = new SolidBrush(Color.FromArgb(214, 180, 120))) g.FillEllipse(sand, -40, 200, 340, 120);
            using (var leaf = new SolidBrush(Color.FromArgb(70, 170, 80)))
            {
                g.FillEllipse(leaf, 30, 120, 22, 110);
                g.FillEllipse(leaf, 52, 145, 18, 90);
                g.FillEllipse(leaf, 200, 135, 20, 100);
            }
            g.ResetClip();
        }

        // Betta vermelho
        var red = Color.FromArgb(220, 40, 55);
        using (var tail = new GraphicsPath())
        {
            tail.AddBezier(110, 115, 80, 50, 30, 60, 35, 125);
            tail.AddBezier(35, 125, 30, 190, 80, 200, 110, 135);
            tail.CloseFigure();
            using var tb = new LinearGradientBrush(new RectangleF(30, 50, 85, 150), Color.FromArgb(255, 120, 130), Color.FromArgb(170, 20, 40), 0f);
            g.FillPath(tb, tail);
        }
        using (var fin = new SolidBrush(Color.FromArgb(185, 25, 45)))
        {
            g.FillPolygon(fin, new[] { new PointF(150, 100), new PointF(120, 62), new PointF(100, 105) });
            g.FillPolygon(fin, new[] { new PointF(160, 140), new PointF(125, 190), new PointF(105, 135) });
        }
        using (var body = new LinearGradientBrush(new RectangleF(95, 95, 120, 60), Color.FromArgb(255, 110, 120), red, 90f))
            g.FillEllipse(body, 95, 98, 120, 56);
        using (var w = new SolidBrush(Color.White)) g.FillEllipse(w, 175, 108, 20, 20);
        using (var b = new SolidBrush(Color.FromArgb(25, 25, 35))) g.FillEllipse(b, 181, 112, 12, 12);
        using (var bub = new Pen(Color.FromArgb(230, 255, 255, 255), 5))
        {
            g.DrawEllipse(bub, 205, 60, 18, 18);
            g.DrawEllipse(bub, 190, 30, 12, 12);
        }
        using (var outline = new Pen(Color.FromArgb(200, 255, 255, 255), 6))
        using (var path = RoundRect(rect, 48))
            g.DrawPath(outline, path);
        return bmp;
    }

    static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = rad * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // .ico com imagens PNG dentro (aceito pelo Windows Vista em diante)
    static void WriteIcon(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var sizes = new[] { 256, 64, 48, 32, 16 };
        var pngs = sizes.Select(s =>
        {
            using var bmp = IconImage(s);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToArray();

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0);
        w.Write((short)1);
        w.Write((short)sizes.Length);
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((short)1);
            w.Write((short)32);
            w.Write(pngs[i].Length);
            w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var p in pngs) w.Write(p);
    }
}
