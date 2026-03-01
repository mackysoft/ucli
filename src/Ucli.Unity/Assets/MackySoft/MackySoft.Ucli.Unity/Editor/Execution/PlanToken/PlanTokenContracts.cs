using System;
using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Coordinates plan-token issuance and validation around phase execution. </summary>
    internal interface IPlanTokenCoordinator
    {
        /// <summary> Issues one plan token from normalized request and plan traces. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The plan-phase operation traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The token issue result. </returns>
        PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);

        /// <summary> Validates one incoming call plan token against request and current state. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The pre-call plan traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The validation result. </returns>
        PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            CancellationToken cancellationToken = default);
    }

    /// <summary> Represents one plan-token issuance result. </summary>
    /// <param name="PlanToken"> The issued token string when issuance succeeded. </param>
    /// <param name="Failure"> The failure details when issuance failed. </param>
    internal sealed record PlanTokenIssueResult (
        string? PlanToken,
        OperationFailure? Failure)
    {
        /// <summary> Gets a value indicating whether issuance succeeded. </summary>
        public bool IsSuccess => Failure is null && !string.IsNullOrWhiteSpace(PlanToken);

        /// <summary> Creates a successful issuance result. </summary>
        /// <param name="planToken"> The issued token. </param>
        /// <returns> The successful result. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="planToken" /> is empty or whitespace. </exception>
        public static PlanTokenIssueResult Success (string planToken)
        {
            if (string.IsNullOrWhiteSpace(planToken))
            {
                throw new ArgumentException("Plan token must not be empty.", nameof(planToken));
            }

            return new PlanTokenIssueResult(planToken, null);
        }

        /// <summary> Creates a failed issuance result. </summary>
        /// <param name="failure"> The issuance failure details. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static PlanTokenIssueResult Failed (OperationFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new PlanTokenIssueResult(null, failure);
        }
    }

    /// <summary> Represents one plan-token validation result. </summary>
    /// <param name="Failure"> The validation failure details when validation failed; otherwise <see langword="null" />. </param>
    internal sealed record PlanTokenValidationResult (OperationFailure? Failure)
    {
        /// <summary> Gets a value indicating whether validation succeeded. </summary>
        public bool IsSuccess => Failure is null;

        /// <summary> Creates a successful validation result. </summary>
        /// <returns> The successful result. </returns>
        public static PlanTokenValidationResult Success ()
        {
            return new PlanTokenValidationResult((OperationFailure?)null);
        }

        /// <summary> Creates a failed validation result. </summary>
        /// <param name="failure"> The validation failure details. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="failure" /> is <see langword="null" />. </exception>
        public static PlanTokenValidationResult Failed (OperationFailure failure)
        {
            if (failure == null)
            {
                throw new ArgumentNullException(nameof(failure));
            }

            return new PlanTokenValidationResult(failure);
        }
    }
}
