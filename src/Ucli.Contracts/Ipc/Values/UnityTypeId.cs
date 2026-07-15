using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Unity type identifier that must resolve in the project. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity type identifier that must resolve in the project.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.TypeExists)]
public sealed class UnityTypeId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityTypeId" /> class. </summary>
    /// <param name="value"> The Unity type identifier. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    [JsonConstructor]
    public UnityTypeId (string value)
        : base(value)
    {
    }

    /// <summary> Attempts to parse one Unity type identifier. </summary>
    /// <param name="value"> The candidate Unity type identifier. </param>
    /// <param name="typeId"> The parsed identifier when parsing succeeds; otherwise, <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is valid; otherwise, <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityTypeId? typeId)
    {
        typeId = null;
        if (!IsValidString(value))
        {
            return false;
        }

        typeId = new UnityTypeId(value!);
        return true;
    }
}
