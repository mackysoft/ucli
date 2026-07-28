using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies one normalized execution step inside the uCLI runtime. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class IpcExecuteStepId : UcliStringValue
{
    /// <summary> Initializes an internal execute-step identifier. </summary>
    /// <param name="value"> The stable runtime identifier derived from the public step's array position. </param>
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
