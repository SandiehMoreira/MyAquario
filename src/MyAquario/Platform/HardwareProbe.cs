using System;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Windows.Forms;

namespace MyAquario
{
    /// <summary>Olha RAM, processador e placa de vídeo para sugerir o gráfico ideal.</summary>
    static class HardwareProbe
    {
        static Tier? cached;
        public static string Summary { get; private set; } = "verificando...";
        public static bool Done => cached.HasValue;

        public static Tier Recommend()
        {
            if (cached.HasValue) return cached.Value;

            double ramGb = 0;
            var ms = new Native.MEMORYSTATUSEX();
            if (Native.GlobalMemoryStatusEx(ms)) ramGb = ms.ullTotalPhys / 1073741824.0;
            int cores = Environment.ProcessorCount;

            string gpu = "";
            try
            {
                using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject o in s.Get())
                {
                    var name = (o["Name"] as string ?? "").Trim();
                    if (name.Length == 0 || name.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    gpu += (gpu.Length > 0 ? " / " : "") + name;
                }
            }
            catch (Exception ex) { Log.Error(ex); }

            bool dedicated = Regex.IsMatch(gpu, @"GeForce|RTX|GTX|Quadro|Radeon RX|Radeon Pro|Arc A|Arc B", RegexOptions.IgnoreCase);
            Tier t;
            if (ramGb < 4.5 || cores <= 2) t = Tier.Basico;
            else if (dedicated && ramGb >= 7.5 && cores >= 4) t = Tier.Ultra;
            else t = Tier.Normal;

            Summary = $"{Math.Round(ramGb)} GB de RAM · {cores} núcleos · {(gpu.Length > 0 ? gpu : "vídeo não identificado")}";
            cached = t;
            return t;
        }

        public static bool OnBattery => SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
    }

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static void Set(bool on)
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
                if (on) k.SetValue("MyAquario", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("MyAquario", false);
            }
            catch (Exception ex) { Log.Error(ex); }
        }

        public static bool IsOn()
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return k?.GetValue("MyAquario") != null;
            }
            catch { return false; }
        }
    }
}
