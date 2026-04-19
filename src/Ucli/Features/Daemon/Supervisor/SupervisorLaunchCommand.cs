namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Represents one resolved command line used to relaunch the current uCLI executable. </summary>
internal sealed record SupervisorLaunchCommand (
    string FileName,
    IReadOnlyList<string> Arguments);