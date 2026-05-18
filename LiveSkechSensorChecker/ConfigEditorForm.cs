namespace LiveSkechSensorChecker;

internal sealed class ConfigEditorForm : Form
{
    // 설정 파일 저장 경로 및 기존 값 보존 필드
    private readonly string _configPath;
    private readonly int _originalAlertThresholdSeconds;
    private readonly int _originalRebootAlertAttemptCount;
    private readonly int _originalLaunchPathCurrentIndex;
    private readonly DateTime? _originalLaunchPathLastAdvancedDateUtc;
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
    // 실행 파일 순환 탭 UI 요소
    private readonly ListBox _launchPathListBox;
    private readonly Label _currentIndexLabel;
    private readonly Label _todayProgramLabel;
    private readonly DataGridView _weeklyScheduleGrid;
    private readonly NumericUpDown _initialTimeoutSeconds;
    private readonly NumericUpDown _alertThresholdSecondsForMain;
    private readonly NumericUpDown _rebootAlertAttemptCountForMain;
    private readonly CheckBox _forceCenterClickFallbackCheck;
    private readonly NumericUpDown _centerClickDelaySeconds;
    private readonly NumericUpDown _centerClickX;
    private readonly NumericUpDown _centerClickY;
    private readonly CheckBox _enableHelperFocusProcessCheck;
    private readonly NumericUpDown _helperFocusDelaySeconds;
    private readonly DataGridView _peerGrid;

    public bool IsSaved { get; private set; }

    public ConfigEditorForm(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _originalAlertThresholdSeconds = config.AlertThresholdSeconds ?? config.PeerHeartbeatTimeoutSeconds;
        _originalRebootAlertAttemptCount = config.RebootAlertAttemptCount;
        _originalLaunchPathCurrentIndex = config.MainBehavior?.LaunchPathCurrentIndex ?? 0;
        _originalLaunchPathLastAdvancedDateUtc = config.MainBehavior?.LaunchPathLastAdvancedDateUtc;
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
        var mainTabs = new TabControl { Dock = DockStyle.Top, Height = 500 };
        var mainGeneralTab = new TabPage("Main 기본 설정");
        var mainLaunchListTab = new TabPage("실행 파일 순환");

        var mainTopTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true };
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        mainTopTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _initialTimeoutSeconds = NewRangeUpDown(5, 300);
        _alertThresholdSecondsForMain = NewRangeUpDown(2, 300);
        _rebootAlertAttemptCountForMain = NewRangeUpDown(1, 20);
        _forceCenterClickFallbackCheck = new CheckBox { Text = "중앙 클릭 fallback 사용", AutoSize = true };
        _centerClickDelaySeconds = NewRangeUpDown(0, 300);
        _centerClickX = NewRangeUpDown(0, 5000);
        _centerClickY = NewRangeUpDown(0, 5000);
        _enableHelperFocusProcessCheck = new CheckBox { Text = "헬퍼 포커스 프로세스 사용", AutoSize = true };
        _helperFocusDelaySeconds = NewRangeUpDown(0, 300);

        AddGridRow(mainTopTable, 0, "초기 점검 타임아웃", _initialTimeoutSeconds, "알림 임계(초)", _alertThresholdSecondsForMain);
        AddGridRow(mainTopTable, 1, "경고 시도횟수", _rebootAlertAttemptCountForMain, null, null);

        var fallbackPanel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        fallbackPanel.Controls.Add(_forceCenterClickFallbackCheck);
        fallbackPanel.Controls.Add(new Label { AutoSize = true, Text = "클릭 딜레이(초)" });
        fallbackPanel.Controls.Add(_centerClickDelaySeconds);
        fallbackPanel.Controls.Add(new Label { AutoSize = true, Text = "X" });
        fallbackPanel.Controls.Add(_centerClickX);
        fallbackPanel.Controls.Add(new Label { AutoSize = true, Text = "Y" });
        fallbackPanel.Controls.Add(_centerClickY);
        fallbackPanel.Controls.Add(_enableHelperFocusProcessCheck);
        fallbackPanel.Controls.Add(new Label { AutoSize = true, Text = "헬퍼 딜레이(초)" });
        fallbackPanel.Controls.Add(_helperFocusDelaySeconds);

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

