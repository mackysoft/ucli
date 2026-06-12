namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the Unity BuildPipeline inputs resolved by the build precondition probe. </summary>
/// <param name="TargetStableName"> The uCLI build target stable name from the resolved build profile. </param>
/// <param name="UnityBuildTarget"> The Unity <c>BuildTarget</c> enum literal. </param>
/// <param name="UnityBuildTargetGroup"> The Unity <c>BuildTargetGroup</c> enum literal. </param>
/// <param name="SceneSource"> The resolved scene source literal. </param>
/// <param name="Scenes"> The project-relative scene paths passed to BuildPipeline. </param>
/// <param name="BuildOptions"> The Unity <c>BuildOptions</c> flags literal. </param>
public sealed record IpcBuildInputProbe (
    string TargetStableName,
    string UnityBuildTarget,
    string UnityBuildTargetGroup,
    string SceneSource,
    IReadOnlyList<string> Scenes,
    string BuildOptions);
