using MackySoft.Ucli.Application.Features.Play.UseCases.Status;
using MackySoft.Ucli.Contracts.Schemas;
using MackySoft.Ucli.Hosting.Cli.Codes;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Hosting.Cli.Play;
using MackySoft.Ucli.Hosting.Cli.Schemas;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for catalog, Schema delivery, and play commands. </summary>
internal static class UcliCatalogCommandOutputContracts
{
    internal static UcliCommandOutputContract OpsList { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.OpsList,
            OpsCommandResultFactory.ListSuccessPayloadTypeInfo,
            OpsCommandResultFactory.ErrorPayloadTypeInfo,
            OpsCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract OpsDescribe { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.OpsDescribe,
            OpsCommandResultFactory.DescribeSuccessPayloadTypeInfo,
            OpsCommandResultFactory.ErrorPayloadTypeInfo,
            OpsCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract SchemaList { get; } =
        CreateSchema<UcliStaticSchemaManifest>(UcliCommandNames.SchemaList);

    internal static UcliCommandOutputContract SchemaGet { get; } =
        CreateSchema<UcliSchemaGetPayload>(UcliCommandNames.SchemaGet);

    internal static UcliCommandOutputContract SchemaExport { get; } =
        CreateSchema<UcliSchemaExportPayload>(UcliCommandNames.SchemaExport);

    internal static UcliCommandOutputContract CodesList { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.CodesList,
            CodeCatalogPayloadProjector.ListPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract CodesDescribe { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.CodesDescribe,
            CodeCatalogPayloadProjector.DescribePayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract PlayStatus { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.PlayStatus,
            UcliCommandOutputContracts.ResolveTypeInfo<PlayStatusExecutionOutput>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);

    internal static UcliCommandOutputContract PlayEnter { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.PlayEnter,
            PlayEnterCommandResultFactory.SuccessPayloadTypeInfo,
            PlayEnterCommandResultFactory.ErrorPayloadTypeInfo,
            PlayEnterCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract PlayExit { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.PlayExit,
            PlayExitCommandResultFactory.SuccessPayloadTypeInfo,
            PlayExitCommandResultFactory.ErrorPayloadTypeInfo,
            PlayExitCommandResultFactory.CreateEmptyErrorPayload);

    private static UcliCommandOutputContract CreateSchema<T> (string command)
    {
        return UcliCommandOutputContracts.Complete(
            command,
            UcliCommandOutputContracts.ResolveTypeInfo<T>(),
            UcliCommandOutputContracts.EmptyPayloadTypeInfo,
            UcliCommandOutputContracts.EmptyPayload);
    }
}
