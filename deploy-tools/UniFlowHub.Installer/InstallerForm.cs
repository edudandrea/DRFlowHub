using System.Diagnostics;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UniFlowHub.Installer;

public sealed class InstallerForm : Form
{
    private readonly TextBox _targetPath = new();
    private readonly Button _browseButton = new();
    private readonly Button _installButton = new();
    private readonly CheckBox _preserveAppSettings = new();
    private readonly CheckBox _useAppOffline = new();
    private readonly CheckBox _restartIis = new();
    private readonly TextBox _siteName = new();
    private readonly TextBox _appPoolName = new();
    private readonly ComboBox _oracleEnvironment = new();
    private readonly TextBox _oracleDveUser = new();
    private readonly TextBox _oracleDvePassword = new();
    private readonly TextBox _oracleDveHost = new();
    private readonly TextBox _oracleProductionUser = new();
    private readonly TextBox _oracleProductionPassword = new();
    private readonly TextBox _oracleProductionHost = new();
    private readonly TextBox _log = new();
    private readonly ProgressBar _progress = new();
    private readonly Label _statusLabel = new();

    private static readonly Color PageBackColor = Color.FromArgb(244, 247, 251);
    private static readonly Color HeaderStartColor = Color.FromArgb(11, 31, 58);
    private static readonly Color HeaderEndColor = Color.FromArgb(22, 91, 170);
    private static readonly Color CardBackColor = Color.White;
    private static readonly Color BorderColor = Color.FromArgb(218, 226, 238);
    private static readonly Color PrimaryColor = Color.FromArgb(20, 101, 192);
    private static readonly Color PrimaryHoverColor = Color.FromArgb(15, 82, 159);
    private static readonly Color TextColor = Color.FromArgb(27, 39, 54);
    private static readonly Color MutedTextColor = Color.FromArgb(91, 108, 130);

    public InstallerForm()
    {
        Text = "Instalador UniFlowHub";
        Width = 920;
        Height = 900;
        MinimumSize = new Size(880, 840);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5F);
        BackColor = PageBackColor;
        DoubleBuffered = true;

