using System;

namespace MyAquario
{
    enum Species { Betta, Corydora, Rasbora, Neon }

    sealed class SpeciesInfo
    {
        public string Name, Plural;
        public float Length, Speed;
        // Faixa de altura onde o peixe gosta de nadar (0 = superfície, 1 = fundo)
        public float ZoneTop, ZoneBottom;
        public bool Schools, BottomFeeder;
    }

    static class SpeciesData
    {
        static readonly SpeciesInfo[] All =
        {
            new SpeciesInfo { Name = "Betta", Plural = "bettas", Length = 26, Speed = 14, ZoneTop = 0.0f, ZoneBottom = 0.75f },
            new SpeciesInfo { Name = "Coridora", Plural = "coridoras", Length = 14, Speed = 10, ZoneTop = 0.85f, ZoneBottom = 1f, BottomFeeder = true },
            new SpeciesInfo { Name = "Rasbora-arlequim", Plural = "rasboras-arlequim", Length = 11, Speed = 20, ZoneTop = 0.15f, ZoneBottom = 0.7f, Schools = true },
            new SpeciesInfo { Name = "Neon", Plural = "neons", Length = 9, Speed = 22, ZoneTop = 0.25f, ZoneBottom = 0.8f, Schools = true },
        };

        public static SpeciesInfo Get(Species s) => All[(int)s];
    }

    sealed class TankDef
    {
        public int Level, Liters, W, H, PointsNeeded, AddCount;
        public string Name;
        public Species Adds;
    }

    static class Tanks
    {
        // W/H em "unidades de mundo"; a janela multiplica pelo tamanho escolhido.
        public static readonly TankDef[] All =
        {
            new TankDef { Level = 1, Name = "Aquário do Betta",    Liters = 10,  W = 170, H = 139, PointsNeeded = 0,    Adds = Species.Betta,    AddCount = 1 },
            new TankDef { Level = 2, Name = "Aquário Plantado",    Liters = 30,  W = 220, H = 154, PointsNeeded = 150,  Adds = Species.Corydora, AddCount = 3 },
            new TankDef { Level = 3, Name = "Aquário Comunitário", Liters = 60,  W = 280, H = 174, PointsNeeded = 500,  Adds = Species.Rasbora,  AddCount = 6 },
            new TankDef { Level = 4, Name = "Aquário Grande",      Liters = 120, W = 350, H = 199, PointsNeeded = 1200, Adds = Species.Neon,     AddCount = 8 },
        };

        public static TankDef Get(int level) => All[Math.Max(1, Math.Min(All.Length, level)) - 1];
        public static TankDef Next(int level) => level >= 1 && level < All.Length ? All[level] : null;
    }
}
