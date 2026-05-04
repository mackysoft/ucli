using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Resolves project-scope SKILL target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillHostRegistry hostRegistry;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostRegistry"> The supported host registry. </param>
    public SkillInstallTargetResolver (SkillHostRegistry? hostRegistry = null)
    {
        this.hostRegistry = hostRegistry ?? new SkillHostRegistry();
    }

    /// <summary> Resolves the target root for one install request. </summary>
    /// <param name="request"> The install request. </param>
    /// <returns> The canonical absolute target root or path-safety failure. </returns>
    public SkillOperationResult<string> ResolveTargetRoot (SkillInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Scope != SkillScopeKind.Project)
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Unsupported SKILL install scope: {request.Scope}");
        }

        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        var descriptorResult = hostRegistry.TryGetDescriptor(request.Host);
        if (!descriptorResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(descriptorResult.Failure!.Code, descriptorResult.Failure.Message);
        }

        var descriptor = descriptorResult.Value!;
        var targetRoot = string.IsNullOrWhiteSpace(request.TargetRoot)
            ? Path.Combine(repositoryRoot, descriptor.ProjectTargetDirectory)
            : Path.IsPathRooted(request.TargetRoot)
                ? request.TargetRoot
                : Path.Combine(repositoryRoot, request.TargetRoot);

        return SkillPathBoundary.ResolveUnderRoot(repositoryRoot, targetRoot);
    }
}
