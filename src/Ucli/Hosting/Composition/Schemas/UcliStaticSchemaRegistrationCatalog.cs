using System.Text.Json.Serialization.Metadata;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary>
/// Assigns uCLI logical names and delivery paths to the serializer contracts used by public product boundaries.
/// </summary>
internal static class UcliStaticSchemaRegistrationCatalog
{
    private static readonly IReadOnlyList<UcliStaticSchemaRegistration> Registrations =
        CreateRegistrations();

    /// <summary> Gets the immutable registration set. </summary>
    public static IReadOnlyList<UcliStaticSchemaRegistration> GetAll ()
    {
        return Registrations;
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateRegistrations ()
    {
        var registrations = CreateCoreRegistrations();
        registrations.AddRange(UcliCommandPayloadSchemaRegistrationCatalog.GetAll());
        return Array.AsReadOnly(registrations.ToArray());
    }

    private static List<UcliStaticSchemaRegistration> CreateCoreRegistrations ()
    {
        var registrations = new List<UcliStaticSchemaRegistration>();
        registrations.AddRange(CreateSchemaSetRegistrations());
        registrations.AddRange(CreateCommonReferenceRegistrations());
        registrations.AddRange(CreateRequestRegistrations());
        return registrations;
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateSchemaSetRegistrations ()
    {
        return
        [
            Metadata<UcliStaticSchemaManifest>(
                "schema.manifest",
                "schema-manifest.schema.json"),
            Document<CommandResult>(
                "cli-output.envelope",
                "cli-output/envelope.schema.json",
                UcliStaticSchemaKind.CliOutputEnvelope),
        ];
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateRequestRegistrations ()
    {
        return
        [
            RequestDocument<UcliRequestJsonContract>(
                "request.envelope",
                "request/request-envelope.schema.json",
                usages:
                [
                    StandardInputUsage(UcliCommandNames.Call),
                    StandardInputUsage(UcliCommandNames.Plan),
                    StandardInputUsage(UcliCommandNames.Validate),
                ],
                dynamicValidationSources: [UcliCommandNames.OpsDescribe]),
        ];
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateCommonReferenceRegistrations ()
    {
        return
        [
            NonNullDocument<ArtifactRef>(
                "common.artifact-ref",
                "common/artifact-ref.schema.json",
                UcliStaticSchemaKind.CommonDefinition),
            NonNullDocument<ExecutionRef>(
                "common.execution-ref",
                "common/execution-ref.schema.json",
                UcliStaticSchemaKind.CommonDefinition),
        ];
    }

    private static UcliStaticSchemaRegistration Metadata<T> (
        string name,
        string path)
    {
        return Document<T>(
            name,
            path,
            UcliStaticSchemaKind.SchemaSetMetadata);
    }

    private static UcliStaticSchemaRegistration Document<T> (
        string name,
        string path,
        UcliStaticSchemaKind kind,
        string? command = null,
        CommandResultStatus? status = null)
    {
        return Document(
            name,
            path,
            kind,
            ResolveTypeInfo<T>(),
            CreatePayloadMetadata(command, status));
    }

    private static UcliStaticSchemaRegistration NonNullDocument<T> (
        string name,
        string path,
        UcliStaticSchemaKind kind)
    {
        var rootType = UcliNonNullJsonObject.MakeValueType(typeof(T));
        return Document(
            name,
            path,
            kind,
            CliOutputJsonSerializerOptions.Default.GetTypeInfo(rootType),
            metadata: null);
    }

    private static UcliStaticSchemaRegistration Document (
        string name,
        string path,
        UcliStaticSchemaKind kind,
        JsonTypeInfo typeInfo,
        UcliStaticSchemaManifestMetadata? metadata)
    {
        return new UcliStaticSchemaRegistration(
            name,
            RootRelativePath.Parse(path),
            kind,
            typeInfo,
            metadata);
    }

    private static UcliStaticSchemaRegistration RequestDocument<T> (
        string name,
        string path,
        IReadOnlyList<UcliStaticSchemaUsage>? usages = null,
        IReadOnlyList<string>? staticDependencies = null,
        IReadOnlyList<string>? dynamicValidationSources = null)
    {
        return new UcliStaticSchemaRegistration(
            name,
            RootRelativePath.Parse(path),
            UcliStaticSchemaKind.UserInputDocument,
            IpcJsonSerializerOptions.StrictPropertyNames.GetTypeInfo(typeof(T)),
            new UcliStaticSchemaManifestMetadata(
                Command: null,
                Status: null,
                usages,
                staticDependencies,
                dynamicValidationSources));
    }

    private static UcliStaticSchemaManifestMetadata? CreatePayloadMetadata (
        string? command,
        CommandResultStatus? status)
    {
        return command == null && status == null
            ? null
            : new UcliStaticSchemaManifestMetadata(command, status);
    }

    private static UcliStaticSchemaUsage StandardInputUsage (string command)
    {
        return new UcliStaticSchemaUsage
        {
            Command = command,
            Delivery = UcliStaticSchemaDelivery.StandardInput,
            Locator = null,
        };
    }

    private static JsonTypeInfo ResolveTypeInfo<T> ()
    {
        return CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(T));
    }
}
