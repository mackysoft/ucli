using System.Text.Json;
using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class EvalCommandTestData
{
    public const string EvalSource = "context.DeclareNoChanges(); return new { ok = true };";

    private static readonly Sha256Digest SourceDigest = Sha256Digest.Parse(new string('a', 64));
    private static readonly Sha256Digest ExecutionDigest = Sha256Digest.Parse(new string('b', 64));

    public static EvalServiceResult CreateSuccessfulServiceResult (Guid requestId, CsEvalSourceKind sourceKind = CsEvalSourceKind.Snippet)
    {
        var plan = CreatePlan(sourceKind);
        var project = plan.Project;
        var compile = ((CsEvalPlanSuccessResult)plan.Eval).Compile;
        var readPostcondition = CreateCallReadPostcondition();
        var call = new IpcEvalResponse(
            project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Applied,
            new CsEvalCallSuccessResult(
                SourceDigest,
                sourceKind,
                "Snippet.Run",
                ExecutionDigest,
                compile,
                durationMilliseconds: 7,
                logs: [],
                CsEvalReturnValue.Json(JsonSerializer.SerializeToElement(new { ok = true })),
                new CsEvalTouchedResources(
                    noChanges: true,
                    scenes: [],
                    prefabs: [],
                    assets: [],
                    projectSettings: [])),
            null,
            readPostcondition);
        return EvalServiceResult.FromUnityResult(requestId, plan.Project, UnityEvalExecutionResult.Success(plan, call));
    }

    public static EvalServiceResult CreateCallFailureServiceResult (Guid requestId)
    {
        var plan = CreatePlan(CsEvalSourceKind.Snippet);
        return EvalServiceResult.FromUnityResult(
            requestId,
            plan.Project,
            UnityEvalExecutionResult.CallFailure(
                plan,
                ExecutionError.Timeout("Timed out while waiting for eval.call."),
                callWasSent: true));
    }

    public static EvalServiceResult CreatePreEntryCallFailureServiceResult (Guid requestId)
    {
        var plan = CreatePlan(CsEvalSourceKind.Snippet);
        var partial = CreatePartialResult(plan);
        var errorResponse = new IpcEvalErrorResponse(
            plan.Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.NotApplied,
            partial,
            null);
        return EvalServiceResult.FromUnityResult(
            requestId,
            plan.Project,
            UnityEvalExecutionResult.CallFailure(
                plan,
                ExecutionError.InvalidArgument("eval.call was rejected before entry invocation."),
                callWasSent: true,
                errorResponse));
    }

    public static EvalServiceResult CreatePostEntryCallFailureServiceResult (Guid requestId)
    {
        var plan = CreatePlan(CsEvalSourceKind.Snippet);
        var partial = CreatePartialResult(plan);
        var errorResponse = new IpcEvalErrorResponse(
            plan.Project,
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            partial,
            CreateCallReadPostcondition());
        return EvalServiceResult.FromUnityResult(
            requestId,
            plan.Project,
            UnityEvalExecutionResult.CallFailure(
                plan,
                ExecutionError.InvalidArgument("eval.call entry point failed."),
                callWasSent: true,
                errorResponse));
    }

    private static IpcEvalResponse CreatePlan (CsEvalSourceKind sourceKind)
    {
        var project = new UnityProjectIdentity(Path.GetFullPath("UnityProject"), ProjectFingerprintTestFactory.Create("project-fingerprint"), "6000.1.4f1");
        var compile = new CsEvalPlanCompileResult(succeeded: true, diagnostics: []);
        return new IpcEvalResponse(
            project,
            CsEvalPhase.Plan,
            ExecutionApplicationState.NotApplied,
            new CsEvalPlanSuccessResult(SourceDigest, sourceKind, "Snippet.Run", ExecutionDigest, compile),
            "plan-token-1",
            null);
    }

    private static CsEvalPartialErrorResult CreatePartialResult (IpcEvalResponse plan)
    {
        var result = (CsEvalPlanSuccessResult)plan.Eval;
        return new CsEvalPartialErrorResult(
            result.SourceDigest,
            result.SourceKind,
            result.ResolvedEntryPoint,
            result.ExecutionDigest,
            result.Compile,
            null,
            null,
            null,
            null);
    }

    private static ExecutionReadPostcondition CreateCallReadPostcondition () => new(
    [
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.AssetSearch, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.GuidPath, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.SceneTreeLite, DateTimeOffset.UnixEpoch, null),
    ]);
}
