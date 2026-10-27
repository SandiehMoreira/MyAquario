using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MyAquario
{
    enum PowerLevel { Full, Rest, Paused }

    /// <summary>
    /// Liga tudo: estado do jogo, janela, ícone na bandeja, laço de animação e economia de energia.
    ///  - Full:   gráfico escolhido, FPS do gráfico
    ///  - Rest:   PC parado há alguns minutos → pixel art a 6 FPS, texturas soltas da memória
    ///  - Paused: tela bloqueada, jogo/vídeo em tela cheia ou aquário escondido → animação parada
    /// O tempo do pet continua contando pelo relógio em qualquer caso.
    /// </summary>
    sealed class AquarioApp : ApplicationContext
    {
        public readonly AppSettings Settings;
        public readonly GameState State;
        readonly World world;
        readonly TankWindow tank;
        readonly InfoWindow info = new InfoWindow();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly Timer frameTimer = new Timer();
        readonly Timer powerTimer = new Timer { Interval = 1000 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Dictionary<Tier, IRenderer> renderers = new Dictionary<Tier, IRenderer>();
        readonly Native.WinEventProc foregroundProc;
        readonly IntPtr foregroundHook;

        IRenderer current;
        PowerLevel level = PowerLevel.Full;
        bool locked, exiting;
        double lastFrame, lastSave;
        SettingsForm settingsForm;
        ToolStripMenuItem miUpgrade, miShow, miQuality;

        public Tier CurrentTier => current?.Tier ?? Tier.Basico;
        public PowerLevel Level => level;

        public AquarioApp()
        {
            Settings = Storage.Load("settings.json", () => new AppSettings());
            State = Storage.Load("save.json", () => new GameState());

            double awayHours = (DateTime.UtcNow.Ticks - State.LastUpdateUtc) / (double)TimeSpan.TicksPerHour;
            Care.CatchUp(State, DateTime.UtcNow);
            world = new World(State);
            SaveState(); // garante o save do peixe novo mesmo se o app for fechado à força

            if (!State.HelpShown)
            {
                State.HelpShown = true;
                world.Toast($"Oi! Eu sou o {State.BettaName} e estou com fome! Clique no potinho de ração ao lado do aquário para me dar comida.", 12);
            }
            else if (awayHours > 3)
                world.Toast(AwayMessage(awayHours), 6);

            menu = BuildMenu();
            tray = new NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "MyAquario",
                Visible = true,
                ContextMenuStrip = menu,
            };
            tray.DoubleClick += (s, e) => OpenSettings();

            tank = new TankWindow(world)
            {
                ContextMenuStrip = menu,
                DesktopMode = Settings.DesktopMode,
            };
            info.DesktopMode = Settings.DesktopMode;
            tank.JarClicked += () => world.FeedFromJar();
            tank.TapAt += (x, y) => world.Tap(x, y);
            tank.BadgeClicked += Upgrade;
            tank.Moved += () => { Settings.WindowX = tank.Left; Settings.WindowY = tank.Top; Settings.HasPosition = true; SaveSettings(); };
            tank.CreateControl();
            UpdateScale();
            PlaceWindow();
            if (Settings.ShowTank) { tank.Show(); tank.ApplyZOrder(); }

            // O registro manda (o instalador pode ter ligado a opção "abrir com o Windows").
            Settings.StartWithWindows = Startup.IsOn();

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            foregroundProc = OnForeground;
            foregroundHook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, foregroundProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);

            frameTimer.Tick += (s, e) => Frame();
            powerTimer.Tick += (s, e) => PowerTick();
            powerTimer.Start();
            ApplyPower(true);

            Task.Run(() => HardwareProbe.Recommend()).ContinueWith(_ =>
            {
                if (exiting) return;
                tank.BeginInvoke(new Action(OnProbeDone));
            });
        }

        string AwayMessage(double hours)
        {
            string tempo = hours >= 48 ? $"{Math.Floor(hours / 24)} dias" : $"{Math.Floor(hours)} horas";
            return State.Hunger > 70
                ? $"Você ficou fora {tempo}. {State.BettaName} está com fome!"
                : $"Você ficou fora {tempo}. {State.BettaName} sentiu sua falta!";
        }

        static Icon LoadIcon()
        {
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
            catch { return SystemIcons.Application; }
        }

        void OnProbeDone()
        {
            ApplyPower(true);
            settingsForm?.RefreshAll();
            var rec = HardwareProbe.Recommend();
            if (Settings.Quality == "auto" && rec > Tier.Basico && !PackManager.IsInstalled(rec) && !Settings.PackOfferShown)
            {
                Settings.PackOfferShown = true;
                SaveSettings();
                tray.ShowBalloonTip(8000, "MyAquario",
                    $"Seu PC aguenta o gráfico {TierText.Name(rec)} ({TierText.Style(rec)}). Clique aqui para baixar o pacote.",
                    ToolTipIcon.Info);
                tray.BalloonTipClicked -= OnOfferClicked;
                tray.BalloonTipClicked += OnOfferClicked;
            }
        }

        void OnOfferClicked(object sender, EventArgs e)
        {
            tray.BalloonTipClicked -= OnOfferClicked;
            OpenSettings();
        }

        // ---- Menu ----

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Alimentar", null, (s, e) => world.FeedFromJar());
            m.Items.Add("Limpar o aquário", null, (s, e) => world.Clean());
            miUpgrade = new ToolStripMenuItem("Mudar para um aquário maior...", null, (s, e) => Upgrade());
            m.Items.Add(miUpgrade);
            m.Items.Add(new ToolStripSeparator());

            miQuality = new ToolStripMenuItem("Gráfico");
            foreach (var key in new[] { "auto", "basico", "normal", "ultra" })
            {
                var k = key;
                var item = new ToolStripMenuItem(QualityLabel(k), null, (s, e) => ChooseQualityFromMenu(k)) { Tag = k };
                miQuality.DropDownItems.Add(item);
            }
            m.Items.Add(miQuality);
            m.Items.Add("Configurações...", null, (s, e) => OpenSettings());
            miShow = new ToolStripMenuItem("Esconder o aquário", null, (s, e) => ToggleTank());
            m.Items.Add(miShow);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Sair", null, (s, e) => ExitApp());

            m.Opening += (s, e) =>
            {
                miUpgrade.Enabled = Care.UpgradeReady(State);
                var next = Tanks.Next(State.TankLevel);
                miUpgrade.Text = next == null ? "Aquário máximo alcançado"
                    : miUpgrade.Enabled ? $"Mudar para o {next.Name}..." : $"Próximo aquário: {Math.Floor(State.CarePoints)}/{next.PointsNeeded} pontos";
                miShow.Text = Settings.ShowTank ? "Esconder o aquário" : "Mostrar o aquário";
                foreach (ToolStripMenuItem it in miQuality.DropDownItems)
                {
                    var k = (string)it.Tag;
                    it.Checked = Settings.Quality == k;
                    var t = TierText.FromKey(k);
                    it.Text = QualityLabel(k) + (t.HasValue && !PackManager.IsInstalled(t.Value) ? "  (baixar)" : "");
                }
            };
            return m;
        }

        static string QualityLabel(string key) => key switch
        {
            "basico" => "Básico — pixel art",
            "normal" => "Normal — cartoon 2D",
            "ultra" => "Ultra — realista",
            _ => "Automático",
        };

        void ChooseQualityFromMenu(string key)
        {
            var t = TierText.FromKey(key);
            if (t.HasValue && !PackManager.IsInstalled(t.Value))
            {
                // Precisa baixar: a tela de configurações cuida do download e do progresso.
                OpenSettings();
                settingsForm?.SelectQuality(key);
                return;
            }
            Settings.Quality = key;
            ApplySettings();
            settingsForm?.RefreshAll();
            if (t.HasValue) SettingsForm.OfferCleanup(this, t.Value, null);
        }

        public void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(this);
                settingsForm.FormClosed += (s, e) => settingsForm = null;
            }
            settingsForm.Show();
            settingsForm.Activate();
        }

        void ToggleTank()
        {
            Settings.ShowTank = !Settings.ShowTank;
            if (Settings.ShowTank) { tank.Show(); tank.ApplyZOrder(); }
            else { tank.Hide(); info.HideInfo(); }
            SaveSettings();
            ApplyPower(true);
        }

        // ---- Aquário maior ----

        void Upgrade()
        {
            var next = Tanks.Next(State.TankLevel);
            if (next == null || !Care.UpgradeReady(State)) return;
            var info = SpeciesData.Get(next.Adds);
            var answer = MessageBox.Show(
                $"{State.BettaName} juntou {Math.Floor(State.CarePoints)} pontos de cuidado!\n\n" +
                $"Mudar para o {next.Name} ({next.Liters} L)?\n" +
                $"Chegam {next.AddCount} {info.Plural} para fazer companhia.",
                "MyAquario — aquário maior", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;

            int bottom = tank.Bottom, left = tank.Left;
            State.TankLevel = next.Level;
            State.Cleanliness = 100;
            State.UpgradeNoticeShown = false;
            world.Rebuild();
            var size = tank.PixelSize;
            tank.Location = new Point(left, bottom - size.Height);
            KeepOnScreen();
            world.Toast($"Bem-vindos ao {next.Name}!", 5);
            SaveState();
            if (level == PowerLevel.Paused) tank.RenderFrame(current);
        }

        // Área só do aquário na tela (sem o potinho de ração), para centralizar o painel sobre o vidro.
        Rectangle TankArea() =>
            new Rectangle(tank.Left, tank.Top, (int)Math.Round(world.TankW * tank.RenderScale), tank.Height);

        public void RecolorBetta()
        {
            foreach (var f in world.Fish)
                if (f.Sp == Species.Betta) f.Variant = State.BettaColor;
        }

        // ---- Janela, tamanho, posição ----

        void UpdateScale()
        {
            float dpi;
            using (var g = tank.CreateGraphics()) dpi = g.DpiX / 96f;
            tank.RenderScale = (float)Settings.SizeScale * dpi;
        }

        void PlaceWindow()
        {
            var size = tank.PixelSize;
            tank.Size = size;
            if (Settings.HasPosition) tank.Location = new Point(Settings.WindowX, Settings.WindowY);
            else
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                tank.Location = new Point(wa.Right - size.Width - 40, wa.Bottom - size.Height - 30);
            }
            KeepOnScreen();
        }

        void KeepOnScreen()
        {
            var size = tank.PixelSize;
            var rect = new Rectangle(tank.Location, size);
            var wa = Screen.FromRectangle(rect).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(rect.X, wa.Right - size.Width));
            int y = Math.Max(wa.Top, Math.Min(rect.Y, wa.Bottom - size.Height));
            tank.Location = new Point(x, y);
            Settings.WindowX = x;
            Settings.WindowY = y;
        }

        void OnDisplayChanged(object sender, EventArgs e) => tank.BeginInvoke(new Action(KeepOnScreen));

        /// <summary>Chamado pela tela de configurações depois de qualquer mudança.</summary>
        public void ApplySettings()
        {
            int bottom = tank.Bottom, left = tank.Left;
            tank.DesktopMode = Settings.DesktopMode;
            info.DesktopMode = Settings.DesktopMode;
            info.ApplyZOrder();
            UpdateScale();
            var size = tank.PixelSize;
            if (size != tank.Size) tank.Location = new Point(left, bottom - size.Height);
            KeepOnScreen();
            tank.ApplyZOrder();
            if (Settings.StartWithWindows != Startup.IsOn()) Startup.Set(Settings.StartWithWindows);
            SaveSettings();
            ApplyPower(true);
            if (level != PowerLevel.Paused) Frame();
        }

        // ---- Gráfico e energia ----

        public Tier DesiredTier()
        {
            Tier t = TierText.FromKey(Settings.Quality) ?? (HardwareProbe.Done ? HardwareProbe.Recommend() : Tier.Basico);
            if (Settings.SaveOnBattery && HardwareProbe.OnBattery && t == Tier.Ultra) t = Tier.Normal;
            while (t > Tier.Basico && !PackManager.IsInstalled(t)) t--;
            return t;
        }

        IRenderer GetRenderer(Tier t)
        {
            if (renderers.TryGetValue(t, out var r)) return r;
            r = t switch
            {
                Tier.Normal => new CartoonRenderer(PackManager.Dir(PackManager.ForTier(Tier.Normal))),
                Tier.Ultra => new UltraRenderer(PackManager.Dir(PackManager.ForTier(Tier.Ultra))),
                _ => (IRenderer)new PixelRenderer(),
            };
            renderers[t] = r;
            return r;
        }

        /// <summary>Solta um gráfico da memória (antes de apagar o pacote dele).</summary>
        public void ForgetRenderer(Tier t)
        {
            if (!renderers.TryGetValue(t, out var r)) return;
            if (current == r) current = GetRenderer(Tier.Basico);
            r.Dispose();
            renderers.Remove(t);
        }

        PowerLevel ComputeLevel()
        {
            if (!Settings.ShowTank || locked || Native.UserAway() || Native.SomethingFullscreen()) return PowerLevel.Paused;
            if (Settings.AutoRest && Native.IdleSeconds() > Settings.IdleMinutes * 60) return PowerLevel.Rest;
            return PowerLevel.Full;
        }

        void ApplyPower(bool force = false)
        {
            var lvl = ComputeLevel();
            var tier = lvl == PowerLevel.Rest ? Tier.Basico : DesiredTier();
            if (!force && lvl == level && current != null && current.Tier == tier) return;

            var previous = level;
            if (lvl != previous || current == null || current.Tier != tier)
                Log.Write($"energia: {previous} -> {lvl}, gráfico {TierText.Name(tier)}");
            level = lvl;
            current = GetRenderer(tier);

            if (lvl != PowerLevel.Full)
            {
                foreach (var r in renderers.Values)
                    if (r != current) r.Release();
                if (previous == PowerLevel.Full) TrimMemory();
            }

            if (lvl == PowerLevel.Paused)
            {
                frameTimer.Stop();
                // Deixa um quadro desenhado: a janela "layered" fica invisível até receber o primeiro.
                if (tank.Visible) Frame();
                if (!Settings.AlwaysShowInfo) info.HideInfo();
                current.Release();
                SaveState();
            }
            else
            {
                // 12 quadros/s no repouso: leve, mas ainda parece vivo (6 parecia travado).
                int fps = lvl == PowerLevel.Rest ? 12 : current.Fps;
                if (Settings.SaveOnBattery && HardwareProbe.OnBattery) fps = Math.Min(fps, 20);
                frameTimer.Interval = Math.Max(10, 1000 / fps);
                if (!frameTimer.Enabled)
                {
                    lastFrame = clock.Elapsed.TotalSeconds;
                    frameTimer.Start();
                }
            }
            settingsForm?.RefreshStatus();
        }

        static void TrimMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            try { Native.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1)); } catch { }
        }

        void Frame()
        {
            if (exiting || !tank.Visible) return;
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - lastFrame);
            lastFrame = now;
            Care.CatchUp(State, DateTime.UtcNow);
            world.Update(dt);
            tank.UpgradeVisible = Care.UpgradeReady(State);
            try
            {
                tank.RenderFrame(current);
                info.ShowInfo(world, TankArea(), tank.RenderScale, Settings.AlwaysShowInfo || tank.Hover);
            }
            catch (Exception ex)
            {
                // Se um gráfico falhar (pacote corrompido etc.), volta para o Básico em vez de travar.
                Log.Error(ex);
                if (current.Tier != Tier.Basico) { ForgetRenderer(current.Tier); current = GetRenderer(Tier.Basico); }
            }
        }

        void PowerTick()
        {
            Care.CatchUp(State, DateTime.UtcNow);
            ApplyPower();
            if (level == PowerLevel.Paused && Settings.AlwaysShowInfo && tank.Visible)
                info.ShowInfo(world, TankArea(), tank.RenderScale, true);
            Notify();
            tray.Text = Truncate($"MyAquario · {State.BettaName} está {Care.Mood(State)}", 63);
            double now = clock.Elapsed.TotalSeconds;
            if (now - lastSave > 60) SaveState();
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max);

        void Notify()
        {
            if (!Settings.Notifications) return;
            long now = DateTime.UtcNow.Ticks;
            if (State.Hunger > 75 && now - State.LastHungerNotice > TimeSpan.FromHours(4).Ticks)
            {
                State.LastHungerNotice = now;
                tray.ShowBalloonTip(5000, "MyAquario", $"{State.BettaName} está com fome! Clique no potinho de ração ao lado do aquário.", ToolTipIcon.Info);
            }
            else if (State.Cleanliness < 30 && now - State.LastDirtNotice > TimeSpan.FromHours(8).Ticks)
            {
                State.LastDirtNotice = now;
                tray.ShowBalloonTip(5000, "MyAquario", "A água está suja. Clique com o botão direito no aquário → Limpar.", ToolTipIcon.Info);
            }
            else if (Care.UpgradeReady(State) && !State.UpgradeNoticeShown)
            {
                State.UpgradeNoticeShown = true;
                tray.ShowBalloonTip(6000, "MyAquario", $"{State.BettaName} pode ganhar um aquário maior! Clique no selo dourado em cima do aquário.", ToolTipIcon.Info);
            }
        }

        // ---- Eventos do Windows ----

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            locked = e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.ConsoleDisconnect
                || e.Reason == SessionSwitchReason.RemoteDisconnect;
            tank.BeginInvoke(new Action(() => ApplyPower(true)));
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) tank.BeginInvoke(new Action(SaveState));
            if (e.Mode == PowerModes.Resume || e.Mode == PowerModes.StatusChange)
                tank.BeginInvoke(new Action(() => ApplyPower(true)));
        }

        void OnForeground(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            var cls = Native.ClassName(hwnd);
            if (cls == "WorkerW" || cls == "Progman")
            {
                tank.PeekAboveDesktop();
                info.PeekAboveDesktop();
            }
        }

        // ---- Salvar e sair ----

        public void SaveState()
        {
            lastSave = clock.Elapsed.TotalSeconds;
            Storage.Save("save.json", State);
        }

        public void SaveSettings() => Storage.Save("settings.json", Settings);

        public void ExitApp()
        {
            if (exiting) return;
            exiting = true;
            frameTimer.Stop();
            powerTimer.Stop();
            Care.CatchUp(State, DateTime.UtcNow);
            SaveState();
            SaveSettings();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
            settingsForm?.Close();
            tray.Visible = false;
            tray.Dispose();
            foreach (var r in renderers.Values) r.Dispose();
            info.Close();
            info.Dispose();
            tank.Close();
            tank.Dispose();
            ExitThread();
        }
    }
}
