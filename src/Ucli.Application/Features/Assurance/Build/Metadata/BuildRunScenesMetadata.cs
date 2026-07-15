using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents resolved build scene input persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunScenesMetadata (
    BuildProfileSceneSource Source,
    IReadOnlyList<SceneAssetPath> Paths);
