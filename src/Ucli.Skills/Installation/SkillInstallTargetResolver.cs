using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Resolves project-scope SKILL target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillInstallTargetResolver (SkillHostAdapterSet hostAdapters)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
    }

    /// <summary> Resolves the target root for one install request. </summary>
    /// <param name="request"> The install request. </param>
    /// <returns> The canonical host target or path-safety failure. </returns>
    public SkillOperationResult<SkillResolvedInstallTarget> ResolveTarget (SkillInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Scope != SkillScopeKind.Project)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Unsupported SKILL install scope: {request.Scope}");
        }

        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        var adapterResult = hostAdapters.GetAdapter(request.Host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
        }

        var descriptor = adapterResult.Value!.Descriptor;
        var targetRoot = string.IsNullOrWhiteSpace(request.TargetRoot)
            ? Path.Combine(repositoryRoot, descriptor.ProjectTargetDirectory)
            : Path.IsPathRooted(request.TargetRoot)
                ? request.TargetRoot
                : Path.Combine(repositoryRoot, request.TargetRoot);

        var resolvedTargetRoot = SkillPackagePathBoundary.ResolveUnderRoot(repositoryRoot, targetRoot);
        return resolvedTargetRoot.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(descriptor.HostKey, resolvedTargetRoot.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(resolvedTargetRoot.Failure!.Code, resolvedTargetRoot.Failure.Message);
    }
}
