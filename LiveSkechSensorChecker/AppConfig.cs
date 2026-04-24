using System.Text.Json;

namespace LiveSkechSensorChecker;

internal sealed class AppConfig
{
    public required string Role { get; init; }
    public required UdpConfig Udp { get; init; }
    public required List<PeerConfig> Peers { get; init; }
    public required LocalMonitoringConfig LocalMonitoring { get; init; }
    public MainBehaviorConfig? MainBehavior { get; init; }
    public int HeartbeatIntervalSeconds { get; init; } = 2;
    public int AlertThresholdSeconds { get; init; } = 10;

    public static AppConfig Load(string basePath)
    {
        var path = Path.Combine(basePath, "config.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("config.json 파일을 찾을 수 없습니다.", path);
        }

        var json = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);
        if (config is null)
        {
            throw new InvalidOperationException("config.json 파싱에 실패했습니다.");
        }

        return config;
    }
}

internal sealed class UdpConfig
{
    public int ListenPort { get; init; }
    public string? MainIp { get; init; }
    public int MainPort { get; init; }
}

internal sealed class PeerConfig
{
    public required string Name { get; init; }
    public required string Ip { get; init; }
    public required string Role { get; init; }
    public required List<string> Processes { get; init; }
}

internal sealed class LocalMonitoringConfig
{
    public required string PcName { get; init; }
    public required List<string> Processes { get; init; }
}

internal sealed class MainBehaviorConfig
{
    public required string LaunchOnAllHealthyPath { get; init; }
    public string? LaunchArguments { get; init; }
    public int InitialCheckTimeoutSeconds { get; init; } = 20;
}

internal sealed class HeartbeatPacket
{
    public required string PcName { get; init; }
    public required string Role { get; init; }
    public DateTime TimestampUtc { get; init; }
    public required List<ProcessState> Processes { get; init; }
}

internal sealed class ProcessState
{
    public required string ProcessName { get; init; }
    public bool IsRunning { get; init; }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}
