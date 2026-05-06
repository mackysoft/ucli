using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Distribution;

/// <summary> Reads generated canonical SKILL packages from a <c>skills</c> directory. </summary>
public sealed class CanonicalSkillPackageReader
{
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillManifestValidator manifestValidator;

    /// <summary> Initializes a new instance of the <see cref="CanonicalSkillPackageReader" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="manifestValidator"> The manifest validator. </param>
    public CanonicalSkillPackageReader (
        SkillHostAdapterSet hostAdapters,
        SkillDigestCalculator digestCalculator,
        SkillManifestJsonSerializer manifestSerializer,
        SkillManifestValidator manifestValidator)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.manifestValidator = manifestValidator ?? throw new ArgumentNullException(nameof(manifestValidator));
    }

    /// <summary> Reads all generated canonical SKILL packages under a package root. </summary>
    /// <param name="packageRoot"> The generated <c>skills</c> directory. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> ReadAllAsync (
        string packageRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageRoot);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(packageRoot))
        {
            return Failure($"Generated skills directory does not exist: {packageRoot}");
        }

        var fullPackageRoot = Path.GetFullPath(packageRoot);
        var packages = new List<CanonicalSkillPackage>();
        foreach (var skillDirectory in Directory.GetDirectories(fullPackageRoot).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ReadOneAsync(fullPackageRoot, skillDirectory, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                    result.Failure!.Code,
                    result.Failure.Message);
            }

            packages.Add(result.Value!);
        }

        if (packages.Count == 0)
        {
            return Failure($"Generated skills directory does not contain any packages: {fullPackageRoot}");
        }

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages
            .OrderBy(static package => package.Manifest.SkillName, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<SkillOperationResult<CanonicalSkillPackage>> ReadOneAsync (
        string packageRoot,
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var directoryResult = SkillPackagePathBoundary.ResolveUnderRoot(packageRoot, skillDirectory);
        if (!directoryResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillPackage>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
        }

        var filesResult = await ReadFilesAsync(directoryResult.Value!, cancellationToken).ConfigureAwait(false);
        if (!filesResult.IsSuccess)
        {
            return SkillOperationResult<CanonicalSkillPackage>.FailureResult(filesResult.Failure!.Code, filesResult.Failure.Message);
        }

        var files = filesResult.Value!;
        var manifestFile = files.SingleOrDefault(static file => string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal));
        if (manifestFile is null)
        {
            return PackageFailure("Generated SKILL package is missing ucli-skill.json.");
        }

        var manifestResult = manifestSerializer.TryDeserialize(manifestFile.Content);
        if (!manifestResult.IsSuccess)
        {
            return PackageFailure(manifestResult.Failure!.Message);
        }

        var manifest = manifestResult.Value!;
        var validationResult = manifestValidator.Validate(manifest);
        if (!validationResult.IsSuccess)
        {
            return PackageFailure(validationResult.Failure!.Message);
        }

        if (!string.Equals(Path.GetFileName(directoryResult.Value!), manifest.SkillName, StringComparison.Ordinal))
        {
            return PackageFailure($"ucli-skill.json skillName must match generated package directory name: {manifest.SkillName}");
        }

        if (!string.Equals(manifestFile.Content, manifestSerializer.Serialize(manifest), StringComparison.Ordinal))
        {
            return PackageFailure($"ucli-skill.json is not canonical: {manifest.SkillName}");
        }

        var fileValidationResult = ValidateFiles(files, manifest);
        if (!fileValidationResult.IsSuccess)
        {
            return PackageFailure(fileValidationResult.Failure!.Message);
        }

        var digestValidationResult = ValidateDigests(files, manifest);
        if (!digestValidationResult.IsSuccess)
        {
            return PackageFailure(digestValidationResult.Failure!.Message);
        }

        return SkillOperationResult<CanonicalSkillPackage>.Success(new CanonicalSkillPackage(
            manifest,
            files));
    }

    private async ValueTask<SkillOperationResult<IReadOnlyList<SkillPackageFile>>> ReadFilesAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<SkillPackageFile>();
        var readResult = await ReadDirectoryEntriesAsync(skillDirectory, skillDirectory, files, cancellationToken).ConfigureAwait(false);
        if (!readResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillPackageFile>>.FailureResult(
                readResult.Failure!.Code,
                readResult.Failure.Message);
        }

        return SkillOperationResult<IReadOnlyList<SkillPackageFile>>.Success(files
            .OrderBy(static file => file.RelativePath, StringComparer.Ordinal)
            .ToArray());
    }

    private async ValueTask<SkillOperationResult<bool>> ReadDirectoryEntriesAsync (
        string skillDirectory,
        string directoryPath,
        List<SkillPackageFile> files,
        CancellationToken cancellationToken)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(skillDirectory, entryPath).Replace(Path.DirectorySeparatorChar, '/');
            if (!SkillRelativePath.IsSafeFilePath(relativePath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsafe path: {relativePath}");
            }

            if (Directory.Exists(entryPath))
            {
                if (!SkillPackageFileSystemEntryGuard.IsDirectory(entryPath))
                {
                    return BoolFailure(
                        $"Generated SKILL package contains an unsupported non-regular directory: {relativePath}");
                }

                var directoryResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
                if (!directoryResult.IsSuccess)
                {
                    return SkillOperationResult<bool>.FailureResult(directoryResult.Failure!.Code, directoryResult.Failure.Message);
                }

                var result = await ReadDirectoryEntriesAsync(skillDirectory, directoryResult.Value!, files, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    return result;
                }

                continue;
            }

            if (!File.Exists(entryPath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular path: {relativePath}");
            }

            if (!SkillPackageFileSystemEntryGuard.IsRegularFile(entryPath))
            {
                return BoolFailure(
                    $"Generated SKILL package contains an unsupported non-regular file: {relativePath}");
            }

            var pathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, entryPath);
            if (!pathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(pathResult.Failure!.Code, pathResult.Failure.Message);
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(pathResult.Value!, cancellationToken).ConfigureAwait(false));
            files.Add(SkillPackageFile.Create(relativePath, content));
        }

        return SkillOperationResult<bool>.Success(true);
    }
    private SkillOperationResult<bool> ValidateFiles (
        IReadOnlyList<SkillPackageFile> files,
        SkillManifest manifest)
    {
        if (!files.Any(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)))
        {
            return BoolFailure($"Generated SKILL package is missing SKILL.md: {manifest.SkillName}");
        }

        var hostArtifactPaths = manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in files)
        {
            if (string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || string.Equals(file.RelativePath, "ucli-skill.json", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal)
                || hostArtifactPaths.Contains(file.RelativePath))
            {
                continue;
            }

            return BoolFailure($"Generated SKILL package contains an unsupported file: {manifest.SkillName}/{file.RelativePath}");
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private SkillOperationResult<bool> ValidateDigests (
        IReadOnlyList<SkillPackageFile> files,
        SkillManifest manifest)
    {
        var contentDigest = digestCalculator.ComputeDigest(files
            .Where(static file => string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal)
                || file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
            .Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content)));

        if (!string.Equals(contentDigest, manifest.ContentDigest, StringComparison.Ordinal))
        {
            return BoolFailure($"Generated SKILL contentDigest does not match files: {manifest.SkillName}");
        }

        var metadata = new SkillHostMetadata(manifest.SkillName, manifest.DisplayName, manifest.Description);
        var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host, StringComparer.Ordinal);
        foreach (var adapter in hostAdapters.Adapters)
        {
            var artifact = artifactByHost[adapter.Descriptor.HostKey];
            var hostArtifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", hostArtifacts.Frontmatter);
            if (!string.Equals(frontmatterDigest, artifact.MaterializedFrontmatterDigest, StringComparison.Ordinal))
            {
                return BoolFailure($"Generated SKILL host frontmatter digest does not match adapter output: {manifest.SkillName}/{artifact.Host}");
            }

            if (adapter.MetadataArtifactPath is null)
            {
                continue;
            }

            var artifactFile = files.SingleOrDefault(file => string.Equals(file.RelativePath, adapter.MetadataArtifactPath, StringComparison.Ordinal));
            if (artifactFile is null)
            {
                return BoolFailure($"Generated SKILL package is missing host artifact: {manifest.SkillName}/{adapter.MetadataArtifactPath}");
            }

            if (hostArtifacts.MetadataContent is null)
            {
                return BoolFailure($"Generated SKILL host artifact adapter output is missing: {manifest.SkillName}/{adapter.MetadataArtifactPath}");
            }

            var expectedArtifactDigest = digestCalculator.ComputeSingleFileDigest(adapter.MetadataArtifactPath, hostArtifacts.MetadataContent);
            if (!string.Equals(expectedArtifactDigest, artifact.Digest, StringComparison.Ordinal))
            {
                return BoolFailure($"Generated SKILL host artifact digest does not match adapter output: {manifest.SkillName}/{adapter.MetadataArtifactPath}");
            }

            var artifactDigest = digestCalculator.ComputeSingleFileDigest(adapter.MetadataArtifactPath, artifactFile.Content);
            if (!string.Equals(artifactDigest, artifact.Digest, StringComparison.Ordinal))
            {
                return BoolFailure($"Generated SKILL host artifact digest does not match files: {manifest.SkillName}/{adapter.MetadataArtifactPath}");
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private static SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>> Failure (string message)
    {
        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static SkillOperationResult<CanonicalSkillPackage> PackageFailure (string message)
    {
        return SkillOperationResult<CanonicalSkillPackage>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }

    private static SkillOperationResult<bool> BoolFailure (string message)
    {
        return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, message);
    }
}
