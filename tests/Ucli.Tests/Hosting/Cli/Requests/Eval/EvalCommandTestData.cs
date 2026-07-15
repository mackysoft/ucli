using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class EvalCommandTestData
{
    public const string EvalSource = "context.DeclareNoTouchedResources(); return new { ok = true };";

    public const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    private static readonly Guid RequestGuid = Guid.Parse(RequestId);

    private static readonly Sha256Digest SourceDigest = Sha256Digest.Parse(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

    private static readonly Sha256Digest ExecutionDigest = Sha256Digest.Parse(
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

    public static CallServiceResult CreateSuccessfulServiceResult ()
    {
        return CallServiceResult.Success(
            new CallExecutionOutput(
                requestId: RequestGuid,
                project: ProjectIdentityInfoTestFactory.Create(),
                opResults:
                [
                    CreateCallOperationResult(),
                ],
                plan: new CallPlanOutput(
                    opResults:
                    [
                        CreatePlanOperationResult(),
                    ],
                    planToken: "plan-token-1"),
                readPostcondition: null),
            "uCLI call completed.");
    }

    public static CallServiceResult CreateDangerousOperationRejectedResult ()
    {
        return CallServiceResult.Failure(
            "Static validation failed.",
            [
                ApplicationFailure.InvalidInput(
                    "Step 'eval' requires dangerous operation 'ucli.cs.eval'. Specify --allowDangerous to execute dangerous operations.",
                    OperationAuthorizationErrorCodes.OperationNotAllowed,
                    new IpcExecuteStepId("eval")),
            ]);
    }

    private static OperationExecutionOperationResult CreateCallOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("eval"),
            Op: UcliPrimitiveOperationNames.CsEval,
            Phase: IpcExecuteOperationPhase.Call,
            Applied: true,
            Changed: false,
            Touched: [])
        {
            Result = IpcPayloadCodec.SerializeToElement(
                new CsEvalResult(
                    SourceDigest,
                    UcliCodeSourceFormKind.Snippet,
                    "Snippet.Run",
                    ExecutionDigest,
                    CreateSuccessfulCompileResult(),
                    7,
                    [],
                    new CsEvalReturnValue(
                        CsEvalReturnValueKind.Json,
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                ok = true,
                            },
                            IpcJsonSerializerOptions.Default)),
                    new CsEvalTouchedResources(
                        CsEvalTouchedResourceState.None,
                        declared: null))),
        };
    }

    private static OperationExecutionOperationResult CreatePlanOperationResult ()
    {
        return new OperationExecutionOperationResult(
            OpId: new IpcExecuteStepId("eval"),
            Op: UcliPrimitiveOperationNames.CsEval,
            Phase: IpcExecuteOperationPhase.Plan,
            Applied: false,
            Changed: false,
            Touched: [])
        {
            Result = IpcPayloadCodec.SerializeToElement(
                new CsEvalResult(
                    SourceDigest,
                    UcliCodeSourceFormKind.Snippet,
                    "Snippet.Run",
                    ExecutionDigest,
                    CreateSuccessfulCompileResult(),
                    durationMilliseconds: null,
                    logs: null,
                    returnValue: null,
                    touchedResources: null)),
        };
    }

    private static CsEvalCompileResult CreateSuccessfulCompileResult ()
    {
        return new CsEvalCompileResult(
            CsEvalCompileStatus.Succeeded,
            diagnostics: []);
    }
}
