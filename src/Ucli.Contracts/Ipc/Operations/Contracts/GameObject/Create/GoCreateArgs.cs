using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Base contract for one concrete GameObject creation placement. </summary>
[Description("GameObject creation operation arguments.")]
public abstract record GoCreateArgs
{
    /// <summary> Initializes the shared GameObject creation contract. </summary>
    /// <param name="name"> The name assigned to the created GameObject. </param>
    protected GoCreateArgs (string name)
    {
        Name = ContractArgumentGuard.RequireValue(name, nameof(name));
    }

    /// <summary> Gets the name assigned to the created GameObject. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Name assigned to the created GameObject.")]
    [Length(1, int.MaxValue)]
    public string Name { get; private init; }
}
