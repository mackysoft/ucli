using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
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
}