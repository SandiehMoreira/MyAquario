using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MyAquario
{
    [DataContract]
    sealed class GameState
    {
        // 0 = barriga cheia, 100 = faminto
        [DataMember] public double Hunger;
        // 100 = água limpa, 0 = muito suja
        [DataMember] public double Cleanliness;
        [DataMember] public double Happiness;
        [DataMember] public double CarePoints;
        [DataMember] public int TankLevel;
        [DataMember] public long LastUpdateUtc;
        [DataMember] public long CreatedUtc;
        [DataMember] public long LastHungerNotice;
        [DataMember] public long LastDirtNotice;
        [DataMember] public string BettaName;
        [DataMember] public int BettaColor;
        [DataMember] public bool OverfeedTipShown;
        [DataMember] public bool UpgradeNoticeShown;
        [DataMember] public bool HelpShown;

        public GameState() { Defaults(); }

        [OnDeserializing]
        void OnDeserializing(StreamingContext c) => Defaults();

        void Defaults()
        {
            Hunger = 70; // começa com fome: a primeira coisa que a pessoa aprende é usar o potinho
            Cleanliness = 100;
            Happiness = 70;
            TankLevel = 1;
            BettaName = "Rubi";
            BettaColor = new Random().Next(4);
            LastUpdateUtc = CreatedUtc = DateTime.UtcNow.Ticks;
        }
    }

    [DataContract]
    sealed class AppSettings
    {
        // "auto", "basico", "normal", "ultra"
        [DataMember] public string Quality;
        [DataMember] public double SizeScale;
        // true = fica atrás das janelas, como enfeite da área de trabalho
        [DataMember] public bool DesktopMode;
        [DataMember] public bool HasPosition;
        [DataMember] public int WindowX, WindowY;
        [DataMember] public bool Notifications;
        [DataMember] public bool StartWithWindows;
        [DataMember] public bool AutoRest;
        [DataMember] public int IdleMinutes;
        [DataMember] public bool SaveOnBattery;
        [DataMember] public bool ShowTank;
        [DataMember] public bool PackOfferShown;

        public AppSettings() { Defaults(); }

        [OnDeserializing]
        void OnDeserializing(StreamingContext c) => Defaults();

        void Defaults()
        {
            Quality = "auto";
            SizeScale = 1.5;
            DesktopMode = true;
            Notifications = true;
            AutoRest = true;
            IdleMinutes = 5;
            SaveOnBattery = true;
            ShowTank = true;
        }
    }

    static class Storage
    {
        public static T Load<T>(string file, Func<T> fallback) where T : class
        {
            var path = Path.Combine(Paths.Data, file);
            try
            {
                if (!File.Exists(path)) return fallback();
                // ReadAllText tira o BOM, caso o arquivo tenha sido editado à mão no Bloco de Notas.
                var bytes = System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path));
                using var ms = new MemoryStream(bytes);
                return (new DataContractJsonSerializer(typeof(T)).ReadObject(ms) as T) ?? fallback();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                try { File.Copy(path, path + ".corrompido", true); } catch { }
                return fallback();
            }
        }

        public static void Save<T>(string file, T obj)
        {
            var path = Path.Combine(Paths.Data, file);
            var tmp = path + ".tmp";
            try
            {
                using (var fs = File.Create(tmp))
                    new DataContractJsonSerializer(typeof(T)).WriteObject(fs, obj);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error(ex); }
        }
    }

    /// <summary>Regras do pet: fome, sujeira, humor e pontos de cuidado.</summary>
    static class Care
    {
        public const double HungerPerHour = 6;   // ~16 h de cheio a faminto
        public const double DirtPerHour = 1.2;   // ~3,5 dias até a água ficar muito suja
        public const double PointsPerHour = 6;   // enquanto estiver feliz

        // Avança o tempo real que passou, inclusive com o PC desligado (máx. 14 dias).
        public static void CatchUp(GameState s, DateTime nowUtc)
        {
            double hours = (nowUtc.Ticks - s.LastUpdateUtc) / (double)TimeSpan.TicksPerHour;
            s.LastUpdateUtc = nowUtc.Ticks;
            if (hours <= 0) return;
            hours = Math.Min(hours, 24 * 14);
            while (hours > 1e-9)
            {
                double h = Math.Min(hours, 1.0 / 6);
                Step(s, h);
                hours -= h;
            }
        }

        static void Step(GameState s, double h)
        {
            s.Hunger = M.Clamp(s.Hunger + HungerPerHour * h, 0, 100);
            s.Cleanliness = M.Clamp(s.Cleanliness - DirtPerHour * h, 0, 100);
            double target = TargetHappiness(s);
            double rate = 12 * h;
            s.Happiness = M.Clamp(s.Happiness + M.Clamp(target - s.Happiness, -rate, rate), 0, 100);
            if (s.Happiness >= 50) s.CarePoints += PointsPerHour * h;
        }

        public static double TargetHappiness(GameState s) =>
            M.Clamp(100 - Math.Max(0, s.Hunger - 35) * 1.1 - Math.Max(0, 65 - s.Cleanliness) * 0.9, 5, 100);

        public static bool UpgradeReady(GameState s)
        {
            var n = Tanks.Next(s.TankLevel);
            return n != null && s.CarePoints >= n.PointsNeeded;
        }

        public static string Mood(GameState s) =>
            s.Happiness >= 75 ? "feliz" : s.Happiness >= 50 ? "tranquilo" : s.Happiness >= 30 ? "entediado" : "triste";
    }
}
