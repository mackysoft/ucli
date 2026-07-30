using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one operation execution step result. </summary>
/// <param name="Op"> The public step operation name. </param>
/// <param name="Phase"> The final phase reached by the step. </param>
/// <param name="Applied"> Whether the step has been applied. </param>
/// <param name="Changed"> Whether the step produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
internal sealed record OperationExecutionOperationResult
{
    private OperationExecutionOperationResult (
        string op,
        IpcExecuteOperationPhase phase,
        bool applied,
        bool changed,
        IReadOnlyList<OperationExecutionTouchedResource> touched,
        Sha256Digest? operationDescriptorDigest,
        Verdict? verdict,
        JsonElement? result,
        IReadOnlyList<OperationExecutionDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(op);
        ArgumentNullException.ThrowIfNull(touched);
        ArgumentNullException.ThrowIfNull(diagnostics);
        if (!TextVocabulary.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "Operation phase must be specified.");
        }

        if (verdict.HasValue && !TextVocabulary.IsDefined(verdict.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(verdict),
                verdict,
                "Operation verdict must be a defined contract value.");
        }

        if (result is { ValueKind: JsonValueKind.Undefined })
        {
            throw new ArgumentException(
                "An operation result payload must contain a defined JSON value.",
                nameof(result));
        }

        if (verdict.HasValue && !result.HasValue)
        {
            throw new ArgumentException(
                "An operation result that carries a verdict must also carry its result evidence.",
                nameof(result));
        }

        if (verdict.HasValue && operationDescriptorDigest == null)
        {
            throw new ArgumentNullException(
                nameof(operationDescriptorDigest),
                "An operation result that carries a verdict must identify the descriptor used to establish it.");
        }

        if (verdict.HasValue && phase != IpcExecuteOperationPhase.Call)
        {
            throw new ArgumentException(
                "Only a Call phase result may carry a verdict.",
                nameof(verdict));
        }

        Op = op;
        Phase = phase;
        Applied = applied;
        Changed = changed;
        Touched = touched;
        OperationDescriptorDigest = operationDescriptorDigest;
        Verdict = verdict;
        Result = result;
        Diagnostics = diagnostics;
    }

    public string Op { get; }

    public IpcExecuteOperationPhase Phase { get; }

    public bool Applied { get; }

    public bool Changed { get; }

    public IReadOnlyList<OperationExecutionTouchedResource> Touched { get; }

    /// <summary>
    /// Gets the fixed descriptor digest for a direct operation step, or <see langword="null" /> for an Edit.
    /// </summary>
    public Sha256Digest? OperationDescriptorDigest { get; }

    /// <summary> Gets the verdict established by a successful judging Call, or <see langword="null" />. </summary>
    public Verdict? Verdict { get; }

    /// <summary> Gets the optional query result payload produced by the step. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; }

    /// <summary> Gets non-fatal diagnostics emitted for this public step. </summary>
    public IReadOnlyList<OperationExecutionDiagnostic> Diagnostics { get; }

    /// <summary> Creates a result that does not establish a verdict. </summary>
    public static OperationExecutionOperationResult CreateWithoutVerdict (
        string op,
        IpcExecuteOperationPhase phase,
        bool applied,
        bool changed,
        IReadOnlyList<OperationExecutionTouchedResource> touched,
        Sha256Digest? operationDescriptorDigest,
        JsonElement? result,
        IReadOnlyList<OperationExecutionDiagnostic> diagnostics)
    {
        return new OperationExecutionOperationResult(
            op,
            phase,
            applied,
            changed,
            touched,
            operationDescriptorDigest,
            verdict: null,
            result,
            diagnostics);
    }

    /// <summary> Creates a successful judging Call result with its verdict and result evidence. </summary>
    public static OperationExecutionOperationResult CreateJudgingCallResult (
        string op,
        bool applied,
        bool changed,
        IReadOnlyList<OperationExecutionTouchedResource> touched,
        Sha256Digest operationDescriptorDigest,
        Verdict verdict,
        JsonElement result,
        IReadOnlyList<OperationExecutionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(operationDescriptorDigest);

        return new OperationExecutionOperationResult(
            op,
            IpcExecuteOperationPhase.Call,
            applied,
            changed,
            touched,
            operationDescriptorDigest,
            verdict,
            result,
            diagnostics);
    }
}
