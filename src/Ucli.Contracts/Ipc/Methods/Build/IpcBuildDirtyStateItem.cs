using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one dirty build input item. </summary>
public sealed record IpcBuildDirtyStateItem
{
    /// <summary> Initializes one dirty build input item. </summary>
    [JsonConstructor]
    public IpcBuildDirtyStateItem (
        IpcBuildDirtyStateItemKind Kind,
        string Path)
    {
        if (!ContractLiteralCodec.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Dirty-state item kind must be specified.");
        }

        this.Kind = Kind;
        this.Path = ContractArgumentGuard.RequireValue(Path, nameof(Path));
    }

    public IpcBuildDirtyStateItemKind Kind { get; }

    public string Path { get; }
}
