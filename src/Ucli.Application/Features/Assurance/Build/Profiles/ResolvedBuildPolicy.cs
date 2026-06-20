using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build policy resolved from a build profile. </summary>
internal sealed record ResolvedBuildPolicy (
    ResolvedBuildRuntimePolicy Runtime,
    BuildProfileProjectMutationMode ProjectMutationMode);
