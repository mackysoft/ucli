using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies one assurance verifier and the claims that reference it. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class AssuranceVerifierId : UcliStringValue
{
    /// <summary> Initializes a stable verifier identifier after validating the shared semantic-string contract. </summary>
    /// <param name="value"> The verifier identifier. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    [JsonConstructor]
    public AssuranceVerifierId (string value)
        : base(value)
    {
    }

    /// <summary> Attempts to create a verifier identifier from one candidate string. </summary>
    /// <param name="value"> The candidate verifier identifier. </param>
    /// <param name="verifierId"> The identifier when validation succeeds; otherwise, <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the candidate satisfies the semantic-string contract; otherwise, <see langword="false" />. </returns>
    public static bool TryCreate (
        string? value,
        [NotNullWhen(true)] out AssuranceVerifierId? verifierId)
    {
        verifierId = null;
        if (!IsValidString(value))
        {
            return false;
        }

        verifierId = new AssuranceVerifierId(value!);
        return true;
    }
}
