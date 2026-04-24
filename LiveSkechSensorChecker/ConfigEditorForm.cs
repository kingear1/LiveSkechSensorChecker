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

        Text = "설정 편집";
        StartPosition = FormStartPosition.CenterParent;
        Width = 980;
        Height = 760;

        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        container.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var summary = new Label
        {
            AutoSize = true,
            Padding = new Padding(8, 10, 8, 10),
            Text = "역할, IP/Port, 프로세스 이름 등 운영 값만 입력/수정할 수 있습니다."
        };

        var editorPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(8),
            AutoScroll = true
        };
        editorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        editorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

        _roleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _roleCombo.Items.AddRange(["main", "sub"]);

        _pcNameText = new TextBox { Dock = DockStyle.Fill };
        _listenPort = NewPortUpDown();
        _mainIpText = new TextBox { Dock = DockStyle.Fill };
        _mainPort = NewPortUpDown();
        _heartbeatSeconds = NewRangeUpDown(1, 60);
        _alertSeconds = NewRangeUpDown(2, 300);
        _localProcessesText = new TextBox { Dock = DockStyle.Fill };

        _launchPathText = new TextBox { Dock = DockStyle.Fill };
        _launchArgsText = new TextBox { Dock = DockStyle.Fill };
        _initialTimeoutSeconds = NewRangeUpDown(5, 300);

        _peersGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Height = 220,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false
        };
        _peersGrid.Columns.Add("name", "PC 이름");
        _peersGrid.Columns.Add("ip", "IP");
        _peersGrid.Columns.Add("role", "Role(main/sub)");
        _peersGrid.Columns.Add("processes", "프로세스(콤마구분)");

        var row = 0;
        AddRow(editorPanel, row++, "역할", _roleCombo);
        AddRow(editorPanel, row++, "내 PC 이름", _pcNameText);
        AddRow(editorPanel, row++, "내 수신 Port", _listenPort);
        AddRow(editorPanel, row++, "Main IP (sub에서 사용)", _mainIpText);
        AddRow(editorPanel, row++, "Main Port", _mainPort);
        AddRow(editorPanel, row++, "Heartbeat 주기(초)", _heartbeatSeconds);
        AddRow(editorPanel, row++, "알림 임계시간(초)", _alertSeconds);
        AddRow(editorPanel, row++, "내 프로세스 이름(콤마구분)", _localProcessesText);
        AddRow(editorPanel, row++, "메인 실행 파일 경로", _launchPathText);
        AddRow(editorPanel, row++, "메인 실행 인자", _launchArgsText);
        AddRow(editorPanel, row++, "초기점검 타임아웃(초)", _initialTimeoutSeconds);

        var peerLabel = new Label
        {
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 4),
            Text = "Peer 목록"
        };

        editorPanel.RowCount = row + 2;
        editorPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        editorPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        editorPanel.Controls.Add(peerLabel, 0, row);
        editorPanel.SetColumnSpan(peerLabel, 2);
        editorPanel.Controls.Add(_peersGrid, 0, row + 1);
        editorPanel.SetColumnSpan(_peersGrid, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var saveButton = new Button { Width = 120, Height = 32, Text = "저장" };
        var cancelButton = new Button { Width = 120, Height = 32, Text = "취소" };
        saveButton.Click += SaveButton_Click;
        cancelButton.Click += (_, _) => Close();
        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);

        container.Controls.Add(summary, 0, 0);
        container.Controls.Add(editorPanel, 0, 1);
        container.Controls.Add(buttons, 0, 2);
        Controls.Add(container);

        BindFromConfig(config);
    }

    private static NumericUpDown NewPortUpDown() => NewRangeUpDown(1, 65535);

    private static NumericUpDown NewRangeUpDown(int min, int max)
        => new() { Dock = DockStyle.Left, Minimum = min, Maximum = max, Width = 120 };

    private static void AddRow(TableLayoutPanel panel, int row, string label, Control control)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lbl = new Label { AutoSize = true, Text = label, Anchor = AnchorStyles.Left, Padding = new Padding(0, 8, 0, 0) };
        panel.Controls.Add(lbl, 0, row);
        panel.Controls.Add(control, 1, row);
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

        var pcName = _pcNameText.Text.Trim();
        if (string.IsNullOrWhiteSpace(pcName))
        {
            throw new InvalidOperationException("내 PC 이름을 입력하세요.");
        }

        var localProcesses = SplitProcesses(_localProcessesText.Text);
        if (localProcesses.Count == 0)
        {
            throw new InvalidOperationException("최소 1개 이상의 로컬 프로세스를 입력하세요.");
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
            var processText = row.Cells[3].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(ip) && string.IsNullOrWhiteSpace(processText))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip) || peerRole is not ("main" or "sub"))
            {
                throw new InvalidOperationException("Peer 항목은 이름/IP/role(main/sub)을 모두 입력해야 합니다.");
            }

            var processes = SplitProcesses(processText);
            if (processes.Count == 0)
            {
                throw new InvalidOperationException($"{name}의 프로세스를 1개 이상 입력하세요.");
            }

            peers.Add(new PeerConfig
            {
                Name = name,
                Ip = ip,
                Role = peerRole,
                Processes = processes
            });
        }

        if (peers.Count == 0)
        {
            throw new InvalidOperationException("최소 1개 이상의 Peer를 입력하세요.");
        }

        MainBehaviorConfig? mainBehavior = null;
        if (role == "main")
        {
            var launchPath = _launchPathText.Text.Trim();
            if (string.IsNullOrWhiteSpace(launchPath))
            {
                throw new InvalidOperationException("Main 역할에서는 실행 파일 경로를 입력하세요.");
            }

            mainBehavior = new MainBehaviorConfig
            {
                LaunchOnAllHealthyPath = launchPath,
                LaunchArguments = _launchArgsText.Text,
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
                PcName = pcName,
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
