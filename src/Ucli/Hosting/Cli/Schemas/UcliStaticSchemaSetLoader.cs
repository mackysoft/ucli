using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Loads one static schema artifact set from a guarded delivery root. </summary>
internal static class UcliStaticSchemaSetLoader
{
    private static readonly RootRelativePath ManifestRelativePath =
        RootRelativePath.Parse("schema-manifest.json");

    /// <summary> Loads and validates the complete static schema set rooted at <paramref name="schemaRoot" />. </summary>
    public static UcliStaticSchemaSet Load (AbsolutePath schemaRoot)
    {
        ArgumentNullException.ThrowIfNull(schemaRoot);

        var manifestPath = ContainedPath.Create(schemaRoot, ManifestRelativePath).Target;
        var manifestBytes = ReadExactFile(manifestPath);
        var manifest = UcliStaticSchemaManifestValidator.Deserialize(manifestBytes);
        var entries = UcliStaticSchemaManifestValidator.Validate(manifest);

        var artifacts = new UcliStoredStaticSchemaArtifact[entries.Count];
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var artifactPath = ContainedPath.Create(schemaRoot, entry.RelativePath).Target;
            var artifactBytes = ReadExactFile(artifactPath);
            UcliStaticSchemaArtifactValidator.Validate(entry.Entry, artifactBytes);
            artifacts[index] = new UcliStoredStaticSchemaArtifact(
                entry.Entry,
                entry.RelativePath,
                artifactBytes);
        }

        return new UcliStaticSchemaSet(manifest, manifestBytes, artifacts);
    }

    private static byte[] ReadExactFile (AbsolutePath path)
    {
        using var stream = FileUtilities.OpenReopenSafeReadStream(path);
        using var contents = new MemoryStream();
        stream.CopyTo(contents);
        return contents.ToArray();
    }
}
