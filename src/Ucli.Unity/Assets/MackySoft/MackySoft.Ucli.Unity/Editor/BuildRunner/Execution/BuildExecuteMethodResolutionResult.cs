using System;
using System.Reflection;
using MackySoft.Ucli.Contracts;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents executeMethod runner method resolution result. </summary>
    internal sealed class BuildExecuteMethodResolutionResult
    {
        private BuildExecuteMethodResolutionResult (
            MethodInfo? method,
            UcliCode? errorCode,
            string? errorMessage)
        {
            Method = method;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public MethodInfo? Method { get; }

        public UcliCode? ErrorCode { get; }

        public string? ErrorMessage { get; }

        public bool IsSuccess => Method != null && ErrorCode == null;

        public static BuildExecuteMethodResolutionResult Success (MethodInfo method)
        {
            return new BuildExecuteMethodResolutionResult(
                method ?? throw new ArgumentNullException(nameof(method)),
                null,
                null);
        }

        public static BuildExecuteMethodResolutionResult Failure (
            UcliCode errorCode,
            string errorMessage)
        {
            if (errorCode == null)
            {
                throw new ArgumentNullException(nameof(errorCode));
            }

            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Error message must not be empty.", nameof(errorMessage));
            }

            return new BuildExecuteMethodResolutionResult(null, errorCode, errorMessage);
        }
    }
}
