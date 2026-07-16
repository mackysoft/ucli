namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Supplies the request identifier required to correlate an IPC response. </summary>
public interface IIpcRequestCorrelation
{
    /// <summary> Gets the non-empty request identifier copied to the response envelope. </summary>
    Guid RequestId { get; }
}
