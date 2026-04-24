namespace LiveSkechSensorChecker;

internal sealed class ConfigEditorForm : Form
{
    private readonly string _configPath;
    private readonly int _originalAlertThresholdSeconds;
    private readonly List<PeerConfig> _fixedPeers;

    private readonly ComboBox _roleCombo;
    private readonly TextBox _pcNameText;
    private readonly NumericUpDown _listenPort;
    private readonly NumericUpDown _sendIntervalSeconds;
    private readonly TextBox _localProcessesText;

    private readonly GroupBox _subGroup;
    private readonly TextBox _mainIpText;
    private readonly NumericUpDown _mainPort;

    private readonly GroupBox _mainGroup;
    private readonly TextBox _launchPathText;
    private readonly NumericUpDown _initialTimeoutSeconds;
    private readonly NumericUpDown _alertSecondsForMain;
    private readonly CheckBox _forceCenterClickFallbackCheck;
    private readonly DataGridView _peerGrid;

    public bool IsSaved { get; private set; }

    public ConfigEditorForm(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _originalAlertThresholdSeconds = config.AlertThresholdSeconds;
        _fixedPeers = config.Peers
            .Select(p => new PeerConfig { Name = p.Name, Ip = p.Ip, Role = p.Role, Processes = [.. p.Processes] })
            .ToList();

        Text = "설정 편집";
        StartPosition = FormStartPosition.CenterParent;
        Width = 980;
        Height = 700;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(8),
            AutoScroll = true
        };

        _roleCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _roleCombo.Items.AddRange(["main", "sub"]);
        _pcNameText = new TextBox { Width = 180 };
        _listenPort = NewRangeUpDown(1, 65535);
        _sendIntervalSeconds = NewRangeUpDown(1, 60);

