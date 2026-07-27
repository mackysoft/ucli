using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Represents the actual payload emitted by <c>schema get</c>. </summary>
internal sealed record UcliSchemaGetPayload (
    string Name,
    UcliStaticSchemaKind Kind,
    string Path,
    [property: JsonPropertyName("$id")]
    string Id,
    Sha256Digest Sha256,
    string? Command,
    CommandResultStatus? Status,
    IReadOnlyList<UcliStaticSchemaUsage> Usages,
    IReadOnlyList<string> StaticDependencies,
    IReadOnlyList<string> DynamicValidationSources,
    UcliJsonObject Document);

/// <summary> Represents the actual payload emitted by <c>schema export</c>. </summary>
internal sealed record UcliSchemaExportPayload (
    string OutputPath,
    UcliStaticSchemaSetName SchemaSet,
    string PackageVersion,
    int FileCount);

/// <summary> Maps the installed schema lookup outcome to the public command result. </summary>
internal static class SchemaGetCommandResultFactory
{
    public static CommandResult? ValidateName (string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? CommandResult.InvalidArgument(
                UcliCommandNames.SchemaGet,
                "Schema logical name must not be null, empty, or whitespace.")
            : null;
    }

    public static CommandResult Create (
        UcliStaticSchemaSet schemaSet,
        string name)
    {
        var artifact = schemaSet.Find(name);
        if (artifact == null)
        {
            return CommandResult.InvalidArgument(
                UcliCommandNames.SchemaGet,
                $"Static schema '{name}' was not found.");
        }

        var entry = artifact.Entry;
        return CommandResult.Success(
            UcliCommandNames.SchemaGet,
            $"Static schema '{entry.Name}' loaded.",
            CreatePayload(artifact));
    }

    private static UcliSchemaGetPayload CreatePayload (
        UcliStaticSchemaArtifact artifact)
    {
        var entry = artifact.Entry;
        return new UcliSchemaGetPayload(
            entry.Name,
            entry.Kind,
            entry.Path,
            entry.Id,
            entry.Sha256,
            entry.Command,
            entry.Status,
            entry.Usages,
            entry.StaticDependencies,
            entry.DynamicValidationSources,
            new UcliJsonObject(artifact.Document));
    }
}
