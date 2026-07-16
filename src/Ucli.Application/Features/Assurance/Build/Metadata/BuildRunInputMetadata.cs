using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved BuildPipeline input persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunInputMetadata (
    BuildProfileInputsKind InputKind,
    BuildRunTargetMetadata Target,
    BuildRunScenesMetadata Scenes,
    BuildRunOptionsMetadata Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildRunUnityBuildProfileInputMetadata? UnityBuildProfile);