        _localProcessesText = new TextBox { Dock = DockStyle.Fill };
        var processPickBtn = new Button { Text = "실행중 프로세스", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 0, 8, 0) };
        processPickBtn.Click += (_, _) => PickProcessesInto(_localProcessesText);

        var commonGroup = new GroupBox { Text = "공통 설정", Dock = DockStyle.Top, AutoSize = true };
        var commonTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, AutoSize = true, Padding = new Padding(8) };
        commonTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        commonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        commonTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        commonTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddGridRow(commonTable, 0, "역할", _roleCombo, "내 PC 이름", _pcNameText);
        AddGridRow(commonTable, 1, "수신 Port", _listenPort, "전송주기(초)", _sendIntervalSeconds);
        commonGroup.Controls.Add(commonTable);

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
        var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, Padding = new Padding(8) };

        var mainTopTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true };
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _launchPathText = new TextBox { Dock = DockStyle.Fill, Width = 420 };
        var browseButton = new Button { Text = "프로그램 선택", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 0, 8, 0) };
        browseButton.Click += BrowseButton_Click;
        _initialTimeoutSeconds = NewRangeUpDown(5, 300);
        _alertSecondsForMain = NewRangeUpDown(2, 300);
        _forceCenterClickFallbackCheck = new CheckBox { Text = "포커스 실패 시 중앙 클릭 fallback 사용", AutoSize = true };

        AddGridRow(mainTopTable, 0, "실행 파일 경로", _launchPathText, null, null);
        mainTopTable.Controls.Add(browseButton, 3, 0);
        AddGridRow(mainTopTable, 1, "초기 점검 타임아웃", _initialTimeoutSeconds, "알림 임계(초)", _alertSecondsForMain);

        var fallbackPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        fallbackPanel.Controls.Add(_forceCenterClickFallbackCheck);

        var peerLabel = new Label { AutoSize = true, Text = "점검할 Sub PC 목록" };
        _peerGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            RowHeadersVisible = false,
            Height = 190
        };
        _peerGrid.Columns.Add("name", "PC 이름");
        _peerGrid.Columns.Add("ip", "IP");
        

        var peerHelpLabel = new Label { AutoSize = true, Text = "Sub PC 이름/IP만 등록" };

        mainLayout.Controls.Add(mainTopTable);
        mainLayout.Controls.Add(fallbackPanel);
        mainLayout.Controls.Add(peerLabel);
        mainLayout.Controls.Add(peerHelpLabel);
        mainLayout.Controls.Add(_peerGrid);
        _mainGroup.Controls.Add(mainLayout);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.RightToLeft, Height = 42 };
        var saveButton = new Button { Text = "저장", Width = 100, Height = 30 };
        var cancelButton = new Button { Text = "취소", Width = 100, Height = 30 };
        saveButton.Click += SaveButton_Click;
        cancelButton.Click += (_, _) => Close();
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);

        root.Controls.Add(commonGroup);
        root.Controls.Add(processGroup);
        root.Controls.Add(_subGroup);
        root.Controls.Add(_mainGroup);
        root.Controls.Add(actions);
        Controls.Add(root);

        BindFromConfig(config);
        ApplyRoleUi();
        _roleCombo.SelectedIndexChanged += (_, _) => ApplyRoleUi();
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "실행할 프로그램 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _launchPathText.Text = dialog.FileName;
        }
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
        _localProcessesText.Text = string.Join(", ", config.LocalMonitoring.Processes);

        _mainIpText.Text = config.Udp.MainIp ?? string.Empty;
        _mainPort.Value = config.Udp.MainPort;

        _launchPathText.Text = config.MainBehavior?.LaunchOnAllHealthyPath ?? string.Empty;
        _initialTimeoutSeconds.Value = config.MainBehavior?.InitialCheckTimeoutSeconds ?? 20;
        _forceCenterClickFallbackCheck.Checked = config.MainBehavior?.ForceCenterClickFallback ?? false;
        _alertSecondsForMain.Value = config.AlertThresholdSeconds;

        _peerGrid.Rows.Clear();
        foreach (var peer in config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)))
        {
                        _peerGrid.Rows.Add(peer.Name, peer.Ip);
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
            throw new InvalidOperationException("로컬 프로세스를 1개 이상 선택하세요.");
        }

        var udp = new UdpConfig
        {
            ListenPort = (int)_listenPort.Value,
            MainPort = (int)_mainPort.Value,
            MainIp = string.IsNullOrWhiteSpace(_mainIpText.Text) ? null : _mainIpText.Text.Trim()
        };

        MainBehaviorConfig? mainBehavior = null;
        List<PeerConfig> peers;
        var alertThreshold = _originalAlertThresholdSeconds;

        if (role == "main")
        {
            var launchPath = _launchPathText.Text.Trim();
            if (string.IsNullOrWhiteSpace(launchPath))
            {
                throw new InvalidOperationException("Main 역할은 실행 파일 경로가 필요합니다.");
            }

            var subPeers = BuildSubPeersFromGrid();
            if (subPeers.Count == 0)
            {
                throw new InvalidOperationException("Main에서는 점검할 Sub PC를 최소 1개 등록해야 합니다.");
            }

            mainBehavior = new MainBehaviorConfig
            {
                LaunchOnAllHealthyPath = launchPath,
                LaunchArguments = string.Empty,
                InitialCheckTimeoutSeconds = (int)_initialTimeoutSeconds.Value,
                ForceCenterClickFallback = _forceCenterClickFallbackCheck.Checked
            };

            var existingMainIp = GetExistingMainIpOrDefault();
            var mainPeer = new PeerConfig
            {
                Name = pcName,
                Ip = existingMainIp,
                Role = "main",
                Processes = [.. localProcesses]
            };

            peers = [mainPeer, .. subPeers];
            alertThreshold = (int)_alertSecondsForMain.Value;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(udp.MainIp))
            {
                throw new InvalidOperationException("Sub 역할은 Main IP가 필요합니다.");
            }

            peers = BuildSubRolePeersFromExisting(pcName, localProcesses);
        }

        return new AppConfig
        {
            Role = role,
            HeartbeatIntervalSeconds = (int)_sendIntervalSeconds.Value,
            AlertThresholdSeconds = alertThreshold,
            Udp = udp,
            LocalMonitoring = new LocalMonitoringConfig
            {
                PcName = pcName,
                Processes = localProcesses
            },
            Peers = peers,
            MainBehavior = mainBehavior
        };
    }

    private string GetExistingMainIpOrDefault()
    {
        var main = _fixedPeers.FirstOrDefault(p => string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase));
        return main is null || string.IsNullOrWhiteSpace(main.Ip) ? "127.0.0.1" : main.Ip;
    }

    private List<PeerConfig> BuildSubPeersFromGrid()
    {
        var list = new List<PeerConfig>();
        foreach (DataGridViewRow row in _peerGrid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var name = row.Cells[0].Value?.ToString()?.Trim() ?? string.Empty;
            var ip = row.Cells[1].Value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(ip))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(ip))
            {
                throw new InvalidOperationException("Sub PC 목록은 이름/IP를 모두 입력해야 합니다.");
            }

            list.Add(new PeerConfig { Name = name, Ip = ip, Role = "sub", Processes = [] });
        }

        return list;
    }

    private List<PeerConfig> BuildSubRolePeersFromExisting(string pcName, List<string> localProcesses)
    {
        var peers = _fixedPeers
            .Where(p => !string.Equals(p.Name, pcName, StringComparison.OrdinalIgnoreCase))
            .Select(p => new PeerConfig
            {
                Name = p.Name,
                Ip = p.Ip,
                Role = p.Role,
                Processes = [.. p.Processes]
            })
            .ToList();

        peers.Add(new PeerConfig { Name = pcName, Ip = "127.0.0.1", Role = "sub", Processes = [.. localProcesses] });
        return peers;
    }

    private static List<string> SplitProcesses(string value)
        => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
