using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Resolves project-scope SKILL target roots. </summary>
public sealed class SkillInstallTargetResolver
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillUserTargetRootResolver userTargetRootResolver;

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillInstallTargetResolver (SkillHostAdapterSet hostAdapters) : this(hostAdapters, new SkillUserTargetRootResolver())
    {
    }

    /// <summary> Initializes a new instance of the <see cref="SkillInstallTargetResolver" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="userTargetRootResolver"> The user-scope target root resolver. </param>
    public SkillInstallTargetResolver (
        SkillHostAdapterSet hostAdapters,
        SkillUserTargetRootResolver userTargetRootResolver)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.userTargetRootResolver = userTargetRootResolver ?? throw new ArgumentNullException(nameof(userTargetRootResolver));
    }

    /// <summary> Resolves the target root for one install request. </summary>
    /// <param name="request"> The install request. </param>
    /// <returns> The canonical host target or path-safety failure. </returns>
    public SkillOperationResult<SkillResolvedInstallTarget> ResolveTarget (SkillInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var adapterResult = hostAdapters.GetAdapter(request.Host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(adapterResult.Failure!.Code, adapterResult.Failure.Message);
        }

        var descriptor = adapterResult.Value!.Descriptor;
        return request.Scope switch
        {
            SkillScopeKind.Project => ResolveProjectTarget(request, descriptor.HostKey, descriptor.ProjectTargetDirectory),
            SkillScopeKind.User => ResolveUserTarget(request, descriptor),
            _ => SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                $"Unsupported SKILL install scope: {request.Scope}"),
        };
    }

    private static SkillOperationResult<SkillResolvedInstallTarget> ResolveProjectTarget (
        SkillInstallRequest request,
        string host,
        string projectTargetDirectory)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                "Project-scope SKILL install requires a repository root.");
        }

        var repositoryRoot = Path.GetFullPath(request.RepositoryRoot);
        var targetRoot = string.IsNullOrWhiteSpace(request.TargetRoot)
            ? Path.Combine(repositoryRoot, projectTargetDirectory)
            : Path.IsPathRooted(request.TargetRoot)
                ? request.TargetRoot
                : Path.Combine(repositoryRoot, request.TargetRoot);

        var resolvedTargetRoot = SkillPackagePathBoundary.ResolveUnderRoot(repositoryRoot, targetRoot);
        return resolvedTargetRoot.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(host, resolvedTargetRoot.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(resolvedTargetRoot.Failure!.Code, resolvedTargetRoot.Failure.Message);
    }

    private SkillOperationResult<SkillResolvedInstallTarget> ResolveUserTarget (
        SkillInstallRequest request,
        SkillHostDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(request.RepositoryRoot))
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                SkillFailureCodes.PathUnsafe,
                "User-scope SKILL install must not use a repository root.");
        }

        if (!string.IsNullOrWhiteSpace(request.TargetRoot))
        {
            if (!Path.IsPathRooted(request.TargetRoot))
            {
                return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    "User-scope SKILL targetDir must be an absolute path.");
            }

            var fullTargetRoot = Path.GetFullPath(request.TargetRoot);
            var explicitTargetResult = SkillPackagePathBoundary.ResolveUnderRoot(fullTargetRoot, fullTargetRoot);
            return explicitTargetResult.IsSuccess
                ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(descriptor.HostKey, explicitTargetResult.Value!))
                : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(explicitTargetResult.Failure!.Code, explicitTargetResult.Failure.Message);
        }

        var defaultTargetResult = userTargetRootResolver.ResolveDefaultTargetRoot(descriptor);
        if (!defaultTargetResult.IsSuccess)
        {
            return SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(defaultTargetResult.Failure!.Code, defaultTargetResult.Failure.Message);
        }

        var targetRoot = defaultTargetResult.Value!;
        var resolvedTargetRoot = SkillPackagePathBoundary.ResolveUnderRoot(targetRoot, targetRoot);
        return resolvedTargetRoot.IsSuccess
            ? SkillOperationResult<SkillResolvedInstallTarget>.Success(new SkillResolvedInstallTarget(descriptor.HostKey, resolvedTargetRoot.Value!))
            : SkillOperationResult<SkillResolvedInstallTarget>.FailureResult(resolvedTargetRoot.Failure!.Code, resolvedTargetRoot.Failure.Message);
    }
}
