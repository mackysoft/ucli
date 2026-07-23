using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents validated inputs needed to account non-metadata build-run artifacts. </summary>
internal sealed class BuildRunArtifactAccountingRequest
{
    /// <summary> Initializes validated build-run artifact accounting inputs. </summary>
    public BuildRunArtifactAccountingRequest (
        BuildRunArtifactPaths paths,
        BuildTargetStableName buildTarget,
        string unityBuildTarget,
        BuildReportSourceEntry? buildReport,
        IReadOnlyList<BuildOutputSourceEntry> outputSources,
        bool allowEmptyOutputManifest)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        if (!TextVocabulary.IsDefined(buildTarget))
        {
            throw new ArgumentOutOfRangeException(nameof(buildTarget), buildTarget, "Build target must be specified.");
        }

        BuildTarget = buildTarget;
        UnityBuildTarget = string.IsNullOrWhiteSpace(unityBuildTarget)
            ? throw new ArgumentException("Unity build target must not be empty.", nameof(unityBuildTarget))
            : unityBuildTarget;
        BuildReport = buildReport;

        ArgumentNullException.ThrowIfNull(outputSources);
        var sourceSnapshot = new BuildOutputSourceEntry[outputSources.Count];
        for (var index = 0; index < outputSources.Count; index++)
        {
            sourceSnapshot[index] = outputSources[index]
                ?? throw new ArgumentException($"Output source at index {index} must not be null.", nameof(outputSources));
        }

        OutputSources = sourceSnapshot.Length == 0
            ? Array.Empty<BuildOutputSourceEntry>()
            : Array.AsReadOnly(sourceSnapshot);
        AllowEmptyOutputManifest = allowEmptyOutputManifest;
    }

    /// <summary> Gets the prepared artifact layout. </summary>
    public BuildRunArtifactPaths Paths { get; }

    /// <summary> Gets the resolved build target stable name. </summary>
    public BuildTargetStableName BuildTarget { get; }

    /// <summary> Gets the Unity <c>BuildTarget</c> enum member name. </summary>
    public string UnityBuildTarget { get; }

    /// <summary> Gets the BuildReport source, or <see langword="null" /> when none was produced. </summary>
    public BuildReportSourceEntry? BuildReport { get; }

    /// <summary> Gets the stable output source snapshot. </summary>
    public IReadOnlyList<BuildOutputSourceEntry> OutputSources { get; }

    /// <summary> Gets a value indicating whether no existing output sources may produce a valid empty manifest. </summary>
    public bool AllowEmptyOutputManifest { get; }
}
