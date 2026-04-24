namespace LiveSkechSensorChecker;

internal sealed class ConfigEditorForm : Form
{
    private readonly string _configPath;
    private readonly List<PeerConfig> _fixedPeers;

    private readonly ComboBox _roleCombo;
    private readonly TextBox _pcNameText;
    private readonly NumericUpDown _listenPort;
    private readonly NumericUpDown _sendIntervalSeconds;
    private readonly NumericUpDown _alertSeconds;
    private readonly TextBox _localProcessesText;

    private readonly GroupBox _subGroup;
    private readonly TextBox _mainIpText;
    private readonly NumericUpDown _mainPort;

    private readonly GroupBox _mainGroup;
    private readonly TextBox _launchPathText;
    private readonly TextBox _launchArgsText;
    private readonly NumericUpDown _initialTimeoutSeconds;

    public bool IsSaved { get; private set; }

    public ConfigEditorForm(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _fixedPeers = config.Peers
            .Select(p => new PeerConfig { Name = p.Name, Ip = p.Ip, Role = p.Role, Processes = [.. p.Processes] })
            .ToList();

        Text = "설정 편집";
        StartPosition = FormStartPosition.CenterParent;
        Width = 900;
        Height = 560;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(8),
            AutoScroll = true
        };

        var help = new Label
        {
            AutoSize = true,
            Text = "역할에 따라 필요한 설정만 표시됩니다.",
            Padding = new Padding(0, 0, 0, 6)
        };

        _roleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _roleCombo.Items.AddRange(["main", "sub"]);
        _pcNameText = new TextBox { Width = 180 };
        _listenPort = NewRangeUpDown(1, 65535);
        _sendIntervalSeconds = NewRangeUpDown(1, 60);
        _alertSeconds = NewRangeUpDown(2, 300);

        _localProcessesText = new TextBox { Dock = DockStyle.Fill };
        var processPickBtn = new Button { Text = "실행중 프로세스 선택", Width = 160 };
        processPickBtn.Click += (_, _) => PickProcessesInto(_localProcessesText);

        var basicGroup = new GroupBox { Text = "공통 설정", Dock = DockStyle.Top, AutoSize = true };
        var basicTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddGridRow(basicTable, 0, "역할", _roleCombo, "내 PC 이름", _pcNameText);
        AddGridRow(basicTable, 1, "수신 Port", _listenPort, "전송주기(초)", _sendIntervalSeconds);
        AddGridRow(basicTable, 2, "알림 임계(초)", _alertSeconds, null, null);
        basicGroup.Controls.Add(basicTable);

        var processGroup = new GroupBox { Text = "로컬 프로세스", Dock = DockStyle.Top, AutoSize = true };
        var processTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
        processTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        processTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        processTable.Controls.Add(_localProcessesText, 0, 0);
        processTable.Controls.Add(processPickBtn, 1, 0);
        processGroup.Controls.Add(processTable);

        _subGroup = new GroupBox { Text = "Sub 전용 설정", Dock = DockStyle.Top, AutoSize = true };
        var subTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        subTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        subTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        subTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        subTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _mainIpText = new TextBox { Width = 180 };
        _mainPort = NewRangeUpDown(1, 65535);
        AddGridRow(subTable, 0, "Main IP", _mainIpText, "Main Port", _mainPort);
        _subGroup.Controls.Add(subTable);

