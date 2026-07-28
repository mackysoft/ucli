using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Validates and normalizes execute request payloads into strict contract models. </summary>
    internal sealed class ExecuteRequestNormalizer : IExecuteRequestNormalizer
    {
        private readonly IPhaseOperationRegistry operationRegistry;

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestNormalizer" /> class. </summary>
        /// <param name="operationRegistry"> The operation registry used to validate Play Mode raw operation support. </param>
        public ExecuteRequestNormalizer (IPhaseOperationRegistry operationRegistry)
        {
            this.operationRegistry = operationRegistry ?? throw new ArgumentNullException(nameof(operationRegistry));
        }

        /// <summary> Validates and normalizes one execute request payload. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The normalization result that contains either normalized request data or one structured error. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public ExecuteRequestNormalizationResult Normalize (
            IpcExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!IpcExecuteCommandNames.IsOperationPipelineCommand(request.Command))
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Execute command is not supported: {request.Command}.",
                    instancePath: null));
            }

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request arguments must be a JSON object.",
                    instancePath: null));
            }

            if (!IpcExecuteArgumentsContractReader.TryRead(
                argumentsObject: request.Arguments,
                argumentsContract: out var parsedArguments,
                error: out var readError))
            {
                return ExecuteRequestNormalizationResult.Failure(MapReadError(readError));
            }

            if (parsedArguments.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: parsedArguments.ProtocolVersion));
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(
                parsedArguments.ProtocolVersion,
                parsedArguments.Steps,
                request.AllowPlayMode);
            var normalizedPlanToken = StringValueNormalizer.TrimToNull(request.PlanToken);
            if (!TryPrepareSourceSteps(
                parsedArguments,
                request.AllowPlayMode,
                operationRegistry,
                out var sourceSteps,
                out var compileError))
            {
                return ExecuteRequestNormalizationResult.Failure(compileError);
            }

            var normalizedRequest = new NormalizedExecuteRequest(
                SourceSteps: sourceSteps,
                AllowDangerous: request.AllowDangerous,
                AllowPlayMode: request.AllowPlayMode,
                PlanToken: normalizedPlanToken,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }

        internal static bool TryPrepareSourceSteps (
            IpcExecuteArgumentsContract argumentsContract,
            bool allowPlayMode,
            IPhaseOperationRegistry operationRegistry,
            out IReadOnlyList<IpcExecuteStepContract> sourceSteps,
            out ExecuteRequestNormalizationError error)
        {
            if (operationRegistry == null)
            {
                throw new ArgumentNullException(nameof(operationRegistry));
            }

            sourceSteps = Array.Empty<IpcExecuteStepContract>();
            error = default!;

            var preparedSteps = new List<IpcExecuteStepContract>(argumentsContract.Steps.Count);
            for (var stepIndex = 0; stepIndex < argumentsContract.Steps.Count; stepIndex++)
            {
                var step = argumentsContract.Steps[stepIndex];
                var stepPath = $"/steps/{stepIndex}";
                switch (step.Kind)
                {
                    case IpcExecuteStepKind.Op:
                        if (!RawOperationPlayModeSupportValidator.TryValidate(
                                operationRegistry,
                                step,
                                stepPath,
                                allowPlayMode,
                                out error))
                        {
                            return false;
                        }

                        break;

                    case IpcExecuteStepKind.Edit:
                        var editStep = step.EditContract
                            ?? throw new InvalidOperationException(
                                $"Normalized edit step at '{stepPath}' has no execution model.");
                        if (allowPlayMode && !TryValidatePlayModeEditStep(stepPath, editStep, out error))
                        {
                            return false;
                        }

                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Normalized request step at '{stepPath}' has unsupported kind '{step.Kind}'.");
                }

                preparedSteps.Add(step);
            }

            sourceSteps = preparedSteps;
            error = default!;
            return true;
        }

        private static bool TryValidatePlayModeEditStep (
            string instancePath,
            IpcEditStepContract editStep,
            out ExecuteRequestNormalizationError error)
        {
            if (editStep.Commit == IpcEditStepContract.CommitKind.Project)
            {
                error = new ExecuteRequestNormalizationError(
                    PlayModeErrorCodes.PlayModePersistenceForbidden,
                    "Play Mode mutation does not allow project-wide commit.",
                    instancePath);
                return false;
            }

            if (editStep.Context.Kind == IpcEditStepContract.ContextKind.Scene)
            {
                if (editStep.Commit != IpcEditStepContract.CommitKind.None)
                {
                    error = new ExecuteRequestNormalizationError(
                        PlayModeErrorCodes.PlayModePersistenceForbidden,
                        "Play Mode scene mutation must use commit:'none'.",
                        instancePath);
                    return false;
                }

                for (var actionIndex = 0; actionIndex < editStep.Actions.Count; actionIndex++)
                {
                    var actionKind = editStep.Actions[actionIndex].Kind;
                    if (actionKind == IpcEditStepContract.ActionKind.CreateAsset)
                    {
                        error = new ExecuteRequestNormalizationError(
                            PlayModeErrorCodes.PlayModePersistenceForbidden,
                            $"Play Mode scene mutation does not allow action '{Vocabulary.GetText(actionKind)}'.",
                            instancePath);
                        return false;
                    }
                }
            }

            error = default!;
            return true;
        }

        private static ExecuteRequestNormalizationError MapReadError (in IpcExecuteArgumentsContractReadError readError)
        {
            return ExecuteRequestNormalizationError.InvalidArgument(
                message: readError.Message,
                instancePath: readError.StepIndex >= 0
                    ? $"/steps/{readError.StepIndex}"
                    : null);
        }
    }
}
