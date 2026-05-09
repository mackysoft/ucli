using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents the outcome of clearing the Unity Editor Console display. </summary>
    internal sealed class UnityConsoleClearResult
    {
        private UnityConsoleClearResult (
            bool isSuccess,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
        }

        /// <summary> Gets a value indicating whether the clear operation succeeded. </summary>
        public bool IsSuccess { get; }

        /// <summary> Gets the failure message when the clear operation fails. </summary>
        public string ErrorMessage { get; }

        /// <summary> Creates a successful result. </summary>
        /// <returns> The successful result. </returns>
        public static UnityConsoleClearResult Success ()
        {
            return new UnityConsoleClearResult(true, string.Empty);
        }

        /// <summary> Creates a failed result. </summary>
        /// <param name="errorMessage"> The failure message. </param>
        /// <returns> The failed result. </returns>
        public static UnityConsoleClearResult Failure (string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("errorMessage must not be empty.", nameof(errorMessage));
            }

            return new UnityConsoleClearResult(false, errorMessage);
        }
    }
}
