using System;
using System.Collections.Generic;
using System.Drawing;

namespace MyAquario
{
    sealed class Fish
    {
        public Species Sp;
        public SpeciesInfo Info;
        public float X, Y, Vx, Vy, TX, TY, Timer, Phase, Facing = 1, OffX, OffY, Scare, Rest;
        public int Variant;
        public float Len => Info.Length;
    }

    sealed class Pellet { public float X, Y, Vy, Seed, Float, GroundTime; public bool Ground; }
    sealed class Bubble { public float X, Y, R, Speed, Wob; }
    sealed class Plant { public float X, H, Phase; public int Kind, Blades, Shade; public bool Front; }
    sealed class Decor { public float X, W, H; public int Kind, Shade; }

    /// <summary>Tudo que está dentro do aquário: geometria, peixes, comida e bolhas.</summary>
    sealed class World
    {
        public readonly GameState State;
        public TankDef Def;

        public float W, H, SurfaceY, GravelTop;
        public RectangleF Glass, Water;

        public readonly List<Fish> Fish = new List<Fish>();
        public readonly List<Pellet> Pellets = new List<Pellet>();
        public readonly List<Bubble> Bubbles = new List<Bubble>();
        public readonly List<Plant> Plants = new List<Plant>();
        public readonly List<Decor> Decor = new List<Decor>();
        public readonly List<PointF> Algae = new List<PointF>();
        public readonly List<PointF> Nest = new List<PointF>();

        public float Time, Night, CleanFx;
        public float? FixedNight; // só para o --snapshot (fotos sempre de dia)
        public bool MouseInside;
        public PointF Mouse;
        public string ToastText;
        public float ToastTime;

        public bool HasAirstone => State.TankLevel >= 2;
        public PointF Airstone => new PointF(Water.Right - 12, GravelTop - 2);

        readonly Random rng = new Random();
        readonly PointF[] school = new PointF[4];
        float bubbleTimer, schoolTimer, tapWindow, lastTapHappy = -999;
        int tapCount;

        public World(GameState s)
        {
            State = s;
            Rebuild();
        }

        float R() => (float)rng.NextDouble();

        public void Rebuild()
        {
            Def = Tanks.Get(State.TankLevel);
            W = Def.W;
            H = Def.H;
            Glass = new RectangleF(3, 12, W - 6, H - 12 - 10);
            Water = RectangleF.FromLTRB(Glass.Left + 2, Glass.Top + 6, Glass.Right - 2, Glass.Bottom - 2);
            SurfaceY = Water.Top;
            GravelTop = Water.Bottom - Math.Max(14f, H * 0.12f);

            Plants.Clear(); Decor.Clear(); Algae.Clear(); Nest.Clear();
            Fish.Clear(); Pellets.Clear(); Bubbles.Clear();

            // Cenário com semente fixa: o mesmo aquário toda vez que abrir.
            var r = new Random(State.TankLevel * 7919 + (int)(State.CreatedUtc % 100000));
            float F() => (float)r.NextDouble();
            float span = GravelTop - SurfaceY;

            int nPlants = 2 + State.TankLevel * 2;
            var back = new List<Plant>();
            var front = new List<Plant>();
            for (int i = 0; i < nPlants; i++)
            {
                int kind = i % 3;
                var p = new Plant
                {
                    Kind = kind,
                    X = Water.Left + 8 + F() * (Water.Width - 16),
                    Phase = F() * 6.28f,
                    Shade = r.Next(3),
                };
                switch (kind)
                {
                    case 0: p.H = span * (0.5f + F() * 0.35f); p.Blades = 4 + r.Next(4); break;
                    case 1: p.H = 14 + F() * 8; p.Blades = 3 + r.Next(3); p.Front = true; break;
                    default: p.H = span * (0.35f + F() * 0.2f); p.Blades = 5 + r.Next(4); p.Front = r.Next(3) == 0; break;
                }
                (p.Front ? front : back).Add(p);
            }
            Plants.AddRange(back);
            Plants.AddRange(front);

            int rocks = 1 + State.TankLevel / 2;
            for (int i = 0; i < rocks; i++)
                Decor.Add(new Decor { Kind = 0, X = Water.Left + 15 + F() * (Water.Width - 30), W = 14 + F() * 12, H = 8 + F() * 6, Shade = r.Next(3) });
            if (State.TankLevel >= 2)
                Decor.Insert(0, new Decor { Kind = 1, X = Water.Left + Water.Width * (0.3f + F() * 0.4f), W = 40 + F() * 20, H = 14 + F() * 6 });

            for (int i = 0; i < 14; i++)
                Algae.Add(new PointF(Glass.Left + 3 + F() * (Glass.Width - 6), SurfaceY + 4 + F() * (Glass.Bottom - SurfaceY - 8)));

            for (int i = 0; i < 18; i++)
                Nest.Add(new PointF(Water.Left + 10 + (F() + F() - 1) * 14 + 8, SurfaceY + 1 + F() * 2));

            foreach (var d in Tanks.All)
                if (d.Level <= State.TankLevel)
                    for (int i = 0; i < d.AddCount; i++) AddFish(d.Adds);

            for (int i = 0; i < school.Length; i++) school[i] = RandomIn(Zone(SpeciesData.Get((Species)i)), 15);
        }

