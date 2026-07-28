using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Exports one validated static schema set without changing its artifact bytes. </summary>
internal static class UcliStaticSchemaSetExporter
{
    private static readonly RootRelativePath ManifestRelativePath =
        RootRelativePath.Parse("schema-manifest.json");

    /// <summary> Exports the exact manifest and schema bytes to a new or empty directory. </summary>
    public static UcliStaticSchemaExportResult Export (
        UcliStaticSchemaSet schemaSet,
        AbsolutePath destination)
    {
        ArgumentNullException.ThrowIfNull(schemaSet);
        ArgumentNullException.ThrowIfNull(destination);
        ValidateDestination(destination);
        var staging = CreateStagingDirectory(destination);

        try
        {
            WriteSchemaSet(schemaSet, staging);
            Commit(staging, destination);
            return new UcliStaticSchemaExportResult(
                destination,
                schemaSet.Artifacts.Count + 1);
        }
        finally
        {
            DirectoryUtilities.DeleteIfExists(staging, recursive: true);
        }
    }

    private static AbsolutePath CreateStagingDirectory (AbsolutePath destination)
    {
        if (!destination.TryGetParent(out var parent))
        {
            throw new InvalidDataException(
                "Static schema export destination must have a parent directory.");
        }

        DirectoryUtilities.Create(parent);
        var staging = ContainedPath.Create(
            parent,
            RootRelativePath.Parse($".ucli-schema-export-{Guid.NewGuid():N}")).Target;
        DirectoryUtilities.Create(staging);
        return staging;
    }

    private static void WriteSchemaSet (
        UcliStaticSchemaSet schemaSet,
        AbsolutePath staging)
    {
        WriteExactFile(
            ContainedPath.Create(staging, ManifestRelativePath).Target,
            schemaSet.ManifestUtf8);
        foreach (var artifact in schemaSet.Artifacts)
        {
            WriteExactFile(
                ContainedPath.Create(staging, artifact.RelativePath).Target,
                artifact.Utf8);
        }
    }

    private static void Commit (
        AbsolutePath staging,
        AbsolutePath destination)
    {
        DirectoryUtilities.DeleteIfExists(destination);
        DirectoryUtilities.Move(staging, destination);
    }

    private static void WriteExactFile (
        AbsolutePath path,
        byte[] contents)
    {
        if (!path.TryGetParent(out var parent))
        {
            throw new InvalidDataException($"Static schema output path must have a parent directory: {path.Value}");
        }

        DirectoryUtilities.Create(parent);
        FileUtilities.WriteAllBytes(path, contents);
    }

    private static void ValidateDestination (AbsolutePath destination)
    {
        if (FileUtilities.FileExists(destination))
        {
            throw new InvalidDataException("Static schema export destination must not be an existing file.");
        }

        if (!DirectoryUtilities.Exists(destination))
        {
            return;
        }

        var attributes = DirectoryUtilities.GetAttributes(destination);
        if ((attributes & FileAttributes.ReparsePoint) != 0
            || !DirectoryUtilities.IsEmpty(destination))
        {
            throw new InvalidDataException("Static schema export destination must not exist or must be an empty ordinary directory.");
        }
    }
}

/// <summary> Describes a completed exact-byte static schema export. </summary>
internal sealed record UcliStaticSchemaExportResult (
    AbsolutePath Destination,
    int FileCount);
