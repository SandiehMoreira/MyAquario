using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MyAquario
{
    /// <summary>
    /// Ferramenta de desenvolvimento: MyAquario.exe --snapshot &lt;pasta-saida&gt; &lt;pasta-dos-pacotes&gt;
    /// Salva um PNG de cada gráfico em cada tamanho de aquário, sem abrir janela nem mexer no save.
    /// </summary>
    static class Snapshot
    {
        public static void Run(string outDir, string packsDir)
        {
            Directory.CreateDirectory(outDir);
            foreach (int level in new[] { 1, 4 })
            {
                var state = new GameState { TankLevel = level, BettaColor = 0, Happiness = 90, Hunger = 40, Cleanliness = level == 4 ? 35 : 100, CarePoints = 2000 };
                var world = new World(state) { FixedNight = 0 };
                for (int i = 0; i < 120; i++) world.Update(1 / 20f); // 6 s de animação
                world.DropFood(world.Water.Left + world.Water.Width * 0.5f);
                world.Update(0.5f);

                var renderers = new IRenderer[]
                {
                    new PixelRenderer(),
                    new CartoonRenderer(Path.Combine(packsDir, "pack-normal")),
                    new UltraRenderer(Path.Combine(packsDir, "pack-ultra")),
                };
                foreach (var r in renderers)
                {
                    const float scale = 3f;
                    using var bmp = new Bitmap((int)Math.Ceiling(world.W * scale), (int)Math.Ceiling(world.H * scale), PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.FromArgb(240, 240, 240));
                        r.Render(g, world, scale);
                        Overlay.Draw(g, world, scale, false);
                    }
                    bmp.Save(Path.Combine(outDir, $"nivel{level}-{TierText.Key(r.Tier)}.png"), ImageFormat.Png);
                    r.Dispose();
                }

                // Painel de informações (o que aparece fora do aquário ao passar o mouse)
                world.Toast("Água limpinha! +10 pontos de cuidado");
                var size = Overlay.InfoSize(world, 2f, true, true);
                using var info = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(info))
                {
                    g.Clear(Color.FromArgb(240, 240, 240));
                    Overlay.DrawInfo(g, world, 2f, true, true);
                }
                info.Save(Path.Combine(outDir, $"nivel{level}-info.png"), ImageFormat.Png);
            }
        }
    }
}
