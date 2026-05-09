namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Represents one Unity process whose command line targets a Unity project. </summary>
/// <param name="ProcessId"> The process identifier. </param>
internal sealed record UnityProjectProcessMatch (
    int ProcessId);
