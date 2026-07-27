namespace MackySoft.Ucli.Hosting.Cli.Daemon.Logs;

/// <summary> Identifies a recovery action required after a log read fails. </summary>
[VocabularyDefinition]
internal enum LogsReadActionRequired
{
    /// <summary> Start the daemon or verify that the selected Unity project path is correct. </summary>
    [VocabularyText("startDaemonOrCheckProjectPath")]
    StartDaemonOrCheckProjectPath = 0,
}
