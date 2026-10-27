using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace MyAquario
{
    static class Paths
    {
        public static readonly string Data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyAquario");

        public static string Packs => Path.Combine(Data, "packs");
        public static string AppDir => AppDomain.CurrentDomain.BaseDirectory;

        static Paths() { Directory.CreateDirectory(Data); }
    }

    static class Log
    {
        static readonly object Gate = new object();

        public static void Write(string msg)
        {
            try
            {
                lock (Gate)
                {
                    var p = Path.Combine(Paths.Data, "log.txt");
                    if (File.Exists(p) && new FileInfo(p).Length > 512 * 1024) File.Delete(p);
                    File.AppendAllText(p, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void Error(Exception ex) => Write("ERRO: " + ex);
    }

    static class M
    {
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float Sin(float x) => (float)Math.Sin(x);
        public static float Cos(float x) => (float)Math.Cos(x);
        public static float Sqrt(float x) => (float)Math.Sqrt(x);
        public static float Frac(float x) => x - (float)Math.Floor(x);
    }

    static class Col
    {
        public static Color Lerp(Color a, Color b, float t)
        {
            t = M.Clamp(t, 0f, 1f);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Shade(Color c, float f) => Color.FromArgb(c.A,
            M.Clamp((int)(c.R * f), 0, 255), M.Clamp((int)(c.G * f), 0, 255), M.Clamp((int)(c.B * f), 0, 255));

        public static Color Light(Color c, float t) => Lerp(c, Color.FromArgb(c.A, 255, 255, 255), t);

        public static Color Alpha(Color c, int a) => Color.FromArgb(M.Clamp(a, 0, 255), c.R, c.G, c.B);

        public static Color Desaturate(Color c, float amount)
        {
            int gray = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
            return Lerp(c, Color.FromArgb(c.A, gray, gray, gray), amount);
        }

        // Escurece puxando para o azul, como luz de aquário apagada.
        public static Color Night(Color c, float n) =>
            Lerp(c, Color.FromArgb(c.A, (int)(c.R * 0.25), (int)(c.G * 0.33), (int)(c.B * 0.55)), n);
    }

    static class Shapes
    {
        public static GraphicsPath RoundRect(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.01f) { p.AddRectangle(r); return p; }
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
