namespace LiveSkechSensorChecker;

internal sealed class ConfigEditorForm : Form
{
    private readonly string _configPath;

    private readonly ComboBox _roleCombo;
    private readonly TextBox _pcNameText;
    private readonly NumericUpDown _listenPort;
    private readonly TextBox _mainIpText;
    private readonly NumericUpDown _mainPort;
    private readonly NumericUpDown _heartbeatSeconds;
    private readonly NumericUpDown _alertSeconds;
    private readonly TextBox _localProcessesText;

    private readonly TextBox _launchPathText;
    private readonly TextBox _launchArgsText;
    private readonly NumericUpDown _initialTimeoutSeconds;

    private readonly DataGridView _peersGrid;

    public bool IsSaved { get; private set; }

    public ConfigEditorForm(string configPath, AppConfig config)
    {
        _configPath = configPath;

        Text = "설정 편집(쉬운 입력)";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1020;
        Height = 780;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var help = new Label
        {
            AutoSize = true,
            Text = "JSON 원문이 아닌 입력 폼으로 설정합니다. (역할/IP/Port/프로세스 등)",
            Padding = new Padding(0, 0, 0, 6)
        };

        _roleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _roleCombo.Items.AddRange(["main", "sub"]);
        _pcNameText = new TextBox { Width = 200 };
        _listenPort = NewRangeUpDown(1, 65535);
        _mainIpText = new TextBox { Width = 180 };
        _mainPort = NewRangeUpDown(1, 65535);
        _heartbeatSeconds = NewRangeUpDown(1, 60);
        _alertSeconds = NewRangeUpDown(2, 300);

        _localProcessesText = new TextBox { Dock = DockStyle.Fill };
        var localProcessPickBtn = new Button { Text = "실행중 프로세스 선택", Width = 160 };
        localProcessPickBtn.Click += (_, _) => PickProcessesInto(_localProcessesText);

        _launchPathText = new TextBox { Dock = DockStyle.Fill };
        _launchArgsText = new TextBox { Dock = DockStyle.Fill };
        _initialTimeoutSeconds = NewRangeUpDown(5, 300);

        var basicGroup = new GroupBox { Text = "기본 설정", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var basicTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        basicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddGridRow(basicTable, 0, "역할", _roleCombo, "내 PC 이름", _pcNameText);
        AddGridRow(basicTable, 1, "수신 Port", _listenPort, "Main IP", _mainIpText);
        AddGridRow(basicTable, 2, "Main Port", _mainPort, "Heartbeat(초)", _heartbeatSeconds);
        AddGridRow(basicTable, 3, "알림 임계(초)", _alertSeconds, null, null);
        basicGroup.Controls.Add(basicTable);

        var processGroup = new GroupBox { Text = "로컬 프로세스", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var processLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Padding = new Padding(8) };
        processLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        processLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        processLayout.Controls.Add(_localProcessesText, 0, 0);
        processLayout.Controls.Add(localProcessPickBtn, 1, 0);
        processGroup.Controls.Add(processLayout);

        var mainGroup = new GroupBox { Text = "Main 동작", Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var mainTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddGridRow(mainTable, 0, "실행 파일 경로", _launchPathText, "실행 인자", _launchArgsText);
        AddGridRow(mainTable, 1, "초기 점검 타임아웃", _initialTimeoutSeconds, null, null);
        mainGroup.Controls.Add(mainTable);

        _peersGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
        _peersGrid.Columns.Add("name", "PC 이름");
        _peersGrid.Columns.Add("ip", "IP");
        _peersGrid.Columns.Add("role", "Role(main/sub)");
        _peersGrid.Columns.Add("processes", "프로세스(콤마구분)");

        var peerGroup = new GroupBox { Text = "Peer 목록", Dock = DockStyle.Fill };
        var peerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        peerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        peerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var peerButtonRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var peerProcessPickBtn = new Button { Text = "선택 행 프로세스 선택", Width = 180 };
        peerProcessPickBtn.Click += (_, _) => PickProcessesForSelectedPeerRow();
        peerButtonRow.Controls.Add(peerProcessPickBtn);
        peerLayout.Controls.Add(peerButtonRow, 0, 0);
        peerLayout.Controls.Add(_peersGrid, 0, 1);
        peerGroup.Controls.Add(peerLayout);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48
        };
        var saveButton = new Button { Text = "저장", Width = 100, Height = 30 };
        var cancelButton = new Button { Text = "취소", Width = 100, Height = 30 };
        saveButton.Click += SaveButton_Click;
        cancelButton.Click += (_, _) => Close();
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);

        root.Controls.Add(help, 0, 0);
        root.Controls.Add(basicGroup, 0, 1);
        root.Controls.Add(processGroup, 0, 2);
        root.Controls.Add(mainGroup, 0, 3);
        root.Controls.Add(peerGroup, 0, 4);
        root.Controls.Add(actions, 0, 5);

        root.RowCount = 6;
        root.RowStyles.Clear();
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Controls.Add(root);

        BindFromConfig(config);
        ApplyRoleUi();
        _roleCombo.SelectedIndexChanged += (_, _) => ApplyRoleUi();
    }

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

