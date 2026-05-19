namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines edit commit literals emitted by execute post-read source facts. </summary>
public static class IpcExecutePostReadCommitNames
{
    /// <summary> Indicates that the source requested no persistence commit. </summary>
    public const string None = "none";

    /// <summary> Indicates that the source requested a context-scoped commit. </summary>
    public const string Context = "context";

    /// <summary> Indicates that the source requested a project-scoped commit. </summary>
    public const string Project = "project";
}
