using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies one public step in an <c>execute</c> request and its correlated response facts. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class IpcExecuteStepId : UcliStringValue
{
    /// <summary> Initializes a public execute-step identifier from its wire value. </summary>
    /// <param name="value"> The identifier shared by <c>steps[].id</c> and correlated <c>opId</c> fields. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value" /> is empty, contains only white-space characters, has outer whitespace, or contains malformed UTF-16 text.
    /// </exception>
    [JsonConstructor]
    public IpcExecuteStepId (string value)
        : base(value)
    {
    }
}
