namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the dirty-state probe result for audited project items. </summary>
/// <param name="Checked"> Whether dirty state was checked. </param>
/// <param name="Dirty"> Whether any audited project item is dirty. </param>
/// <param name="Coverage"> The dirty-state probe coverage. </param>
/// <param name="Items"> The dirty project items ordered by project-relative path. </param>
public sealed record IpcBuildDirtyState (
    bool Checked,
    bool Dirty,
    string Coverage,
    IReadOnlyList<IpcBuildDirtyStateItem> Items);
