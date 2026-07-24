using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the dirty-state probe result for audited project items. </summary>
public sealed record IpcBuildDirtyState
{
    /// <summary> Initializes one dirty-state probe result. </summary>
    [JsonConstructor]
    public IpcBuildDirtyState (
        bool Dirty,
        IpcBuildDirtyStateCoverage Coverage,
        IReadOnlyList<IpcBuildDirtyStateItem> Items)
    {
        if (!TextVocabulary.IsDefined(Coverage))
        {
            throw new ArgumentOutOfRangeException(nameof(Coverage), Coverage, "Dirty-state coverage must be specified.");
        }

        var items = ContractArgumentGuard.RequireItems(Items, nameof(Items));
        ValidateItems(items);
        if (Dirty != (items.Count != 0))
        {
            throw new ArgumentException("Dirty must match whether dirty-state items are present.", nameof(Dirty));
        }

        this.Dirty = Dirty;
        this.Coverage = Coverage;
        this.Items = items;
    }

    public bool Dirty { get; }

    public IpcBuildDirtyStateCoverage Coverage { get; }

    public IReadOnlyList<IpcBuildDirtyStateItem> Items { get; }

    private static void ValidateItems (IReadOnlyList<IpcBuildDirtyStateItem> items)
    {
        ProjectMutationAuditPath? previousPath = null;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (previousPath != null && previousPath.CompareTo(item.Path) >= 0)
            {
                throw new ArgumentException(
                    "Dirty-state items must be ordered by unique project mutation audit path.",
                    nameof(Items));
            }

            previousPath = item.Path;
        }
    }
}
