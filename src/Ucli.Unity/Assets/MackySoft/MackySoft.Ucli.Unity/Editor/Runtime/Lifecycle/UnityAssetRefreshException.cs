using System;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary>
    /// Reports a failure raised by Unity while processing one asset refresh request.
    /// </summary>
    internal sealed class UnityAssetRefreshException : Exception
    {
        public UnityAssetRefreshException (
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
