using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one public-step result within an <c>execute</c> response payload. </summary>
/// <param name="OpId"> The public step identifier that corresponds to request <c>steps[].id</c>. </param>
/// <param name="Op"> The public step name reported to clients. </param>
/// <param name="Phase"> The final phase reached by the step. </param>
/// <param name="Applied"> Whether the step has been applied. </param>
/// <param name="Changed"> Whether the step produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
public sealed record IpcExecuteOperationResult
{
    /// <summary> Initializes one public operation result. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Phase" /> is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecuteOperationResult (
        IpcExecuteStepId OpId,
        string Op,
        IpcExecuteOperationPhase Phase,
        bool Applied,
        bool Changed,
        IReadOnlyList<IpcExecuteTouchedResource> Touched)
    {
        if (!ContractLiteralCodec.IsDefined(Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Operation phase must be specified.");
        }

        this.OpId = OpId ?? throw new ArgumentNullException(nameof(OpId));
        this.Op = ContractArgumentGuard.RequireValue(Op, nameof(Op));
        this.Phase = Phase;
        this.Applied = Applied;
        this.Changed = Changed;
        this.Touched = ContractArgumentGuard.RequireItems(Touched, nameof(Touched));
    }

    public IpcExecuteStepId OpId { get; }

    public string Op { get; }

    public IpcExecuteOperationPhase Phase { get; }

    public bool Applied { get; }

    public bool Changed { get; }

    public IReadOnlyList<IpcExecuteTouchedResource> Touched { get; }

    /// <summary> Gets the optional query result payload produced by the step. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }

    /// <summary> Gets non-fatal diagnostics emitted for this public step. </summary>
    [JsonRequired]
    public IReadOnlyList<IpcExecuteDiagnostic> Diagnostics
    {
        get => diagnostics;
        init => diagnostics = ContractArgumentGuard.RequireItems(value, nameof(Diagnostics));
    }

    private readonly IReadOnlyList<IpcExecuteDiagnostic> diagnostics = Array.Empty<IpcExecuteDiagnostic>();
}