        void AddFish(Species sp)
        {
            var f = new Fish { Sp = sp, Info = SpeciesData.Get(sp) };
            f.Variant = sp == Species.Betta ? State.BettaColor : rng.Next(3);
            var p = RandomIn(Zone(f.Info), 0);
            f.X = f.TX = p.X;
            f.Y = f.TY = p.Y;
            f.Phase = R() * 6;
            f.Facing = rng.Next(2) == 0 ? -1 : 1;
            f.OffX = (R() - 0.5f) * 30;
            f.OffY = (R() - 0.5f) * 16;
            Fish.Add(f);
        }

        PointF RandomIn(RectangleF z, float shrink)
        {
            float w = Math.Max(1, z.Width - shrink * 2), h = Math.Max(1, z.Height - shrink * 2 * 0.4f);
            return new PointF(z.Left + (z.Width - w) / 2 + R() * w, z.Top + (z.Height - h) / 2 + R() * h);
        }

        public RectangleF Zone(SpeciesInfo info)
        {
            float m = info.Length * 0.6f;
            float top, bot;
            if (info.BottomFeeder)
            {
                top = GravelTop - 12;
                bot = GravelTop - 3;
            }
            else
            {
                float a = SurfaceY + 6, b = GravelTop - 6;
                top = a + (b - a) * info.ZoneTop;
                bot = a + (b - a) * info.ZoneBottom;
            }
            return RectangleF.FromLTRB(Water.Left + m, top, Water.Right - m, Math.Max(top + 1, bot));
        }

        RectangleF WaterBounds(Fish f)
        {
            float m = f.Len * 0.5f;
            return RectangleF.FromLTRB(Water.Left + m, SurfaceY + 3, Water.Right - m, GravelTop - 2);
        }

        public void Toast(string text, float seconds = 3.5f)
        {
            ToastText = text;
            ToastTime = seconds;
        }

        // Luz do aquário: acesa das 7h30 às 21h30, com meia hora de transição.
        static float ComputeNight(DateTime now)
        {
            double h = now.Hour + now.Minute / 60.0;
            if (h >= 7.5 && h < 21.5) return 0;
            if (h >= 22 || h < 7) return 1;
            if (h >= 21.5) return (float)((h - 21.5) / 0.5);
            return (float)(1 - (h - 7) / 0.5);
        }

        public void Update(float dt)
        {
            Time += dt;
            Night = FixedNight ?? ComputeNight(DateTime.Now);
            if (ToastTime > 0) ToastTime -= dt;
            if (CleanFx > 0) CleanFx -= dt;
            if (tapWindow > 0) { tapWindow -= dt; if (tapWindow <= 0) tapCount = 0; }

            schoolTimer -= dt;
            if (schoolTimer <= 0)
            {
                schoolTimer = 3 + R() * 4;
                school[(int)Species.Rasbora] = RandomIn(Zone(SpeciesData.Get(Species.Rasbora)), 15);
                school[(int)Species.Neon] = RandomIn(Zone(SpeciesData.Get(Species.Neon)), 15);
            }

            foreach (var f in Fish) UpdateFish(f, dt);
            UpdatePellets(dt);
            UpdateBubbles(dt);
        }

