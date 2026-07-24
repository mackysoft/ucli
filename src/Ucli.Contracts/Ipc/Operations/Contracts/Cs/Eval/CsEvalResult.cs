using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation result.")]
public sealed record CsEvalResult
{
    [JsonConstructor]
    public CsEvalResult (
        Sha256Digest sourceDigest,
        UcliCodeSourceFormKind? sourceKind,
        string? resolvedEntryPoint,
        Sha256Digest executionDigest,
        CsEvalCompileResult compile,
        long? durationMilliseconds,
        IReadOnlyList<CsEvalLogEntry>? logs,
        CsEvalReturnValue? returnValue,
        CsEvalTouchedResources? touchedResources)
    {
        if (sourceKind.HasValue && !TextVocabulary.IsDefined(sourceKind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "C# eval source form must be defined when specified.");
        }

        if (durationMilliseconds.HasValue && durationMilliseconds.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMilliseconds), durationMilliseconds, "C# eval duration must not be negative.");
        }

        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        SourceKind = sourceKind;
        ResolvedEntryPoint = resolvedEntryPoint == null
            ? null
            : ContractArgumentGuard.RequireValue(resolvedEntryPoint, nameof(resolvedEntryPoint));
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        Compile = compile ?? throw new ArgumentNullException(nameof(compile));
        DurationMilliseconds = durationMilliseconds;
        Logs = logs == null
            ? null
            : ContractArgumentGuard.RequireItems(logs, nameof(logs));
        ReturnValue = returnValue;
        TouchedResources = touchedResources;
    }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of the UTF-8 source text.")]
    public Sha256Digest SourceDigest { get; }

    [UcliDescription("Eval source form used for compilation; omitted when the source form could not be classified.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliCodeSourceFormKind? SourceKind { get; }

    [UcliDescription("Entry point resolved from the eval source; omitted when no unique entry point was resolved.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvedEntryPoint { get; }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of normalized eval execution inputs.")]
    public Sha256Digest ExecutionDigest { get; }

    [UcliRequired]
    [UcliDescription("Compile and entry point validation result.")]
    public CsEvalCompileResult Compile { get; }

    [UcliDescription("Call duration in milliseconds; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? DurationMilliseconds { get; }

    [UcliDescription("Structured log entries recorded during call; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CsEvalLogEntry>? Logs { get; }

    [UcliDescription("JSON-serializable entry point return value; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CsEvalReturnValue? ReturnValue { get; }

    [UcliDescription("Touched resources declared by the entry point; omitted for plan results.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CsEvalTouchedResources? TouchedResources { get; }
}
