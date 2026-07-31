using System.Text.Json.Serialization.Metadata;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

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
            UcliStaticSchemaRegistration.SchemaSetMetadata(
                "schema.manifest",
                RootRelativePath.Parse("schema-manifest.schema.json"),
                ResolveTypeInfo<UcliStaticSchemaManifest>()),
            UcliStaticSchemaRegistration.CliOutputEnvelope(
                "cli-output.envelope",
                RootRelativePath.Parse("cli-output/envelope.schema.json"),
                CliOutputJsonSerializerOptions.Default.GetTypeInfo(
                    UcliNonNullJsonObject.MakeValueType(typeof(CommandResult)))),
        ];
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateRequestRegistrations ()
    {
        return
        [
            UcliStaticSchemaRegistration.UserInputDocument(
                "request.envelope",
                RootRelativePath.Parse("request/request-envelope.schema.json"),
                IpcJsonSerializerOptions.StrictPropertyNames.GetTypeInfo(
                    typeof(UcliRequestJsonContract)),
                usages:
                [
                    StandardInputUsage(UcliCommandNames.Call),
                    StandardInputUsage(UcliCommandNames.Plan),
                    StandardInputUsage(UcliCommandNames.Validate),
                ],
                staticDependencies: Array.Empty<string>(),
                dynamicValidationSources: [UcliCommandNames.OpsDescribe]),
        ];
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateCommonReferenceRegistrations ()
    {
        return
        [
            UcliStaticSchemaRegistration.CommonDefinition(
                "common.artifact-ref",
                RootRelativePath.Parse("common/artifact-ref.schema.json"),
                CliOutputJsonSerializerOptions.Default.GetTypeInfo(
                    UcliNonNullJsonObject.MakeValueType(typeof(ArtifactRef)))),
            UcliStaticSchemaRegistration.CommonDefinition(
                "common.execution-ref",
                RootRelativePath.Parse("common/execution-ref.schema.json"),
                CliOutputJsonSerializerOptions.Default.GetTypeInfo(
                    UcliNonNullJsonObject.MakeValueType(typeof(ExecutionRef)))),
            UcliStaticSchemaRegistration.CommonDefinition(
                "common.lifecycle-execution-terminal-record",
                RootRelativePath.Parse("common/lifecycle-execution-terminal-record.schema.json"),
                CliOutputJsonSerializerOptions.Default.GetTypeInfo(
                    UcliNonNullJsonObject.MakeValueType(typeof(LifecycleExecutionTerminalRecord)))),
        ];
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