        void UpdateFish(Fish f, float dt)
        {
            var zone = Zone(f.Info);
            float mood = (float)State.Happiness / 100f;
            float maxV = f.Info.Speed * (0.5f + 0.5f * mood) * (1 - 0.55f * Night);
            float accel = f.Info.Speed * 2.2f;
            Pellet food = WantsFood(f) ? NearestFood(f) : null;

            if (f.Scare > 0)
            {
                f.Scare -= dt;
                maxV = f.Info.Speed * 2.6f;
                accel *= 3;
            }
            else if (food != null)
            {
                f.TX = food.X;
                f.TY = food.Y;
                maxV = f.Info.Speed * 1.7f;
                accel *= 1.8f;
                float dx = food.X - f.X, dy = food.Y - f.Y, reach = f.Len * 0.45f;
                if (dx * dx + dy * dy < reach * reach) Eat(food);
            }
            else if (f.Sp == Species.Betta && MouseInside && Night < 0.5f)
            {
                // Betta é curioso: segue o cursor pelo vidro.
                f.TX = Mouse.X;
                f.TY = Mouse.Y;
                maxV *= 0.8f;
            }
            else if (f.Info.Schools)
            {
                var s = school[(int)f.Sp];
                f.TX = s.X + f.OffX;
                f.TY = s.Y + f.OffY;
            }
            else
            {
                f.Timer -= dt;
                float ddx = f.TX - f.X, ddy = f.TY - f.Y;
                if (f.Timer <= 0 || ddx * ddx + ddy * ddy < 16)
                {
                    var p = RandomIn(zone, 0);
                    f.TX = p.X;
                    f.TY = p.Y;
                    f.Timer = 2.5f + R() * 4;
                    if (f.Sp == Species.Corydora && rng.Next(3) == 0) f.Rest = 2 + R() * 4;
                }
            }

            if (f.Rest > 0 && food == null && f.Scare <= 0)
            {
                f.Rest -= dt;
                maxV *= 0.1f;
            }

            var bounds = (food != null || f.Scare > 0 || (f.Sp == Species.Betta && MouseInside)) ? WaterBounds(f) : zone;
            f.TX = M.Clamp(f.TX, bounds.Left, bounds.Right);
            f.TY = M.Clamp(f.TY, bounds.Top, bounds.Bottom);

            float tx = f.TX - f.X, ty = f.TY - f.Y;
            float d = M.Sqrt(tx * tx + ty * ty);
            float want = d < 0.5f ? 0 : maxV * Math.Min(1, d / 18f);
            float dvx = (d > 0 ? tx / d * want : 0) - f.Vx;
            float dvy = (d > 0 ? ty / d * want * 0.6f : 0) - f.Vy;
            float dv = M.Sqrt(dvx * dvx + dvy * dvy), maxDv = accel * dt;
            if (dv > maxDv) { dvx *= maxDv / dv; dvy *= maxDv / dv; }
            f.Vx += dvx;
            f.Vy += dvy;
            f.X += f.Vx * dt;
            f.Y += f.Vy * dt;

            var wb = WaterBounds(f);
            if (f.X < wb.Left) { f.X = wb.Left; f.Vx = Math.Abs(f.Vx) * 0.3f; }
            if (f.X > wb.Right) { f.X = wb.Right; f.Vx = -Math.Abs(f.Vx) * 0.3f; }
            if (f.Y < wb.Top) { f.Y = wb.Top; f.Vy = 0; }
            if (f.Y > wb.Bottom) { f.Y = wb.Bottom; f.Vy = 0; }

            if (Math.Abs(f.Vx) > 1.5f)
                f.Facing += (Math.Sign(f.Vx) - f.Facing) * Math.Min(1, dt * 5);

            float speed = M.Sqrt(f.Vx * f.Vx + f.Vy * f.Vy);
            f.Phase += dt * (2.2f + speed * 0.35f);
        }

        bool WantsFood(Fish f) => f.Info.BottomFeeder || State.Hunger > 3;

        Pellet NearestFood(Fish f)
        {
            Pellet best = null;
            float bd = float.MaxValue;
            foreach (var p in Pellets)
            {
                // Coridoras limpam o fundo; os outros pegam a comida caindo.
                bool reachable = f.Info.BottomFeeder ? p.Ground || p.Y > GravelTop - 12 : !p.Ground;
                if (!reachable) continue;
                float dx = p.X - f.X, dy = p.Y - f.Y, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = p; }
            }
            return best;
        }