        _mainGroup = new GroupBox { Text = "Main 전용 설정", Dock = DockStyle.Top, AutoSize = true };
        var mainTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _launchPathText = new TextBox { Dock = DockStyle.Fill };
        _launchArgsText = new TextBox { Dock = DockStyle.Fill };
        _initialTimeoutSeconds = NewRangeUpDown(5, 300);
        AddGridRow(mainTable, 0, "실행 파일 경로", _launchPathText, "실행 인자", _launchArgsText);
        AddGridRow(mainTable, 1, "초기 점검 타임아웃", _initialTimeoutSeconds, null, null);
        _mainGroup.Controls.Add(mainTable);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft, Height = 42 };
        var saveButton = new Button { Text = "저장", Width = 100, Height = 30 };
        var cancelButton = new Button { Text = "취소", Width = 100, Height = 30 };
        saveButton.Click += SaveButton_Click;
        cancelButton.Click += (_, _) => Close();
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);

        root.Controls.Add(help);
        root.Controls.Add(basicGroup);
        root.Controls.Add(processGroup);
        root.Controls.Add(_subGroup);
        root.Controls.Add(_mainGroup);
        root.Controls.Add(actions);
        Controls.Add(root);

        BindFromConfig(config);
        ApplyRoleUi();
        _roleCombo.SelectedIndexChanged += (_, _) => ApplyRoleUi();
    }

    private static NumericUpDown NewRangeUpDown(int min, int max)
        => new() { Minimum = min, Maximum = max, Width = 120 };

    private static void AddGridRow(TableLayoutPanel table, int row, string leftLabel, Control leftControl, string? rightLabel, Control? rightControl)
    {
        table.RowCount = Math.Max(table.RowCount, row + 1);
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { AutoSize = true, Text = leftLabel, Padding = new Padding(0, 8, 6, 0) }, 0, row);
        table.Controls.Add(leftControl, 1, row);

        if (rightLabel is not null && rightControl is not null)
        {
            table.Controls.Add(new Label { AutoSize = true, Text = rightLabel, Padding = new Padding(12, 8, 6, 0) }, 2, row);
            table.Controls.Add(rightControl, 3, row);
        }
    }

    private void ApplyRoleUi()
    {
        var isMain = string.Equals(_roleCombo.SelectedItem?.ToString(), "main", StringComparison.OrdinalIgnoreCase);
        _mainGroup.Visible = isMain;
        _subGroup.Visible = !isMain;
    }

    private void PickProcessesInto(TextBox target)
    {
        var existing = SplitProcesses(target.Text);
        using var picker = new ProcessPickerForm(existing);
        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = string.Join(", ", picker.SelectedProcesses);
        }
    }

    private void BindFromConfig(AppConfig config)
    {
        _roleCombo.SelectedItem = config.Role.ToLowerInvariant();
        _pcNameText.Text = config.LocalMonitoring.PcName;
        _listenPort.Value = config.Udp.ListenPort;
        _sendIntervalSeconds.Value = config.HeartbeatIntervalSeconds;
        _alertSeconds.Value = config.AlertThresholdSeconds;
        _localProcessesText.Text = string.Join(", ", config.LocalMonitoring.Processes);

        _mainIpText.Text = config.Udp.MainIp ?? string.Empty;
        _mainPort.Value = config.Udp.MainPort;

        _launchPathText.Text = config.MainBehavior?.LaunchOnAllHealthyPath ?? string.Empty;
        _launchArgsText.Text = config.MainBehavior?.LaunchArguments ?? string.Empty;
        _initialTimeoutSeconds.Value = config.MainBehavior?.InitialCheckTimeoutSeconds ?? 20;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            var config = BuildConfigFromInputs();
            config.Save(_configPath);
            IsSaved = true;
            MessageBox.Show("설정이 저장되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private AppConfig BuildConfigFromInputs()
    {
        var role = _roleCombo.SelectedItem?.ToString()?.Trim().ToLowerInvariant();
        if (role is not ("main" or "sub"))
        {
            throw new InvalidOperationException("역할은 main 또는 sub 이어야 합니다.");
        }

        var pcName = _pcNameText.Text.Trim();
        if (string.IsNullOrWhiteSpace(pcName))
        {
            throw new InvalidOperationException("내 PC 이름을 입력하세요.");
        }

        var localProcesses = SplitProcesses(_localProcessesText.Text);
        if (localProcesses.Count == 0)
        {
            throw new InvalidOperationException("로컬 프로세스를 1개 이상 선택하세요.");
        }

        var udp = new UdpConfig
        {
            ListenPort = (int)_listenPort.Value,
            MainPort = (int)_mainPort.Value,
            MainIp = string.IsNullOrWhiteSpace(_mainIpText.Text) ? null : _mainIpText.Text.Trim()
        };

        MainBehaviorConfig? mainBehavior = null;
        if (role == "main")
        {
            var launchPath = _launchPathText.Text.Trim();
            if (string.IsNullOrWhiteSpace(launchPath))
            {
                throw new InvalidOperationException("Main 역할은 실행 파일 경로가 필요합니다.");
            }

            mainBehavior = new MainBehaviorConfig
            {
                LaunchOnAllHealthyPath = launchPath,
                LaunchArguments = _launchArgsText.Text.Trim(),
                InitialCheckTimeoutSeconds = (int)_initialTimeoutSeconds.Value
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(udp.MainIp))
            {
                throw new InvalidOperationException("Sub 역할은 Main IP가 필요합니다.");
            }
        }

        return new AppConfig
        {
            Role = role,
            HeartbeatIntervalSeconds = (int)_sendIntervalSeconds.Value,
            AlertThresholdSeconds = (int)_alertSeconds.Value,
            Udp = udp,
            LocalMonitoring = new LocalMonitoringConfig
            {
                PcName = pcName,
                Processes = localProcesses
            },
            Peers = _fixedPeers,
            MainBehavior = mainBehavior
        };
    }

    private static List<string> SplitProcesses(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
