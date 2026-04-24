using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LiveSkechSensorChecker;

public partial class Form1 : Form
{
    private readonly AppConfig _config;
    private readonly string _configPath;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, PeerRuntimeState> _peerStates = new();
    private readonly HashSet<string> _alertedPeers = [];

    private UdpClient? _listener;
    private UdpClient? _sender;
    private System.Windows.Forms.Timer? _monitorTimer;
    private bool _launchedTarget;

    public Form1()
    {
        InitializeComponent();

        _configPath = AppConfig.GetConfigPath(AppContext.BaseDirectory);
        _config = AppConfig.Load(AppContext.BaseDirectory);
        Shown += Form1_Shown;
        FormClosing += Form1_FormClosing;
    }

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        try
        {
            SetStatus($"실행 역할: {(IsMainRole() ? "MainPC" : "SubPC")}");
            AppendLog($"프로그램 시작 - 역할: {(IsMainRole() ? "MainPC" : "SubPC")}");
            StartReceiver();

            if (IsMainRole())
            {
                InitializePeerGridForMain();
                await RunInitialMainCheckAsync();
                StartMonitorLoop();
            }
            else
            {
                InitializePeerGridForSub();
                StartSubHeartbeatLoop();
                StartMonitorLoop();
            }
        }
        catch (Exception ex)
        {
            AppendLog($"오류: {ex.Message}");
            MessageBox.Show($"시작 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _cts.Cancel();
        _listener?.Dispose();
        _sender?.Dispose();
        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
    }

    private bool IsMainRole() => string.Equals(_config.Role, "main", StringComparison.OrdinalIgnoreCase);

    private void StartReceiver()
    {
        _listener = new UdpClient(_config.Udp.ListenPort);
        AppendLog($"UDP 수신 대기 시작: Port={_config.Udp.ListenPort}");

        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var result = await _listener.ReceiveAsync(_cts.Token);
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    var packet = JsonSerializer.Deserialize<HeartbeatPacket>(json, JsonOptions.Default);
                    if (packet is null)
                    {
                        continue;
                    }

                    var peer = _peerStates.GetOrAdd(packet.PcName, _ => new PeerRuntimeState(packet.PcName, packet.Role));
                    peer.Update(packet);
                    BeginInvoke(RefreshGrid);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BeginInvoke(() => AppendLog($"수신 오류: {ex.Message}"));
                }
            }
        }, _cts.Token);
    }

    private void StartSubHeartbeatLoop()
    {
        if (string.IsNullOrWhiteSpace(_config.Udp.MainIp))
        {
            throw new InvalidOperationException("Sub 모드에서는 udp.mainIp를 설정해야 합니다.");
        }

        _sender ??= new UdpClient();
        var endpoint = new IPEndPoint(IPAddress.Parse(_config.Udp.MainIp), _config.Udp.MainPort);

        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var packet = BuildLocalHeartbeat();
                    var json = JsonSerializer.Serialize(packet, JsonOptions.Default);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _sender.SendAsync(bytes, endpoint, _cts.Token);
                    BeginInvoke(() =>
                    {
                        SetStatus("SubPC 동작 중 (하트비트 전송)");
                        var processSummary = string.Join(", ", packet.Processes.Select(p => $"{p.ProcessName}:{(p.IsRunning ? "정상" : "비정상")}"));
                        AppendLog($"전송 -> {endpoint.Address}:{endpoint.Port} / PC={packet.PcName} / {processSummary}");
                        RefreshSubGrid(packet);
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BeginInvoke(() => AppendLog($"전송 오류: {ex.Message}"));
                }

                await Task.Delay(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds), _cts.Token);
            }
        }, _cts.Token);
    }

    private HeartbeatPacket BuildLocalHeartbeat()
    {
        var processStates = _config.LocalMonitoring.Processes
            .Select(p => new ProcessState
            {
                ProcessName = p,
                IsRunning = Process.GetProcessesByName(p).Length > 0
            })
            .ToList();

        return new HeartbeatPacket
        {
            PcName = _config.LocalMonitoring.PcName,
            Role = _config.Role,
            TimestampUtc = DateTime.UtcNow,
            Processes = processStates
        };
    }

    private async Task RunInitialMainCheckAsync()
    {
        SetStatus("초기 점검 중...");
        AppendLog("MainPC 초기 점검 시작");

        var mainBehavior = _config.MainBehavior ?? throw new InvalidOperationException("mainBehavior 설정이 필요합니다.");
        var targetPeers = _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)).ToList();

        var timeout = TimeSpan.FromSeconds(mainBehavior.InitialCheckTimeoutSeconds);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            RefreshGrid();
            var allHealthy = targetPeers.All(IsPeerHealthy);
            if (allHealthy)
            {
                SetStatus("초기 점검 완료: 모든 SubPC 정상");
                AppendLog("초기 점검 성공");
                LaunchConfiguredProgramIfNeeded();
                return;
            }

            await Task.Delay(1000, _cts.Token);
        }

        SetStatus("초기 점검 실패: 일부 SubPC 미응답/프로세스 비정상");
        AppendLog("초기 점검 타임아웃");
        MessageBox.Show("초기 점검에서 모든 SubPC의 정상 상태를 확인하지 못했습니다.", "주의", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private bool IsPeerHealthy(PeerConfig peer)
    {
        if (!_peerStates.TryGetValue(peer.Name, out var state))
        {
            return false;
        }

        if (DateTime.UtcNow - state.LastHeartbeatUtc > TimeSpan.FromSeconds(_config.AlertThresholdSeconds))
        {
            return false;
        }

        return peer.Processes.All(processName =>
            state.Processes.TryGetValue(processName, out var isRunning) && isRunning);
    }

    private void LaunchConfiguredProgramIfNeeded()
    {
        if (_launchedTarget)
        {
            return;
        }

        var mainBehavior = _config.MainBehavior;
        if (mainBehavior is null)
        {
            return;
        }

        var path = mainBehavior.LaunchOnAllHealthyPath;
        if (!File.Exists(path))
        {
            AppendLog($"실행 대상 파일 없음: {path}");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = mainBehavior.LaunchArguments ?? string.Empty,
            UseShellExecute = true
        });
        _launchedTarget = true;

        AppendLog($"연동 소프트웨어 실행: {path}");
    }

    private void StartMonitorLoop()
    {
        _monitorTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _monitorTimer.Tick += (_, _) =>
        {
            if (IsMainRole())
            {
                MonitorPeersAndAlert();
            }
            else
            {
                MonitorLocalSub();
            }
        };
        _monitorTimer.Start();
    }

    private void MonitorPeersAndAlert()
    {
        var targetPeers = _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var peer in targetPeers)
        {
            var healthy = IsPeerHealthy(peer);
            if (healthy)
            {
                _alertedPeers.Remove(peer.Name);
                continue;
            }

            if (_alertedPeers.Add(peer.Name))
            {
                var message = $"{peer.Name} 상태 이상 감지. PC 점검이 필요합니다.";
                AppendLog(message);
                MessageBox.Show(message, "모니터링 알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        RefreshGrid();
    }

    private void MonitorLocalSub()
    {
        var packet = BuildLocalHeartbeat();
        var allOk = packet.Processes.All(p => p.IsRunning);
        SetStatus(allOk ? "SubPC 정상 동작" : "SubPC 프로세스 점검 필요");
    }

    private void InitializePeerGridForMain()
    {
        peerGrid.Rows.Clear();
        foreach (var peer in _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var processName in peer.Processes)
            {
                peerGrid.Rows.Add(peer.Name, peer.Role, processName, "미수신", "-");
            }
        }
    }

    private void InitializePeerGridForSub()
    {
        peerGrid.Rows.Clear();
        foreach (var processName in _config.LocalMonitoring.Processes)
        {
            peerGrid.Rows.Add(_config.LocalMonitoring.PcName, _config.Role, processName, "점검 중", DateTime.Now.ToString("HH:mm:ss"));
        }
    }

    private void RefreshGrid()
    {
        if (!IsMainRole())
        {
            return;
        }

        foreach (DataGridViewRow row in peerGrid.Rows)
        {
            var pc = row.Cells[0].Value?.ToString() ?? string.Empty;
            var processName = row.Cells[2].Value?.ToString() ?? string.Empty;

            if (!_peerStates.TryGetValue(pc, out var state))
            {
                row.Cells[3].Value = "미수신";
                row.Cells[4].Value = "-";
                continue;
            }

            if (DateTime.UtcNow - state.LastHeartbeatUtc > TimeSpan.FromSeconds(_config.AlertThresholdSeconds))
            {
                row.Cells[3].Value = "타임아웃";
                row.Cells[4].Value = state.LastHeartbeatUtc.ToLocalTime().ToString("HH:mm:ss");
                continue;
            }

            if (state.Processes.TryGetValue(processName, out var isRunning))
            {
                row.Cells[3].Value = isRunning ? "정상" : "비정상";
                row.Cells[4].Value = state.LastHeartbeatUtc.ToLocalTime().ToString("HH:mm:ss");
            }
        }
    }

    private void RefreshSubGrid(HeartbeatPacket packet)
    {
        foreach (DataGridViewRow row in peerGrid.Rows)
        {
            var processName = row.Cells[2].Value?.ToString() ?? string.Empty;
            var process = packet.Processes.FirstOrDefault(p => p.ProcessName == processName);
            if (process is null)
            {
                continue;
            }

            row.Cells[3].Value = process.IsRunning ? "정상" : "비정상";
            row.Cells[4].Value = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    private void editConfigButton_Click(object sender, EventArgs e)
    {
        using var editor = new ConfigEditorForm(_configPath, _config);
        editor.ShowDialog(this);

        if (!editor.IsSaved)
        {
            return;
        }

        AppendLog("설정이 저장되었습니다.");
        MessageBox.Show("설정이 저장되었습니다. 변경사항 적용을 위해 프로그램을 재시작해주세요.", "안내", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        logTextBox.AppendText(line);
    }
}

internal sealed class PeerRuntimeState(string name, string role)
{
    public string Name { get; } = name;
    public string Role { get; } = role;
    public DateTime LastHeartbeatUtc { get; private set; } = DateTime.MinValue;
    public Dictionary<string, bool> Processes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void Update(HeartbeatPacket packet)
    {
        LastHeartbeatUtc = packet.TimestampUtc;
        Processes.Clear();
        foreach (var process in packet.Processes)
        {
            Processes[process.ProcessName] = process.IsRunning;
        }
    }
}
