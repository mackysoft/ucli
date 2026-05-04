using MackySoft.Ucli.Skills.Generation;
using MackySoft.Ucli.Skills.Hosts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Materialization;

/// <summary> Materializes canonical SKILL packages for supported hosts. </summary>
public sealed class SkillMaterializationService
{
    private readonly SkillHostAdapterSet hostAdapters;

    /// <summary> Initializes a new instance of the <see cref="SkillMaterializationService" /> class. </summary>
    /// <param name="hostAdapters"> The supported host adapter set. </param>
    public SkillMaterializationService (SkillHostAdapterSet? hostAdapters = null)
    {
        this.hostAdapters = hostAdapters ?? new SkillHostAdapterSet();
    }

    /// <summary> Materializes one canonical package for one host. </summary>
    /// <param name="package"> The canonical package. </param>
    /// <param name="host"> The target host. </param>
    /// <returns> The materialized package or unsupported-host failure. </returns>
    public SkillOperationResult<SkillMaterializedPackage> Materialize (
        CanonicalSkillPackage package,
        SkillHostKind host)
    {
        ArgumentNullException.ThrowIfNull(package);

        var adapterResult = hostAdapters.GetAdapter(host);
        if (!adapterResult.IsSuccess)
        {
            return SkillOperationResult<SkillMaterializedPackage>.FailureResult(
                adapterResult.Failure!.Code,
                adapterResult.Failure.Message);
        }

        var metadata = new Sources.SkillSourceMetadata(
            Sources.SkillSourceMetadata.CurrentSchemaVersion,
            package.SkillName,
            package.DisplayName,
            package.Description,
            package.Files
                .Where(static file => file.RelativePath.StartsWith("references/", StringComparison.Ordinal))
                .Select(static file => file.RelativePath["references/".Length..])
                .Order(StringComparer.Ordinal)
                .ToArray());

        var artifacts = adapterResult.Value!.BuildArtifacts(metadata);
        var files = new List<SkillPackageFile>();
        foreach (var file in package.Files)
        {
            if (string.Equals(file.RelativePath, "SKILL.md", StringComparison.Ordinal))
            {
                files.Add(SkillPackageFile.Create(file.RelativePath, artifacts.Frontmatter + "\n" + file.Content));
                continue;
            }

            files.Add(file);
        }

        files.AddRange(artifacts.AdditionalFiles);

        return SkillOperationResult<SkillMaterializedPackage>.Success(new SkillMaterializedPackage(
            package.SkillName,
            host,
            files.OrderBy(static file => file.RelativePath, StringComparer.Ordinal).ToArray()));
    }
}
