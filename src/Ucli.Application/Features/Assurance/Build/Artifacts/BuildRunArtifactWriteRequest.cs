namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Represents all inputs needed to persist completed build-run artifacts. </summary>
/// <param name="Paths"> The prepared artifact layout. </param>
/// <param name="Metadata"> The metadata document fields persisted into <c>build.json</c>. </param>
/// <param name="BuildReportJson"> The serialized Unity BuildReport JSON. </param>
/// <param name="BuildLogText"> The build log text. </param>
/// <param name="TargetStableName"> The resolved build target stable name. </param>
internal sealed record BuildRunArtifactWriteRequest (
    BuildRunArtifactPaths Paths,
    BuildRunMetadataDocument Metadata,
    string BuildReportJson,
    string BuildLogText,
    string TargetStableName);
