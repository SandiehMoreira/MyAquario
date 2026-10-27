using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyAquario
{
    /// <summary>
    /// Janela sem borda, com transparência por pixel, que mora na área de trabalho.
    /// Clique em cima = comida, clique no vidro = chamar o peixe, arrastar = mover.
    /// </summary>
    sealed class TankWindow : Form
    {
        readonly World world;
        LayeredSurface surface;
        bool allowZ, down, dragging;
        Point downCursor, downWin;

        public float RenderScale = 1.5f;
        public bool DesktopMode = true;
        public bool Hover { get; private set; }
        public bool UpgradeVisible { get; set; }

        public event Action<float> FeedAt;
        public event Action<float, float> TapAt;
        public event Action BadgeClicked, Moved;

        public TankWindow(World world)
        {
            this.world = world;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "MyAquario";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public Size PixelSize => new Size((int)Math.Ceiling(world.W * RenderScale), (int)Math.Ceiling(world.H * RenderScale));

        protected override void WndProc(ref Message m)
        {
            // Clicar no aquário não rouba o foco do programa que a pessoa está usando.
            if (m.Msg == Native.WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)Native.MA_NOACTIVATE;
                return;
            }
            // No modo área de trabalho, qualquer mudança de camada manda a janela para o fundo.
            if (m.Msg == Native.WM_WINDOWPOSCHANGING && DesktopMode && !allowZ)
            {
                var wp = (Native.WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(Native.WINDOWPOS));
                if ((wp.flags & Native.SWP_NOZORDER) == 0)
                {
                    wp.hwndInsertAfter = Native.HWND_BOTTOM;
                    Marshal.StructureToPtr(wp, m.LParam, false);
                }
            }
            base.WndProc(ref m);
        }

        public void ApplyZOrder()
        {
            if (!IsHandleCreated) return;
            if (DesktopMode)
            {
                TopMost = false;
                Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            else TopMost = true;
        }

        /// <summary>Depois do Win+D / "mostrar área de trabalho", traz o aquário de volta por cima do papel de parede.</summary>
        public void PeekAboveDesktop()
        {
            if (!DesktopMode || !Visible) return;
            allowZ = true;
            const uint f = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, f);
            Native.SetWindowPos(Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, f);
            allowZ = false;
        }

        public void RenderFrame(IRenderer r)
        {
            var size = PixelSize;
            if (surface == null) surface = new LayeredSurface(size.Width, size.Height);
            else surface.Resize(size.Width, size.Height);
            if (Width != size.Width || Height != size.Height) Size = size;

            using (var g = Graphics.FromImage(surface.Bitmap))
            {
                g.Clear(Color.Transparent);
                r.Render(g, world, RenderScale);
                Overlay.Draw(g, world, RenderScale, Hover, UpgradeVisible);
            }
            surface.Present(Handle, Left, Top);
        }

        public void ReleaseSurface()
        {
            surface?.Dispose();
            surface = null;
        }

        // ---- Mouse ----

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                down = true;
                dragging = false;
                downCursor = Cursor.Position;
                downWin = Location;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            world.MouseInside = true;
            world.Mouse = new PointF(e.X / RenderScale, e.Y / RenderScale);
            if (down)
            {
                var c = Cursor.Position;
                if (!dragging && (Math.Abs(c.X - downCursor.X) > 4 || Math.Abs(c.Y - downCursor.Y) > 4)) dragging = true;
                if (dragging)
                {
                    Location = new Point(downWin.X + c.X - downCursor.X, downWin.Y + c.Y - downCursor.Y);
                    surface?.Present(Handle, Left, Top);
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && down)
            {
                down = false;
                if (dragging) Moved?.Invoke();
                else HandleClick(e.Location);
            }
            base.OnMouseUp(e);
        }

        void HandleClick(Point p)
        {
            if (UpgradeVisible && RectangleF.Inflate(Overlay.BadgeRect(world, RenderScale), 3, 3).Contains(p))
            {
                BadgeClicked?.Invoke();
                return;
            }
            float x = p.X / RenderScale, y = p.Y / RenderScale;
            if (y < world.SurfaceY + (world.GravelTop - world.SurfaceY) * 0.3f) FeedAt?.Invoke(x);
            else TapAt?.Invoke(x, y);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Hover = true;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hover = false;
            world.MouseInside = false;
            base.OnMouseLeave(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ReleaseSurface();
            base.Dispose(disposing);
        }
    }
}
