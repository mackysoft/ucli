using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    internal sealed class EvalCallUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly CsEvalCompilationService compilationService;
        private readonly CsEvalEntryPointReflectionResolver entryPointResolver;
        private readonly CsEvalReturnValueSerializer returnValueSerializer;
        private readonly IUnityEditorReadinessGate readinessGate;
        private readonly UnityProjectIdentity project;
        private readonly IPlanTokenEnvironment tokenEnvironment;
        private readonly MutationReadPostconditionJournal admissionJournal;

        public EvalCallUnityIpcMethodHandler (CsEvalCompilationService compilationService, CsEvalEntryPointReflectionResolver entryPointResolver, CsEvalReturnValueSerializer returnValueSerializer, IUnityEditorReadinessGate readinessGate, UnityProjectIdentity project, IPlanTokenEnvironment tokenEnvironment, MutationReadPostconditionJournal admissionJournal)
        {
            this.compilationService = compilationService ?? throw new ArgumentNullException(nameof(compilationService));
            this.entryPointResolver = entryPointResolver ?? throw new ArgumentNullException(nameof(entryPointResolver));
            this.returnValueSerializer = returnValueSerializer ?? throw new ArgumentNullException(nameof(returnValueSerializer));
            this.readinessGate = readinessGate ?? throw new ArgumentNullException(nameof(readinessGate));
            this.project = project ?? throw new ArgumentNullException(nameof(project));
            this.tokenEnvironment = tokenEnvironment ?? throw new ArgumentNullException(nameof(tokenEnvironment));
            this.admissionJournal = admissionJournal ?? throw new ArgumentNullException(nameof(admissionJournal));
        }

        public UnityIpcMethod Method => UnityIpcMethod.EvalCall;

        public async ValueTask<IpcResponse> HandleAsync (ValidatedUnityIpcRequest request, IpcRequestCancellation cancellation)
        {
            if (!UnityIpcRequestCodec.TryDecodeEvalCallRequest(request, out var payload, out var error)) return error!;
            var config = await EvalConfigResolver.ResolveAsync(tokenEnvironment, cancellation.Token);
            if (config != EvalConfigResolution.Enabled)
            {
                return CreateConfigError(request, config, ExecutionApplicationState.NotApplied);
            }
            if (!payload!.AllowDangerous || string.IsNullOrWhiteSpace(payload.PlanToken)) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval plan token is required.", null, ExecutionApplicationState.NotApplied);
            var ready = await readinessGate.EnsureExecutionReadyAsync(true, cancellation.Token, payload.AllowPlayMode);
            if (!ready.IsReady) return CreateError(request, ready.Error!.Code, ready.Error.Message, null, ExecutionApplicationState.NotApplied);
            var compilation = compilationService.CompileAndValidate(payload.Source, payload.SourceKind, payload.AllowDangerous, payload.AllowPlayMode, cancellation.Token);
            if (!compilation.IsSuccess) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval call source is invalid.", CreatePartial(compilation), ExecutionApplicationState.NotApplied);
            var tokenValidation = await ValidatePlanTokenAsync(payload.PlanToken, compilation, payload.AllowDangerous, payload.AllowPlayMode, cancellation.Token);
            if (!tokenValidation.IsValid) return CreateError(request, tokenValidation.ErrorCode!, tokenValidation.Error!, CreatePartial(compilation), ExecutionApplicationState.NotApplied);
            if (!compilationService.TryEmitAssembly(compilation.Compilation, cancellation.Token, out var bytes, out _, out var emitError)) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, emitError, CreatePartial(compilation), ExecutionApplicationState.NotApplied);
            // Emission may take long enough for the Editor generation or eval configuration to change.
            // Admission must use the snapshot that was revalidated after emission, never a stale plan snapshot.
            tokenValidation = await ValidatePlanTokenAsync(payload.PlanToken, compilation, payload.AllowDangerous, payload.AllowPlayMode, cancellation.Token);
            if (!tokenValidation.IsValid) return CreateError(request, tokenValidation.ErrorCode!, tokenValidation.Error!, CreatePartial(compilation), ExecutionApplicationState.NotApplied);
            var admission = await admissionJournal.TryAdmitEvalCallAsync(
                tokenValidation.Snapshot!.RepositoryRoot,
                tokenValidation.Snapshot.ProjectFingerprint,
                new EvalCallAdmission(
                    tokenValidation.DecodedToken!.Payload.Nonce.ToString(),
                    MackySoft.Ucli.Contracts.Cryptography.Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes(payload.PlanToken)),
                    request.RequestId,
                    compilation.SourceDigest,
                    compilation.ExecutionDigest,
                    tokenValidation.Snapshot.DomainReloadGeneration,
                    tokenValidation.DecodedToken.Payload.IssuedAtUtc,
                    tokenValidation.DecodedToken.Payload.ExpiresAtUtc),
                cancellation.Token);
            if (!admission.IsAdmitted)
            {
                var message = admission.IsReplay
                    ? "C# eval plan token has already been consumed."
                    : admission.Failure!.Message;
                return CreateError(request, UcliCoreErrorCodes.InvalidArgument, message, CreatePartial(compilation), ExecutionApplicationState.NotApplied);
            }

            MethodInfo method;
            try
            {
                if (!entryPointResolver.TryResolve(Assembly.Load(bytes), compilation.EntryPointName!.Value, out method, out var entryPointError)) return CreateError(request, UcliCoreErrorCodes.InvalidArgument, entryPointError, CreatePartial(compilation), ExecutionApplicationState.Indeterminate, admission.ReadPostcondition);
            }
            catch (Exception exception)
            {
                return CreateError(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.InnerException?.Message ?? exception.Message,
                    CreatePartial(compilation),
                    ExecutionApplicationState.Indeterminate,
                    admission.ReadPostcondition);
            }
            var context = new UcliCsEvalContext(cancellation.Token);
            var stopwatch = Stopwatch.StartNew();
            object value;
            try
            {
                var invoked = method.Invoke(null, new object[] { context });
                value = await CsEvalEntryPointReturnValueResolver.ResolveAsync(
                    method.ReturnType,
                    invoked,
                    cancellation.Token,
                    static (_, _) => { });
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                return CreateError(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    exception.InnerException?.Message ?? exception.Message,
                    CreatePartial(
                        compilation,
                        stopwatch.ElapsedMilliseconds,
                        context.Logs,
                        null,
                        context.HasImpactDeclaration ? CsEvalTouchedResourceMapper.CreateResult(context) : null),
                    ExecutionApplicationState.Indeterminate,
                    admission.ReadPostcondition);
            }

            stopwatch.Stop();
            if (!context.HasImpactDeclaration)
            {
                return CreateError(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "C# eval entry point must call DeclareNoChanges or one of the Touch* methods before completing.",
                    new CsEvalPartialErrorResult(
                        compilation.SourceDigest,
                        compilation.SourceKind,
                        compilation.ResolvedEntryPoint,
                        compilation.ExecutionDigest,
                        compilation.Compile,
                        stopwatch.ElapsedMilliseconds,
                        context.Logs,
                        null,
                        null),
                    ExecutionApplicationState.Indeterminate,
                    admission.ReadPostcondition);
            }
            var touched = CsEvalTouchedResourceMapper.CreateResult(context);
            if (!returnValueSerializer.TrySerialize(value, out var returnValue, out var returnError))
            {
                return CreateError(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    returnError,
                    CreatePartial(compilation, stopwatch.ElapsedMilliseconds, context.Logs, null, touched),
                    ExecutionApplicationState.Indeterminate,
                    admission.ReadPostcondition);
            }

            // A completed entry point is not safely reportable as applied when its Editor generation
            // (or the validated eval configuration) changed while it was running.
            tokenValidation = await ValidatePlanTokenAsync(payload.PlanToken, compilation, payload.AllowDangerous, payload.AllowPlayMode, cancellation.Token);
            if (!tokenValidation.IsValid)
            {
                return CreateError(
                    request,
                    tokenValidation.ErrorCode!,
                    tokenValidation.Error!,
                    CreatePartial(compilation, stopwatch.ElapsedMilliseconds, context.Logs, returnValue, touched),
                    ExecutionApplicationState.Indeterminate,
                    admission.ReadPostcondition);
            }

            var result = new CsEvalCallSuccessResult(compilation.SourceDigest, payload.SourceKind, compilation.ResolvedEntryPoint!, compilation.ExecutionDigest, compilation.Compile, stopwatch.ElapsedMilliseconds, context.Logs, returnValue, touched);
            return UnityIpcResponseFactory.CreateSuccessResponse(request, new IpcEvalResponse(project, CsEvalPhase.Call, ExecutionApplicationState.Applied, result, null, admission.ReadPostcondition));
        }

        private static CsEvalPartialErrorResult CreatePartial (CsEvalCompilationResult compilation)
        {
            return new CsEvalPartialErrorResult(compilation.SourceDigest, compilation.SourceKind, compilation.ResolvedEntryPoint, compilation.ExecutionDigest, compilation.Compile, null, null, null, null);
        }

        private static CsEvalPartialErrorResult CreatePartial (
            CsEvalCompilationResult compilation,
            long durationMilliseconds,
            System.Collections.Generic.IReadOnlyList<CsEvalLogEntry> logs,
            CsEvalReturnValue? returnValue,
            CsEvalTouchedResources? touchedResources)
        {
            return new CsEvalPartialErrorResult(
                compilation.SourceDigest,
                compilation.SourceKind,
                compilation.ResolvedEntryPoint,
                compilation.ExecutionDigest,
                compilation.Compile,
                durationMilliseconds,
                logs,
                returnValue,
                touchedResources);
        }

        private IpcResponse CreateError (ValidatedUnityIpcRequest request, UcliCode code, string message, CsEvalPartialErrorResult? partial, ExecutionApplicationState applicationState, ExecutionReadPostcondition? readPostcondition = null)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                code,
                message,
                null,
                new IpcEvalErrorResponse(project, CsEvalPhase.Call, applicationState, partial, readPostcondition));
        }

        private IpcResponse CreateConfigError (ValidatedUnityIpcRequest request, EvalConfigResolution config, ExecutionApplicationState applicationState)
        {
            return config switch
            {
                EvalConfigResolution.Invalid => CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval configuration is invalid.", null, applicationState),
                EvalConfigResolution.Unavailable => CreateError(request, UcliCoreErrorCodes.InternalError, "C# eval configuration could not be loaded.", null, applicationState),
                _ => CreateError(request, UcliCoreErrorCodes.InvalidArgument, "C# eval is disabled by config evalEnabled=false.", null, applicationState),
            };
        }

        private async ValueTask<EvalPlanTokenValidation> ValidatePlanTokenAsync (string token, CsEvalCompilationResult compilation, bool allowDangerous, bool allowPlayMode, System.Threading.CancellationToken cancellationToken)
        {
            if (!PlanTokenCompactCodec.TryDecodeToken(token, out var decoded))
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token is invalid.");
            }

            var snapshot = tokenEnvironment.Capture();
            if (!PlanTokenKeyStore.TryLoadOrCreate(snapshot, out var key, out var keyError))
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, keyError!);
            }
            if (!PlanTokenCompactCodec.VerifySignature(decoded, key)
                || decoded.Payload.ProjectFingerprint != snapshot.ProjectFingerprint
                || decoded.Payload.RequestDigest != compilation.SourceDigest
                || decoded.Payload.CompiledExecutionDigest != compilation.ExecutionDigest)
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token does not match the current request.");
            }

            if (tokenEnvironment.UtcNow > decoded.Payload.ExpiresAtUtc)
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token has expired.");
            }

            var claims = decoded.Payload.EvalClaims;
            var config = await EvalConfigResolver.ResolveAsync(tokenEnvironment, cancellationToken);
            if (config != EvalConfigResolution.Enabled)
            {
                return config switch
                {
                    EvalConfigResolution.Invalid => EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval configuration is invalid."),
                    EvalConfigResolution.Unavailable => EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InternalError, "C# eval configuration could not be loaded."),
                    _ => EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token does not match the current eval configuration or request flags."),
                };
            }

            if (claims is null
                || !claims.EvalEnabled
                || claims.SourceKind != compilation.SourceKind
                || claims.AllowDangerous != allowDangerous
                || claims.AllowPlayMode != allowPlayMode)
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token does not match the current eval configuration or request flags.");
            }

            var generation = MackySoft.Ucli.Contracts.Cryptography.Sha256Digest.Compute(
                System.Text.Encoding.UTF8.GetBytes(snapshot.DomainReloadGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (decoded.Payload.StateFingerprint != generation)
            {
                return EvalPlanTokenValidation.Failure(UcliCoreErrorCodes.InvalidArgument, "C# eval plan token was issued for a previous Editor generation.");
            }

            return EvalPlanTokenValidation.Success(decoded, snapshot);
        }

        private sealed record EvalPlanTokenValidation (PlanTokenDecodedToken? DecodedToken, PlanTokenEnvironmentSnapshot? Snapshot, UcliCode? ErrorCode, string? Error)
        {
            public bool IsValid => ErrorCode is null;

            public static EvalPlanTokenValidation Success (PlanTokenDecodedToken decodedToken, PlanTokenEnvironmentSnapshot snapshot) => new(decodedToken, snapshot, null, null);

            public static EvalPlanTokenValidation Failure (UcliCode errorCode, string error) => new(null, null, errorCode, error);
        }
    }
}
