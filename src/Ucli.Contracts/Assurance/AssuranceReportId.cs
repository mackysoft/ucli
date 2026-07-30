using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies one report within an assurance result. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class AssuranceReportId : UcliStringValue
{
    /// <summary> Initializes a report identifier after validating the shared semantic-string contract. </summary>
    /// <param name="value"> The report identifier. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    [JsonConstructor]
    public AssuranceReportId (string value)
        : base(value)
    {
    }
}