        var header = new HeaderPanel
        {
            Location = new Point(0, 0),
            Size = new Size(ClientSize.Width, 136),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var brandMark = new Label
        {
            Text = "DR",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(55, 128, 222),
            Location = new Point(28, 28),
            Size = new Size(54, 54)
        };

        var title = new Label
        {
            Text = "Instalação do UniFlowHub",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(100, 25)
        };

        var description = new Label
        {
            Text = "Publique a aplicação no IIS com preservação das configurações locais e controle seguro do site.",
            AutoSize = false,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(221, 232, 247),
            BackColor = Color.Transparent,
            Location = new Point(102, 70),
            Width = 720,
            Height = 34
        };

        var versionPill = new Label
        {
            Text = "Deploy assistido",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(72, 145, 232),
            Location = new Point(748, 32),
            Size = new Size(126, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        header.Controls.AddRange([brandMark, title, description, versionPill]);

        var targetCard = CreateCard(new Point(28, 160), new Size(836, 132));
        targetCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var targetTitle = CreateSectionTitle("Destino da instalação", new Point(22, 18));
        var targetHint = CreateMutedLabel("Escolha a pasta onde os arquivos publicados do UniFlowHub serão copiados.", new Point(22, 46), new Size(650, 24));

        _targetPath.Text = @"C:\inetpub\uniflowhub";
        _targetPath.Location = new Point(22, 78);
        _targetPath.Width = 650;
        _targetPath.Height = 32;
        _targetPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        StyleTextBox(_targetPath);

        _browseButton.Text = "Procurar";
        _browseButton.Location = new Point(690, 76);
        _browseButton.Width = 118;
        _browseButton.Height = 36;
        _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        StyleSecondaryButton(_browseButton);
        _browseButton.Click += BrowseButton_Click;

        targetCard.Controls.AddRange([targetTitle, targetHint, _targetPath, _browseButton]);

        var optionsCard = CreateCard(new Point(28, 312), new Size(404, 186));
        var optionsTitle = CreateSectionTitle("Opções de instalação", new Point(22, 18));

        _preserveAppSettings.Text = "Preservar appsettings.json existente";
        _preserveAppSettings.Checked = true;
        _preserveAppSettings.Location = new Point(22, 58);
        StyleCheckBox(_preserveAppSettings);

        _useAppOffline.Text = "Colocar aplicação offline temporariamente";
        _useAppOffline.Checked = true;
        _useAppOffline.Location = new Point(22, 94);
        StyleCheckBox(_useAppOffline);

        _restartIis.Text = "Parar IIS antes da cópia e reiniciar ao final";
        _restartIis.Checked = true;
        _restartIis.Location = new Point(22, 130);
        StyleCheckBox(_restartIis);

        optionsCard.Controls.AddRange([optionsTitle, _preserveAppSettings, _useAppOffline, _restartIis]);

        var iisCard = CreateCard(new Point(460, 312), new Size(404, 186));
        iisCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var iisTitle = CreateSectionTitle("Configuração IIS", new Point(22, 18));
        var siteLabel = CreateFieldLabel("Site IIS", new Point(22, 58));
        var appPoolLabel = CreateFieldLabel("Application Pool", new Point(210, 58));

        _siteName.Text = "DrFlowHub";
        _siteName.Location = new Point(22, 84);
        _siteName.Width = 160;
        _siteName.Height = 32;
        StyleTextBox(_siteName);

        _appPoolName.Text = "DrFlowHub";
        _appPoolName.Location = new Point(210, 84);
        _appPoolName.Width = 160;
        _appPoolName.Height = 32;
        StyleTextBox(_appPoolName);

        _installButton.Text = "Instalar agora";
        _installButton.Location = new Point(210, 130);
        _installButton.Width = 160;
        _installButton.Height = 40;
        StylePrimaryButton(_installButton);
        _installButton.Click += InstallButton_Click;

        iisCard.Controls.AddRange([iisTitle, siteLabel, _siteName, appPoolLabel, _appPoolName, _installButton]);

        var oracleCard = CreateCard(new Point(28, 518), new Size(836, 176));
        oracleCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var oracleTitle = CreateSectionTitle("Banco Oracle", new Point(22, 18));
        var oracleHint = CreateMutedLabel("Defina apenas as conexões Oracle usadas pelo UniFlowHub. As demais configurações internas serão preservadas sem aparecer nesta tela.", new Point(22, 46), new Size(760, 24));
        var environmentLabel = CreateFieldLabel("Ambiente ativo", new Point(22, 78));
        var dveUserLabel = CreateFieldLabel("Usuário testes", new Point(176, 78));
        var dvePasswordLabel = CreateFieldLabel("Senha testes", new Point(326, 78));
        var dveHostLabel = CreateFieldLabel("Host testes", new Point(476, 78));
        var prodUserLabel = CreateFieldLabel("Usuário produção", new Point(22, 124));
        var prodPasswordLabel = CreateFieldLabel("Senha produção", new Point(176, 124));
        var prodHostLabel = CreateFieldLabel("Host produção", new Point(326, 124));

        _oracleEnvironment.DropDownStyle = ComboBoxStyle.DropDownList;
        _oracleEnvironment.Items.AddRange(["Testes", "Produção"]);
        _oracleEnvironment.SelectedIndex = 0;
        _oracleEnvironment.Location = new Point(22, 100);
        _oracleEnvironment.Width = 126;
        _oracleEnvironment.Height = 32;
        StyleComboBox(_oracleEnvironment);

        _oracleDveUser.Location = new Point(176, 100);
        _oracleDveUser.Width = 124;
        _oracleDveUser.Height = 32;
        StyleTextBox(_oracleDveUser);

        _oracleDvePassword.Location = new Point(326, 100);
        _oracleDvePassword.Width = 124;
        _oracleDvePassword.Height = 32;
        _oracleDvePassword.UseSystemPasswordChar = true;
        StyleTextBox(_oracleDvePassword);

        _oracleDveHost.Location = new Point(476, 100);
        _oracleDveHost.Width = 160;
        _oracleDveHost.Height = 32;
        StyleTextBox(_oracleDveHost);

        _oracleProductionUser.Location = new Point(22, 146);
        _oracleProductionUser.Width = 124;
        _oracleProductionUser.Height = 32;
        StyleTextBox(_oracleProductionUser);

        _oracleProductionPassword.Location = new Point(176, 146);
        _oracleProductionPassword.Width = 124;
        _oracleProductionPassword.Height = 32;
        _oracleProductionPassword.UseSystemPasswordChar = true;
        StyleTextBox(_oracleProductionPassword);

        _oracleProductionHost.Location = new Point(326, 146);
        _oracleProductionHost.Width = 160;
        _oracleProductionHost.Height = 32;
        StyleTextBox(_oracleProductionHost);

        oracleCard.Controls.AddRange([
            oracleTitle,
            oracleHint,
            environmentLabel,
            dveUserLabel,
            dvePasswordLabel,
            dveHostLabel,
            prodUserLabel,
            prodPasswordLabel,
            prodHostLabel,
            _oracleEnvironment,
            _oracleDveUser,
            _oracleDvePassword,
            _oracleDveHost,
            _oracleProductionUser,
            _oracleProductionPassword,
            _oracleProductionHost
        ]);

        var progressCard = CreateCard(new Point(28, 714), new Size(836, 72));
        progressCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _statusLabel.Text = "Pronto para instalar";
        _statusLabel.AutoSize = true;
        _statusLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        _statusLabel.ForeColor = TextColor;
        _statusLabel.Location = new Point(22, 15);

        _progress.Location = new Point(22, 42);
        _progress.Width = 792;
        _progress.Height = 12;
        _progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _progress.Style = ProgressBarStyle.Continuous;

        progressCard.Controls.AddRange([_statusLabel, _progress]);

        var logCard = CreateCard(new Point(28, 806), new Size(836, 52));
        logCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        var logTitle = CreateSectionTitle("Atividade da instalação", new Point(22, 14));

        _log.Location = new Point(22, 42);
        _log.Width = 792;
        _log.Height = 34;
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.ReadOnly = true;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = Color.FromArgb(248, 250, 253);
        _log.ForeColor = Color.FromArgb(44, 56, 70);
        _log.Font = new Font("Consolas", 9F);

        logCard.Controls.AddRange([logTitle, _log]);

        Controls.AddRange([
            header,
            targetCard,
            optionsCard,
            iisCard,
            oracleCard,
            progressCard,
            logCard
        ]);

        LoadOracleDefaultsIntoForm();
    }

    private static RoundedPanel CreateCard(Point location, Size size)
    {
        return new RoundedPanel
        {
            Location = location,
            Size = size,
            BackColor = CardBackColor,
            BorderLineColor = BorderColor,
            Radius = 18,
            Padding = new Padding(18)
        };
    }

    private static Label CreateSectionTitle(string text, Point location)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = TextColor,
            Location = location
        };
    }

