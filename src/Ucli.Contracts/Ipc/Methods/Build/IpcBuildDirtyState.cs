using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the dirty-state probe result for audited project items. </summary>
public sealed record IpcBuildDirtyState
{
    /// <summary> Initializes one dirty-state probe result. </summary>
    [JsonConstructor]
    public IpcBuildDirtyState (
        bool Checked,
        bool Dirty,
        IpcBuildDirtyStateCoverage Coverage,
        IReadOnlyList<IpcBuildDirtyStateItem> Items)
    {
        if (!ContractLiteralCodec.IsDefined(Coverage))
        {
            throw new ArgumentOutOfRangeException(nameof(Coverage), Coverage, "Dirty-state coverage must be specified.");
        }

        this.Checked = Checked;
        this.Dirty = Dirty;
        this.Coverage = Coverage;
        this.Items = ContractArgumentGuard.RequireItems(Items, nameof(Items));
    }

    public bool Checked { get; }

    public bool Dirty { get; }

    public IpcBuildDirtyStateCoverage Coverage { get; }

    public IReadOnlyList<IpcBuildDirtyStateItem> Items { get; }
}
