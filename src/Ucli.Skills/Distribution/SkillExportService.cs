using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Installation;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Exports official SKILL packages to a host-materialized output directory. </summary>
public sealed class SkillExportService
{
    private readonly SkillMaterializationService materializationService;

    /// <summary> Initializes a new instance of the <see cref="SkillExportService" /> class. </summary>
    /// <param name="materializationService"> The materialization service. </param>
    public SkillExportService (SkillMaterializationService? materializationService = null)
    {
        this.materializationService = materializationService ?? new SkillMaterializationService();
    }

    /// <summary> Exports all packages into an output root. </summary>
    /// <param name="packages"> The canonical packages. </param>
    /// <param name="host"> The target host. </param>
    /// <param name="outputRoot"> The output root directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The output root or failure. </returns>
    public async ValueTask<SkillOperationResult<string>> ExportAsync (
        IReadOnlyList<CanonicalSkillPackage> packages,
        SkillHostKind host,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var fullOutputRoot = Path.GetFullPath(outputRoot);
        foreach (var package in packages.OrderBy(static package => package.SkillName, StringComparer.Ordinal))
        {
            var materializedResult = materializationService.Materialize(package, host);
            if (!materializedResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
            }

            var skillDirectory = Path.Combine(fullOutputRoot, package.SkillName);
            foreach (var file in materializedResult.Value!.Files)
            {
                var filePathResult = SkillPathBoundary.ResolvePackageFilePath(skillDirectory, file.RelativePath);
                if (!filePathResult.IsSuccess)
                {
                    return SkillOperationResult<string>.FailureResult(filePathResult.Failure!.Code, filePathResult.Failure.Message);
                }

                await FileUtilities.WriteAllTextAtomically(filePathResult.Value!, file.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        return SkillOperationResult<string>.Success(fullOutputRoot);
    }
}
