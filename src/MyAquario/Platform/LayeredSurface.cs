using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MyAquario
{
    /// <summary>
    /// Bitmap com transparência por pixel ligado direto a uma janela "layered".
    /// O GDI+ desenha na mesma memória que o Windows usa, sem cópia extra.
    /// </summary>
    sealed class LayeredSurface : IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Bitmap Bitmap { get; private set; }

        IntPtr memDC, hBmp, oldBmp;

        public LayeredSurface(int w, int h) { Create(w, h); }

        void Create(int w, int h)
        {
            Width = Math.Max(1, w);
            Height = Math.Max(1, h);
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            memDC = Native.CreateCompatibleDC(screen);
            Native.ReleaseDC(IntPtr.Zero, screen);

            var bi = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER)),
                biWidth = Width,
                biHeight = -Height, // de cima para baixo, igual ao Bitmap do .NET
                biPlanes = 1,
                biBitCount = 32,
            };
            hBmp = Native.CreateDIBSection(memDC, ref bi, 0, out IntPtr bits, IntPtr.Zero, 0);
            oldBmp = Native.SelectObject(memDC, hBmp);
            Bitmap = new Bitmap(Width, Height, Width * 4, PixelFormat.Format32bppPArgb, bits);
        }

        public void Resize(int w, int h)
        {
            if (w == Width && h == Height) return;
            Free();
            Create(w, h);
        }

        public void Present(IntPtr hwnd, int x, int y)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero);
            var dst = new Native.POINT(x, y);
            var size = new Native.SIZE(Width, Height);
            var src = new Native.POINT(0, 0);
            var blend = new Native.BLENDFUNCTION
            {
                BlendOp = Native.AC_SRC_OVER,
                SourceConstantAlpha = 255,
                AlphaFormat = Native.AC_SRC_ALPHA,
            };
            Native.UpdateLayeredWindow(hwnd, screen, ref dst, ref size, memDC, ref src, 0, ref blend, Native.ULW_ALPHA);
            Native.ReleaseDC(IntPtr.Zero, screen);
        }

        void Free()
        {
            Bitmap?.Dispose();
            Bitmap = null;
            if (memDC != IntPtr.Zero)
            {
                Native.SelectObject(memDC, oldBmp);
                Native.DeleteObject(hBmp);
                Native.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
        }

        public void Dispose() => Free();
    }
}
