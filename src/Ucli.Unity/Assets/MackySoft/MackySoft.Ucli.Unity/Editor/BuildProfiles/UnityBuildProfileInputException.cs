using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Represents a Unity Build Profile input resolution or application failure. </summary>
    internal sealed class UnityBuildProfileInputException : Exception
    {
        /// <summary> Initializes a new instance of the <see cref="UnityBuildProfileInputException" /> class. </summary>
        public UnityBuildProfileInputException (string message)
            : base(message)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UnityBuildProfileInputException" /> class. </summary>
        public UnityBuildProfileInputException (
            string message,
            Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
