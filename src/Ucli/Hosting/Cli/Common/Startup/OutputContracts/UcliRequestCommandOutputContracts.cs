using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for request and query commands. </summary>
internal static class UcliRequestCommandOutputContracts
{
    internal static UcliCommandOutputContract Refresh { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Refresh,
            RefreshCommandResultFactory.SuccessPayloadTypeInfo,
            RefreshCommandResultFactory.ErrorPayloadTypeInfo,
            RefreshCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Resolve { get; } =
        CreateReadIndexRequest(UcliCommandNames.Resolve);

    internal static UcliCommandOutputContract Validate { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Validate,
            ValidateCommandResultFactory.SuccessPayloadTypeInfo,
            ValidateCommandResultFactory.ErrorPayloadTypeInfo,
            ValidateCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Plan { get; } =
        UcliCommandOutputContracts.OperationExecution(
            UcliCommandNames.Plan,
            PlanCommandResultFactory.SuccessPayloadTypeInfo,
            PlanCommandResultFactory.ErrorPayloadTypeInfo,
            PlanCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Call { get; } =
        CreateCall(UcliCommandNames.Call);

    internal static UcliCommandOutputContract Eval { get; } =
        CreateCall(UcliCommandNames.Eval);

    internal static UcliCommandOutputContract QueryAssetsFind { get; } =
        CreateReadIndexRequest(UcliCommandNames.QueryAssetsFind);

    internal static UcliCommandOutputContract QuerySceneTree { get; } =
        CreateReadIndexRequest(UcliCommandNames.QuerySceneTree);

    internal static UcliCommandOutputContract QueryGoDescribe { get; } =
        CreateReadIndexRequest(UcliCommandNames.QueryGoDescribe);

    internal static UcliCommandOutputContract QueryCompSchema { get; } =
        CreateReadIndexRequest(UcliCommandNames.QueryCompSchema);

    internal static UcliCommandOutputContract QueryAssetSchema { get; } =
        CreateReadIndexRequest(UcliCommandNames.QueryAssetSchema);

    private static UcliCommandOutputContract CreateReadIndexRequest (string command)
    {
        return UcliCommandOutputContracts.OperationExecution(
            command,
            UcliCommandOutputContracts.ResolveTypeInfo<ReadIndexRequestCommandPayload>(),
            CommandErrorPayload.TypeInfo<ReadIndexRequestCommandPayload>(),
            CommandErrorPayload.Empty<ReadIndexRequestCommandPayload>);
    }

    private static UcliCommandOutputContract CreateCall (string command)
    {
        return UcliCommandOutputContracts.OperationExecution(
            command,
            CallExecutionPayloadProjector.SuccessPayloadTypeInfo,
            CallExecutionPayloadProjector.ErrorPayloadTypeInfo,
            CallExecutionPayloadProjector.CreateEmptyError);
    }
}
