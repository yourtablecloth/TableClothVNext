using System.Text.Json.Serialization;

namespace TableCloth3.Launcher.Json;

// Models for MCP configuration
public sealed record McpConfigRoot
{
    public Dictionary<string, McpServerConfig> McpServers { get; init; } = new();
}

public sealed record McpServerConfig
{
    public string Command { get; init; } = string.Empty;
    public string[] Args { get; init; } = [];
    public Dictionary<string, string> Env { get; init; } = new();
}

[JsonSerializable(typeof(McpConfigRoot))]
[JsonSerializable(typeof(McpServerConfig))]
[JsonSerializable(typeof(Dictionary<string, McpServerConfig>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
public partial class McpConfigJsonSerializerContext : JsonSerializerContext
{
}