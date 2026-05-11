namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;

/// <summary> Defines the Unity startup observation context used to classify ambiguous log signals. </summary>
internal enum DaemonStartupFailureClassificationContext
{
    /// <summary> Startup is observed in a non-interactive batchmode or headless context. </summary>
    Batchmode,

    /// <summary> Startup is observed in an interactive GUI Editor context. </summary>
    Gui,
}
