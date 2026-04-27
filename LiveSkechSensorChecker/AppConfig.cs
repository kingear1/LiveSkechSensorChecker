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
    public int? AlertThresholdSeconds { get; init; }
    public int PeerHeartbeatTimeoutSeconds { get; init; } = 10;
    public int RebootAlertAttemptCount { get; init; } = 3;

    public static string GetConfigPath(string basePath) => Path.Combine(basePath, "config.json");

    public static AppConfig Load(string basePath)
    {
        var path = GetConfigPath(basePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("config.json 파일을 찾을 수 없습니다.", path);
        }

        var json = File.ReadAllText(path);
        return Parse(json);
    }

    public static AppConfig Parse(string json)
    {
        var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions.Default);
        if (config is null)
        {
            throw new InvalidOperationException("config.json 파싱에 실패했습니다.");
        }

        return config;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions.Default);

    public void Save(string path) => File.WriteAllText(path, ToJson());
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
    public bool ForceCenterClickFallback { get; init; } = false;
    public int CenterClickDelaySeconds { get; init; } = 30;
    public int CenterClickX { get; init; } = 500;
    public int CenterClickY { get; init; } = 500;
    public bool EnableHelperFocusProcess { get; init; } = false;
    public int HelperFocusDelaySeconds { get; init; } = 3;
}

internal sealed class HeartbeatPacket
{
    public required string PcName { get; init; }
    public required string Role { get; init; }
    public DateTime TimestampUtc { get; init; }
    public bool IsHealthy { get; init; }
    public string PacketType { get; init; } = "heartbeat";
    public string? TargetPcName { get; init; }
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
