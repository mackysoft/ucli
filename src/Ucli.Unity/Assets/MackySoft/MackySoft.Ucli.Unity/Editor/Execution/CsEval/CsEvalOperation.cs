using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Implements <c>ucli.cs.eval</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CsEvalOperation : UcliOperation<CsEvalArgs, CsEvalResult>
    {
        private readonly CsEvalCompilationService compilationService;

        private readonly CsEvalEntryPointReflectionResolver entryPointResolver;

        private readonly CsEvalReturnValueSerializer returnValueSerializer;

        public CsEvalOperation (
            CsEvalCompilationService compilationService,
            CsEvalEntryPointReflectionResolver entryPointResolver,
            CsEvalReturnValueSerializer returnValueSerializer)
        {
            this.compilationService = compilationService ?? throw new ArgumentNullException(nameof(compilationService));
            this.entryPointResolver = entryPointResolver ?? throw new ArgumentNullException(nameof(entryPointResolver));
            this.returnValueSerializer = returnValueSerializer ?? throw new ArgumentNullException(nameof(returnValueSerializer));
        }

        public override UcliOperationMetadata Metadata { get; } = CreateMetadata();

        protected override Task<OperationPhaseStepResult> ValidateAsync (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> PlanAsync (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = compilationService.CompileAndValidate(args.Source, cancellationToken);
            if (!compilation.IsSuccess)
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, compilation.FailureMessage!, compilation.CreatePlanResult()));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(compilation.CreatePlanResult())));
        }

        protected override Task<OperationPhaseStepResult> CallAsync (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = compilationService.CompileAndValidate(args.Source, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!compilation.IsSuccess)
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, compilation.FailureMessage!, compilation.CreatePlanResult()));
            }

            if (!compilationService.TryEmitAssembly(compilation.Compilation, cancellationToken, out var assemblyBytes, out var emitDiagnostics, out var emitError))
            {
                var emitResult = new CsEvalResult(
                    compilation.SourceDigest,
                    compilation.SourceKind,
                    compilation.ResolvedEntryPoint,
                    compilation.ExecutionDigest,
                    new CsEvalCompileResult(CsEvalCompileStatusValues.Failed, emitDiagnostics),
                    durationMilliseconds: null,
                    logs: null,
                    returnValue: null,
                    touchedResources: null);
                return Task.FromResult(CreateInvalidArgumentFailure(operation, emitError, emitResult));
            }

            if (compilation.EntryPointName == null)
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, "C# eval source did not resolve an entry point.", compilation.CreatePlanResult()));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var assembly = Assembly.Load(assemblyBytes);
            cancellationToken.ThrowIfCancellationRequested();
            if (!entryPointResolver.TryResolve(assembly, compilation.EntryPointName.Value, out var method, out var entryPointError))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, entryPointError, compilation.CreatePlanResult()));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var context = new UcliCsEvalContext();
            var stopwatch = Stopwatch.StartNew();
            object? returnObject;
            try
            {
                returnObject = method.Invoke(null, new object[] { context });
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                stopwatch.Stop();
                return Task.FromResult(CreatePostInvocationInvalidArgumentFailure(
                    operation,
                    $"C# eval entry point threw {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}",
                    compilation,
                    stopwatch.ElapsedMilliseconds,
                    context));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                stopwatch.Stop();
                return Task.FromResult(CreatePostInvocationInvalidArgumentFailure(
                    operation,
                    $"C# eval entry point invocation failed. {exception.Message}",
                    compilation,
                    stopwatch.ElapsedMilliseconds,
                    context));
            }

            stopwatch.Stop();
            if (!returnValueSerializer.TrySerialize(returnObject, out var returnValue, out var returnValueError))
            {
                return Task.FromResult(CreatePostInvocationInvalidArgumentFailure(
                    operation,
                    returnValueError,
                    compilation,
                    stopwatch.ElapsedMilliseconds,
                    context));
            }

            return Task.FromResult(CreateCallSuccess(compilation, stopwatch.ElapsedMilliseconds, context, returnValue));
        }

        private static UcliOperationMetadata CreateMetadata ()
        {
            var assurance = new UcliOperationAssuranceContract(
                sideEffects: new[]
                {
                    UcliOperationSideEffect.SceneContentMutation,
                    UcliOperationSideEffect.PrefabContentMutation,
                    UcliOperationSideEffect.AssetContentMutation,
                    UcliOperationSideEffect.ProjectSettingsMutation,
                    UcliOperationSideEffect.SceneSave,
                    UcliOperationSideEffect.PrefabSave,
                    UcliOperationSideEffect.AssetSave,
                    UcliOperationSideEffect.ProjectSave,
                    UcliOperationSideEffect.ExternalProcess,
                    UcliOperationSideEffect.FilesystemWrite,
                    UcliOperationSideEffect.ArbitrarySourceExecution,
                    UcliOperationSideEffect.DestructiveScope,
                },
                touchedKinds: new[]
                {
                    UcliTouchedResourceKindNames.Scene,
                    UcliTouchedResourceKindNames.Prefab,
                    UcliTouchedResourceKindNames.Asset,
                    UcliTouchedResourceKindNames.ProjectSettings,
                },
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Compile the supplied C# source and validate the required entry point without invoking user code.",
                callSemantics: "Compile, emit, load, and execute the user C# entry point inside the Unity Editor process.",
                touchedContract: "Reports touched resources declared by user code through the eval context; declarations are caller-controlled and are not a complete guarantee of all Unity state changes.",
                readPostconditionContract: "Scene, prefab, asset, ProjectSettings, and readIndex surfaces may be stale after eval execution regardless of declared touched resources.",
                failureSemantics: "Compilation and entry-point failures occur before user code runs; once synchronous user code is invoked it cannot be forcibly stopped until it returns or throws.",
                dangerousNotes: new[]
                {
                    "Executes user C# inside the Unity Editor process and can mutate project state outside declared touched resources.",
                    "Synchronous user code cannot be forcibly stopped while it is executing.",
                });
            var describe = UcliOperationDescribeContractBuilder.Create<CsEvalArgs, CsEvalResult>(
                "Compiles and executes a C# source unit in the Unity editor process as a dangerous eval operation.",
                assurance);
            describe.CodeContract = UcliOperationCodeContractBuilder.CreateCSharp(
                CsEvalEntryPointName.RequiredSignature,
                CsEvalEntryPointName.MatchRule,
                requiredStatic: true,
                new[] { typeof(UcliCsEvalContext) },
                "Return null or a JSON-serializable object value; a snippet without return yields null. DTO and anonymous object serialization may execute public getters. Task, Task<T>, ValueTask, and ValueTask<T> are rejected.",
                new[]
                {
                    new UcliCodeSourceFormContract(
                        CsEvalSourceKindValues.CompilationUnit,
                        "Complete C# compilation unit containing using directives, namespace or type declarations, and exactly one public static object? Run(UcliCsEvalContext context) method."),
                    new UcliCodeSourceFormContract(
                        CsEvalSourceKindValues.Snippet,
                        "Run method body snippet. Leading using directives, statements, explicit return, no return, and one expression are accepted; snippets without return produce null."),
                },
                new[] { typeof(UcliCsEvalContext) });

            return UcliOperationMetadata.Create<CsEvalArgs, CsEvalResult>(
                operationName: UcliPrimitiveOperationNames.CsEval,
                kind: UcliOperationKind.Mutation,
                describeContract: describe,
                requiresPreCallPlanReplay: true);
        }

        private static CsEvalResult CreateCallResult (
            CsEvalCompilationResult compilation,
            long durationMilliseconds,
            UcliCsEvalContext context,
            CsEvalReturnValue? returnValue,
            CsEvalTouchedResources touchedResources)
        {
            return new CsEvalResult(
                compilation.SourceDigest,
                compilation.SourceKind,
                compilation.ResolvedEntryPoint,
                compilation.ExecutionDigest,
                compilation.Compile,
                durationMilliseconds,
                context.Logs,
                returnValue,
                touchedResources);
        }

        private static OperationPhaseStepResult CreateCallSuccess (
            CsEvalCompilationResult compilation,
            long durationMilliseconds,
            UcliCsEvalContext context,
            CsEvalReturnValue returnValue)
        {
            var touchedResources = CsEvalTouchedResourceMapper.CreateResult(context);
            var touched = CsEvalTouchedResourceMapper.CreateTouches(context);
            var changed = IsChanged(touchedResources);
            return OperationPhaseStepResult.Success(
                    applied: true,
                    changed: changed,
                    touched: touched,
                    result: IpcPayloadCodec.SerializeToElement(CreateCallResult(compilation, durationMilliseconds, context, returnValue, touchedResources)))
                .WithReadInvalidations(CreateReadInvalidations());
        }

        private static OperationPhaseStepResult CreateInvalidArgumentFailure (
            NormalizedOperation operation,
            string message,
            CsEvalResult result)
        {
            return OperationPhaseStepResult.Failed(
                new OperationFailure(
                    Code: UcliCoreErrorCodes.InvalidArgument,
                    Message: message,
                    OpId: operation.Id),
                result: IpcPayloadCodec.SerializeToElement(result));
        }

        private static OperationPhaseStepResult CreatePostInvocationInvalidArgumentFailure (
            NormalizedOperation operation,
            string message,
            CsEvalCompilationResult compilation,
            long durationMilliseconds,
            UcliCsEvalContext context)
        {
            var touchedResources = CsEvalTouchedResourceMapper.CreateResult(context);
            var touched = CsEvalTouchedResourceMapper.CreateTouches(context);
            var changed = IsChanged(touchedResources);
            return OperationPhaseStepResult.Failed(
                    new OperationFailure(
                        Code: UcliCoreErrorCodes.InvalidArgument,
                        Message: message,
                        OpId: operation.Id),
                    applied: true,
                    changed: changed,
                    touched: touched,
                    result: IpcPayloadCodec.SerializeToElement(CreateCallResult(compilation, durationMilliseconds, context, returnValue: null, touchedResources)))
                .WithReadInvalidations(CreateReadInvalidations());
        }

        private static bool IsChanged (CsEvalTouchedResources touchedResources)
        {
            return !string.Equals(touchedResources.State, CsEvalTouchedResourceStateValues.None, StringComparison.Ordinal);
        }

        private static IReadOnlyList<OperationReadInvalidation> CreateReadInvalidations ()
        {
            return OperationReadInvalidationUtilities.CreateUnknownMutation();
        }
    }
}
