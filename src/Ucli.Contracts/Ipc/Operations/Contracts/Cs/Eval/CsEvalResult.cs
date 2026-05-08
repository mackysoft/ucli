using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation result.")]
public sealed record CsEvalResult
{
    [JsonConstructor]
    public CsEvalResult (
        string sourceDigest,
        string entryPoint,
        string executionDigest,
        CsEvalCompileResult compile,
        long? durationMilliseconds,
        IReadOnlyList<CsEvalLogEntry>? logs,
        CsEvalReturnValue? returnValue,
        CsEvalTouchedResources? touchedResources)
    {
        SourceDigest = sourceDigest;
        EntryPoint = entryPoint;
        ExecutionDigest = executionDigest;
        Compile = compile;
        DurationMilliseconds = durationMilliseconds;
        Logs = logs;
        ReturnValue = returnValue;
        TouchedResources = touchedResources;
    }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of the UTF-8 source text.")]
    public string SourceDigest { get; init; }

    [UcliRequired]
    [UcliDescription("Entry point selected for the eval source.")]
    public string EntryPoint { get; init; }

    [UcliRequired]
    [UcliDescription("SHA-256 digest of normalized eval execution inputs.")]
    public string ExecutionDigest { get; init; }

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
