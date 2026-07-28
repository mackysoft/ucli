using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Tests.Cli;

internal static class OpsCommandTestSupport
{
    public static OpsListServiceResult CreateListSuccess (params OpsOperationListItem[] operations)
    {
        return OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations: operations,
                ReadIndex: CreateProbableReadIndex()),
            "uCLI ops list completed.");
    }

    public static RecordingOpsService CreateService ()
    {
        return new RecordingOpsService(
            CreateListSuccess(),
            CreateDefaultDescribeSuccess());
    }

    public static OpsDescribeServiceResult CreateDefaultDescribeSuccess ()
    {
        var operationName = UcliPrimitiveOperationNames.GoDescribe;
        var description = "Returns a GameObject description including components and child hierarchy.";
        var assurance = OpsCliOutputContractTestSupport.CreateAssurance(
            UcliOperationKind.Query,
            OperationPolicy.Safe);
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            operationName,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GoDescribeArgs)),
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        var describe = UcliOperationDescribeContractBuilder.Create(
            generationResult,
            description,
            assurance);

        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    name: operationName,
                    kind: UcliOperationKind.Query,
                    policy: OperationPolicy.Safe,
                    playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                    description: description,
                    argsContract: describe.ArgsContract!.Value,
                    resultContract: describe.ResultContract,
                    assurance: assurance,
                    codeContract: null),
                ReadIndex: CreateProbableReadIndex()),
            $"uCLI ops describe completed for '{operationName}'.");
    }

    private static ReadIndexInfo CreateProbableReadIndex ()
    {
        return new ReadIndexInfo(
            true,
            true,
            ReadIndexInfoSource.Index,
            IndexFreshness.Probable,
            DateTimeOffset.Parse("2026-03-07T00:00:00+00:00"),
            null);
    }

}