    private static NumericUpDown NewRangeUpDown(int min, int max)
        => new() { Minimum = min, Maximum = max, Width = 120 };

    private void ApplyRoleUi()
    {
        var isMain = string.Equals(_roleCombo.SelectedItem?.ToString(), "main", StringComparison.OrdinalIgnoreCase);
        _launchPathText.Enabled = isMain;
        _launchArgsText.Enabled = isMain;
        _initialTimeoutSeconds.Enabled = isMain;
    }

    private void PickProcessesInto(TextBox target)
    {
        var existing = SplitProcesses(target.Text);
        using var picker = new ProcessPickerForm(existing);
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        target.Text = string.Join(", ", picker.SelectedProcesses);
    }

    private void PickProcessesForSelectedPeerRow()
    {
        if (_peersGrid.CurrentRow is null || _peersGrid.CurrentRow.IsNewRow)
        {
            MessageBox.Show("Peer 목록에서 행을 먼저 선택하세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var current = _peersGrid.CurrentRow.Cells[3].Value?.ToString() ?? string.Empty;
        using var picker = new ProcessPickerForm(SplitProcesses(current));
        if (picker.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _peersGrid.CurrentRow.Cells[3].Value = string.Join(", ", picker.SelectedProcesses);
    }

    private void BindFromConfig(AppConfig config)
    {
        _roleCombo.SelectedItem = config.Role.ToLowerInvariant();
        _pcNameText.Text = config.LocalMonitoring.PcName;
        _listenPort.Value = config.Udp.ListenPort;
        _mainIpText.Text = config.Udp.MainIp ?? string.Empty;
        _mainPort.Value = config.Udp.MainPort;
        _heartbeatSeconds.Value = config.HeartbeatIntervalSeconds;
        _alertSeconds.Value = config.AlertThresholdSeconds;
        _localProcessesText.Text = string.Join(", ", config.LocalMonitoring.Processes);

        _launchPathText.Text = config.MainBehavior?.LaunchOnAllHealthyPath ?? string.Empty;
        _launchArgsText.Text = config.MainBehavior?.LaunchArguments ?? string.Empty;
        _initialTimeoutSeconds.Value = config.MainBehavior?.InitialCheckTimeoutSeconds ?? 20;

        _peersGrid.Rows.Clear();
        foreach (var peer in config.Peers)
        {
            _peersGrid.Rows.Add(peer.Name, peer.Ip, peer.Role, string.Join(", ", peer.Processes));
        }
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

        var localProcesses = SplitProcesses(_localProcessesText.Text);
        if (localProcesses.Count == 0)
        {
            throw new InvalidOperationException("로컬 프로세스를 1개 이상 선택하세요.");
        }

        var peers = new List<PeerConfig>();
        foreach (DataGridViewRow row in _peersGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var name = row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
            var ip = row.Cells[1].Value?.ToString()?.Trim() ?? string.Empty;
            var peerRole = row.Cells[2].Value?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
            var processes = SplitProcesses(row.Cells[3].Value?.ToString() ?? string.Empty);

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(ip) && processes.Count == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip) || peerRole is not ("main" or "sub"))
            {
                throw new InvalidOperationException("Peer는 이름/IP/role(main/sub)을 모두 입력하세요.");
            }

            if (processes.Count == 0)
            {
                throw new InvalidOperationException($"{name}의 프로세스를 선택하세요.");
            }

            peers.Add(new PeerConfig { Name = name, Ip = ip, Role = peerRole, Processes = processes });
        }

        if (peers.Count == 0)
        {
            throw new InvalidOperationException("Peer는 최소 1개 이상 필요합니다.");
        }

        MainBehaviorConfig? mainBehavior = null;
        if (role == "main")
        {
            var path = _launchPathText.Text.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Main 역할은 실행 파일 경로가 필요합니다.");
            }

            mainBehavior = new MainBehaviorConfig
            {
                LaunchOnAllHealthyPath = path,
                LaunchArguments = _launchArgsText.Text.Trim(),
                InitialCheckTimeoutSeconds = (int)_initialTimeoutSeconds.Value
            };
        }

        return new AppConfig
        {
            Role = role,
            HeartbeatIntervalSeconds = (int)_heartbeatSeconds.Value,
            AlertThresholdSeconds = (int)_alertSeconds.Value,
            Udp = new UdpConfig
            {
                ListenPort = (int)_listenPort.Value,
                MainIp = string.IsNullOrWhiteSpace(_mainIpText.Text) ? null : _mainIpText.Text.Trim(),
                MainPort = (int)_mainPort.Value
            },
            LocalMonitoring = new LocalMonitoringConfig
            {
                PcName = _pcNameText.Text.Trim(),
                Processes = localProcesses
            },
            Peers = peers,
            MainBehavior = mainBehavior
        };
    }

    private static List<string> SplitProcesses(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
