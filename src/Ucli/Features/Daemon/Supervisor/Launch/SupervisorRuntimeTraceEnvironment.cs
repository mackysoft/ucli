namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Captures opt-in runtime trace environment variables for detached supervisor hosts. </summary>
internal static class SupervisorRuntimeTraceEnvironment
{
    internal const string TraceDirectoryEnvironmentVariableName = "UCLI_RUNTIME_TRACE_DIR";

    internal const string TraceSessionEnvironmentVariableName = "UCLI_RUNTIME_TRACE_SESSION";

    /// <summary> Captures runtime trace variables from the current process environment. </summary>
    /// <returns> Ordered environment entries to propagate to the supervisor process. </returns>
    public static IReadOnlyList<KeyValuePair<string, string>> Capture ()
    {
        var traceDirectory = Environment.GetEnvironmentVariable(TraceDirectoryEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(traceDirectory))
        {
            return Array.Empty<KeyValuePair<string, string>>();
        }

        var traceSession = Environment.GetEnvironmentVariable(TraceSessionEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(traceSession))
        {
            return
            [
                new KeyValuePair<string,string>(TraceDirectoryEnvironmentVariableName, traceDirectory),
            ];
        }

        return
        [
            new KeyValuePair<string,string>(TraceDirectoryEnvironmentVariableName, traceDirectory),
            new KeyValuePair<string,string>(TraceSessionEnvironmentVariableName, traceSession),
        ];
    }
}
