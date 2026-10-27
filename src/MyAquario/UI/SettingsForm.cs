using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MyAquario
{
    sealed class SettingsForm : Form
    {
        readonly AquarioApp app;
        readonly ComboBox cbQuality = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly Label lblHardware = new Label { AutoSize = false };
        readonly Label lblNow = new Label { AutoSize = false, ForeColor = Color.DimGray };
        readonly PackRow[] rows;
        readonly CheckBox chkRest = new CheckBox { Text = "Modo repouso: com o PC parado, usar o gráfico mais leve", AutoSize = true };
        readonly NumericUpDown nudIdle = new NumericUpDown { Minimum = 1, Maximum = 60, Width = 50 };
        readonly CheckBox chkBattery = new CheckBox { Text = "No notebook fora da tomada: economizar bateria", AutoSize = true };
        readonly TextBox txtName = new TextBox { MaxLength = 16, Width = 140 };
        readonly ComboBox cbColor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
        readonly ComboBox cbSize = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
        readonly CheckBox chkDesktop = new CheckBox { Text = "Fixar na área de trabalho (fica atrás das janelas)", AutoSize = true };
        readonly CheckBox chkStartup = new CheckBox { Text = "Abrir junto com o Windows", AutoSize = true };
        readonly CheckBox chkNotify = new CheckBox { Text = "Avisar quando estiver com fome ou com a água suja", AutoSize = true };

        static readonly string[] QualityKeys = { "auto", "basico", "normal", "ultra" };
        static readonly double[] Sizes = { 1.0, 1.5, 2.0, 2.5 };
        bool loading, busy;
        CancellationTokenSource cts;

        sealed class PackRow
        {
            public PackInfo Pack;
            public Label Name, Status;
            public Button Action;
            public ProgressBar Progress;
        }

        public SettingsForm(AquarioApp app)
        {
            this.app = app;
            Text = "MyAquario — Configurações";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(480, 618);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ---- Gráficos ----
            var gGfx = new GroupBox { Text = "Gráficos", Location = new Point(10, 8), Size = new Size(460, 268) };
            gGfx.Controls.Add(new Label { Text = "Qualidade:", Location = new Point(12, 26), AutoSize = true });
            cbQuality.Location = new Point(90, 22);
            cbQuality.Width = 355;
            gGfx.Controls.Add(cbQuality);
            lblHardware.Location = new Point(12, 54);
            lblHardware.Size = new Size(436, 34);
            gGfx.Controls.Add(lblHardware);

            var basic = new Label { Text = "Básico — pixel art", Font = new Font(Font, FontStyle.Bold), Location = new Point(12, 92), AutoSize = true };
            var basicStatus = new Label { Text = "Já vem com o programa · bem leve", Location = new Point(12, 110), AutoSize = true, ForeColor = Color.DimGray };
            gGfx.Controls.Add(basic);
            gGfx.Controls.Add(basicStatus);

            rows = PackManager.Catalog.Select((p, i) =>
            {
                int y = 138 + i * 52;
                var row = new PackRow
                {
                    Pack = p,
                    Name = new Label { Text = p.Name, Font = new Font(Font, FontStyle.Bold), Location = new Point(12, y), AutoSize = true },
                    Status = new Label { Location = new Point(12, y + 18), Size = new Size(330, 18), ForeColor = Color.DimGray },
                    Action = new Button { Location = new Point(352, y + 2), Size = new Size(96, 28) },
                    Progress = new ProgressBar { Location = new Point(12, y + 38), Size = new Size(436, 8), Visible = false },
                };
                row.Action.Click += async (s, e) => await PackAction(row);
                gGfx.Controls.AddRange(new Control[] { row.Name, row.Status, row.Action, row.Progress });
                return row;
            }).ToArray();

            var lnkFile = new LinkLabel { Text = "Instalar pacote de um arquivo...", Location = new Point(12, 244), AutoSize = true };
            lnkFile.LinkClicked += (s, e) => InstallFromFile();
            gGfx.Controls.Add(lnkFile);
            Controls.Add(gGfx);

            // ---- Economia ----
            var gPower = new GroupBox { Text = "Economia de energia", Location = new Point(10, 282), Size = new Size(460, 110) };
            chkRest.Location = new Point(12, 24);
            gPower.Controls.Add(chkRest);
            gPower.Controls.Add(new Label { Text = "Entrar em repouso depois de", Location = new Point(30, 52), AutoSize = true });
            nudIdle.Location = new Point(200, 49);
            gPower.Controls.Add(nudIdle);
            gPower.Controls.Add(new Label { Text = "minutos sem mexer no PC", Location = new Point(256, 52), AutoSize = true });
            chkBattery.Location = new Point(12, 78);
            gPower.Controls.Add(chkBattery);
            Controls.Add(gPower);

            lblNow.Location = new Point(14, 396);
            lblNow.Size = new Size(456, 18);
            Controls.Add(lblNow);

            // ---- Aquário ----
            var gTank = new GroupBox { Text = "Aquário", Location = new Point(10, 416), Size = new Size(460, 140) };
            gTank.Controls.Add(new Label { Text = "Nome do betta:", Location = new Point(12, 26), AutoSize = true });
            txtName.Location = new Point(110, 22);
            gTank.Controls.Add(txtName);
            cbColor.Location = new Point(262, 22);
            cbColor.Items.AddRange(WorldLook.BettaColorNames);
            gTank.Controls.Add(cbColor);
            gTank.Controls.Add(new Label { Text = "Tamanho:", Location = new Point(12, 56), AutoSize = true });
            cbSize.Location = new Point(110, 52);
            cbSize.Items.AddRange(new object[] { "Pequeno", "Médio", "Grande", "Enorme" });
            gTank.Controls.Add(cbSize);
            chkDesktop.Location = new Point(12, 82);
            chkStartup.Location = new Point(12, 104);
            chkNotify.Location = new Point(12, 126);
            gTank.Controls.AddRange(new Control[] { chkDesktop, chkStartup, chkNotify });
            gTank.Height = 156;
            Controls.Add(gTank);

            var btnClose = new Button { Text = "Fechar", Location = new Point(384, 580), Size = new Size(86, 28) };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
            CancelButton = btnClose;

            RefreshAll();

            cbQuality.SelectedIndexChanged += async (s, e) => { if (!loading) await OnQualityChanged(); };
            chkRest.CheckedChanged += (s, e) => { if (loading) return; app.Settings.AutoRest = chkRest.Checked; nudIdle.Enabled = chkRest.Checked; app.ApplySettings(); };
            nudIdle.ValueChanged += (s, e) => { if (loading) return; app.Settings.IdleMinutes = (int)nudIdle.Value; app.ApplySettings(); };
            chkBattery.CheckedChanged += (s, e) => { if (loading) return; app.Settings.SaveOnBattery = chkBattery.Checked; app.ApplySettings(); };
            txtName.TextChanged += (s, e) =>
            {
                if (loading) return;
                var name = txtName.Text.Trim();
                if (name.Length == 0) return;
                app.State.BettaName = name;
                app.SaveState();
            };
            cbColor.SelectedIndexChanged += (s, e) => { if (loading || cbColor.SelectedIndex < 0) return; app.State.BettaColor = cbColor.SelectedIndex; app.SaveState(); RebuildFishColors(); };
            cbSize.SelectedIndexChanged += (s, e) => { if (loading || cbSize.SelectedIndex < 0) return; app.Settings.SizeScale = Sizes[cbSize.SelectedIndex]; app.ApplySettings(); };
            chkDesktop.CheckedChanged += (s, e) => { if (loading) return; app.Settings.DesktopMode = chkDesktop.Checked; app.ApplySettings(); };
            chkStartup.CheckedChanged += (s, e) => { if (loading) return; app.Settings.StartWithWindows = chkStartup.Checked; app.ApplySettings(); };
            chkNotify.CheckedChanged += (s, e) => { if (loading) return; app.Settings.Notifications = chkNotify.Checked; app.SaveSettings(); };
        }

        void RebuildFishColors() => app.RecolorBetta();

        public void RefreshAll()
        {
            loading = true;
            var rec = HardwareProbe.Done ? TierText.Name(HardwareProbe.Recommend()) : "...";
            cbQuality.Items.Clear();
            cbQuality.Items.AddRange(new object[]
            {
                $"Automático (recomendado para este PC: {rec})",
                "Básico — pixel art (leve, já instalado)",
                "Normal — cartoon 2D",
                "Ultra — realista",
            });
            cbQuality.SelectedIndex = Math.Max(0, Array.IndexOf(QualityKeys, app.Settings.Quality));
            lblHardware.Text = "Seu PC: " + HardwareProbe.Summary;

            chkRest.Checked = app.Settings.AutoRest;
            nudIdle.Value = Math.Max(1, Math.Min(60, app.Settings.IdleMinutes));
            nudIdle.Enabled = app.Settings.AutoRest;
            chkBattery.Checked = app.Settings.SaveOnBattery;
            txtName.Text = app.State.BettaName;
            cbColor.SelectedIndex = app.State.BettaColor % WorldLook.BettaColorNames.Length;
            int si = Array.FindIndex(Sizes, v => Math.Abs(v - app.Settings.SizeScale) < 0.01);
            cbSize.SelectedIndex = si < 0 ? 1 : si;
            chkDesktop.Checked = app.Settings.DesktopMode;
            chkStartup.Checked = app.Settings.StartWithWindows;
            chkNotify.Checked = app.Settings.Notifications;
            loading = false;

            RefreshPacks();
            RefreshStatus();
        }

        void RefreshPacks()
        {
            foreach (var r in rows)
            {
                bool installed = PackManager.IsInstalled(r.Pack.Tier);
                if (busy) continue;
                r.Status.Text = installed
                    ? $"Instalado · {PackManager.FormatSize(PackManager.SizeBytes(r.Pack))}"
                    : r.Pack.Description;
                r.Action.Text = installed ? "Apagar" : "Baixar";
                r.Action.Enabled = true;
            }
        }

        public void RefreshStatus()
        {
            if (IsDisposed) return;
            string mode = app.Level switch
            {
                PowerLevel.Rest => "em repouso (pixel art, 6 quadros/s)",
                PowerLevel.Paused => "pausado (tela bloqueada ou algo em tela cheia)",
                _ => "normal",
            };
            lblNow.Text = $"Agora: gráfico {TierText.Name(app.CurrentTier)}, {mode}";
        }

        public void SelectQuality(string key)
        {
            int i = Array.IndexOf(QualityKeys, key);
            if (i >= 0) cbQuality.SelectedIndex = i; // dispara OnQualityChanged, que oferece o download
        }

        async System.Threading.Tasks.Task OnQualityChanged()
        {
            string key = QualityKeys[Math.Max(0, cbQuality.SelectedIndex)];
            Tier target = TierText.FromKey(key) ?? HardwareProbe.Recommend();

            if (!PackManager.IsInstalled(target))
            {
                var pack = PackManager.ForTier(target);
                var answer = MessageBox.Show(this,
                    $"O gráfico {TierText.Name(target)} ({TierText.Style(target)}) precisa do pacote {TierText.Name(target)}.\n\nBaixar agora?",
                    "MyAquario", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                bool ok = answer == DialogResult.Yes && await Install(rows.First(r => r.Pack == pack));
                if (!ok && key != "auto")
                {
                    loading = true;
                    cbQuality.SelectedIndex = Math.Max(0, Array.IndexOf(QualityKeys, app.Settings.Quality));
                    loading = false;
                    return;
                }
            }

            app.Settings.Quality = key;
            app.ApplySettings();
            if (key != "auto") OfferCleanup(app, target, this);
            RefreshPacks();
            RefreshStatus();
        }

        /// <summary>Ao descer de gráfico, pergunta se quer apagar os pacotes que ficaram sem uso.</summary>
        public static void OfferCleanup(AquarioApp app, Tier chosen, IWin32Window owner)
        {
            var unused = PackManager.Catalog.Where(p => p.Tier > chosen && PackManager.IsInstalled(p.Tier)).ToList();
            if (unused.Count == 0) return;
            long bytes = unused.Sum(PackManager.SizeBytes);
            string names = string.Join(" e ", unused.Select(p => TierText.Name(p.Tier)));
            var answer = MessageBox.Show(owner,
                $"Você escolheu o gráfico {TierText.Name(chosen)}.\n\n" +
                $"Quer apagar o pacote {names} para liberar {PackManager.FormatSize(bytes)}?\n" +
                "Se preferir, deixe instalado para trocar quando quiser. Dá para baixar de novo depois.",
                "MyAquario", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            foreach (var p in unused) DeletePack(app, p, owner);
        }

        static void DeletePack(AquarioApp app, PackInfo p, IWin32Window owner)
        {
            try
            {
                app.ForgetRenderer(p.Tier);
                PackManager.Delete(p);
                if (app.Settings.Quality == TierText.Key(p.Tier)) app.Settings.Quality = "basico";
                app.ApplySettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(owner, "Não consegui apagar o pacote: " + ex.Message, "MyAquario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        async System.Threading.Tasks.Task PackAction(PackRow row)
        {
            if (busy) { cts?.Cancel(); return; }
            if (PackManager.IsInstalled(row.Pack.Tier))
            {
                var answer = MessageBox.Show(this, $"Apagar o pacote {TierText.Name(row.Pack.Tier)}? Dá para baixar de novo quando quiser.",
                    "MyAquario", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
                DeletePack(app, row.Pack, this);
                RefreshAll();
                return;
            }
            if (await Install(row))
            {
                var answer = MessageBox.Show(this, $"Pacote {TierText.Name(row.Pack.Tier)} instalado! Usar agora?",
                    "MyAquario", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    app.Settings.Quality = TierText.Key(row.Pack.Tier);
                    app.ApplySettings();
                    OfferCleanup(app, row.Pack.Tier, this);
                }
                RefreshAll();
            }
        }

        async System.Threading.Tasks.Task<bool> Install(PackRow row)
        {
            busy = true;
            cts = new CancellationTokenSource();
            foreach (var r in rows) r.Action.Enabled = r == row;
            row.Action.Text = "Cancelar";
            row.Status.Text = "Baixando...";
            row.Progress.Value = 0;
            row.Progress.Visible = true;
            try
            {
                var progress = new Progress<int>(v => { if (!row.Progress.IsDisposed) row.Progress.Value = Math.Max(0, Math.Min(100, v)); });
                await PackManager.InstallAsync(row.Pack, progress, cts.Token);
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                Log.Error(ex);
                if (!IsDisposed)
                    MessageBox.Show(this, "Não deu para baixar o pacote.\n\n" + ex.Message + "\n\nVerifique a internet e tente de novo.",
                        "MyAquario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally
            {
                busy = false;
                cts.Dispose();
                cts = null;
                if (!IsDisposed)
                {
                    row.Progress.Visible = false;
                    RefreshPacks();
                }
            }
        }

        void InstallFromFile()
        {
            using var dlg = new OpenFileDialog { Filter = "Pacote do MyAquario (*.zip)|*.zip", Title = "Escolha o pacote" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var p = PackManager.InstallFromFile(dlg.FileName);
                app.ForgetRenderer(p.Tier);
                app.ApplySettings();
                MessageBox.Show(this, $"Pacote {TierText.Name(p.Tier)} instalado.", "MyAquario");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                MessageBox.Show(this, "Não consegui instalar: " + ex.Message, "MyAquario", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshAll();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cts?.Cancel();
            base.OnFormClosing(e);
        }
    }
}
