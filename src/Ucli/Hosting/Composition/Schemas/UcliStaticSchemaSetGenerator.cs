using System.Text.Json;
using System.Xml.Linq;
using MackySoft.FileSystem;
using MackySoft.JsonSchema.Generation;
using MackySoft.JsonSchema.Generation.Projection;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Json.Generation;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Schemas;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary> Generates and publishes the complete static schema artifact set. </summary>
internal static class UcliStaticSchemaSetGenerator
{
    private static readonly RootRelativePath ManifestRelativePath =
        RootRelativePath.Parse("schema-manifest.json");

    private static readonly RootRelativePath VersionPropsRelativePath =
        RootRelativePath.Parse("Directory.Build.props");

    private static readonly JsonSerializerOptions ManifestSerializerOptions =
        new(CliOutputJsonSerializerOptions.Default)
        {
            WriteIndented = true,
        };

    public static void Generate (
        AbsolutePath outputRoot,
        AbsolutePath repositoryRoot,
        string? packageVersion)
    {
        ArgumentNullException.ThrowIfNull(outputRoot);
        ArgumentNullException.ThrowIfNull(repositoryRoot);
        var resolvedPackageVersion = ResolvePackageVersion(repositoryRoot, packageVersion);
        UcliStaticSchemaOutputDirectory.Prepare(outputRoot, repositoryRoot);

        var registrations = UcliStaticSchemaRegistrationCatalog.GetAll();
        var generatedArtifacts = PairResults(
            registrations,
            UcliJsonContractGenerator.GenerateSet(CreateRequests(registrations)));
        WriteArtifacts(outputRoot, generatedArtifacts);
        WriteManifestFile(
            outputRoot,
            CreateManifest(resolvedPackageVersion, generatedArtifacts));
    }

    private static IEnumerable<JsonContractGenerationRequest> CreateRequests (
        IReadOnlyList<UcliStaticSchemaRegistration> registrations)
    {
        return registrations.Select(
            static registration =>
                new JsonContractGenerationRequest(
                    registration.ContractId,
                    registration.TypeInfo,
                    new JsonSchemaDocumentOptions(
                        JsonSchemaDocumentKind.Complete,
                        UcliStaticSchemaSet.CreateSchemaId(registration.Path),
                        registration.Name)));
    }

    private static IReadOnlyList<UcliGeneratedStaticSchemaArtifact> PairResults (
        IReadOnlyList<UcliStaticSchemaRegistration> registrations,
        IReadOnlyList<JsonContractGenerationResult> results)
    {
        var registrationsByContractId = registrations.ToDictionary(
            static registration => registration.ContractId,
            StringComparer.Ordinal);
        return results
            .Select(
                result => new UcliGeneratedStaticSchemaArtifact(
                    registrationsByContractId[result.Model.ContractId],
                    result.GetJsonSchemaUtf8()))
            .ToArray();
    }

    private static void WriteArtifacts (
        AbsolutePath outputRoot,
        IReadOnlyList<UcliGeneratedStaticSchemaArtifact> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            WriteArtifact(outputRoot, artifact.Registration.Path, artifact.Utf8);
        }
    }

    private static UcliStaticSchemaManifest CreateManifest (
        string packageVersion,
        IReadOnlyList<UcliGeneratedStaticSchemaArtifact> artifacts)
    {
        return new UcliStaticSchemaManifest
        {
            SchemaSet = UcliStaticSchemaSetName.Ucli,
            PackageVersion = packageVersion,
            ProtocolVersion = IpcProtocol.CurrentVersion,
            JsonSchemaDialect = UcliJsonSchemaDialect.Draft202012,
            Schemas = CreateManifestEntries(artifacts),
        };
    }

    private static IReadOnlyList<UcliStaticSchemaEntry> CreateManifestEntries (
        IReadOnlyList<UcliGeneratedStaticSchemaArtifact> artifacts)
    {
        var entries = artifacts
            .Select(
                static artifact =>
                    CreateManifestEntry(artifact.Registration, artifact.Utf8))
            .ToList();
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        return entries.AsReadOnly();
    }

    private static UcliStaticSchemaEntry CreateManifestEntry (
        UcliStaticSchemaRegistration registration,
        byte[] schemaUtf8)
    {
        return new UcliStaticSchemaEntry
        {
            Name = registration.Name,
            Kind = registration.Kind,
            Path = registration.Path.Value,
            Id = UcliStaticSchemaSet.CreateSchemaId(registration.Path),
            Sha256 = Sha256Digest.Compute(schemaUtf8),
            Command = registration.Command,
            Status = registration.Status,
            Usages = registration.Usages,
            StaticDependencies = registration.StaticDependencies,
            DynamicValidationSources = registration.DynamicValidationSources,
        };
    }

    private static void WriteArtifact (
        AbsolutePath outputRoot,
        RootRelativePath relativePath,
        byte[] utf8)
    {
        var outputPath = ContainedPath.Create(outputRoot, relativePath).Target;
        if (!outputPath.TryGetParent(out var parent))
        {
            throw new InvalidOperationException($"Schema output path has no parent: {outputPath.Value}");
        }

        DirectoryUtilities.Create(parent);
        FileUtilities.WriteAllBytes(outputPath, utf8);
    }

    private static void WriteManifestFile (
        AbsolutePath outputRoot,
        UcliStaticSchemaManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, ManifestSerializerOptions)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        var manifestPath = ContainedPath.Create(outputRoot, ManifestRelativePath).Target;
        FileUtilities.WriteAllTextAtomically(
            manifestPath,
            json.EndsWith('\n') ? json : json + "\n");
    }

    private static string ReadPackageVersion (AbsolutePath repositoryRoot)
    {
        var propsPath = ContainedPath.Create(repositoryRoot, VersionPropsRelativePath).Target;
        if (!FileUtilities.FileExists(propsPath))
        {
            throw new InvalidOperationException($"Directory.Build.props was not found: {propsPath.Value}");
        }

        using var stream = FileUtilities.OpenReopenSafeReadStream(propsPath);
        var document = XDocument.Load(stream);
        var version = document.Descendants("Version").SingleOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException($"Version property was not found: {propsPath.Value}");
        }

        return version;
    }

    private static string ResolvePackageVersion (
        AbsolutePath repositoryRoot,
        string? packageVersion)
    {
        var resolvedPackageVersion = packageVersion ?? ReadPackageVersion(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedPackageVersion);
        return resolvedPackageVersion;
    }
}

/// <summary> Couples provider output bytes to the uCLI delivery registration that produced them. </summary>
internal sealed record UcliGeneratedStaticSchemaArtifact (
    UcliStaticSchemaRegistration Registration,
    byte[] Utf8);
