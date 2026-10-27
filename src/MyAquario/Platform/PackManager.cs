using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyAquario
{
    sealed class PackInfo
    {
        public string Id, Name, Description;
        public Tier Tier;
        public string FileName => "pack-" + Id + ".zip";
    }

#pragma warning disable CS0649 // preenchido pelo leitor de JSON
    [DataContract]
    sealed class PackManifest
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "name")] public string Name;
        [DataMember(Name = "version")] public string Version;
    }
#pragma warning restore CS0649

    /// <summary>
    /// Pacotes gráficos opcionais. O Básico vem dentro do programa; Normal e Ultra
    /// são baixados quando a pessoa escolhe e podem ser apagados para liberar espaço.
    /// </summary>
    static class PackManager
    {
        public const string BaseUrl = "https://github.com/SandiehMoreira/MyAquario/releases/latest/download/";
        const long MaxDownload = 300L * 1024 * 1024;

        public static readonly PackInfo[] Catalog =
        {
            new PackInfo { Id = "normal", Tier = Tier.Normal, Name = "Normal — cartoon 2D", Description = "Visual liso com contorno, plantas balançando e degradês." },
            new PackInfo { Id = "ultra", Tier = Tier.Ultra, Name = "Ultra — realista", Description = "Texturas HD, luz dançando no fundo, raios de sol e sombras." },
        };

        public static PackInfo ForTier(Tier t) => Catalog.FirstOrDefault(p => p.Tier == t);
        public static string Dir(PackInfo p) => Path.Combine(Paths.Packs, p.Id);

        public static bool IsInstalled(Tier t)
        {
            if (t == Tier.Basico) return true;
            var p = ForTier(t);
            return p != null && File.Exists(Path.Combine(Dir(p), "manifest.json"));
        }

        public static long SizeBytes(PackInfo p)
        {
            var d = Dir(p);
            return Directory.Exists(d) ? new DirectoryInfo(d).GetFiles().Sum(f => f.Length) : 0;
        }

        public static string FormatSize(long bytes) =>
            bytes >= 1024 * 1024 ? (bytes / 1048576.0).ToString("0.0") + " MB" : Math.Max(1, bytes / 1024) + " KB";

        public static async Task InstallAsync(PackInfo p, IProgress<int> progress, CancellationToken ct)
        {
            // Um zip ao lado do programa (versão offline / testes) tem preferência sobre o download.
            string local = Path.Combine(Paths.AppDir, "packs", p.FileName);
            if (File.Exists(local))
            {
                progress?.Report(50);
                await Task.Run(() => Extract(local, p), ct);
                progress?.Report(100);
                return;
            }

            string tmp = Path.Combine(Path.GetTempPath(), "MyAquario-" + p.FileName);
            try
            {
                await Download(BaseUrl + p.FileName, tmp, progress, ct);
                await Task.Run(() => Extract(tmp, p), ct);
                progress?.Report(100);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        /// <summary>Instala um zip escolhido pela pessoa. Retorna o pacote instalado.</summary>
        public static PackInfo InstallFromFile(string zipPath)
        {
            string id;
            using (var za = ZipFile.OpenRead(zipPath))
            {
                var entry = za.Entries.FirstOrDefault(e => e.Name == "manifest.json")
                    ?? throw new InvalidDataException("Esse arquivo não é um pacote do MyAquario.");
                using var s = entry.Open();
                id = (new DataContractJsonSerializer(typeof(PackManifest)).ReadObject(s) as PackManifest)?.Id;
            }
            var p = Catalog.FirstOrDefault(c => c.Id == id) ?? throw new InvalidDataException("Pacote desconhecido: " + id);
            Extract(zipPath, p);
            return p;
        }

        static readonly Regex SafeName = new Regex(@"^[A-Za-z0-9_\-]+\.(png|json)$");

        static void Extract(string zip, PackInfo expected)
        {
            string dest = Dir(expected), tmp = dest + ".tmp";
            Directory.CreateDirectory(Paths.Packs);
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            try
            {
                using (var za = ZipFile.OpenRead(zip))
                    foreach (var e in za.Entries)
                    {
                        // Só arquivos simples (png/json), sem pastas: nada sai de dentro da pasta do pacote.
                        if (string.IsNullOrEmpty(e.Name) || !SafeName.IsMatch(e.Name)) continue;
                        if (e.Length > 32L * 1024 * 1024) throw new InvalidDataException("Arquivo grande demais no pacote.");
                        e.ExtractToFile(Path.Combine(tmp, e.Name), true);
                    }

                PackManifest man;
                using (var fs = File.OpenRead(Path.Combine(tmp, "manifest.json")))
                    man = new DataContractJsonSerializer(typeof(PackManifest)).ReadObject(fs) as PackManifest;
                if (man == null || man.Id != expected.Id) throw new InvalidDataException("O pacote baixado não é o esperado.");

                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.Move(tmp, dest);
            }
            catch
            {
                try { Directory.Delete(tmp, true); } catch { }
                throw;
            }
        }

        static async Task Download(string url, string file, IProgress<int> progress, CancellationToken ct)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MyAquario/0.1");
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                throw new Exception("Esse pacote ainda não foi publicado no servidor.");
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? 0;
            using var src = await resp.Content.ReadAsStreamAsync();
            using var dst = File.Create(file);
            var buf = new byte[81920];
            long read = 0;
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                await dst.WriteAsync(buf, 0, n, ct);
                read += n;
                if (read > MaxDownload) throw new InvalidDataException("Download maior que o esperado.");
                if (total > 0) progress?.Report((int)(read * 95 / total));
            }
        }

        public static void Delete(PackInfo p)
        {
            var d = Dir(p);
            if (Directory.Exists(d)) Directory.Delete(d, true);
        }
    }
}
