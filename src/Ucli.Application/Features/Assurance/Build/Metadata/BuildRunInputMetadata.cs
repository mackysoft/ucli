using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved BuildPipeline input persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunInputMetadata (
    string InputKind,
    BuildTargetOutput Target,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options);