        var generalPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, Padding = new Padding(8) };
        generalPanel.Controls.Add(mainTopTable);
        generalPanel.Controls.Add(fallbackPanel);
        generalPanel.Controls.Add(peerLabel);
        generalPanel.Controls.Add(peerHelpLabel);
        generalPanel.Controls.Add(_peerGrid);
        mainGeneralTab.Controls.Add(generalPanel);

        _launchPathListBox = new ListBox { Dock = DockStyle.Fill, Height = 180 };
        var launchListButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var addLaunchPathButton = new Button { Text = "실행파일 추가", AutoSize = true };
        var removeLaunchPathButton = new Button { Text = "선택 삭제", AutoSize = true };
        addLaunchPathButton.Click += AddLaunchPathButton_Click;
        removeLaunchPathButton.Click += (_, _) =>
        {
            if (_launchPathListBox.SelectedIndex >= 0)
            {
                _launchPathListBox.Items.RemoveAt(_launchPathListBox.SelectedIndex);
                RefreshLaunchSchedulePreview();
            }
        };
        launchListButtons.Controls.Add(addLaunchPathButton);
        launchListButtons.Controls.Add(removeLaunchPathButton);

        var launchTabHelp = new Label
        {
            AutoSize = true,
            Text = "리스트 개수에 맞춰 매일 다음 인덱스 실행 (마지막이면 0번으로 순환)"
        };
        _currentIndexLabel = new Label { AutoSize = true, Text = "현재 인덱스: -" };
        _todayProgramLabel = new Label { AutoSize = true, Text = "오늘 실행: -" };
        _weeklyScheduleGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            Height = 240,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle = BorderStyle.FixedSingle
        };
        _weeklyScheduleGrid.Columns.Add("day", "요일");
        _weeklyScheduleGrid.Columns.Add("date", "날짜");
        _weeklyScheduleGrid.Columns.Add("index", "인덱스");
        _weeklyScheduleGrid.Columns.Add("program", "프로그램");
        _weeklyScheduleGrid.Columns[0].FillWeight = 18;
        _weeklyScheduleGrid.Columns[1].FillWeight = 22;
        _weeklyScheduleGrid.Columns[2].FillWeight = 18;
        _weeklyScheduleGrid.Columns[3].FillWeight = 42;
        var scheduleHelp = new Label { AutoSize = true, Text = "이번주(월~일) 일정표 - 매주 월요일 기준으로 갱신" };
        var launchTabLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, Padding = new Padding(8) };
        launchTabLayout.Controls.Add(launchTabHelp);
        launchTabLayout.Controls.Add(launchListButtons);
        launchTabLayout.Controls.Add(_launchPathListBox);
        launchTabLayout.Controls.Add(_currentIndexLabel);
        launchTabLayout.Controls.Add(_todayProgramLabel);
        launchTabLayout.Controls.Add(scheduleHelp);
        launchTabLayout.Controls.Add(_weeklyScheduleGrid);
        mainLaunchListTab.Controls.Add(launchTabLayout);

        mainTabs.TabPages.Add(mainGeneralTab);
        mainTabs.TabPages.Add(mainLaunchListTab);
        mainLayout.Controls.Add(mainTabs);
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

        _launchPathListBox.Items.Clear();
        foreach (var launchPath in config.MainBehavior?.LaunchPathList ?? [])
        {
            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                _launchPathListBox.Items.Add(launchPath);
            }
        }
        _initialTimeoutSeconds.Value = config.MainBehavior?.InitialCheckTimeoutSeconds ?? 20;
        _forceCenterClickFallbackCheck.Checked = config.MainBehavior?.ForceCenterClickFallback ?? false;
        _centerClickDelaySeconds.Value = config.MainBehavior?.CenterClickDelaySeconds ?? 30;
        _centerClickX.Value = config.MainBehavior?.CenterClickX ?? 500;
        _centerClickY.Value = config.MainBehavior?.CenterClickY ?? 500;
        _enableHelperFocusProcessCheck.Checked = config.MainBehavior?.EnableHelperFocusProcess ?? false;
        _helperFocusDelaySeconds.Value = config.MainBehavior?.HelperFocusDelaySeconds ?? 3;
        _alertThresholdSecondsForMain.Value = config.AlertThresholdSeconds ?? config.PeerHeartbeatTimeoutSeconds;
        _rebootAlertAttemptCountForMain.Value = config.RebootAlertAttemptCount;
        RefreshLaunchSchedulePreview();

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

        var udp = new UdpConfig
        {
            ListenPort = (int)_listenPort.Value,
            MainPort = (int)_mainPort.Value,
            MainIp = string.IsNullOrWhiteSpace(_mainIpText.Text) ? null : _mainIpText.Text.Trim()
        };

        MainBehaviorConfig? mainBehavior = null;
        List<PeerConfig> peers;
        var alertThreshold = _originalAlertThresholdSeconds;
        var rebootAlertAttemptCount = _originalRebootAlertAttemptCount;

        if (role == "main")
        {
            var launchPathList = _launchPathListBox.Items.Cast<object>()
                .Select(x => x.ToString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (launchPathList.Count == 0)
            {
                throw new InvalidOperationException("Main 역할은 실행 파일 순환 목록에 1개 이상 필요합니다.");
            }

            var subPeers = BuildSubPeersFromGrid();

            mainBehavior = new MainBehaviorConfig
            {
                LaunchOnAllHealthyPath = launchPathList[0],
                LaunchPathList = launchPathList,
                LaunchPathCurrentIndex = _originalLaunchPathCurrentIndex,
                LaunchPathLastAdvancedDateUtc = _originalLaunchPathLastAdvancedDateUtc,
                LaunchArguments = string.Empty,
                InitialCheckTimeoutSeconds = (int)_initialTimeoutSeconds.Value,
                ForceCenterClickFallback = _forceCenterClickFallbackCheck.Checked,
                CenterClickDelaySeconds = (int)_centerClickDelaySeconds.Value,
                CenterClickX = (int)_centerClickX.Value,
                CenterClickY = (int)_centerClickY.Value,
                EnableHelperFocusProcess = _enableHelperFocusProcessCheck.Checked,
                HelperFocusDelaySeconds = (int)_helperFocusDelaySeconds.Value
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
            alertThreshold = (int)_alertThresholdSecondsForMain.Value;
            rebootAlertAttemptCount = (int)_rebootAlertAttemptCountForMain.Value;
        }
        else
        {
            if (localProcesses.Count == 0)
            {
                throw new InvalidOperationException("Sub 역할은 로컬 프로세스를 1개 이상 선택하세요.");
            }

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
            PeerHeartbeatTimeoutSeconds = alertThreshold,
            RebootAlertAttemptCount = rebootAlertAttemptCount,
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

    private void AddLaunchPathButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "순환 실행 파일 추가",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _launchPathListBox.Items.Add(dialog.FileName);
            RefreshLaunchSchedulePreview();
        }
    }

    /// <summary>
    /// 실행 파일 순환 미리보기(현재 인덱스, 오늘 실행 파일, 이번주 월~일 일정표)를 갱신합니다.
    /// </summary>
    private void RefreshLaunchSchedulePreview()
    {
        var launchPathList = _launchPathListBox.Items.Cast<object>()
            .Select(x => x.ToString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (launchPathList.Count == 0)
        {
            _currentIndexLabel.Text = "현재 인덱스: -";
            _todayProgramLabel.Text = "오늘 실행: -";
            _weeklyScheduleGrid.Rows.Clear();
            return;
        }

        var currentIndex = _originalLaunchPathCurrentIndex;
        if (currentIndex < 0 || currentIndex >= launchPathList.Count)
        {
            currentIndex = 0;
        }

        _currentIndexLabel.Text = $"현재 인덱스: {currentIndex}";
        _todayProgramLabel.Text = $"오늘 실행: {Path.GetFileName(launchPathList[currentIndex])}";

        var now = DateTime.UtcNow.Date;
        var dayOffset = ((int)now.DayOfWeek + 6) % 7; // Monday=0
        var monday = now.AddDays(-dayOffset);
        _weeklyScheduleGrid.Rows.Clear();
        var todayRowIndex = -1;
        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            var indexForDay = (currentIndex + i) % launchPathList.Count;
            var rowIndex = _weeklyScheduleGrid.Rows.Add(
                date.ToString("ddd"),
                date.ToString("MM-dd"),
                indexForDay.ToString(),
                Path.GetFileName(launchPathList[indexForDay]));

            if (date == now)
            {
                todayRowIndex = rowIndex;
            }
        }

        if (todayRowIndex >= 0)
        {
            var row = _weeklyScheduleGrid.Rows[todayRowIndex];
            row.DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 214);
            row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(255, 212, 128);
            row.DefaultCellStyle.SelectionForeColor = Color.Black;
            row.DefaultCellStyle.Font = new Font(_weeklyScheduleGrid.Font, FontStyle.Bold);
            row.DividerHeight = 2;
        }
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
