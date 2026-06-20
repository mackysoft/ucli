namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the project mutation audit captured around build runner invocation. </summary>
/// <param name="Mode"> The project mutation policy mode used for this audit. </param>
/// <param name="Coverage"> The audit coverage. </param>
/// <param name="Mutated"> Whether the project snapshot changed across runner invocation. </param>
/// <param name="BeforeDigest"> The aggregate pre-runner project digest. </param>
/// <param name="AfterDigest"> The aggregate post-runner project digest. </param>
/// <param name="Items"> The project file changes ordered by project-relative path. </param>
public sealed record IpcBuildProjectMutationAudit (
    string Mode,
    string Coverage,
    bool Mutated,
    string BeforeDigest,
    string AfterDigest,
    IReadOnlyList<IpcBuildProjectMutationAuditItem> Items);
