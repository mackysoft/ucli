using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one public-step result within an <c>execute</c> response payload. </summary>
/// <param name="Op"> The public step name reported to clients. </param>
/// <param name="Phase"> The final phase reached by the step. </param>
/// <param name="Applied"> Whether the step has been applied. </param>
/// <param name="Changed"> Whether the step produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
public sealed record IpcExecuteOperationResult
{
    /// <summary> Initializes one public operation result. </summary>
    /// <param name="Op"> The public step name reported to clients. </param>
    /// <param name="Phase"> The final phase reached by the step. </param>
    /// <param name="Applied"> Whether the step has been applied. </param>
    /// <param name="Changed"> Whether the step produced persistent changes. </param>
    /// <param name="Touched"> The touched persistence-unit resources. </param>
    /// <param name="OperationDescriptorDigest"> The direct operation descriptor digest, or <see langword="null" /> for Edit. </param>
    /// <param name="Verdict"> The verdict established by a successful judging Call, or <see langword="null" />. </param>
    /// <param name="Result"> The result evidence, or <see langword="null" /> when the step did not produce a result. </param>
    /// <param name="Diagnostics"> The diagnostics emitted for the public step. </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="Op" /> is empty, or <paramref name="Verdict" /> is present without
    /// <paramref name="Result" />.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="Op" />, <paramref name="Touched" />, or <paramref name="Diagnostics" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Phase" /> is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecuteOperationResult (
        string Op,
        IpcExecuteOperationPhase Phase,
        bool Applied,
        bool Changed,
        IReadOnlyList<IpcExecuteTouchedResource> Touched,
        Sha256Digest? OperationDescriptorDigest,
        Verdict? Verdict,
        JsonElement? Result,
        IReadOnlyList<IpcExecuteDiagnostic> Diagnostics)
    {
        if (!TextVocabulary.IsDefined(Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(Phase), Phase, "Operation phase must be specified.");
        }

        if (Result is { ValueKind: JsonValueKind.Undefined })
        {
            throw new ArgumentException(
                "An operation result payload must contain a defined JSON value.",
                nameof(Result));
        }

        if (Verdict.HasValue && !Result.HasValue)
        {
            throw new ArgumentException(
                "An operation result that carries a verdict must also carry its result evidence.",
                nameof(Result));
        }

        if (Verdict.HasValue && OperationDescriptorDigest == null)
        {
            throw new ArgumentNullException(
                nameof(OperationDescriptorDigest),
                "An operation result that carries a verdict must identify the descriptor used to establish it.");
        }

        this.Op = ContractArgumentGuard.RequireValue(Op, nameof(Op));
        this.Phase = Phase;
        this.Applied = Applied;
        this.Changed = Changed;
        this.Touched = ContractArgumentGuard.RequireItems(Touched, nameof(Touched));
        this.OperationDescriptorDigest = OperationDescriptorDigest;
        this.Verdict = Verdict;
        this.Result = Result;
        this.Diagnostics = ContractArgumentGuard.RequireItems(Diagnostics, nameof(Diagnostics));
    }

    public string Op { get; }

    public IpcExecuteOperationPhase Phase { get; }

    public bool Applied { get; }

    public bool Changed { get; }

    public IReadOnlyList<IpcExecuteTouchedResource> Touched { get; }

    /// <summary>
    /// Gets the fixed descriptor digest for a direct operation step, or <see langword="null" /> for an Edit.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest? OperationDescriptorDigest { get; private init; }

    /// <summary> Gets the verdict established by a successful judging Call, or <see langword="null" />. </summary>
    [JsonInclude]
    [JsonRequired]
    public Verdict? Verdict
    {
        get => verdict;
        private init
        {
            if (value.HasValue && !TextVocabulary.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Verdict),
                    value,
                    "Operation verdict must be a defined contract value.");
            }

            if (value.HasValue && Phase != IpcExecuteOperationPhase.Call)
            {
                throw new ArgumentException(
                    "Only a Call phase result may carry a verdict.",
                    nameof(Verdict));
            }

            verdict = value;
        }
    }

    /// <summary> Gets the optional query result payload produced by the step. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; }

    /// <summary> Gets non-fatal diagnostics emitted for this public step. </summary>
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<IpcExecuteDiagnostic> Diagnostics { get; private init; }

    private readonly Verdict? verdict;
}
