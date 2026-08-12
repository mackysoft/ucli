using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    internal sealed class EvalPlanUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly CsEvalCompilationService compilationService;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly UnityProjectIdentity project;
        private readonly IPlanTokenEnvironment tokenEnvironment;

        public EvalPlanUnityIpcMethodHandler (CsEvalCompilationService compilationService, IUnityEditorReadinessGate readinessGate, UnityProjectIdentity project, IPlanTokenEnvironment tokenEnvironment)
        {
            this.compilationService = compilationService ?? throw new ArgumentNullException(nameof(compilationService));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.tokenEnvironment = tokenEnvironment ?? throw new ArgumentNullException(nameof(tokenEnvironment));
        }

        public UnityIpcMethod Method => UnityIpcMethod.EvalPlan;

        public async ValueTask<IpcResponse> HandleAsync (ValidatedUnityIpcRequest request, IpcRequestCancellation cancellation)
        {
            if (!UnityIpcRequestCodec.TryDecodeEvalPlanRequest(request, out var payload, out var error)) return error!;
            var config = await EvalConfigResolver.ResolveAsync(tokenEnvironment, cancellation.Token);
            if (config != EvalConfigResolution.Enabled)
            {
                return CreateConfigError(request, config);
            }
            if (!payload!.AllowDangerous) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval requires allowDangerous=true.", null);
            var ready = await readinessGate.EnsureExecutionReadyAsync(true, cancellation.Token, payload.AllowPlayMode);
            if (!ready.IsReady) return CreateError(request, ready.Error!.Code, ready.Error.Message, null);
            var compilation = compilationService.CompileAndValidate(payload.Source, payload.SourceKind, payload.AllowDangerous, payload.AllowPlayMode, cancellation.Token);
            if (!compilation.IsSuccess) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, compilation.FailureMessage!, new CsEvalPartialErrorResult(compilation.SourceDigest, compilation.SourceKind, compilation.ResolvedEntryPoint, compilation.ExecutionDigest, compilation.Compile, null, null, null, null));
            if (!TryIssuePlanToken(compilation, payload.AllowPlayMode, out var token, out var tokenError))
            {
                return CreateError(request, UcliCoreErrorCodes.InternalError, tokenError!, null);
            }

            var result = new CsEvalPlanSuccessResult(compilation.SourceDigest, payload.SourceKind, compilation.ResolvedEntryPoint!, compilation.ExecutionDigest, compilation.Compile);
            return UnityIpcResponseFactory.CreateSuccessResponse(request, new IpcEvalResponse(project, CsEvalPhase.Plan, ExecutionApplicationState.NotApplied, result, token, null));
        }

        private IpcResponse CreateError (ValidatedUnityIpcRequest request, UcliCode code, string message, CsEvalPartialErrorResult? partial)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                code,
                message,
                null,
                new IpcEvalErrorResponse(project, CsEvalPhase.Plan, ExecutionApplicationState.NotApplied, partial, null));
        }

        private IpcResponse CreateConfigError (ValidatedUnityIpcRequest request, EvalConfigResolution config)
        {
            return config switch
            {
                EvalConfigResolution.Invalid => CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval configuration is invalid.", null),
                EvalConfigResolution.Unavailable => CreateError(request, UcliCoreErrorCodes.InternalError, "C# eval configuration could not be loaded.", null),
                _ => CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval is disabled by config evalEnabled=false.", null),
            };
        }

        private bool TryIssuePlanToken (CsEvalCompilationResult compilation, bool allowPlayMode, out string? token, out string? error)
        {
            try
            {
                var snapshot = tokenEnvironment.Capture();
                if (!PlanTokenKeyStore.TryLoadOrCreate(snapshot, out var key, out error))
                {
                    token = null;
                    return false;
                }

                var issuedAtUtc = tokenEnvironment.UtcNow;
                var stateFingerprint = MackySoft.Ucli.Contracts.Cryptography.Sha256Digest.Compute(
                    System.Text.Encoding.UTF8.GetBytes(snapshot.DomainReloadGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                token = PlanTokenCompactCodec.CreateSignedToken(key, new PlanTokenPayload(
                    snapshot.ProjectFingerprint,
                    compilation.SourceDigest,
                    compilation.ExecutionDigest,
                    stateFingerprint,
                    issuedAtUtc,
                    issuedAtUtc.AddMinutes(15),
                    PlanTokenNonce.Create(),
                    new EvalPlanTokenClaims(compilation.SourceKind, EvalEnabled: true, AllowDangerous: true, AllowPlayMode: allowPlayMode)));
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                token = null;
                error = $"Failed to issue eval plan token. {exception.Message}";
                return false;
            }
        }
    }
}
