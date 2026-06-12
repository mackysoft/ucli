namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the dirty-state probe result for build input assets. </summary>
/// <param name="Checked"> Whether dirty state was checked. </param>
/// <param name="Dirty"> Whether any build input item is dirty. </param>
/// <param name="Items"> The dirty build input items ordered by project-relative path. </param>
public sealed record IpcBuildDirtyState (
    bool Checked,
    bool Dirty,
    IReadOnlyList<IpcBuildDirtyStateItem> Items);
