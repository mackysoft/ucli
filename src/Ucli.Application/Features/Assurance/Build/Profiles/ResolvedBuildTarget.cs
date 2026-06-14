namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents one resolved buildTarget stable name. </summary>
internal sealed record ResolvedBuildTarget (
    BuildTargetStableName StableNameValue,
    string StableName,
    string UnityBuildTargetLiteral);
