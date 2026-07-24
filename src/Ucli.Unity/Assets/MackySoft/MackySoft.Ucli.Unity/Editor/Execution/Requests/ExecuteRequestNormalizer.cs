using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
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

            if (!IpcExecuteArgumentsContractReader.TryRead(
                argumentsObject: request.Arguments,
                profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
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

            if (parsedArguments.Steps is null)
            {
                return ExecuteRequestNormalizationResult.Failure(ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null));
            }

            var validatedSteps = new List<IpcExecuteStepContract>(parsedArguments.Steps.Count);
            foreach (var step in parsedArguments.Steps)
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
                parsedArguments.ProtocolVersion,
                validatedSteps,
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

            if (argumentsContract.Steps == null)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    message: "Request property 'steps' is required.",
                    opId: null);
                return false;
            }

            var preparedSteps = new List<IpcExecuteStepContract>(argumentsContract.Steps.Count);
            foreach (var step in argumentsContract.Steps)
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
                    case IpcExecuteStepKind.Op:
                        if (!RawOperationPlayModeSupportValidator.TryValidate(operationRegistry, step, allowPlayMode, out error))
                        {
                            return false;
                        }

                        if (!TryValidateOpStep(step, out error))
                        {
                            return false;
                        }

                        break;

                    case IpcExecuteStepKind.Edit:
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
            IpcExecuteStepContract step,
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
            IpcExecuteStepContract step,
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
            IpcExecuteStepId stepId,
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
                            $"Play Mode scene mutation does not allow action '{TextVocabulary.GetText(actionKind)}'.",
                            stepId);
                        return false;
                    }
                }
            }

            error = default!;
            return true;
        }

        private static ExecuteRequestNormalizationError MapReadError (in IpcExecuteArgumentsContractReadError readError)
        {
            if (readError.Kind == IpcExecuteArgumentsContractReadErrorKind.StepEditContractViolation)
            {
                return ExecuteRequestNormalizationError.InvalidArgument(
                    message: readError.DiagnosticMessage ?? "Request arguments are invalid.",
                    opId: readError.StepId);
            }

            var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);
            var stepId = violation.StepId!;
            return violation.Kind switch
            {
                IpcExecuteArgumentsContractViolationKind.ArgumentsMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments must be a JSON object.",
                    null),
                IpcExecuteArgumentsContractViolationKind.UnknownArgumentsProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Request contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcExecuteArgumentsContractViolationKind.ProtocolVersionMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' is required.",
                    null),
                IpcExecuteArgumentsContractViolationKind.ProtocolVersionTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'protocolVersion' must be an integer.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' is required.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request property 'steps' must be an array.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} must be an object.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepKindMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is required.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepKindTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must be a string.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepKindEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not be empty.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepKindOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' must not contain leading or trailing whitespace.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepKindUnsupported => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'kind' is unsupported: {violation.UnknownPropertyName}.",
                    null),
                IpcExecuteArgumentsContractViolationKind.UnknownStepProperty => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} contains an unknown property: {violation.UnknownPropertyName}.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepIdMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' is required.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepIdTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must be a string.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepIdEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not be empty.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepIdOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step at index {violation.StepIndex} property 'id' must not contain leading or trailing whitespace.",
                    null),
                IpcExecuteArgumentsContractViolationKind.StepOpMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepOpTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must be a string.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepOpEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not be empty.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepOpOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'op' must not contain leading or trailing whitespace.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepArgsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepArgsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'args' must be an object.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepOnMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepOnTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'on' must be an object.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepSelectMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepSelectTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'select' must be an object.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepActionsMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepActionsTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must be an array.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepActionMustBeObject => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'actions' must contain only objects.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepCommitMissing => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' is required.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepCommitTypeMismatch => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must be a string.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepCommitEmptyOrWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not be empty.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.StepCommitOuterWhitespace => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step '{stepId}' property 'commit' must not contain leading or trailing whitespace.",
                    stepId),
                IpcExecuteArgumentsContractViolationKind.DuplicatedStepId => ExecuteRequestNormalizationError.InvalidArgument(
                    $"Step id is duplicated: {violation.DuplicatedStepId}.",
                    violation.DuplicatedStepId),
                _ => ExecuteRequestNormalizationError.InvalidArgument(
                    "Request arguments are invalid.",
                    null),
            };
        }
    }
}
