using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Materialization;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Classifies local drift in one installed SKILL package for doctor diagnostics. </summary>
public sealed class SkillInstalledPackageDriftAnalyzer
{
    private const string SkillBodyPath = "SKILL.md";
    private const string ManifestPath = "ucli-skill.json";
    private const string ReferencesPrefix = "references/";

    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillMaterializationService materializationService;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageDriftAnalyzer" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="materializationService"> The materialization service. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledPackageDriftAnalyzer (
        SkillInstalledManifestReader installedManifestReader,
        SkillMaterializationService materializationService,
        SkillDigestCalculator digestCalculator)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.materializationService = materializationService ?? throw new ArgumentNullException(nameof(materializationService));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Classifies local drift for one installed skill directory. </summary>
    /// <param name="package"> The current canonical package. </param>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by doctor execution. </param>
    /// <returns> The primary drift category or a read/manifest failure. </returns>
    public async ValueTask<SkillOperationResult<SkillInstalledPackageDrift>> AnalyzeAsync (
        CanonicalSkillPackage package,
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        var manifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!manifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(manifestResult.Failure!.Code, manifestResult.Failure.Message);
        }

        var installedManifest = manifestResult.Value!.Manifest;
        var hostArtifact = installedManifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        if (hostArtifact is null)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest does not contain host artifact '{host}'.");
        }

        var frontmatterResult = await ReadInstalledFrontmatterAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!frontmatterResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(frontmatterResult.Failure!.Code, frontmatterResult.Failure.Message);
        }

        if (frontmatterResult.Value!.Length == 0)
        {
            return Drift(
                SkillFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter is missing or invalid: {installedManifest.SkillName}");
        }

        var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", frontmatterResult.Value);
        if (!string.Equals(frontmatterDigest, hostArtifact.MaterializedFrontmatterDigest, StringComparison.Ordinal))
        {
            return Drift(
                SkillFailureCodes.InstallTargetFrontmatterDigestMismatch,
                $"Installed SKILL frontmatter digest does not match manifest: {installedManifest.SkillName}");
        }

        var hostArtifactResult = await MatchesHostArtifactAsync(skillDirectory, hostArtifact, cancellationToken).ConfigureAwait(false);
        if (!hostArtifactResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(hostArtifactResult.Failure!.Code, hostArtifactResult.Failure.Message);
        }

        if (!hostArtifactResult.Value)
        {
            return Drift(
                SkillFailureCodes.InstallTargetHostArtifactDigestMismatch,
                $"Installed SKILL host artifact digest does not match manifest: {installedManifest.SkillName}");
        }

        var materializedResult = materializationService.Materialize(package, host);
        if (!materializedResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(materializedResult.Failure!.Code, materializedResult.Failure.Message);
        }

        var expectedPaths = materializedResult.Value!.Files
            .Select(static file => file.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        var installedPathsResult = ReadInstalledFilePaths(skillDirectory);
        if (!installedPathsResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(installedPathsResult.Failure!.Code, installedPathsResult.Failure.Message);
        }

        if (!expectedPaths.SetEquals(installedPathsResult.Value!))
        {
            return Drift(
                SkillFailureCodes.InstallTargetFileSetMismatch,
                $"Installed SKILL file set does not match materialized package: {installedManifest.SkillName}");
        }

        var contentDigestResult = await ComputeInstalledContentDigestAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!contentDigestResult.IsSuccess)
        {
            return SkillOperationResult<SkillInstalledPackageDrift>.FailureResult(contentDigestResult.Failure!.Code, contentDigestResult.Failure.Message);
        }

        if (!string.Equals(contentDigestResult.Value, installedManifest.ContentDigest, StringComparison.Ordinal))
        {
            return Drift(
                SkillFailureCodes.InstallTargetContentDigestMismatch,
                $"Installed SKILL contentDigest does not match manifest: {installedManifest.SkillName}");
        }

        return Drift(
            SkillFailureCodes.InstallTargetDigestMismatch,
            $"Installed SKILL package differs from canonical package: {installedManifest.SkillName}");
    }

    private async ValueTask<SkillOperationResult<string>> ReadInstalledFrontmatterAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<string>.Success(string.Empty);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        return SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter)
            ? SkillOperationResult<string>.Success(frontmatter)
            : SkillOperationResult<string>.Success(string.Empty);
    }

    private async ValueTask<SkillOperationResult<bool>> MatchesHostArtifactAsync (
        string skillDirectory,
        SkillHostArtifactManifest hostArtifact,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostArtifact.Path))
        {
            return SkillOperationResult<bool>.Success(true);
        }

        if (string.IsNullOrWhiteSpace(hostArtifact.Digest))
        {
            return SkillOperationResult<bool>.FailureResult(
                SkillFailureCodes.ManifestInvalid,
                $"Manifest host artifact '{hostArtifact.Host}' is missing a digest.");
        }

        var artifactPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, hostArtifact.Path);
        if (!artifactPathResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(artifactPathResult.Failure!.Code, artifactPathResult.Failure.Message);
        }

        if (!File.Exists(artifactPathResult.Value!))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(artifactPathResult.Value!, cancellationToken).ConfigureAwait(false));
        var digest = digestCalculator.ComputeSingleFileDigest(hostArtifact.Path, content);
        return SkillOperationResult<bool>.Success(string.Equals(digest, hostArtifact.Digest, StringComparison.Ordinal));
    }

    private static SkillOperationResult<HashSet<string>> ReadInstalledFilePaths (string skillDirectory)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return SkillOperationResult<HashSet<string>>.FailureResult(resolvedPathResult.Failure!.Code, resolvedPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, resolvedPathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            paths.Add(relativePath);
        }

        return SkillOperationResult<HashSet<string>>.Success(paths);
    }

    private async ValueTask<SkillOperationResult<string>> ComputeInstalledContentDigestAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(skillBodyResult.Failure!.Code, skillBodyResult.Failure.Message);
        }

        if (skillBodyResult.Value!.Length == 0)
        {
            return SkillOperationResult<string>.Success(string.Empty);
        }

        var digestInputs = new List<SkillDigestInputFile>
        {
            new(SkillBodyPath, skillBodyResult.Value),
        };

        foreach (var referencePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, referencePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return SkillOperationResult<string>.FailureResult(resolvedPathResult.Failure!.Code, resolvedPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, resolvedPathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            if (!relativePath.StartsWith(ReferencesPrefix, StringComparison.Ordinal)
                || string.Equals(relativePath, ManifestPath, StringComparison.Ordinal)
                || string.Equals(relativePath, SkillBodyPath, StringComparison.Ordinal))
            {
                continue;
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new SkillDigestInputFile(relativePath, content));
        }

        return SkillOperationResult<string>.Success(digestCalculator.ComputeDigest(digestInputs));
    }

    private static async ValueTask<SkillOperationResult<string>> ReadInstalledSkillBodyAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<string>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<string>.Success(string.Empty);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<string>.Success(string.Empty);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return SkillOperationResult<string>.Success(body);
    }

    private static SkillOperationResult<SkillInstalledPackageDrift> Drift (
        SkillFailureCode code,
        string message)
    {
        return SkillOperationResult<SkillInstalledPackageDrift>.Success(new SkillInstalledPackageDrift(code, message));
    }
}
