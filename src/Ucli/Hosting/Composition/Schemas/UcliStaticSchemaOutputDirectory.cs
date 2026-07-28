using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary> Prepares an explicitly selected output directory without replacing unrelated content. </summary>
internal static class UcliStaticSchemaOutputDirectory
{
    private static readonly RootRelativePath ManifestRelativePath =
        RootRelativePath.Parse("schema-manifest.json");

    private static readonly JsonSerializerOptions OwnershipSerializerOptions =
        new(CliOutputJsonSerializerOptions.Default)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    public static void Prepare (
        AbsolutePath outputRoot,
        AbsolutePath repositoryRoot)
    {
        ValidateLocation(outputRoot, repositoryRoot);
        if (!DirectoryUtilities.Exists(outputRoot))
        {
            DirectoryUtilities.Create(outputRoot);
            return;
        }

        ValidateExistingDirectory(outputRoot);
        DirectoryUtilities.DeleteIfExists(outputRoot, recursive: true);
        DirectoryUtilities.Create(outputRoot);
    }

    private static void ValidateLocation (
        AbsolutePath outputRoot,
        AbsolutePath repositoryRoot)
    {
        if (!outputRoot.TryGetParent(out _)
            || ContainedPath.TryCreate(outputRoot, repositoryRoot, out _, out _))
        {
            throw new InvalidOperationException(
                "Schema output root must not be a filesystem root or contain the repository root.");
        }
    }

    private static void ValidateExistingDirectory (AbsolutePath outputRoot)
    {
        if ((DirectoryUtilities.GetAttributes(outputRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Schema output root must not be a reparse point.");
        }

        var manifestPath = ContainedPath.Create(outputRoot, ManifestRelativePath).Target;
        if (!DirectoryUtilities.IsEmpty(outputRoot)
            && !FileUtilities.FileExists(manifestPath))
        {
            throw new InvalidOperationException(
                $"Refusing to replace a non-empty schema output directory without a uCLI schema manifest: {outputRoot.Value}");
        }

        if (FileUtilities.FileExists(manifestPath))
        {
            ValidateOwnership(manifestPath, outputRoot);
        }
    }

    private static void ValidateOwnership (
        AbsolutePath manifestPath,
        AbsolutePath outputRoot)
    {
        UcliSchemaSetOwnership? ownership;
        try
        {
            using var stream = FileUtilities.OpenReopenSafeReadStream(manifestPath);
            ownership = JsonSerializer.Deserialize<UcliSchemaSetOwnership>(
                stream,
                OwnershipSerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Refusing to replace a schema output directory with an unreadable manifest: {outputRoot.Value}",
                exception);
        }

        if (ownership?.SchemaSet != UcliStaticSchemaSetName.Ucli)
        {
            throw new InvalidOperationException(
                $"Refusing to replace a schema output directory with an unexpected manifest: {outputRoot.Value}");
        }
    }

    private sealed record UcliSchemaSetOwnership (
        UcliStaticSchemaSetName? SchemaSet);
}
