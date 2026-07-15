using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved build target identity persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunTargetMetadata (
    BuildTargetStableName StableName,
    string UnityBuildTarget);
