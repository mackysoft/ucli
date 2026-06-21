namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved build target identity persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunTargetMetadata (
    string StableName,
    string UnityBuildTarget);
