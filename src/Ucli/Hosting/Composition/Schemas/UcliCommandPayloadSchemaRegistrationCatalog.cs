using System.Text.Json.Serialization.Metadata;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary>
/// Registers the effective success and error payload contracts from the public command catalog.
/// </summary>
internal static class UcliCommandPayloadSchemaRegistrationCatalog
{
    private static readonly IReadOnlyList<UcliStaticSchemaRegistration> Registrations =
        CreateRegistrations();

    public static IReadOnlyList<UcliStaticSchemaRegistration> GetAll ()
    {
        return Registrations;
    }

    private static IReadOnlyList<UcliStaticSchemaRegistration> CreateRegistrations ()
    {
        var outputContracts = UcliCommandCatalog.OutputContracts;
        var registrations = new List<UcliStaticSchemaRegistration>(
            outputContracts.Count * 2);

        for (var i = 0; i < outputContracts.Count; i++)
        {
            var outputContract = outputContracts[i];
            if (outputContract.SuccessPayloadTypeInfo != null)
            {
                registrations.Add(Create(
                    outputContract.SuccessPayloadTypeInfo,
                    outputContract.Command,
                    CommandResultStatus.Ok,
                    outputContract.HasOperationApplicationStateConstraints));
            }

            registrations.Add(Create(
                outputContract.ErrorPayloadTypeInfo,
                outputContract.Command,
                CommandResultStatus.Error,
                outputContract.HasOperationApplicationStateConstraints));
        }

        return registrations.AsReadOnly();
    }

    private static UcliStaticSchemaRegistration Create (
        JsonTypeInfo typeInfo,
        string command,
        CommandResultStatus status,
        bool hasOperationApplicationStateConstraints)
    {
        var statusText = TextVocabulary.GetText(status);
        var runtimePayloadType = UcliNonNullJsonObject.MakeValueType(typeInfo.Type);
        var name = "cli-output.payload." + command + "." + statusText;
        var path = RootRelativePath.Parse(
            "cli-output/payload/" + command + "." + statusText + ".schema.json");
        var runtimePayloadTypeInfo =
            CliOutputJsonSerializerOptions.Default.GetTypeInfo(runtimePayloadType);
        if (TextVocabulary.TryGetValue<LifecycleExecutionKind>(
            command,
            out var executionKind))
        {
            return UcliStaticSchemaRegistration
                .LifecycleExecutionCliOutputPayload(
                    name,
                    path,
                    runtimePayloadTypeInfo,
                    command,
                    executionKind,
                status);
        }

        if (hasOperationApplicationStateConstraints)
        {
            return UcliStaticSchemaRegistration
                .OperationExecutionCliOutputPayload(
                    name,
                    path,
                    runtimePayloadTypeInfo,
                    command,
                    status);
        }

        return UcliStaticSchemaRegistration.CliOutputPayload(
            name,
            path,
            runtimePayloadTypeInfo,
            command,
            status);
    }
}
