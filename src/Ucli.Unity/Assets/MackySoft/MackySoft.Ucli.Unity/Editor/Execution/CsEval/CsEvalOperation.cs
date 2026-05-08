using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Implements <c>ucli.cs.eval</c> operation flow. </summary>
    [UcliOperation]
    internal sealed class CsEvalOperation : UcliOperation<CsEvalArgs, CsEvalResult>
    {
        private readonly CsEvalCompilationService compilationService = new CsEvalCompilationService();

        private readonly CsEvalEntryPointReflectionResolver entryPointResolver = new CsEvalEntryPointReflectionResolver();

        private readonly CsEvalReturnValueSerializer returnValueSerializer = new CsEvalReturnValueSerializer();

        public override UcliOperationMetadata Metadata { get; } = CreateMetadata();

        protected override Task<OperationPhaseStepResult> Validate (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CsEvalEntryPointName.TryParse(args.EntryPoint, out _, out var entryPointError))
            {
                return Task.FromResult(OperationPhaseExecutionUtilities.CreateInvalidArgumentFailure(operation.Id, entryPointError));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false));
        }

        protected override Task<OperationPhaseStepResult> Plan (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = compilationService.CompileAndValidate(args.Source, args.EntryPoint, cancellationToken);
            if (!compilation.IsSuccess)
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, compilation.FailureMessage!, compilation.CreatePlanResult()));
            }

            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: false,
                changed: false,
                result: IpcPayloadCodec.SerializeToElement(compilation.CreatePlanResult())));
        }

        protected override Task<OperationPhaseStepResult> Call (
            NormalizedOperation operation,
            CsEvalArgs args,
            OperationExecutionContext executionContext,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = compilationService.CompileAndValidate(args.Source, args.EntryPoint, cancellationToken);
            if (!compilation.IsSuccess)
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, compilation.FailureMessage!, compilation.CreatePlanResult()));
            }

            if (!compilationService.TryEmitAssembly(compilation.Compilation, cancellationToken, out var assemblyBytes, out var emitDiagnostics, out var emitError))
            {
                var emitResult = new CsEvalResult(
                    compilation.SourceDigest,
                    compilation.EntryPoint,
                    compilation.ExecutionDigest,
                    new CsEvalCompileResult(CsEvalCompileStatusValues.Failed, emitDiagnostics),
                    durationMilliseconds: null,
                    logs: null,
                    returnValue: null,
                    touchedResources: null);
                return Task.FromResult(CreateInvalidArgumentFailure(operation, emitError, emitResult));
            }

            var assembly = Assembly.Load(assemblyBytes);
            if (!entryPointResolver.TryResolve(assembly, args.EntryPoint, out var method, out var entryPointError))
            {
                return Task.FromResult(CreateInvalidArgumentFailure(operation, entryPointError, compilation.CreatePlanResult()));
            }

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
                var result = CreateCallResult(compilation, stopwatch.ElapsedMilliseconds, context, returnValue: null);
                return Task.FromResult(CreateInvalidArgumentFailure(
                    operation,
                    $"C# eval entry point threw {exception.InnerException.GetType().FullName}: {exception.InnerException.Message}",
                    result));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                stopwatch.Stop();
                var result = CreateCallResult(compilation, stopwatch.ElapsedMilliseconds, context, returnValue: null);
                return Task.FromResult(CreateInvalidArgumentFailure(
                    operation,
                    $"C# eval entry point invocation failed. {exception.Message}",
                    result));
            }

            stopwatch.Stop();
            if (!returnValueSerializer.TrySerialize(method, returnObject, out var returnValue, out var returnValueError))
            {
                var result = CreateCallResult(compilation, stopwatch.ElapsedMilliseconds, context, returnValue: null);
                return Task.FromResult(CreateInvalidArgumentFailure(operation, returnValueError, result));
            }

            var touchedResources = CsEvalTouchedResourceMapper.CreateResult(context);
            var touched = CsEvalTouchedResourceMapper.CreateTouches(context);
            var changed = !string.Equals(touchedResources.State, CsEvalTouchedResourceStateValues.None, StringComparison.Ordinal);
            return Task.FromResult(OperationPhaseStepResult.Success(
                applied: true,
                changed: changed,
                touched: touched,
                result: IpcPayloadCodec.SerializeToElement(CreateCallResult(compilation, stopwatch.ElapsedMilliseconds, context, returnValue))));
        }

        private static UcliOperationMetadata CreateMetadata ()
        {
            var assurance = new UcliOperationAssuranceContract(
                new[]
                {
                    UcliOperationSideEffect.WritesAsset,
                    UcliOperationSideEffect.WritesScene,
                    UcliOperationSideEffect.WritesPrefab,
                    UcliOperationSideEffect.WritesProjectSettings,
                },
                mayDirty: true,
                mayPersist: true,
                new[]
                {
                    IpcExecuteTouchedResourceKindNames.Scene,
                    IpcExecuteTouchedResourceKindNames.Prefab,
                    IpcExecuteTouchedResourceKindNames.Asset,
                    IpcExecuteTouchedResourceKindNames.ProjectSettings,
                },
                UcliOperationPlanMode.ObservesLiveUnity);
            var describe = UcliOperationDescribeContractBuilder.Create<CsEvalArgs, CsEvalResult>(
                "Compiles and executes a C# source unit in the Unity editor process as a dangerous eval operation.",
                assurance);
            describe.CodeContract = UcliOperationCodeContractBuilder.CreateCSharp(
                "public static object? Run(UcliCsEvalContext context)",
                requiredStatic: true,
                new[] { typeof(UcliCsEvalContext) },
                "void, null, or a JSON-serializable value. Task, Task<T>, ValueTask, and ValueTask<T> are rejected.",
                new[] { typeof(UcliCsEvalContext) });

            return UcliOperationMetadata.Create<CsEvalArgs, CsEvalResult>(
                operationName: UcliPrimitiveOperationNames.CsEval,
                kind: UcliOperationKind.Mutation,
                policy: OperationPolicy.Dangerous,
                describeContract: describe,
                requiresPreCallPlanReplay: true);
        }

        private static CsEvalResult CreateCallResult (
            CsEvalCompilationResult compilation,
            long durationMilliseconds,
            UcliCsEvalContext context,
            CsEvalReturnValue? returnValue)
        {
            return new CsEvalResult(
                compilation.SourceDigest,
                compilation.EntryPoint,
                compilation.ExecutionDigest,
                compilation.Compile,
                durationMilliseconds,
                context.Logs,
                returnValue,
                CsEvalTouchedResourceMapper.CreateResult(context));
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
    }
}
