using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build target identity. </summary>
internal sealed record BuildTargetOutput (
    BuildTargetStableName StableName,
    string UnityBuildTarget);
