using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Carries the closed input accepted by <c>eval.plan</c>. </summary>
[Description("Dedicated C# evaluation plan request.")]
public sealed record IpcEvalPlanRequest
{
    /// <summary> Initializes an evaluation plan request. </summary>
    [JsonConstructor]
    public IpcEvalPlanRequest (string source, CsEvalSourceKind sourceKind, bool allowDangerous, bool allowPlayMode)
    {
        Source = ContractArgumentGuard.RequireValue(source, nameof(source));
        if (!TextVocabulary.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "C# eval source kind must be specified.");
        }

        SourceKind = sourceKind;
        AllowDangerous = allowDangerous;
        AllowPlayMode = allowPlayMode;
    }

    [JsonInclude, JsonRequired, Length(1, int.MaxValue)]
    public string Source { get; private init; }

    [JsonInclude, JsonRequired]
    public CsEvalSourceKind SourceKind { get; private init; }

    [JsonInclude, JsonRequired]
    public bool AllowDangerous { get; private init; }

    [JsonInclude, JsonRequired]
    public bool AllowPlayMode { get; private init; }
}
