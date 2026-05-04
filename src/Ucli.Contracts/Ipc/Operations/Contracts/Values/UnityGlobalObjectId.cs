using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity GlobalObjectId string used for exact object resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity GlobalObjectId string used for exact object resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.GlobalObjectId)]
public sealed record UnityGlobalObjectId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityGlobalObjectId" /> class. </summary>
    /// <param name="value"> The Unity GlobalObjectId string. </param>
    [JsonConstructor]
    public UnityGlobalObjectId (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a Unity GlobalObjectId contract value. </summary>
    /// <param name="value"> The Unity GlobalObjectId string. </param>
    /// <returns> The semantic Unity GlobalObjectId value. </returns>
    public static implicit operator UnityGlobalObjectId (string value)
    {
        return new UnityGlobalObjectId(value);
    }
}
