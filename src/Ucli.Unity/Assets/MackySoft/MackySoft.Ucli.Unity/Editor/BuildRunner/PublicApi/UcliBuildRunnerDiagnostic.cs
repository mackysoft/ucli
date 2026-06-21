using System;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents one diagnostic returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerDiagnostic
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerDiagnostic" /> class. </summary>
        /// <param name="code"> The diagnostic code. </param>
        /// <param name="severity"> The diagnostic severity literal: <c>info</c>, <c>warning</c>, or <c>error</c>. </param>
        /// <param name="message"> The diagnostic message. </param>
        public UcliBuildRunnerDiagnostic (
            string code,
            string severity,
            string message)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("code must not be empty.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(severity))
            {
                throw new ArgumentException("severity must not be empty.", nameof(severity));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("message must not be empty.", nameof(message));
            }

            Code = code;
            Severity = severity;
            Message = message;
        }

        /// <summary> Gets the diagnostic code. </summary>
        public string Code { get; }

        /// <summary> Gets the diagnostic severity literal. </summary>
        public string Severity { get; }

        /// <summary> Gets the diagnostic message. </summary>
        public string Message { get; }
    }
}
