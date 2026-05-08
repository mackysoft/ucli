namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Defines uCLI-owned C# eval diagnostic identifiers. </summary>
    internal static class CsEvalDiagnosticIds
    {
        public const string DiagnosticsTruncated = "UCEVAL_DIAGNOSTICS_TRUNCATED";

        public const string EntryPointAmbiguous = "UCEVAL_ENTRYPOINT_AMBIGUOUS";

        public const string EntryPointContextUnavailable = "UCEVAL_ENTRYPOINT_CONTEXT_UNAVAILABLE";

        public const string EntryPointMissing = "UCEVAL_ENTRYPOINT_MISSING";

        public const string EntryPointRejected = "UCEVAL_ENTRYPOINT_REJECTED";

        public const string ReturnValueTooLarge = "UCEVAL_RETURN_VALUE_TOO_LARGE";

        public const string SnippetUnsupported = "UCEVAL_SNIPPET_UNSUPPORTED";

        public const string SourceTooLarge = "UCEVAL_SOURCE_TOO_LARGE";
    }
}
