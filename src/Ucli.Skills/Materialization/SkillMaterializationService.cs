using MackySoft.Ucli.Skills.Hosts.Registration;
using MackySoft.Ucli.Skills.Packaging;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Materialization;

/// <summary> Materializes canonical SKILL packages for supported hosts. </summary>
public sealed class SkillMaterializationService
{
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="SkillMaterializationService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillMaterializationService (SkillHostAdapterSet hostAdapters)
    {
        this.hostAdapters = hostAdapters ?? throw new ArgumentNullException(nameof(hostAdapters));
    }

    /// <summary> Materializes one canonical package for one host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="host"> The target host. </param>
    /// <returns> The materialized package or unsupported-host failure. </returns>
    public SkillOperationResult<SkillMaterializedPackage> Materialize (
        CanonicalSkillPackage package,
        string host)
    {
        ArgumentNullException.ThrowIfNull(package);

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackage>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var metadata = new Hosts.Contracts.SkillHostMetadata(
            package.SkillName,
            package.DisplayName,
            package.Description);

        var adapter = adapterResult.Value!;
        var artifacts = adapter.BuildArtifacts(metadata);
        var files = new List<SkillPackageFile>();
        var hostArtifactFilePaths = package.Manifest.HostArtifacts
            .Select(static artifact => artifact.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in package.Files)
        {
            if (string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal))
            {
                files.Add(SkillPackageFile.Create(file.RelativePath, artifacts.Frontmatter + "\n" + file.Content));
                continue;
            }

            if (hostArtifactFilePaths.Contains(file.RelativePath))
            {
                continue;
            }

            files.Add(file);
        }

        if (adapter.MetadataArtifactPath is null)
        {
            if (artifacts.MetadataContent is not null)
            {
                throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must not emit metadata artifacts.");
            }
        }
        else
        {
            if (artifacts.MetadataContent is null)
            {
                throw new InvalidOperationException($"Host adapter '{adapter.Descriptor.HostKey}' must emit metadata artifact '{adapter.MetadataArtifactPath}'.");
            }

            files.Add(SkillPackageFile.Create(adapter.MetadataArtifactPath, artifacts.MetadataContent));
        }

        return SkillOperationResult<SkillMaterializedPackage>.Success(new SkillMaterializedPackage(
            package.SkillName,
            adapter.Descriptor.HostKey,
            files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray()));
    }
}
