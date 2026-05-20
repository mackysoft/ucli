namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one public step source fact used by post-read verification. </summary>
/// <param name="OpId"> The public step identifier matching <c>opResults[].opId</c>. </param>
/// <param name="SourceKind"> The public mutation source kind. </param>
/// <param name="PlayModeMutation"> Whether the step mutated Play Mode state. </param>
/// <param name="Commit"> The requested edit commit kind, or <see langword="null" /> when not applicable. </param>
/// <param name="PersistenceExpected"> Whether the source is expected to touch a persistence unit when it changes state. </param>
/// <param name="ExpectedPostState"> The expected post-state availability for this source. </param>
public sealed record IpcExecutePostReadSourceStep (
    string OpId,
    string SourceKind,
    bool PlayModeMutation,
    string? Commit,
    bool PersistenceExpected,
    string ExpectedPostState);
