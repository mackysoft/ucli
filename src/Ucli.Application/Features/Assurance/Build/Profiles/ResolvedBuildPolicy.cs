using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build policy resolved from a build profile. </summary>
internal sealed class ResolvedBuildPolicy
{
    /// <summary> Initializes a resolved build policy. </summary>
    public ResolvedBuildPolicy (
        ResolvedBuildRuntimePolicy runtime,
        BuildProfileProjectMutationMode projectMutationMode)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        if (!ContractLiteralCodec.IsDefined(projectMutationMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectMutationMode),
                projectMutationMode,
                "Project mutation mode must be defined.");
        }

        ProjectMutationMode = projectMutationMode;
    }

    /// <summary> Gets the allowed runtime modes. </summary>
    public ResolvedBuildRuntimePolicy Runtime { get; }

    /// <summary> Gets the project mutation mode. </summary>
    public BuildProfileProjectMutationMode ProjectMutationMode { get; }
}
