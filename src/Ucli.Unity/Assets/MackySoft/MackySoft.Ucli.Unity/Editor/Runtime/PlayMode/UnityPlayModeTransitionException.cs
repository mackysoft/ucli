using System;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Reports a failure raised by Unity while accepting one Play Mode transition request.
    /// </summary>
    internal sealed class UnityPlayModeTransitionException : Exception
    {
        public UnityPlayModeTransitionException (
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
