using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation result.")]
public sealed record CsEvalResult
{
    [JsonConstructor]
    public CsEvalResult (
        Sha256Digest sourceDigest,
        string? sourceKind,
        string? resolvedEntryPoint,
        Sha256Digest executionDigest,
        CsEvalCompileResult compile,
        long? durationMilliseconds,
        IReadOnlyList<CsEvalLogEntry>? logs,
        CsEvalReturnValue? returnValue,
        CsEvalTouchedResources? touchedResources)
    {
        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        SourceKind = sourceKind;
        ResolvedEntryPoint = resolvedEntryPoint;
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        Compile = compile;
        DurationMilliseconds = durationMilliseconds;
        Logs = logs;
        ReturnValue = returnValue;
        TouchedResources = touchedResources;
    }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of the UTF-8 source text.")]
    public Sha256Digest SourceDigest { get; init; }

    [UcliDescription("Eval source form used for compilation; omitted when the source form could not be classified.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceKind { get; init; }

    [UcliDescription("Entry point resolved from the eval source; omitted when no unique entry point was resolved.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvedEntryPoint { get; init; }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of normalized eval execution inputs.")]
    public Sha256Digest ExecutionDigest { get; init; }

    [UcliRequired]
    [UcliDescription("Compile and entry point validation result.")]
    public CsEvalCompileResult Compile { get; init; }

    [UcliDescription("Call duration in milliseconds; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMilliseconds { get; init; }

    [UcliDescription("Structured log entries recorded during call; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CsEvalLogEntry>? Logs { get; init; }

    [UcliDescription("JSON-serializable entry point return value; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CsEvalReturnValue? ReturnValue { get; init; }

    [UcliDescription("Touched resources declared by the entry point; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CsEvalTouchedResources? TouchedResources { get; init; }
}
