namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents one resolved build target stable name. </summary>
internal sealed record ResolvedBuildTarget (
    BuildTargetStableName StableNameValue,
    string StableName,
    string UnityBuildTargetLiteral);
