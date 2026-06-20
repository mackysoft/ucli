using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC request payload. </summary>
/// <param name="RunId"> The CLI-generated build run identifier. </param>
/// <param name="BuildTarget"> The uCLI buildTarget stable name from the resolved build profile. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum literal. </param>
/// <param name="SceneSource"> The resolved scene source literal. </param>
/// <param name="ScenePaths"> The explicit scene paths, or an empty list when scene source is Editor Build Settings. </param>
/// <param name="Development"> Whether the development build option is enabled. </param>
/// <param name="OutputPath"> The absolute runner working output root. </param>
/// <param name="OutputLayout"> The command-derived BuildPipeline output layout, or <see langword="null" /> for executeMethod runners. </param>
/// <param name="BuildReportPath"> The absolute path where Unity writes the normalized BuildReport artifact. </param>
/// <param name="BuildLogPath"> The absolute path where Unity writes the build log artifact. </param>
/// <param name="AllowedEditorModes"> The editor mode literals allowed by the resolved build profile runtime policy. </param>
/// <param name="ProjectMutationMode"> The project mutation mode literal from the resolved build profile. </param>
public sealed record IpcBuildRunRequest (
    string RunId,
    string BuildTarget,
    string UnityBuildTarget,
    string SceneSource,
    IReadOnlyList<string> ScenePaths,
    bool Development,
    string OutputPath,
    IpcBuildOutputLayout? OutputLayout,
    string BuildReportPath,
    string BuildLogPath,
    IReadOnlyList<string> AllowedEditorModes,
    string ProjectMutationMode)
{
    /// <summary> Gets the resolved build runner kind literal. </summary>
    public string RunnerKind { get; init; } = "buildPipeline";

    /// <summary> Gets the resolved build profile path used for runner context construction. </summary>
    public string? ProfilePath { get; init; }

    /// <summary> Gets the canonical build profile digest used for runner context construction. </summary>
    public string? ProfileDigest { get; init; }

    /// <summary> Gets the resolved executeMethod runner method identity. </summary>
    public string? RunnerMethod { get; init; }

    /// <summary> Gets the substitution-resolved non-secret runner arguments. </summary>
    public IReadOnlyDictionary<string, string> RunnerArguments { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary> Gets the requested runner environment entry names. </summary>
    public IReadOnlyList<string> RunnerEnvironment { get; init; } = Array.Empty<string>();

    /// <summary> Gets the runner environment values resolved by the uCLI runtime for IPC delivery only. </summary>
    public IReadOnlyDictionary<string, string> RunnerEnvironmentValues { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary> Gets the request timeout budget propagated by the caller, in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
