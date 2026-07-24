using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the Unity BuildPipeline inputs resolved by the build precondition probe. </summary>
public sealed record IpcBuildInputProbe
{
    /// <summary> Initializes one resolved BuildPipeline input snapshot. </summary>
    /// <param name="InputKind"> The resolved build input kind. </param>
    /// <param name="BuildTarget"> The stable build target name. </param>
    /// <param name="UnityBuildTarget"> The Unity build target literal. </param>
    /// <param name="UnityBuildTargetGroup"> The Unity build target group literal. </param>
    /// <param name="SceneSource"> The source from which the scene list was resolved. </param>
    /// <param name="Scenes"> The validated scene asset paths resolved for the build. </param>
    /// <param name="BuildOptions"> The Unity build option flags. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Scenes" /> or a required string is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="Scenes" /> contains a <see langword="null" /> item or a required string is empty. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when an enum value is undefined. </exception>
    [JsonConstructor]
    public IpcBuildInputProbe (
        BuildProfileInputsKind InputKind,
        BuildTargetStableName BuildTarget,
        string UnityBuildTarget,
        string UnityBuildTargetGroup,
        BuildProfileSceneSource SceneSource,
        IReadOnlyList<SceneAssetPath> Scenes,
        string BuildOptions)
    {
        if (!TextVocabulary.IsDefined(InputKind))
        {
            throw new ArgumentOutOfRangeException(nameof(InputKind), InputKind, "Build input kind must be specified.");
        }

        if (!TextVocabulary.IsDefined(SceneSource))
        {
            throw new ArgumentOutOfRangeException(nameof(SceneSource), SceneSource, "Build scene source must be specified.");
        }

        if (!TextVocabulary.IsDefined(BuildTarget))
        {
            throw new ArgumentOutOfRangeException(nameof(BuildTarget), BuildTarget, "Build target must be specified.");
        }

        this.InputKind = InputKind;
        this.BuildTarget = BuildTarget;
        this.UnityBuildTarget = ContractArgumentGuard.RequireValue(UnityBuildTarget, nameof(UnityBuildTarget));
        this.UnityBuildTargetGroup = ContractArgumentGuard.RequireValue(UnityBuildTargetGroup, nameof(UnityBuildTargetGroup));
        this.SceneSource = SceneSource;
        this.Scenes = ContractArgumentGuard.RequireItems(Scenes, nameof(Scenes));
        this.BuildOptions = ContractArgumentGuard.RequireValue(BuildOptions, nameof(BuildOptions));
    }

    public BuildProfileInputsKind InputKind { get; }

    public BuildTargetStableName BuildTarget { get; }

    public string UnityBuildTarget { get; }

    public string UnityBuildTargetGroup { get; }

    public BuildProfileSceneSource SceneSource { get; }

    /// <summary> Gets the validated, read-only scene asset paths resolved for the build. </summary>
    public IReadOnlyList<SceneAssetPath> Scenes { get; }

    public string BuildOptions { get; }
}
