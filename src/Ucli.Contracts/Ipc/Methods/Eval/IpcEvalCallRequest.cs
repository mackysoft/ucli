using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Carries the closed input accepted by <c>eval.call</c>. </summary>
[Description("Dedicated C# evaluation call request.")]
public sealed record IpcEvalCallRequest
{
    /// <summary> Initializes an evaluation call request. </summary>
    [JsonConstructor]
    public IpcEvalCallRequest (string source, CsEvalSourceKind sourceKind, bool allowDangerous, bool allowPlayMode, string planToken)
    {
        Source = ContractArgumentGuard.RequireValue(source, nameof(source));
        if (!TextVocabulary.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "C# eval source kind must be specified.");
        }

        SourceKind = sourceKind;
        AllowDangerous = allowDangerous;
        AllowPlayMode = allowPlayMode;
        PlanToken = ContractArgumentGuard.RequireValue(planToken, nameof(planToken));
    }

    [JsonInclude, JsonRequired, Length(1, int.MaxValue)]
    public string Source { get; private init; }

    [JsonInclude, JsonRequired]
    public CsEvalSourceKind SourceKind { get; private init; }

    [JsonInclude, JsonRequired]
    public bool AllowDangerous { get; private init; }

    [JsonInclude, JsonRequired]
    public bool AllowPlayMode { get; private init; }

    [JsonInclude, JsonRequired, Length(1, int.MaxValue)]
    public string PlanToken { get; private init; }
}
