namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies one persisted Unity oneshot bootstrap envelope from command-line arguments. </summary>
public sealed record IpcOneshotBootstrapArguments : IpcBatchmodeBootstrapArguments
{
    /// <summary> Initializes one validated Unity oneshot bootstrap reference. </summary>
    /// <param name="BootstrapId"> The non-empty bootstrap-envelope identifier. </param>
    public IpcOneshotBootstrapArguments (Guid BootstrapId)
    {
        this.BootstrapId = ContractArgumentGuard.RequireNonEmptyGuid(BootstrapId, nameof(BootstrapId));
    }

    /// <summary> Gets the non-empty bootstrap-envelope identifier. </summary>
    public Guid BootstrapId { get; }
}
