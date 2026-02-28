namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one operation result within an <c>execute</c> response payload. </summary>
/// <param name="OpId"> The operation identifier that corresponds to request <c>ops[].id</c>. </param>
/// <param name="Op"> The operation name. </param>
/// <param name="Phase"> The final phase reached by the operation. </param>
/// <param name="Applied"> Whether the operation has been applied. </param>
/// <param name="Changed"> Whether the operation produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
public sealed record IpcExecuteOperationResult (
    string OpId,
    string Op,
    string Phase,
    bool Applied,
    bool Changed,
    IReadOnlyList<IpcExecuteTouchedResource> Touched);