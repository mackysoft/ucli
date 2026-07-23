using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity
{
    /// <summary> Represents one diagnostic returned by a uCLI executeMethod build runner. </summary>
    public sealed class UcliBuildRunnerDiagnostic
    {
        /// <summary> Initializes a new instance of the <see cref="UcliBuildRunnerDiagnostic" /> class. </summary>
        /// <param name="code"> The diagnostic code. </param>
        /// <param name="severity"> The diagnostic severity. </param>
        /// <param name="message"> The diagnostic message. </param>
        public UcliBuildRunnerDiagnostic (
            string code,
            UcliDiagnosticSeverity severity,
            string message)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("code must not be empty.", nameof(code));
            }

            if (!TextVocabulary.IsDefined(severity))
            {
                throw new ArgumentOutOfRangeException(nameof(severity), severity, "severity must be specified.");
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

        /// <summary> Gets the diagnostic severity. </summary>
        public UcliDiagnosticSeverity Severity { get; }

        /// <summary> Gets the diagnostic message. </summary>
        public string Message { get; }
    }
}
