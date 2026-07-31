using System;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Carries one action-owned Play Mode transition failure without exposing
    /// a transport-specific error envelope to the execution provider.
    /// </summary>
    internal sealed record PlayTransitionExecutionError
    {
        public PlayTransitionExecutionError (
            UcliCode code,
            string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = string.IsNullOrWhiteSpace(message)
                ? throw new ArgumentException(
                    "Play Mode transition error message must not be empty.",
                    nameof(message))
                : message;
        }

        public UcliCode Code { get; }

        public string Message { get; }
    }
}
