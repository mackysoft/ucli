using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved BuildPipeline input persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunInputMetadata (
    string InputKind,
    string BuildTarget,
    string UnityBuildTarget,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildRunUnityBuildProfileInputMetadata? UnityBuildProfile);