    private static Label CreateMutedLabel(string text, Point location, Size size)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = MutedTextColor,
            Location = location,
            Size = size
        };
    }

    private static Label CreateFieldLabel(string text, Point location)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = MutedTextColor,
            Location = location
        };
    }

    private static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.FromArgb(249, 251, 254);
        textBox.ForeColor = TextColor;
        textBox.Font = new Font("Segoe UI", 10F);
    }

    private static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.FromArgb(249, 251, 254);
        comboBox.ForeColor = TextColor;
        comboBox.Font = new Font("Segoe UI", 10F);
    }

    private static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.AutoSize = true;
        checkBox.Font = new Font("Segoe UI", 9.5F);
        checkBox.ForeColor = TextColor;
        checkBox.BackColor = Color.Transparent;
    }

    private static void StylePrimaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = PrimaryColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.MouseEnter += (_, _) => button.BackColor = PrimaryHoverColor;
        button.MouseLeave += (_, _) => button.BackColor = PrimaryColor;
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Color.White;
        button.ForeColor = PrimaryColor;
        button.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(238, 245, 255);
        button.MouseLeave += (_, _) => button.BackColor = Color.White;
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecione a pasta onde o UniFlowHub será instalado",
            SelectedPath = _targetPath.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _targetPath.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        ToggleControls(false);
        _log.Clear();
        _progress.Value = 0;
        _statusLabel.Text = "Preparando instalação...";

        try
        {
            await Task.Run(Install);
            SetProgress(100);
            Log("Instalação concluída.");
            MessageBox.Show(this, "Instalação concluída com sucesso.", "UniFlowHub", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("ERRO: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Falha na instalação", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ToggleControls(true);
        }
    }

    private void Install()
    {
        var source = Path.GetFullPath(AppContext.BaseDirectory);
        var target = Path.GetFullPath(GetText(_targetPath));

        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("Informe a pasta de destino.");
        }

        if (string.Equals(source.TrimEnd('\\'), target.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A pasta de destino não pode ser a mesma pasta do pacote de deploy.");
        }

        ValidateOracleSettings();

        Log("Pacote: " + source);
        Log("Destino: " + target);
        Directory.CreateDirectory(target);

        SetProgress(10);
        Log("Preparando arquivos de configuração.");

        SetProgress(20);
        var appOfflinePath = Path.Combine(target, "app_offline.htm");
        var createdAppOffline = false;
        var restartIis = IsChecked(_restartIis);
        var siteName = GetText(_siteName);
        var appPoolName = GetText(_appPoolName);

        try
        {
            if (restartIis)
            {
                StopIis(siteName, appPoolName);
            }

            if (IsChecked(_useAppOffline) && !File.Exists(appOfflinePath))
            {
                File.WriteAllText(appOfflinePath, "<html><body>UniFlowHub em manutenção.</body></html>");
                createdAppOffline = true;
                Log("Aplicação colocada offline temporariamente.");
                Thread.Sleep(1500);
            }

            SetProgress(35);
            CopyPackage(source, target);
            ApplyOracleSettings(target);
            SetProgress(70);
            RunMigrationsIfNeeded(source, target);
            SetProgress(95);
        }
        finally
        {
            if (createdAppOffline && File.Exists(appOfflinePath))
            {
                try
                {
                    File.Delete(appOfflinePath);
                    Log("Aplicação retirada do modo offline.");
                }
                catch (Exception ex)
                {
                    Log("Não foi possível remover app_offline.htm: " + ex.Message);
                }
            }

            if (restartIis)
            {
                StartIis(siteName, appPoolName);
            }
        }
    }

    private void CopyPackage(string source, string target)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Install-UniFlowHub-GUI.exe",
            "Install-UniFlowHub-GUI.dll",
            "Install-UniFlowHub-GUI.deps.json",
            "Install-UniFlowHub-GUI.runtimeconfig.json",
            "Install-UniFlowHub-GUI.pdb",
            "Install-UniFlowHub.cmd",
            "Install-UniFlowHub.ps1"
        };

        if (IsChecked(_preserveAppSettings) && File.Exists(Path.Combine(target, "appsettings.json")))
        {
            excluded.Add("appsettings.json");
            Log("Preservando appsettings.json existente.");
        }

        foreach (var entry in Directory.GetFileSystemEntries(source))
        {
            var name = Path.GetFileName(entry);
            if (excluded.Contains(name))
            {
                Log("Preservando: " + name);
                continue;
            }

            var destination = Path.Combine(target, name);
            if (Directory.Exists(entry))
            {
                if (string.Equals(name, "wwwroot", StringComparison.OrdinalIgnoreCase) && Directory.Exists(destination))
                {
                    Log("Limpando wwwroot antigo...");
                    Directory.Delete(destination, recursive: true);
                }

                CopyDirectory(entry, destination);
            }
            else
            {
                File.Copy(entry, destination, overwrite: true);
            }
        }

        Log("Arquivos copiados.");
    }

    private void LoadOracleDefaultsIntoForm()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(configPath))
        {
            ApplyOracleDefaults(
                new OracleConnectionParts("cnp", "ninguemsabe", "192.168.1.2"),
                new OracleConnectionParts("cnp", "ninguemsabe", "192.168.1.2"),
                "DVE");
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject();
            var connectionStrings = root?["ConnectionStrings"]?.AsObject();
            var oracle = root?["Oracle"]?.AsObject();
            var dve = ParseOracleConnection(connectionStrings?["OracleConnectionDve"]?.GetValue<string>() ?? string.Empty);
            var production = ParseOracleConnection(connectionStrings?["OracleConnectionProduction"]?.GetValue<string>()
                ?? connectionStrings?["OracleConnection"]?.GetValue<string>()
                ?? string.Empty);
            var environment = oracle?["Environment"]?.GetValue<string>() ?? "DVE";

            ApplyOracleDefaults(dve, production, environment);
        }
        catch (Exception ex)
        {
            Log("Não foi possível carregar padrões Oracle do pacote: " + ex.Message);
            ApplyOracleDefaults(
                new OracleConnectionParts("cnp", "ninguemsabe", "192.168.1.2"),
                new OracleConnectionParts("cnp", "ninguemsabe", "192.168.1.2"),
                "DVE");
        }
    }

    private void ApplyOracleSettings(string target)
    {
        var configPath = Path.Combine(target, "appsettings.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("appsettings.json não foi encontrado no destino.");
        }

        var root = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject() ?? new JsonObject();
        var connectionStrings = root["ConnectionStrings"] as JsonObject;
        if (connectionStrings is null)
        {
            connectionStrings = new JsonObject();
            root["ConnectionStrings"] = connectionStrings;
        }

        var oracle = root["Oracle"] as JsonObject;
        if (oracle is null)
        {
            oracle = new JsonObject();
            root["Oracle"] = oracle;
        }

        var selectedEnvironment = GetSelectedOracleEnvironment();
        var dveTemplate = connectionStrings["OracleConnectionDve"]?.GetValue<string>()
            ?? connectionStrings["OracleConnection"]?.GetValue<string>()
            ?? string.Empty;
        var productionTemplate = connectionStrings["OracleConnectionProduction"]?.GetValue<string>()
            ?? connectionStrings["OracleConnection"]?.GetValue<string>()
            ?? string.Empty;

        var dveConnection = BuildOracleConnection(
            dveTemplate,
            GetText(_oracleDveUser),
            GetText(_oracleDvePassword),
            GetText(_oracleDveHost),
            "teste.drsul");
        var productionConnection = BuildOracleConnection(
            productionTemplate,
            GetText(_oracleProductionUser),
            GetText(_oracleProductionPassword),
            GetText(_oracleProductionHost),
            "apollo.drsul");

        connectionStrings["OracleConnectionDve"] = dveConnection;
        connectionStrings["OracleConnectionProduction"] = productionConnection;
        connectionStrings["OracleConnection"] = selectedEnvironment == "PRODUCTION" ? productionConnection : dveConnection;
        oracle["Environment"] = selectedEnvironment;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, root.ToJsonString(options));
        Log("Configurações Oracle aplicadas ao appsettings.json.");
    }

    private void ValidateOracleSettings()
    {
        var fields = new[]
        {
            ("usuário do Oracle de testes", _oracleDveUser),
            ("senha do Oracle de testes", _oracleDvePassword),
            ("host do Oracle de testes", _oracleDveHost),
            ("usuário do Oracle de produção", _oracleProductionUser),
            ("senha do Oracle de produção", _oracleProductionPassword),
            ("host do Oracle de produção", _oracleProductionHost)
        };

        foreach (var (label, field) in fields)
        {
            if (string.IsNullOrWhiteSpace(GetText(field)))
            {
                throw new InvalidOperationException("Informe o " + label + ".");
            }
        }
    }

    private void ApplyOracleDefaults(OracleConnectionParts dve, OracleConnectionParts production, string environment)
    {
        _oracleDveUser.Text = dve.User;
        _oracleDvePassword.Text = dve.Password;
        _oracleDveHost.Text = dve.Host;
        _oracleProductionUser.Text = production.User;
        _oracleProductionPassword.Text = production.Password;
        _oracleProductionHost.Text = production.Host;
        _oracleEnvironment.SelectedIndex = string.Equals(environment, "PRODUCTION", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private string GetSelectedOracleEnvironment()
    {
        if (_oracleEnvironment.InvokeRequired)
        {
            return (string)_oracleEnvironment.Invoke(() => GetSelectedOracleEnvironment());
        }

        return _oracleEnvironment.SelectedIndex == 1 ? "PRODUCTION" : "DVE";
    }

    private static OracleConnectionParts ParseOracleConnection(string connection)
    {
        return new OracleConnectionParts(
            MatchValue(connection, @"User\s+Id\s*=\s*([^;]*)", "cnp"),
            MatchValue(connection, @"Password\s*=\s*([^;]*)", "ninguemsabe"),
            MatchValue(connection, @"\(HOST\s*=\s*([^)]+)\)", "192.168.1.2"));
    }

    private static string MatchValue(string value, string pattern, string fallback)
    {
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : fallback;
    }

    private static string BuildOracleConnection(string template, string user, string password, string host, string serviceName)
    {
        var connection = string.IsNullOrWhiteSpace(template)
            ? $"User Id={user};Password={password};Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT=1526)))(CONNECT_DATA=(SERVICE_NAME={serviceName})))"
            : template;

        connection = ReplaceConnectionValue(connection, "User Id", user);
        connection = ReplaceConnectionValue(connection, "Password", password);
        connection = Regex.IsMatch(connection, @"\(HOST\s*=\s*[^)]+\)", RegexOptions.IgnoreCase)
            ? Regex.Replace(connection, @"\(HOST\s*=\s*[^)]+\)", "(HOST=" + host + ")", RegexOptions.IgnoreCase)
            : connection;

        return connection;
    }

    private static string ReplaceConnectionValue(string connection, string key, string value)
    {
        var pattern = @"\b" + Regex.Escape(key) + @"\s*=\s*[^;]*";
        if (Regex.IsMatch(connection, pattern, RegexOptions.IgnoreCase))
        {
            return Regex.Replace(connection, pattern, key + "=" + value, RegexOptions.IgnoreCase);
        }

        return key + "=" + value + ";" + connection;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private void RunMigrationsIfNeeded(string source, string target)
    {
        var exePath = Path.Combine(target, "UniFlowHub.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException("UniFlowHub.exe não foi encontrado no destino.");
        }

        Log("Aplicando migrations pendentes...");
        var migrationOutput = new StringBuilder();
        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = "--migrate",
            WorkingDirectory = target,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Não foi possível iniciar UniFlowHub.exe --migrate.");
        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            migrationOutput.AppendLine(e.Data);
            Log(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            migrationOutput.AppendLine(e.Data);
            Log(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var details = migrationOutput.ToString().Trim();
            var message = "Falha ao aplicar migrations do banco interno PostgreSQL. Código: " + process.ExitCode;

            if (!string.IsNullOrWhiteSpace(details))
            {
                message += Environment.NewLine + details;
            }

            throw new InvalidOperationException(message);
        }

        Log("Migrations aplicadas com sucesso.");
    }

    private void StopIis(string siteName, string appPoolName)
    {
        if (!string.IsNullOrWhiteSpace(siteName))
        {
            Log("Parando site IIS: " + siteName);
            RunAppCmd(["stop", "site", "/site.name:" + siteName], "Não foi possível parar o site. Execute como Administrador ou confira o nome do site.");
        }

        if (!string.IsNullOrWhiteSpace(appPoolName))
        {
            Log("Parando App Pool IIS: " + appPoolName);
            RunAppCmd(["stop", "apppool", "/apppool.name:" + appPoolName], "Não foi possível parar o App Pool. Execute como Administrador ou confira o nome do App Pool.");
        }
    }

    private void StartIis(string siteName, string appPoolName)
    {
        if (!string.IsNullOrWhiteSpace(appPoolName))
        {
            Log("Iniciando App Pool IIS: " + appPoolName);
            RunAppCmd(["start", "apppool", "/apppool.name:" + appPoolName], "Não foi possível iniciar o App Pool. Inicie manualmente pelo IIS.");
        }

        if (!string.IsNullOrWhiteSpace(siteName))
        {
            Log("Iniciando site IIS: " + siteName);
            RunAppCmd(["start", "site", "/site.name:" + siteName], "Não foi possível iniciar o site. Inicie manualmente pelo IIS.");
        }
    }

    private void RunAppCmd(string[] arguments, string warning)
    {
        var appCmd = Path.Combine(Environment.SystemDirectory, "inetsrv", "appcmd.exe");
        if (!File.Exists(appCmd))
        {
            Log("appcmd.exe não encontrado. Pulando controle do IIS.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = appCmd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Log(warning);
            return;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            Log(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Log(error.Trim());
        }

        if (process.ExitCode != 0)
        {
            Log(warning);
        }
    }

    private void ToggleControls(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(() => ToggleControls(enabled));
            return;
        }

        _targetPath.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _preserveAppSettings.Enabled = enabled;
        _useAppOffline.Enabled = enabled;
        _restartIis.Enabled = enabled;
        _siteName.Enabled = enabled;
        _appPoolName.Enabled = enabled;
        _oracleEnvironment.Enabled = enabled;
        _oracleDveUser.Enabled = enabled;
        _oracleDvePassword.Enabled = enabled;
        _oracleDveHost.Enabled = enabled;
        _oracleProductionUser.Enabled = enabled;
        _oracleProductionPassword.Enabled = enabled;
        _oracleProductionHost.Enabled = enabled;
        _installButton.Enabled = enabled;
        _statusLabel.Text = enabled ? "Pronto para instalar" : "Instalação em andamento...";
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => Log(message));
            return;
        }

        _statusLabel.Text = message;
        _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
    }

    private void SetProgress(int value)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetProgress(value));
            return;
        }

        _progress.Value = Math.Clamp(value, _progress.Minimum, _progress.Maximum);
    }

    private static string GetText(TextBox textBox)
    {
        if (textBox.InvokeRequired)
        {
            return (string)textBox.Invoke(() => textBox.Text);
        }

        return textBox.Text;
    }

    private static bool IsChecked(CheckBox checkBox)
    {
        if (checkBox.InvokeRequired)
        {
            return (bool)checkBox.Invoke(() => checkBox.Checked);
        }

        return checkBox.Checked;
    }

    private sealed record OracleConnectionParts(string User, string Password, string Host);

    private sealed class HeaderPanel : Panel
    {
        public HeaderPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new LinearGradientBrush(ClientRectangle, HeaderStartColor, HeaderEndColor, 0F);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderLineColor { get; set; } = InstallerForm.BorderColor;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 16;

        public RoundedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            using var path = CreateRoundedRectanglePath(ClientRectangle, Radius);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectanglePath(ClientRectangle, Radius);
            using var brush = new SolidBrush(BackColor);
            using var pen = new Pen(BorderLineColor);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            var rectangle = new Rectangle(bounds.Location, new Size(diameter, diameter));
            var path = new GraphicsPath();

            path.AddArc(rectangle, 180, 90);
            rectangle.X = bounds.Right - diameter - 1;
            path.AddArc(rectangle, 270, 90);
            rectangle.Y = bounds.Bottom - diameter - 1;
            path.AddArc(rectangle, 0, 90);
            rectangle.X = bounds.Left;
            path.AddArc(rectangle, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
