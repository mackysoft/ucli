using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
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