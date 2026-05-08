namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds internal guardrails that keep eval IPC payloads bounded. </summary>
    internal static class CsEvalSafetyLimits
    {
        public const int MaxSourceBytes = 4 * 1024 * 1024;

        public const int MaxDiagnostics = 100;

        public const int MaxDiagnosticMessageBytes = 4096;

        public const int MaxLogEntries = 1000;

        public const int MaxLogMessageBytes = 8192;

        public const int MaxTouchedResources = 4096;

        public const int MaxReturnValueBytes = 8 * 1024 * 1024;
    }
}
