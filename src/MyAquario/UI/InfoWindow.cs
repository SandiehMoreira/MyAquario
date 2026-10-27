using System;
using System.Drawing;
using System.Windows.Forms;

namespace MyAquario
{
    /// <summary>
    /// Janelinha de informações que aparece por cima do aquário (ou embaixo, se não couber).
    /// Não recebe clique nem foco: o mouse passa direto por ela.
    /// </summary>
    sealed class InfoWindow : Form
    {
        LayeredSurface surface;

        public InfoWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "MyAquario - info";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT
                    | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        /// <summary>Igual ao aquário: no modo área de trabalho fica atrás das janelas; senão, sempre por cima.</summary>
        public bool DesktopMode = true;

        public void ApplyZOrder()
        {
            if (!IsHandleCreated || !Visible) return;
            const uint f = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
            if (DesktopMode) Native.SetWindowPos(Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, f);
            Native.SetWindowPos(Handle, DesktopMode ? Native.HWND_BOTTOM : Native.HWND_TOPMOST, 0, 0, 0, 0, f);
        }

        /// <summary>Depois do Win+D, volta a aparecer por cima do papel de parede.</summary>
        public void PeekAboveDesktop()
        {
            if (!DesktopMode || !Visible) return;
            const uint f = Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE;
            Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, f);
            Native.SetWindowPos(Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0, f);
        }

        public void ShowInfo(World w, Rectangle tank, float scale, bool status)
        {
            bool toast = Overlay.HasToast(w);
            if (!status && !toast)
            {
                if (Visible) Hide();
                return;
            }

            var size = Overlay.InfoSize(w, scale, status, toast);
            if (surface == null) surface = new LayeredSurface(size.Width, size.Height);
            else surface.Resize(size.Width, size.Height);
            using (var g = Graphics.FromImage(surface.Bitmap))
            {
                g.Clear(Color.Transparent);
                Overlay.DrawInfo(g, w, scale, status, toast);
            }

            // Em cima do aquário, centralizado; se não couber na tela, embaixo.
            var wa = Screen.FromRectangle(tank).WorkingArea;
            int gap = (int)(4 * scale);
            int x = tank.Left + (tank.Width - size.Width) / 2;
            int y = tank.Top - size.Height - gap;
            if (y < wa.Top) y = tank.Bottom + gap;
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - size.Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - size.Height));

            if (Width != size.Width || Height != size.Height) Size = size;
            if (!Visible)
            {
                Show();
                ApplyZOrder();
            }
            if (Left != x || Top != y) Location = new Point(x, y);
            surface.Present(Handle, x, y);
        }

        public void HideInfo()
        {
            if (Visible) Hide();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { surface?.Dispose(); surface = null; }
            base.Dispose(disposing);
        }
    }
}
