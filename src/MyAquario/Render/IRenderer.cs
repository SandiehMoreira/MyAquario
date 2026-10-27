using System;
using System.Drawing;

namespace MyAquario
{
    enum Tier { Basico = 0, Normal = 1, Ultra = 2 }

    interface IRenderer : IDisposable
    {
        Tier Tier { get; }
        int Fps { get; }
        /// <summary>Desenha o aquário. <paramref name="scale"/> = pixels por unidade de mundo.</summary>
        void Render(Graphics g, World w, float scale);
        /// <summary>Solta texturas e caches (modo repouso). Recarrega sozinho no próximo Render.</summary>
        void Release();
    }

    static class TierText
    {
        public static string Name(Tier t) => t switch
        {
            Tier.Normal => "Normal",
            Tier.Ultra => "Ultra",
            _ => "Básico",
        };

        public static string Style(Tier t) => t switch
        {
            Tier.Normal => "cartoon 2D",
            Tier.Ultra => "realista",
            _ => "pixel art",
        };

        public static string Key(Tier t) => t switch
        {
            Tier.Normal => "normal",
            Tier.Ultra => "ultra",
            _ => "basico",
        };

        public static Tier? FromKey(string key) => key switch
        {
            "basico" => Tier.Basico,
            "normal" => Tier.Normal,
            "ultra" => Tier.Ultra,
            _ => (Tier?)null,
        };
    }

    static class WorldLook
    {
        // 0 = limpo, 1 = muito sujo (começa a aparecer abaixo de 80% de limpeza)
        public static float Dirt(World w) => M.Clamp((80f - (float)w.State.Cleanliness) / 80f, 0f, 1f);
        public static bool Sad(World w) => w.State.Happiness < 35;

        public static readonly Color[] BettaColors =
        {
            Color.FromArgb(205, 35, 50),   // vermelho
            Color.FromArgb(45, 90, 215),   // azul
            Color.FromArgb(140, 55, 180),  // roxo
            Color.FromArgb(25, 165, 175),  // turquesa
        };

        public static readonly string[] BettaColorNames = { "Vermelho", "Azul", "Roxo", "Turquesa" };
    }
}
