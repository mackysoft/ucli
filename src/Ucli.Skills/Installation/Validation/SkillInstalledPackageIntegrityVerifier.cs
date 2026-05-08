using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation.Validation;

/// <summary> Verifies an installed SKILL package against its own installed manifest. </summary>
public sealed class SkillInstalledPackageIntegrityVerifier
{
    private const string SkillBodyPath = "SKILL.md";
    private const string ManifestPath = "ucli-skill.json";
    private const string ReferencesDirectory = "references";
    private const string ReferencesPrefix = "references/";

    private readonly SkillInstalledManifestReader installedManifestReader;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillManifestJsonSerializer manifestSerializer;
    private readonly SkillHostMaterializationInspector hostInspector;
    private readonly SkillDigestCalculator digestCalculator;

    /// <summary> Initializes a new instance of the <see cref="SkillInstalledPackageIntegrityVerifier" /> class. </summary>
    /// <param name="installedManifestReader"> The installed manifest reader. </param>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    /// <param name="hostInspector"> The host materialization inspector. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    public SkillInstalledPackageIntegrityVerifier (
        SkillInstalledManifestReader installedManifestReader,
        SkillHostAdapterSet hostAdapters,
        SkillManifestJsonSerializer manifestSerializer,
        SkillHostMaterializationInspector hostInspector,
        SkillDigestCalculator digestCalculator)
    {
        this.installedManifestReader = installedManifestReader ?? throw new ArgumentNullException(nameof(installedManifestReader));
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
        this.manifestSerializer = manifestSerializer ?? throw new ArgumentNullException(nameof(manifestSerializer));
        this.hostInspector = hostInspector ?? throw new ArgumentNullException(nameof(hostInspector));
        this.digestCalculator = digestCalculator ?? throw new ArgumentNullException(nameof(digestCalculator));
    }

    /// <summary> Verifies that one installed package is clean and materialized for the requested host. </summary>
    /// <param name="skillDirectory"> The installed skill directory. </param>
    /// <param name="host"> The requested host. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The installed manifest when integrity verification succeeds; otherwise a failure. </returns>
    public async ValueTask<SkillOperationResult<SkillManifest>> VerifyAsync (
        string skillDirectory,
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        cancellationToken.ThrowIfCancellationRequested();

        var installedManifestResult = await installedManifestReader.ReadRequiredAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!installedManifestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                installedManifestResult.Failure!.Code,
                installedManifestResult.Failure.Message);
        }

        var installedManifest = installedManifestResult.Value!;
        var manifest = installedManifest.Manifest;
        var manifestIntegrityResult = VerifyInstalledManifestIntegrity(installedManifest);
        if (!manifestIntegrityResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(manifestIntegrityResult.Failure!.Code, manifestIntegrityResult.Failure.Message);
        }

        if (!manifestIntegrityResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL manifest does not match its generated host artifact metadata: {manifest.SkillName}");
        }

        var differentHostResult = await hostInspector.MatchesDifferentHostAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!differentHostResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(differentHostResult.Failure!.Code, differentHostResult.Failure.Message);
        }

        if (differentHostResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetHostConflict,
                $"Installed skill directory is materialized for another host: {skillDirectory}");
        }

        var hostMatchResult = await hostInspector.MatchesHostAsync(skillDirectory, manifest, host, cancellationToken).ConfigureAwait(false);
        if (!hostMatchResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(hostMatchResult.Failure!.Code, hostMatchResult.Failure.Message);
        }

        if (!hostMatchResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed skill directory does not match requested host materialization: {skillDirectory}");
        }

        var digestResult = await VerifyInstalledContentDigestAsync(skillDirectory, manifest, cancellationToken).ConfigureAwait(false);
        if (!digestResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(digestResult.Failure!.Code, digestResult.Failure.Message);
        }

        if (!digestResult.Value)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL files do not match installed contentDigest: {manifest.SkillName}");
        }

        var fileSetResult = VerifyInstalledFileSet(skillDirectory, manifest, host);
        if (!fileSetResult.IsSuccess)
        {
            return SkillOperationResult<SkillManifest>.FailureResult(fileSetResult.Failure!.Code, fileSetResult.Failure.Message);
        }

        return fileSetResult.Value
            ? SkillOperationResult<SkillManifest>.Success(manifest)
            : SkillOperationResult<SkillManifest>.FailureResult(
                SkillFailureCodes.InstallTargetDigestMismatch,
                $"Installed SKILL file set contains unmanaged files: {manifest.SkillName}");
    }

    private SkillOperationResult<bool> VerifyInstalledManifestIntegrity (SkillInstalledManifest installedManifest)
    {
        if (!string.Equals(installedManifest.ManifestText, manifestSerializer.Serialize(installedManifest.Manifest), StringComparison.Ordinal))
        {
            return SkillOperationResult<bool>.Success(false);
        }

        var manifest = installedManifest.Manifest;
        var metadata = new SkillHostMetadata(manifest.SkillName, manifest.DisplayName, manifest.Description);
        var artifactByHost = manifest.HostArtifacts.ToDictionary(static artifact => artifact.Host, StringComparer.Ordinal);
        foreach (var adapter in hostAdapters.Adapters)
        {
            if (!artifactByHost.TryGetValue(adapter.Descriptor.HostKey, out var artifact))
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Manifest host artifact '{adapter.Descriptor.HostKey}' is missing.");
            }

            var artifacts = adapter.BuildArtifacts(metadata);
            var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", artifacts.Frontmatter);
            if (!string.Equals(artifact.MaterializedFrontmatterDigest, frontmatterDigest, StringComparison.Ordinal))
            {
                return SkillOperationResult<bool>.Success(false);
            }

            if (adapter.MetadataArtifactPath is null)
            {
                if (artifact.Path is not null || artifact.Digest is not null)
                {
                    return SkillOperationResult<bool>.FailureResult(
                        SkillFailureCodes.ManifestInvalid,
                        $"Manifest host artifact '{artifact.Host}' must not contain metadata artifact fields.");
                }

                continue;
            }

            if (artifacts.MetadataContent is null)
            {
                return SkillOperationResult<bool>.FailureResult(
                    SkillFailureCodes.ManifestInvalid,
                    $"Host adapter '{adapter.Descriptor.HostKey}' did not generate metadata content.");
            }

            var metadataDigest = digestCalculator.ComputeSingleFileDigest(adapter.MetadataArtifactPath, artifacts.MetadataContent);
            if (!string.Equals(artifact.Path, adapter.MetadataArtifactPath, StringComparison.Ordinal)
                || !string.Equals(artifact.Digest, metadataDigest, StringComparison.Ordinal))
            {
                return SkillOperationResult<bool>.Success(false);
            }
        }

        return SkillOperationResult<bool>.Success(true);
    }

    private async ValueTask<SkillOperationResult<bool>> VerifyInstalledContentDigestAsync (
        string skillDirectory,
        SkillManifest manifest,
        CancellationToken cancellationToken)
    {
        var digestInputResult = await ReadInstalledDigestInputsAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!digestInputResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(digestInputResult.Failure!.Code, digestInputResult.Failure.Message);
        }

        var actualDigest = digestCalculator.ComputeDigest(digestInputResult.Value!);
        return SkillOperationResult<bool>.Success(string.Equals(actualDigest, manifest.ContentDigest, StringComparison.Ordinal));
    }

    private static async ValueTask<SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>> ReadInstalledDigestInputsAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillBodyResult = await ReadInstalledSkillBodyAsync(skillDirectory, cancellationToken).ConfigureAwait(false);
        if (!skillBodyResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                skillBodyResult.Failure!.Code,
                skillBodyResult.Failure.Message);
        }

        if (!skillBodyResult.Value.Exists)
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.Success(Array.Empty<SkillDigestInputFile>());
        }

        var digestInputs = new List<SkillDigestInputFile>
        {
            new(SkillBodyPath, skillBodyResult.Value.Body),
        };

        var referencesPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, ReferencesDirectory);
        if (!referencesPathResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                referencesPathResult.Failure!.Code,
                referencesPathResult.Failure.Message);
        }

        var referencesPath = referencesPathResult.Value!;
        if (!Directory.Exists(referencesPath))
        {
            return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.Success(digestInputs);
        }

        foreach (var referencePath in Directory.EnumerateFiles(referencesPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, referencePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                    resolvedPathResult.Failure!.Code,
                    resolvedPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, resolvedPathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            if (!relativePath.StartsWith(ReferencesPrefix, StringComparison.Ordinal))
            {
                return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.FailureResult(
                    SkillFailureCodes.PathUnsafe,
                    $"Reference file path escaped references directory: {relativePath}");
            }

            var content = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(resolvedPathResult.Value!, cancellationToken).ConfigureAwait(false));
            digestInputs.Add(new SkillDigestInputFile(relativePath, content));
        }

        return SkillOperationResult<IReadOnlyList<SkillDigestInputFile>>.Success(digestInputs);
    }

    private static async ValueTask<SkillOperationResult<InstalledSkillBody>> ReadInstalledSkillBodyAsync (
        string skillDirectory,
        CancellationToken cancellationToken)
    {
        var skillPathResult = SkillPackagePathBoundary.ResolvePackageFilePath(skillDirectory, SkillBodyPath);
        if (!skillPathResult.IsSuccess)
        {
            return SkillOperationResult<InstalledSkillBody>.FailureResult(skillPathResult.Failure!.Code, skillPathResult.Failure.Message);
        }

        if (!File.Exists(skillPathResult.Value!))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var skillText = SkillTextNormalizer.NormalizeToLf(await File.ReadAllTextAsync(skillPathResult.Value!, cancellationToken).ConfigureAwait(false));
        if (!SkillHostMaterializationInspector.TryExtractFrontmatter(skillText, out var frontmatter))
        {
            return SkillOperationResult<InstalledSkillBody>.Success(InstalledSkillBody.Missing);
        }

        var body = skillText[frontmatter.Length..];
        if (body.StartsWith('\n'))
        {
            body = body[1..];
        }

        return SkillOperationResult<InstalledSkillBody>.Success(new InstalledSkillBody(true, body));
    }

    private static SkillOperationResult<bool> VerifyInstalledFileSet (
        string skillDirectory,
        SkillManifest manifest,
        string host)
    {
        var allowedPaths = new HashSet<string>(StringComparer.Ordinal)
        {
            SkillBodyPath,
            ManifestPath,
        };

        var hostArtifact = manifest.HostArtifacts.SingleOrDefault(artifact => string.Equals(artifact.Host, host, StringComparison.Ordinal));
        if (hostArtifact is null)
        {
            return SkillOperationResult<bool>.FailureResult(SkillFailureCodes.ManifestInvalid, $"Manifest does not contain host artifact '{host}'.");
        }

        if (!string.IsNullOrWhiteSpace(hostArtifact.Path))
        {
            allowedPaths.Add(hostArtifact.Path);
        }

        var allowedDirectoryPaths = SkillInstalledDirectorySet.BuildParentDirectories(allowedPaths);
        foreach (var filePath in Directory.EnumerateFiles(skillDirectory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var resolvedPathResult = SkillPackagePathBoundary.ResolveUnderRoot(skillDirectory, filePath);
            if (!resolvedPathResult.IsSuccess)
            {
                return SkillOperationResult<bool>.FailureResult(resolvedPathResult.Failure!.Code, resolvedPathResult.Failure.Message);
            }

            var relativePath = Path.GetRelativePath(skillDirectory, resolvedPathResult.Value!).Replace(Path.DirectorySeparatorChar, '/');
            if (allowedPaths.Contains(relativePath) || relativePath.StartsWith(ReferencesPrefix, StringComparison.Ordinal))
            {
                SkillInstalledDirectorySet.AddParentDirectories(allowedDirectoryPaths, relativePath);
                continue;
            }

            return SkillOperationResult<bool>.Success(false);
        }

        var directorySetResult = SkillInstalledDirectorySet.ContainsOnlyAllowedDirectories(skillDirectory, allowedDirectoryPaths);
        if (!directorySetResult.IsSuccess)
        {
            return SkillOperationResult<bool>.FailureResult(directorySetResult.Failure!.Code, directorySetResult.Failure.Message);
        }

        return directorySetResult;
    }

    private readonly record struct InstalledSkillBody (
        bool Exists,
        string Body)
    {
        public static InstalledSkillBody Missing { get; } = new(false, string.Empty);
    }
}
