using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Provides official SKILL packages generated from bundled source definitions. </summary>
public sealed class OfficialSkillPackageProvider
{
    private readonly BundledSkillDefinitionRootResolver definitionRootResolver;
    private readonly SkillPackageGenerationService generationService;

    /// <summary> Initializes a new instance of the <see cref="OfficialSkillPackageProvider" /> class. </summary>
    /// <param name="definitionRootResolver"> The bundled SKILL definition root resolver. </param>
    /// <param name="generationService"> The canonical package generation service. </param>
    public OfficialSkillPackageProvider (
        BundledSkillDefinitionRootResolver definitionRootResolver,
        SkillPackageGenerationService generationService)
    {
        this.definitionRootResolver = definitionRootResolver ?? throw new ArgumentNullException(nameof(definitionRootResolver));
        this.generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));
    }

    /// <summary> Gets all official SKILL packages from the bundled source definitions. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The official canonical packages, or a source-resolution failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GetPackagesAsync (
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string definitionsRoot;
        try
        {
            definitionsRoot = definitionRootResolver.Resolve();
        }
        catch (DirectoryNotFoundException ex)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                SkillFailureCodes.SourceInvalid,
                ex.Message);
        }

        return await generationService.GenerateAllAsync(definitionsRoot, cancellationToken).ConfigureAwait(false);
    }
}
