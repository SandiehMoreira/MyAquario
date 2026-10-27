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
        // Visita: nada até onde clicaram no vidro
        public float VisitT, VisitX, VisitY;
        // Pulo para fora d'água: 0 = normal, 1 = subindo até a superfície, 2 = no ar
        public int JumpStage;
        public float JumpT, JumpDir, JumpX0;
        public float Len => Info.Length;
    }

    sealed class Pellet { public float X, Y, Vy, Seed, Float, GroundTime; public bool Ground; }
    sealed class Bubble { public float X, Y, R, Speed, Wob; }
    sealed class Drop { public float X, Y, Vx, Vy; }
    sealed class Plant { public float X, H, Phase; public int Kind, Blades, Shade; public bool Front; }
    sealed class Decor { public float X, W, H; public int Kind, Shade; }

    /// <summary>Tudo que está dentro do aquário: geometria, peixes, comida e bolhas.</summary>
    sealed class World
    {
        public readonly GameState State;
        public TankDef Def;

        // W inclui o espaço do potinho de ração; TankW é só o aquário.
        public float W, H, TankW, SurfaceY, GravelTop;
        public RectangleF Glass, Water, Jar;
        public float JarShake;
        public const float JarSpace = 26;

        public readonly List<Fish> Fish = new List<Fish>();
        public readonly List<Pellet> Pellets = new List<Pellet>();
        public readonly List<Bubble> Bubbles = new List<Bubble>();
        public readonly List<Drop> Drops = new List<Drop>();
        public readonly List<Plant> Plants = new List<Plant>();
        public readonly List<Decor> Decor = new List<Decor>();
        public readonly List<PointF> Algae = new List<PointF>();
        public readonly List<PointF> Nest = new List<PointF>();

        // Night: 0 = luz acesa, 1 = apagada (comportamento dos peixes).
        // Dim: quanto o desenho escurece; a noite é uma penumbra de luar, não um breu.
        public float Time, Night, CleanFx;
        public float Dim => Night * 0.5f;
        public float? FixedNight; // só para o --snapshot (fotos sempre de dia)
        // Interruptor: clicar na tampa acende/apaga até a próxima troca automática (7h30 / 21h30).
        public bool? LightOverride;
        DateTime overrideUntil;
        public bool LightOn => Night < 0.5f;
        // Cada aquário novo vem com uma luminária melhor (mais brilho vindo de cima).
        public float LampPower => 0.55f + 0.15f * State.TankLevel;
        public bool MouseInside;
        public PointF Mouse;
        public string ToastText;
        public float ToastTime;

        public bool HasAirstone => State.TankLevel >= 2;
        public PointF Airstone => new PointF(Water.Right - 12, GravelTop - 2);

        readonly Random rng = new Random();
        readonly PointF[] school = new PointF[4];
        float bubbleTimer, schoolTimer, tapWindow, lastTapHappy = -999, hungryTipTime = -999, jumpTimer = 20;
        int tapCount;

        public World(GameState s)
        {
            State = s;
            Night = ComputeNight(DateTime.Now); // começa já no estado certo, sem transição
            Rebuild();
        }

        float R() => (float)rng.NextDouble();

        public void Rebuild()
        {
            Def = Tanks.Get(State.TankLevel);
            TankW = Def.W;
            W = Def.W + JarSpace;
            H = Def.H;
            Glass = new RectangleF(3, 12, TankW - 6, H - 12 - 10);
            // 20 de ar entre a água e a tampa: espaço para o betta pular.
            Water = RectangleF.FromLTRB(Glass.Left + 2, Glass.Top + 20, Glass.Right - 2, Glass.Bottom - 2);
            Jar = new RectangleF(TankW + 5, H - 1 - 24, 18, 24);
            SurfaceY = Water.Top;
            GravelTop = Water.Bottom - Math.Max(14f, H * 0.12f);

            Plants.Clear(); Decor.Clear(); Algae.Clear(); Nest.Clear();
            Fish.Clear(); Pellets.Clear(); Bubbles.Clear(); Drops.Clear();

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
            var now = DateTime.Now;
            if (LightOverride.HasValue && now >= overrideUntil) LightOverride = null;
            float targetNight = LightOverride.HasValue ? (LightOverride.Value ? 0 : 1) : ComputeNight(now);
            // Transição suave ao clicar no interruptor
            Night = FixedNight ?? Night + (targetNight - Night) * Math.Min(1, dt * 3);
            if (ToastTime > 0) ToastTime -= dt;
            if (CleanFx > 0) CleanFx -= dt;
            if (JarShake > 0) JarShake -= dt;
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
            UpdateDrops(dt);
        }

        void UpdateFish(Fish f, float dt)
        {
            if (f.Sp == Species.Betta && UpdateJump(f, dt)) return;
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
            else if (f.VisitT > 0)
            {
                // Clicaram no vidro: vai até lá ver o que é.
                f.VisitT -= dt;
                f.TX = f.VisitX;
                f.TY = f.VisitY;
                maxV *= 1.2f;
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

            var bounds = (food != null || f.Scare > 0 || f.VisitT > 0) ? WaterBounds(f) : zone;
            f.TX = M.Clamp(f.TX, bounds.Left, bounds.Right);
            f.TY = M.Clamp(f.TY, bounds.Top, bounds.Bottom);
            Steer(f, maxV, accel, dt);
        }

        void Steer(Fish f, float maxV, float accel, float dt)
        {
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

        // Muito feliz (humor 85+), sem fome e de dia: de vez em quando dá um pulinho para fora d'água.
        bool UpdateJump(Fish f, float dt)
        {
            if (f.JumpStage == 0)
            {
                bool canJump = State.Happiness >= 85 && State.Hunger < 40 && Night < 0.5f
                    && Pellets.Count == 0 && f.Scare <= 0 && f.VisitT <= 0;
                if (!canJump) { jumpTimer = Math.Max(jumpTimer, 8); return false; }
                jumpTimer -= dt;
                if (jumpTimer > 0) return false;
                jumpTimer = 35 + R() * 55;
                StartJumpApproach(f);
            }

            if (f.JumpStage == 1)
            {
                // Sobe até a superfície, já pegando impulso para o lado do pulo.
                f.TX = M.Clamp(f.X + f.JumpDir * 12, Water.Left + 15, Water.Right - 15);
                f.TY = SurfaceY + 3;
                Steer(f, f.Info.Speed * 1.6f, f.Info.Speed * 4, dt);
                if (f.Y <= SurfaceY + 5)
                {
                    f.JumpStage = 2;
                    f.JumpT = 0;
                    f.JumpX0 = f.X;
                    Splash(f.X, 6);
                }
                return true;
            }

            // No ar: um arco curto e de volta para a água.
            const float duration = 1.1f, height = 11, distance = 26;
            float px = f.X, py = f.Y;
            f.JumpT = Math.Min(1, f.JumpT + dt / duration);
            float t = f.JumpT;
            f.X = f.JumpX0 + f.JumpDir * distance * t;
            f.Y = SurfaceY + 3 - M.Sin(t * (float)Math.PI) * height;
            f.Vx = (f.X - px) / dt;
            f.Vy = (f.Y - py) / dt;
            f.Facing = f.JumpDir;
            f.Phase += dt * 9;
            if (t >= 1)
            {
                f.JumpStage = 0;
                f.Vy = 12;
                f.Y = SurfaceY + 4;
                Splash(f.X, 8);
                for (int i = 0; i < 6; i++)
                    Bubbles.Add(new Bubble { X = f.X + (R() - 0.5f) * 8, Y = SurfaceY + 4 + R() * 6, R = 0.6f + R(), Speed = 10 + R() * 10, Wob = R() * 6 });
            }
            return true;
        }

        void StartJumpApproach(Fish f)
        {
            f.JumpStage = 1;
            f.JumpDir = f.Facing >= 0 ? 1 : -1;
            // Precisa de espaço para o arco dentro do aquário.
            if (f.X + f.JumpDir * 40 > Water.Right - 10 || f.X + f.JumpDir * 40 < Water.Left + 10) f.JumpDir = -f.JumpDir;
        }

        /// <summary>Só para o --snapshot: congela o betta no meio do pulo.</summary>
        public void DebugJumpPose(float t)
        {
            var f = Fish.Find(x => x.Sp == Species.Betta);
            if (f == null) return;
            f.X = Water.Left + Water.Width * 0.4f;
            f.Facing = 1;
            StartJumpApproach(f);
            f.JumpStage = 2;
            f.JumpX0 = f.X;
            f.JumpT = t - 0.02f;
            Splash(f.X, 6);
            UpdateJump(f, 0.02f * 1.1f);
        }

        void Splash(float x, int n)
        {
            for (int i = 0; i < n; i++)
                Drops.Add(new Drop { X = x + (R() - 0.5f) * 5, Y = SurfaceY - 0.5f, Vx = (R() - 0.5f) * 30, Vy = -(18 + R() * 22) });
        }

        void UpdateDrops(float dt)
        {
            for (int i = Drops.Count - 1; i >= 0; i--)
            {
                var d = Drops[i];
                d.Vy += 70 * dt;
                d.X += d.Vx * dt;
                d.Y += d.Vy * dt;
                if (d.Vy > 0 && d.Y >= SurfaceY) Drops.RemoveAt(i);
            }
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
            if (State.Hunger <= 3) Toast(State.BettaName + " está sem fome agora!");
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
                return;
            }

            var betta = Fish.Find(f => f.Sp == Species.Betta);
            if (betta == null || betta.JumpStage != 0) return;
            if (State.Hunger > 60)
            {
                // Com fome ele não vem brincar.
                if (Time - hungryTipTime > 15)
                {
                    hungryTipTime = Time;
                    Toast($"{State.BettaName} está com fome e não quer brincar. Dê ração no potinho!", 5);
                }
                return;
            }
            betta.VisitT = 3.5f;
            betta.VisitX = x;
            betta.VisitY = y;
            betta.Rest = 0;
            if (Time - lastTapHappy > 30)
            {
                lastTapHappy = Time;
                State.Happiness = Math.Min(100, State.Happiness + 2);
            }
        }

        public void ToggleLight()
        {
            bool on = !LightOn;
            LightOverride = on;
            var now = DateTime.Now;
            DateTime lightsOn = now.Date.AddHours(7.5), lightsOff = now.Date.AddHours(21.5);
            overrideUntil = now < lightsOn ? lightsOn : now < lightsOff ? lightsOff : lightsOn.AddDays(1);
            Toast(on ? "Luz acesa!" : "Luz apagada. Boa noite, peixinhos!", 2.5f);
        }

        public void FeedFromJar()
        {
            JarShake = 0.45f;
            DropFood(Water.Left + Water.Width * (0.3f + R() * 0.45f));
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
