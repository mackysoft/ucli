using MackySoft.Ucli.Skills.Digests;
using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Manifests;
using MackySoft.Ucli.Skills.Shared;
using MackySoft.Ucli.Skills.Sources;

namespace MackySoft.Ucli.Skills.Generation;

/// <summary> Generates canonical host-independent SKILL packages from source definitions. </summary>
public sealed class SkillPackageGenerationService
{
    private readonly SkillSourceDefinitionReader sourceReader;
    private readonly SkillHostAdapterSet hostAdapters;
    private readonly SkillDigestCalculator digestCalculator;
    private readonly SkillManifestJsonSerializer manifestSerializer;

    /// <summary> Initializes a new instance of the <see cref="SkillPackageGenerationService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    /// <param name="sourceReader"> The source definition reader. </param>
    /// <param name="digestCalculator"> The digest calculator. </param>
    /// <param name="manifestSerializer"> The manifest serializer. </param>
    public SkillPackageGenerationService (
        SkillHostAdapterSet hostAdapters,
        SkillSourceDefinitionReader? sourceReader = null,
        SkillDigestCalculator? digestCalculator = null,
        SkillManifestJsonSerializer? manifestSerializer = null)
    {
        ArgumentNullException.ThrowIfNull(hostAdapters);

        this.sourceReader = sourceReader ?? new SkillSourceDefinitionReader();
        this.hostAdapters = hostAdapters;
        this.digestCalculator = digestCalculator ?? new SkillDigestCalculator();
        this.manifestSerializer = manifestSerializer ?? new SkillManifestJsonSerializer();
    }

    /// <summary> Generates all official SKILL packages under one source definitions root. </summary>
    /// <param name="definitionsRoot"> The <c>SkillDefinitions</c> directory path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The canonical packages or validation failure. </returns>
    public async ValueTask<SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>> GenerateAllAsync (
        string definitionsRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionsRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var sourceResult = await sourceReader.ReadAllAsync(definitionsRoot, cancellationToken).ConfigureAwait(false);
        if (!sourceResult.IsSuccess)
        {
            return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.FailureResult(
                sourceResult.Failure!.Code,
                sourceResult.Failure.Message);
        }

        var packages = sourceResult.Value!
            .Select(Generate)
            .OrderBy(static package => package.SkillName, StringComparer.Ordinal)
            .ToArray();

        return SkillOperationResult<IReadOnlyList<CanonicalSkillPackage>>.Success(packages);
    }

    /// <summary> Generates one canonical package from one source definition. </summary>
    /// <param name="definition"> The source definition. </param>
    /// <returns> The canonical package. </returns>
    public CanonicalSkillPackage Generate (SkillSourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var bodyFile = SkillPackageFile.Create("SKILL.md", definition.SkillTemplate);
        var referenceFiles = definition.References
            .OrderBy(static reference => reference.FileName, StringComparer.Ordinal)
            .Select(static reference => SkillPackageFile.Create($"references/{reference.FileName}", reference.Template))
            .ToArray();

        var contentDigest = digestCalculator.ComputeDigest(
            new[] { new SkillDigestInputFile(bodyFile.RelativePath, bodyFile.Content) }
                .Concat(referenceFiles.Select(static file => new SkillDigestInputFile(file.RelativePath, file.Content))));

        var hostArtifacts = hostAdapters.Adapters
            .Select(adapter => BuildHostArtifact(definition.Metadata, adapter))
            .OrderBy(static artifact => artifact.Host, StringComparer.Ordinal)
            .ToArray();

        var manifest = new SkillManifest(
            SkillManifest.CurrentSchemaVersion,
            definition.Metadata.SkillName,
            contentDigest,
            hostArtifacts);

        var manifestFile = SkillPackageFile.Create("ucli-skill.json", manifestSerializer.Serialize(manifest));
        var files = new[] { bodyFile, manifestFile }.Concat(referenceFiles).OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray();

        return new CanonicalSkillPackage(
            SkillName: definition.Metadata.SkillName,
            DisplayName: definition.Metadata.DisplayName,
            Description: definition.Metadata.Description,
            Manifest: manifest,
            Files: files);
    }

    private SkillHostArtifactManifest BuildHostArtifact (
        SkillSourceMetadata metadata,
        ISkillHostAdapter adapter)
    {
        var artifacts = adapter.BuildArtifacts(metadata);
        var frontmatterDigest = digestCalculator.ComputeSingleFileDigest("SKILL.md.frontmatter", artifacts.Frontmatter);
        var additionalFiles = artifacts.AdditionalFiles.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray();

        if (adapter.MetadataArtifactPath is null)
        {
            if (additionalFiles.Length != 0)
            {
                throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must not emit metadata artifacts.");
            }

            return new SkillHostArtifactManifest(adapter.Descriptor.HostKey, null, null, frontmatterDigest);
        }

        if (additionalFiles.Length != 1 || !string.Equals(additionalFiles[0].RelativePath, adapter.MetadataArtifactPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must emit metadata artifact '{adapter.MetadataArtifactPath}'.");
        }

        var fileArtifact = additionalFiles[0];
        return new SkillHostArtifactManifest(
            adapter.Descriptor.HostKey,
            fileArtifact.RelativePath,
            digestCalculator.ComputeSingleFileDigest(fileArtifact.RelativePath, fileArtifact.Content),
            frontmatterDigest);
    }
}
