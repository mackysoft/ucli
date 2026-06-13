using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>build.run</c> IPC request payload. </summary>
/// <param name="RunId"> The CLI-generated build run identifier. </param>
/// <param name="TargetStableName"> The uCLI build target stable name from the resolved build profile. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum literal. </param>
/// <param name="SceneSource"> The resolved scene source literal. </param>
/// <param name="ScenePaths"> The explicit scene paths, or an empty list when scene source is Editor Build Settings. </param>
/// <param name="Development"> Whether the development build option is enabled. </param>
/// <param name="OutputPath"> The absolute output path passed to Unity BuildPipeline. </param>
/// <param name="BuildReportPath"> The absolute path where Unity writes the normalized BuildReport artifact. </param>
/// <param name="BuildLogPath"> The absolute path where Unity writes the build log artifact. </param>
public sealed record IpcBuildRunRequest (
    string RunId,
    string TargetStableName,
    string UnityBuildTarget,
    string SceneSource,
    IReadOnlyList<string> ScenePaths,
    bool Development,
    string OutputPath,
    string BuildReportPath,
    string BuildLogPath)
{
    /// <summary> Gets the request timeout budget propagated by the caller, in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
