using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
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
    private readonly Dictionary<string, Label> _peerCheckLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastRebootAttemptUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _rebootAttemptCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _initialProblemDetectedUtc = new(StringComparer.OrdinalIgnoreCase);
    private int _spinnerFrame;
    private bool _rebootPending;

    private UdpClient? _listener;
    private UdpClient? _sender;
    private UdpClient? _mainCommandSender;
    private System.Windows.Forms.Timer? _monitorTimer;
    private bool _launchedTarget;
    private bool _allowExitFromTray;

    public Form1()
    {
        InitializeComponent();

        _configPath = AppConfig.GetConfigPath(AppContext.BaseDirectory);
        _config = AppConfig.Load(AppContext.BaseDirectory);
        trayIcon.Icon = SystemIcons.Application;
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
                peerCheckStatePanel.Visible = true;
                InitializePeerGridForMain();
                await RunInitialMainCheckAsync();
                StartMonitorLoop();
            }
            else
            {
                peerCheckStatePanel.Visible = false;
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
        if (!_allowExitFromTray && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        _cts.Cancel();
        _listener?.Dispose();
        _sender?.Dispose();
        _mainCommandSender?.Dispose();
        _monitorTimer?.Stop();
        _monitorTimer?.Dispose();
    }

    private bool IsMainRole() => string.Equals(_config.Role, "main", StringComparison.OrdinalIgnoreCase);

    private int GetAlertThresholdSeconds()
        => Math.Max(1, _config.AlertThresholdSeconds ?? _config.PeerHeartbeatTimeoutSeconds);

    private bool IsMainLocalHealthy()
    {
        if (!IsMainRole())
        {
            return true;
        }

        var processNames = _config.LocalMonitoring.Processes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (processNames.Count == 0)
        {
            return true;
        }

        return processNames.All(processName => Process.GetProcessesByName(processName).Length > 0);
    }

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

                    if (string.Equals(packet.PacketType, "reboot", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsMainRole() && string.Equals(packet.TargetPcName, _config.LocalMonitoring.PcName, StringComparison.OrdinalIgnoreCase))
                        {
                            BeginInvoke(() => HandleRebootCommand());
                        }
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
                        AppendLog($"전송 -> {endpoint.Address}:{endpoint.Port} / PC={packet.PcName} / 상태={(packet.IsHealthy ? "정상" : "비정상")}");
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
        var allHealthy = _config.LocalMonitoring.Processes
            .All(processName => Process.GetProcessesByName(processName).Length > 0);

        return new HeartbeatPacket
        {
            PcName = _config.LocalMonitoring.PcName,
            Role = _config.Role,
            TimestampUtc = DateTime.UtcNow,
            IsHealthy = allHealthy,
            PacketType = "heartbeat"
        };
    }

    private async Task RunInitialMainCheckAsync()
    {
        SetStatus("초기 점검 중...");
        AppendLog("MainPC 초기 점검 시작");

        var mainBehavior = _config.MainBehavior ?? throw new InvalidOperationException("mainBehavior 설정이 필요합니다.");
        var targetPeers = _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)).ToList();

        var timeoutSeconds = Math.Max(1, mainBehavior.InitialCheckTimeoutSeconds);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var started = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            RefreshGrid();
            UpdatePeerCheckIndicators(targetPeers);
            var mainLocalHealthy = IsMainLocalHealthy();

            foreach (var peer in targetPeers)
            {
                if (!IsPeerProblem(peer))
                {
                    _initialProblemDetectedUtc.Remove(peer.Name);
                    continue;
                }

                if (!_initialProblemDetectedUtc.TryGetValue(peer.Name, out var detectedAt))
                {
                    _initialProblemDetectedUtc[peer.Name] = DateTime.UtcNow;
                    continue;
                }

                if (DateTime.UtcNow - detectedAt >= TimeSpan.FromSeconds(5))
                {
                    AttemptRebootIfDue(peer);
                }
            }

            var allPeersHealthy = targetPeers.All(IsPeerHealthy);
            var allHealthy = allPeersHealthy && mainLocalHealthy;
            if (allHealthy)
            {
                MarkAllPeerChecksAsDone(targetPeers);
                SetStatus("초기 점검 완료: 모든 모니터링 대상 정상");
                AppendLog("초기 점검 성공");
                LaunchConfiguredProgramIfNeeded();
                return;
            }

            if (!mainLocalHealthy)
            {
                SetStatus("초기 점검 중: MainPC 로컬 프로세스 비정상");
            }

            await Task.Delay(250, _cts.Token);
        }

        MarkTimeoutPeerChecks(targetPeers);
        var mainLocalHealthyAtTimeout = IsMainLocalHealthy();
        SetStatus(mainLocalHealthyAtTimeout
            ? "초기 점검 실패: 일부 SubPC 미응답/프로세스 비정상"
            : "초기 점검 실패: MainPC 로컬 프로세스 또는 SubPC 상태 비정상");
        AppendLog("초기 점검 타임아웃");

        if (!mainLocalHealthyAtTimeout)
        {
            AppendLog("MainPC 로컬 프로세스 점검 실패");
        }

        foreach (var peer in targetPeers.Where(IsPeerProblem))
        {
            AttemptRebootIfDue(peer);
            if (ShouldShowWarning(peer) && _alertedPeers.Add(peer.Name))
            {
                ShowUnresolvedWarning(peer);
            }
        }
    }

    private bool IsPeerHealthy(PeerConfig peer)
    {
        if (!_peerStates.TryGetValue(peer.Name, out var state))
        {
            return false;
        }

        if (DateTime.UtcNow - state.LastHeartbeatUtc > TimeSpan.FromSeconds(GetAlertThresholdSeconds()))
        {
            return false;
        }

        return state.IsHealthy;
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

        var launched = Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = mainBehavior.LaunchArguments ?? string.Empty,
            UseShellExecute = true
        });
        _launchedTarget = true;

        if (launched is not null)
        {
            _ = Task.Run(async () =>
            {
                TryBringToFront(launched);

                if (mainBehavior.EnableHelperFocusProcess)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, mainBehavior.HelperFocusDelaySeconds)));
                    RunHelperFocusProcess(launched);
                }

                if (mainBehavior.ForceCenterClickFallback)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, mainBehavior.CenterClickDelaySeconds)));
                    TryFixedCoordinateClick(mainBehavior.CenterClickX, mainBehavior.CenterClickY);
                }
            });
        }

        MinimizeToTray();
        AppendLog($"연동 소프트웨어 실행: {path}");
    }

    private static void TryBringToFront(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                process.WaitForInputIdle(5000);
            }
            catch
            {
                // 콘솔/백그라운드 프로세스일 수 있으므로 무시
            }

            IntPtr handle = IntPtr.Zero;
            for (var i = 0; i < 15; i++)
            {
                if (process.HasExited)
                {
                    return;
                }

                process.Refresh();
                handle = process.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    break;
                }

                Thread.Sleep(200);
            }

            if (handle == IntPtr.Zero)
            {
                return;
            }

            const int SwRestore = 9;
            NativeMethods.ShowWindowAsync(handle, SwRestore);
            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetForegroundWindow(handle);

        }
        catch
        {
            // 포커스 강제는 OS 정책에 따라 실패 가능 (best-effort)
        }
    }

    private static void TryFixedCoordinateClick(int x, int y)
    {
        try
        {
            NativeMethods.SetCursorPos(x, y);
            NativeMethods.mouse_event(0x0002 | 0x0004, 0, 0, 0, UIntPtr.Zero);
        }
        catch
        {
            // fallback 실패는 무시
        }
    }

    private static void RunHelperFocusProcess(Process process)
    {
        try
        {
            process.Refresh();
            var title = process.MainWindowTitle;
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var escaped = title.Replace("'", "''");
            var script = $"Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.Interaction]::AppActivate('{escaped}')";
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // helper focus process 실패는 무시
        }
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


    private bool IsPeerProblem(PeerConfig peer)
    {
        if (!_peerStates.TryGetValue(peer.Name, out var state))
        {
            return true;
        }

        var timeout = DateTime.UtcNow - state.LastHeartbeatUtc > TimeSpan.FromSeconds(GetAlertThresholdSeconds());
        return timeout || !state.IsHealthy;
    }

    private void AttemptRebootIfDue(PeerConfig peer)
    {
        var now = DateTime.UtcNow;
        if (_lastRebootAttemptUtc.TryGetValue(peer.Name, out var last) && now - last < TimeSpan.FromMinutes(2))
        {
            return;
        }

        try
        {
            _mainCommandSender ??= new UdpClient();
            var commandPacket = new HeartbeatPacket
            {
                PcName = _config.LocalMonitoring.PcName,
                Role = _config.Role,
                TimestampUtc = DateTime.UtcNow,
                IsHealthy = true,
                PacketType = "reboot",
                TargetPcName = peer.Name
            };

            var payload = JsonSerializer.Serialize(commandPacket, JsonOptions.Default);
            var bytes = Encoding.UTF8.GetBytes(payload);
            var endpoint = new IPEndPoint(IPAddress.Parse(peer.Ip), _config.Udp.ListenPort);
            _mainCommandSender.Send(bytes, bytes.Length, endpoint);

            _lastRebootAttemptUtc[peer.Name] = now;
            _rebootAttemptCounts[peer.Name] = GetRebootAttemptCount(peer.Name) + 1;
            AppendLog($"{peer.Name} 재부팅 신호 전송 ({peer.Ip}:{_config.Udp.ListenPort})");
        }
        catch (Exception ex)
        {
            _lastRebootAttemptUtc[peer.Name] = now;
            _rebootAttemptCounts[peer.Name] = GetRebootAttemptCount(peer.Name) + 1;
            AppendLog($"{peer.Name} 재부팅 신호 전송 실패: {ex.Message}");
        }
    }

    private int GetRebootAttemptCount(string peerName)
        => _rebootAttemptCounts.TryGetValue(peerName, out var count) ? count : 0;

    private bool ShouldShowWarning(PeerConfig peer)
        => GetRebootAttemptCount(peer.Name) >= Math.Max(1, _config.RebootAlertAttemptCount);

    private void ShowUnresolvedWarning(PeerConfig peer)
    {
        var message = $"{peer.Name}PC에 이상이 발생하여 재부팅을 시도했지만 해결되지 않았습니다. {peer.Name}PC를 점검해주세요.";
        AppendLog(message);
        ShowTopMostWarning(message, "모니터링 알림");
    }

    private static void ShowTopMostWarning(string message, string title)
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
    }

    private void HandleRebootCommand()
    {
        if (_rebootPending)
        {
            return;
        }

        _rebootPending = true;
        AppendLog("!!! 재부팅 신호 수신: 5초 후 재부팅합니다 !!!");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 0 /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                BeginInvoke(() => AppendLog($"재부팅 실행 실패: {ex.Message}"));
                _rebootPending = false;
            }
        }, _cts.Token);
    }

    private void MonitorPeersAndAlert()
    {
        var targetPeers = _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)).ToList();
        var hasPeerProblem = false;
        var mainLocalHealthy = IsMainLocalHealthy();

        foreach (var peer in targetPeers)
        {
            if (!IsPeerProblem(peer))
            {
                _alertedPeers.Remove(peer.Name);
                _rebootAttemptCounts.Remove(peer.Name);
                continue;
            }

            hasPeerProblem = true;
            AttemptRebootIfDue(peer);

            if (ShouldShowWarning(peer) && _alertedPeers.Add(peer.Name))
            {
                ShowUnresolvedWarning(peer);
            }
        }

        if (!mainLocalHealthy)
        {
            SetStatus("MainPC 로컬 프로세스 비정상");
        }
        else if (hasPeerProblem)
        {
            SetStatus("SubPC 점검 필요");
        }
        else
        {
            SetStatus("모든 모니터링 대상 정상");
        }

        RefreshGrid();
    }

    private void MonitorLocalSub()
    {
        var packet = BuildLocalHeartbeat();
        var allOk = packet.IsHealthy;
        SetStatus(allOk ? "SubPC 정상 동작" : "SubPC 프로세스 점검 필요");
    }

    private void InitializePeerGridForMain()
    {
        peerGrid.Rows.Clear();
        var targetPeers = _config.Peers.Where(p => !string.Equals(p.Role, "main", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var peer in targetPeers)
        {
            peerGrid.Rows.Add(peer.Name, peer.Role, "미수신", "-");
        }

        BuildPeerCheckIndicators(targetPeers);
    }

    private void BuildPeerCheckIndicators(List<PeerConfig> peers)
    {
        peerCheckStatePanel.Controls.Clear();
        _peerCheckLabels.Clear();

        foreach (var peer in peers)
        {
            var label = new Label
            {
                AutoSize = true,
                Text = $"{peer.Name}: ⏳ 확인 준비",
                Margin = new Padding(3)
            };

            peerCheckStatePanel.Controls.Add(label);
            _peerCheckLabels[peer.Name] = label;
        }
    }

    private void UpdatePeerCheckIndicators(List<PeerConfig> peers)
    {
        var spinnerFrames = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        var spinner = spinnerFrames[_spinnerFrame % spinnerFrames.Length];
        _spinnerFrame++;

        foreach (var peer in peers)
        {
            if (!_peerCheckLabels.TryGetValue(peer.Name, out var label))
            {
                continue;
            }

            label.Text = IsPeerHealthy(peer)
                ? $"{peer.Name}: ✅ 확인 완료"
                : $"{peer.Name}: {spinner} 확인중";
        }
    }

    private void MarkAllPeerChecksAsDone(List<PeerConfig> peers)
    {
        foreach (var peer in peers)
        {
            if (_peerCheckLabels.TryGetValue(peer.Name, out var label))
            {
                label.Text = $"{peer.Name}: ✅ 확인 완료";
            }
        }
    }

    private void MarkTimeoutPeerChecks(List<PeerConfig> peers)
    {
        foreach (var peer in peers)
        {
            if (!_peerCheckLabels.TryGetValue(peer.Name, out var label))
            {
                continue;
            }

            label.Text = IsPeerHealthy(peer)
                ? $"{peer.Name}: ✅ 확인 완료"
                : $"{peer.Name}: ⚠️ 확인 실패";
        }
    }

    private void InitializePeerGridForSub()
    {
        peerGrid.Rows.Clear();
        peerGrid.Rows.Add(_config.LocalMonitoring.PcName, _config.Role, "점검 중", DateTime.Now.ToString("HH:mm:ss"));
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
            if (!_peerStates.TryGetValue(pc, out var state))
            {
                row.Cells[2].Value = "미수신";
                row.Cells[3].Value = "-";
                continue;
            }

            if (DateTime.UtcNow - state.LastHeartbeatUtc > TimeSpan.FromSeconds(GetAlertThresholdSeconds()))
            {
                row.Cells[2].Value = "타임아웃";
                row.Cells[3].Value = state.LastHeartbeatUtc.ToLocalTime().ToString("HH:mm:ss");
                continue;
            }

            row.Cells[2].Value = state.IsHealthy ? "정상" : "비정상";
            row.Cells[3].Value = state.LastHeartbeatUtc.ToLocalTime().ToString("HH:mm:ss");
        }
    }

    private void RefreshSubGrid(HeartbeatPacket packet)
    {
        foreach (DataGridViewRow row in peerGrid.Rows)
        {
            row.Cells[2].Value = packet.IsHealthy ? "정상" : "비정상";
            row.Cells[3].Value = DateTime.Now.ToString("HH:mm:ss");
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

    private void MinimizeToTray()
    {
        if (IsDisposed)
        {
            return;
        }

        trayIcon.Visible = true;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Hide();
        trayIcon.ShowBalloonTip(1500, "LiveSkech Sensor Checker", "백그라운드(트레이)로 전환되었습니다.", ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        if (IsDisposed)
        {
            return;
        }

        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void trayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
    {
        RestoreFromTray();
    }

    private void trayRestoreMenuItem_Click(object sender, EventArgs e)
    {
        RestoreFromTray();
    }

    private void trayExitMenuItem_Click(object sender, EventArgs e)
    {
        _allowExitFromTray = true;
        trayIcon.Visible = false;
        Close();
    }

    private void SetStatus(string text)
    {
        statusLabel.Text = text;
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

        try
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }

            logTextBox.AppendText(line);
        }
        catch (ObjectDisposedException)
        {
            // 종료 중 컨트롤 dispose 상태일 수 있으므로 무시
        }
        catch (InvalidOperationException)
        {
            // 종료 타이밍의 핸들 상태 이슈는 무시
        }
    }
}

internal sealed class PeerRuntimeState(string name, string role)
{
    public string Name { get; } = name;
    public string Role { get; } = role;
    public DateTime LastHeartbeatUtc { get; private set; } = DateTime.MinValue;
    public bool IsHealthy { get; private set; }
    public DateTime? UnhealthySignalSinceUtc { get; private set; }

    public void Update(HeartbeatPacket packet)
    {
        LastHeartbeatUtc = packet.TimestampUtc;

        if (packet.IsHealthy)
        {
            IsHealthy = true;
            UnhealthySignalSinceUtc = null;
            return;
        }

        if (IsHealthy || UnhealthySignalSinceUtc is null)
        {
            UnhealthySignalSinceUtc = packet.TimestampUtc;
        }

        IsHealthy = false;
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);
}
