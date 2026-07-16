using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution.Phases;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Validates Play Mode admission for raw operation steps. </summary>
    internal static class RawOperationPlayModeSupportValidator
    {
        /// <summary> Validates one raw operation step against the operation metadata Play Mode contract. </summary>
        public static bool TryValidate (
            IPhaseOperationRegistry operationRegistry,
            IpcExecuteStepContract step,
            bool allowPlayMode,
            out ExecuteRequestNormalizationError error)
        {
            if (operationRegistry == null)
            {
                throw new ArgumentNullException(nameof(operationRegistry));
            }

            if (step == null)
            {
                throw new ArgumentNullException(nameof(step));
            }

            error = default!;
            var operationName = step.OperationName;
            if (operationName == null || string.IsNullOrWhiteSpace(operationName))
            {
                return true;
            }

            if (!operationRegistry.TryResolve(operationName, out var operation))
            {
                if (!allowPlayMode)
                {
                    return true;
                }

                error = ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationName}' is not registered and cannot be used in Play Mode execution.",
                    step.Id);
                return false;
            }

            return TryValidateResolvedSupport(operation.Metadata.PlayModeSupport, operationName, step.Id, allowPlayMode, out error);
        }

        private static bool TryValidateResolvedSupport (
            UcliOperationPlayModeSupport playModeSupport,
            string operationName,
            IpcExecuteStepId? stepId,
            bool allowPlayMode,
            out ExecuteRequestNormalizationError error)
        {
            if (playModeSupport == UcliOperationPlayModeSupport.Required && !allowPlayMode)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationName}' requires --allowPlayMode.",
                    stepId);
                return false;
            }

            if (allowPlayMode && playModeSupport == UcliOperationPlayModeSupport.Disallowed)
            {
                error = ExecuteRequestNormalizationError.InvalidArgument(
                    $"Operation '{operationName}' does not support Play Mode execution.",
                    stepId);
                return false;
            }

            error = default!;
            return true;
        }
    }
}
