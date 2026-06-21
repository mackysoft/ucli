namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build target identity. </summary>
internal sealed record BuildTargetOutput (
    string StableName,
    string UnityBuildTarget);
