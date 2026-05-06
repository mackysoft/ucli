using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Provides official SKILL packages from bundled generated package files. </summary>
public sealed class OfficialSkillPackageProvider
{
    private readonly BundledSkillPackageRootResolver packageRootResolver;
    private readonly CanonicalSkillPackageReader packageReader;

    /// <summary> Initializes a new instance of the <see cref="OfficialSkillPackageProvider" /> class. </summary>
    /// <param name="packageRootResolver"> The bundled generated SKILL package root resolver. </param>
    /// <param name="packageReader"> The canonical package reader. </param>
    public OfficialSkillPackageProvider (
        BundledSkillPackageRootResolver packageRootResolver,
        CanonicalSkillPackageReader packageReader)
    {
        this.packageRootResolver = packageRootResolver ?? throw new ArgumentNullException(nameof(packageRootResolver));
        this.packageReader = packageReader ?? throw new ArgumentNullException(nameof(packageReader));
    }

    /// <summary> Gets all official SKILL packages from bundled generated package files. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The official canonical packages, or a package-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string packageRoot;
        try
        {
            packageRoot = packageRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                ex.Message);
        }

        return await packageReader.ReadAllAsync(packageRoot, cancellationToken).ConfigureAwait(false);
    }
}
