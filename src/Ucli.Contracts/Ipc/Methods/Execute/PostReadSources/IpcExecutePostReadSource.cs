namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents source facts needed to verify post-read claims from a portable execute result. </summary>
/// <param name="SchemaVersion"> The post-read source schema version. </param>
/// <param name="Steps"> The source facts aligned to <c>opResults[].opId</c>. </param>
public sealed record IpcExecutePostReadSource (
    int SchemaVersion,
    IReadOnlyList<IpcExecutePostReadSourceStep> Steps)
{
    /// <summary> Gets the current post-read source schema version. </summary>
    public const int CurrentSchemaVersion = 1;
}
