using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
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
                    opId: null));
            }

            if (request.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request arguments must be a JSON object.",
                    opId: null));
            }

            if (!IpcRequestContractReader.TryRead(
                requestObject: request.Arguments,
                profile: IpcRequestContractReadProfile.StrictExecute,
                requestContract: out var parsedContract,
                error: out var readError))
            {
                return ExecuteRequestNormalizationResult.Failure(MapReadError(readError));
            }

            if (parsedContract.ProtocolVersion != IpcProtocol.CurrentVersion)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.ProtocolVersionMismatch(
                    expectedVersion: IpcProtocol.CurrentVersion,
                    actualVersion: parsedContract.ProtocolVersion));
            }

            if (parsedContract.RequestId is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'requestId' is required.",
                    opId: null));
            }

            if (parsedContract.Steps is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null));
            }

            var validatedSteps = new List<IpcRequestContractStep>(parsedContract.Steps.Count);
            foreach (var step in parsedContract.Steps)
            {
                if (step is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step must be an object.",
                        opId: null));
                }

                if (step.Id is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step id is required.",
                        opId: null));
                }

                if (step.Kind is null)
                {
                    return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Step kind is required.",
                        opId: step.Id));
                }

                validatedSteps.Add(step);
            }

            var canonicalPayload = CanonicalRequestWriter.WriteDigestPayload(
                parsedContract.ProtocolVersion,
                validatedSteps,
                request.AllowPlayMode);
            var normalizedPlanToken = StringValueNormalizer.TrimToNull(request.PlanToken);
            if (!TryPrepareSourceSteps(
                parsedContract,
                request.AllowPlayMode,
                operationRegistry,
                out var sourceSteps,
                out var compileError))
            {
                return ExecuteRequestNormalizationResult.Failure(compileError);
            }

            var normalizedRequest = new NormalizedExecuteRequest(
                ProtocolVersion: parsedContract.ProtocolVersion,
                RequestId: parsedContract.RequestId,
                SourceSteps: sourceSteps,
                AllowDangerous: request.AllowDangerous,
                AllowPlayMode: request.AllowPlayMode,
                PlanToken: normalizedPlanToken,
                CanonicalDigestPayloadUtf8: canonicalPayload);
            return ExecuteRequestNormalizationResult.Success(normalizedRequest);
        }

        internal static bool TryPrepareSourceSteps (
            IpcRequestContract requestContract,
            bool allowPlayMode,
            IPhaseOperationRegistry operationRegistry,
            out IReadOnlyList<IpcRequestContractStep> sourceSteps,
            out ExecuteRequestNormalizationError error)
        {
            if (operationRegistry == null)
            {
                throw new ArgumentNullException(nameof(operationRegistry));
            }

            sourceSteps = Array.Empty<IpcRequestContractStep>();
            error = default!;

            if (requestContract.Steps == null)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null);
                return false;
            }

            var preparedSteps = new List<IpcRequestContractStep>(requestContract.Steps.Count);
            foreach (var step in requestContract.Steps)
            {
                if (step == null || step.Id == null || step.Kind == null)
                {
                    error = ExecuteRequestNormalizationError.InvalidArgument(
                        message: "Request step is incomplete.",
                        opId: step?.Id);
                    return false;
                }

                switch (step.Kind)
                {
                    case IpcRequestStepKind.Op:
                        if (!RawOperationPlayModeSupportValidator.TryValidate(operationRegistry, step, allowPlayMode, out error))
                        {
                            return false;
                        }

                        if (!TryValidateOpStep(step, out error))
                        {
                            return false;
                        }

                        break;

                    case IpcRequestStepKind.Edit:
                        if (!TryValidateEditStep(step, out var editStep, out error))
                        {
                            return false;
                        }

                        if (allowPlayMode && !TryValidatePlayModeEditStep(step.Id, editStep, out error))
                        {
                            return false;
                        }

                        break;

                    default:
                        error = ExecuteRequestNormalizationError.InvalidArgument(
                            message: $"Step '{step.Id}' has unsupported kind.",
                            opId: step.Id);
                        return false;
                }

                preparedSteps.Add(step);
            }

            sourceSteps = preparedSteps;
            error = default!;
            return true;
        }

        private static bool TryValidateOpStep (
            IpcRequestContractStep step,
            out ExecuteRequestNormalizationError error)
        {
            if (step.OperationName == null)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Step operation name is required.",
                    opId: step.Id);
                return false;
            }

            if (!step.Element.TryGetProperty("args", out var argsElement)
                || argsElement.ValueKind != JsonValueKind.Object)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: $"Step '{step.Id}' property 'args' must be an object.",
                    opId: step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private static bool TryValidateEditStep (
            IpcRequestContractStep step,
            out IpcEditStepContract editStep,
            out ExecuteRequestNormalizationError error)
        {
            editStep = default!;
            if (!IpcEditStepContractReader.TryRead(step.Element, out editStep, out var editErrorMessage))
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: editErrorMessage,
                    opId: step.Id);
                return false;
            }

            error = default!;
            return true;
        }

        private static bool TryValidatePlayModeEditStep (
            string stepId,
            IpcEditStepContract editStep,
            out ExecuteRequestNormalizationError error)
        {
            if (editStep.Commit == IpcEditStepContract.CommitKind.Project)
            {
                error = new ExecuteRequestNormalizationError(
                    PlayModeErrorCodes.PlayModePersistenceForbidden,
                    "Play Mode mutation does not allow project-wide commit.",
                    stepId);
                return false;
            }

            if (editStep.Context.Kind == IpcEditStepContract.ContextKind.Scene)
            {
                if (editStep.Commit != IpcEditStepContract.CommitKind.None)
                {
                    error = new ExecuteRequestNormalizationError(
                        PlayModeErrorCodes.PlayModePersistenceForbidden,
                        "Play Mode scene mutation must use commit:'none'.",
                        stepId);
                    return false;
                }

                for (var actionIndex = 0; actionIndex < editStep.Actions.Count; actionIndex++)
                {
                    var actionKind = editStep.Actions[actionIndex].Kind;
                    if (actionKind == IpcEditStepContract.ActionKind.CreateAsset)
                    {
                        error = new ExecuteRequestNormalizationError(
                            PlayModeErrorCodes.PlayModePersistenceForbidden,
                            $"Play Mode scene mutation does not allow action '{ContractLiteralCodec.ToValue(actionKind)}'.",
                            stepId);
                        return false;
                    }
                }
            }

            error = default!;
            return true;
        }

        private static ExecuteRequestNormalizationError MapReadError (in IpcRequestContractReadError readError)
        {
            if (readError.Kind == IpcRequestContractReadErrorKind.StepEditContractViolation)
            {
                return ExecuteRequestNormalizationError.InvalidArgument(
                    message: readError.DiagnosticMessage ?? "Request arguments are invalid.",
                    opId: readError.StepId);
            }

            var violation = IpcRequestContractViolationClassifier.Classify(readError);
            var stepId = violation.StepId ?? string.Empty;
            return violation.Kind switch
            {
                IpcRequestContractViolationKind.RequestMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments must be a JSON object.",
                    null),
                IpcRequestContractViolationKind.UnknownRequestProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Request contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' is required.",
                    null),
                IpcRequestContractViolationKind.ProtocolVersionTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' must be an integer.",
                    null),
                IpcRequestContractViolationKind.RequestIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' is required.",
                    null),
                IpcRequestContractViolationKind.RequestIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be a UUID string.",
                    null),
                IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.RequestIdFormatMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'requestId' must be UUID format 'D'.",
                    null),
                IpcRequestContractViolationKind.StepsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' is required.",
                    null),
                IpcRequestContractViolationKind.StepsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' must be an array.",
                    null),
                IpcRequestContractViolationKind.StepMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} must be an object.",
                    null),
                IpcRequestContractViolationKind.StepKindMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is required.",
                    null),
                IpcRequestContractViolationKind.StepKindTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must be a string.",
                    null),
                IpcRequestContractViolationKind.StepKindEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not be empty.",
                    null),
                IpcRequestContractViolationKind.StepKindOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.StepKindUnsupported => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is unsupported: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.UnknownStepProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcRequestContractViolationKind.StepIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' is required.",
                    null),
                IpcRequestContractViolationKind.StepIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must be a string.",
                    null),
                IpcRequestContractViolationKind.StepIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not be empty.",
                    null),
                IpcRequestContractViolationKind.StepIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not contain leading or trailing whitespace.",
                    null),
                IpcRequestContractViolationKind.StepOpMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepOpTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must be a string.",
                    stepId),
                IpcRequestContractViolationKind.StepOpEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not be empty.",
                    stepId),
                IpcRequestContractViolationKind.StepOpOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not contain leading or trailing whitespace.",
                    stepId),
                IpcRequestContractViolationKind.StepArgsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepArgsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepOnMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepOnTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepSelectMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepSelectTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' must be an object.",
                    stepId),
                IpcRequestContractViolationKind.StepActionsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepActionsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must be an array.",
                    stepId),
                IpcRequestContractViolationKind.StepActionMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must contain only objects.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' is required.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must be a string.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not be empty.",
                    stepId),
                IpcRequestContractViolationKind.StepCommitOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not contain leading or trailing whitespace.",
                    stepId),
                IpcRequestContractViolationKind.DuplicatedStepId => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step id is duplicated: {violation.DuplicatedStepId}.",
                    violation.DuplicatedStepId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments are invalid.",
                    null),
            };
        }
    }
}
