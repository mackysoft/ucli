using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("C# eval operation arguments.")]
public sealed record CsEvalArgs
{
    [JsonConstructor]
    public CsEvalArgs (
        string source)
    {
        Source = ContractArgumentGuard.RequireValue(source, nameof(source));
    }

    [JsonInclude]
    [JsonRequired]
    [Description("C# source to compile in memory. Accepts either a complete compilation unit or a Run method body snippet.")]
    [Length(1, int.MaxValue)]
    public string Source { get; private init; }
}
