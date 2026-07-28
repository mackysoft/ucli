using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Hosting.Cli.Requests.Projection;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup.OutputContracts;

/// <summary> Defines output contracts for request and query commands. </summary>
internal static class UcliRequestCommandOutputContracts
{
    internal static UcliCommandOutputContract Refresh { get; } =
        CreateOperationExecution(UcliCommandNames.Refresh);

    internal static UcliCommandOutputContract Resolve { get; } =
        CreateReadIndexRequest(UcliCommandNames.Resolve);

    internal static UcliCommandOutputContract Validate { get; } =
        UcliCommandOutputContracts.Complete(
            UcliCommandNames.Validate,
            ValidateCommandResultFactory.SuccessPayloadTypeInfo,
            ValidateCommandResultFactory.ErrorPayloadTypeInfo,
            ValidateCommandResultFactory.CreateEmptyErrorPayload);

    internal static UcliCommandOutputContract Plan { get; } =
        UcliCommandOutputContracts.Complete(
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

    private static UcliCommandOutputContract CreateOperationExecution (string command)
    {
        return UcliCommandOutputContracts.Complete(
            command,
            UcliCommandOutputContracts.ResolveTypeInfo<OperationExecutionCommandPayload>(),
            CommandErrorPayload.TypeInfo<OperationExecutionCommandPayload>(),
            CommandErrorPayload.Empty<OperationExecutionCommandPayload>);
    }

    private static UcliCommandOutputContract CreateReadIndexRequest (string command)
    {
        return UcliCommandOutputContracts.Complete(
            command,
            UcliCommandOutputContracts.ResolveTypeInfo<ReadIndexRequestCommandPayload>(),
            CommandErrorPayload.TypeInfo<ReadIndexRequestCommandPayload>(),
            CommandErrorPayload.Empty<ReadIndexRequestCommandPayload>);
    }

    private static UcliCommandOutputContract CreateCall (string command)
    {
        return UcliCommandOutputContracts.Complete(
            command,
            CallExecutionPayloadProjector.SuccessPayloadTypeInfo,
            CallExecutionPayloadProjector.ErrorPayloadTypeInfo,
            CallExecutionPayloadProjector.CreateEmptyError);
    }
}