        void Eat(Pellet p)
        {
            Pellets.Remove(p);
            if (State.Hunger > 3)
            {
                if (State.Hunger > 30) State.CarePoints += 1;
                State.Hunger = Math.Max(0, State.Hunger - 8);
                State.Happiness = Math.Min(100, State.Happiness + 0.6);
            }
        }

        void UpdatePellets(float dt)
        {
            for (int i = Pellets.Count - 1; i >= 0; i--)
            {
                var p = Pellets[i];
                if (p.Ground)
                {
                    p.GroundTime += dt;
                    if (p.GroundTime > 25)
                    {
                        Pellets.RemoveAt(i);
                        State.Cleanliness = Math.Max(0, State.Cleanliness - 1.5);
                        if (!State.OverfeedTipShown)
                        {
                            State.OverfeedTipShown = true;
                            Toast("Comida que sobra suja a água. Dê só o que eles comem!", 6);
                        }
                    }
                    continue;
                }
                if (p.Float > 0)
                {
                    p.Float -= dt;
                    p.X += M.Sin(Time * 2 + p.Seed) * 2 * dt;
                    continue;
                }
                p.Vy = Math.Min(p.Vy + 20 * dt, 9);
                p.Y += p.Vy * dt;
                p.X += M.Sin(Time * 1.7f + p.Seed) * 3 * dt;
                if (p.Y >= GravelTop - 1)
                {
                    p.Y = GravelTop - 1;
                    p.Ground = true;
                }
            }
        }

        void UpdateBubbles(float dt)
        {
            if (HasAirstone)
            {
                bubbleTimer -= dt;
                if (bubbleTimer <= 0)
                {
                    bubbleTimer = 0.18f + R() * 0.25f;
                    var a = Airstone;
                    Bubbles.Add(new Bubble { X = a.X + R() * 3 - 1.5f, Y = a.Y - 1, R = 0.8f + R() * 1.4f, Speed = 18 + R() * 14, Wob = R() * 6 });
                }
            }
            for (int i = Bubbles.Count - 1; i >= 0; i--)
            {
                var b = Bubbles[i];
                b.Y -= b.Speed * dt;
                b.X += M.Sin(Time * 4 + b.Wob) * 6 * dt;
                if (b.Y <= SurfaceY + 1) Bubbles.RemoveAt(i);
            }
            if (Bubbles.Count > 90) Bubbles.RemoveRange(0, Bubbles.Count - 90);
        }

        // ---- Ações do jogador ----

        public void DropFood(float x)
        {
            if (State.Hunger <= 3) Toast(State.BettaName + " está de barriga cheia!");
            if (Pellets.Count >= 18) return;
            x = M.Clamp(x, Water.Left + 6, Water.Right - 6);
            for (int i = 0; i < 3; i++)
                Pellets.Add(new Pellet { X = x + (R() - 0.5f) * 10, Y = SurfaceY + 1, Float = 0.6f + R() * 0.8f, Seed = R() * 6.28f });
        }

        public void Tap(float x, float y)
        {
            tapCount++;
            tapWindow = 4f;
            if (tapCount >= 5)
            {
                foreach (var f in Fish)
                {
                    float dx = f.X - x, dy = f.Y - y, d = Math.Max(1, M.Sqrt(dx * dx + dy * dy));
                    if (d > 80) continue;
                    f.Scare = 1.2f;
                    f.TX = f.X + dx / d * 60;
                    f.TY = f.Y + dy / d * 30;
                }
                State.Happiness = Math.Max(0, State.Happiness - 2);
                if (tapCount == 5) Toast("Bater muito no vidro assusta os peixes!");
            }
            else if (Time - lastTapHappy > 30)
            {
                lastTapHappy = Time;
                State.Happiness = Math.Min(100, State.Happiness + 2);
            }
        }

        public void Clean()
        {
            bool dirty = State.Cleanliness < 70;
            State.Cleanliness = 100;
            if (dirty) State.CarePoints += 10;
            CleanFx = 1.2f;
            Pellets.RemoveAll(p => p.Ground);
            for (int i = 0; i < 30; i++)
                Bubbles.Add(new Bubble { X = Water.Left + 4 + R() * (Water.Width - 8), Y = GravelTop - 2 - R() * 10, R = 0.8f + R() * 1.6f, Speed = 25 + R() * 20, Wob = R() * 6 });
            Toast(dirty ? "Água limpinha! +10 pontos de cuidado" : "A água já estava limpa.");
        }
    }
}
