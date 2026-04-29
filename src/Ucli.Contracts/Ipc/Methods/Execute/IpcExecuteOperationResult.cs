using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one public-step result within an <c>execute</c> response payload. </summary>
/// <param name="OpId"> The public step identifier that corresponds to request <c>steps[].id</c>. </param>
/// <param name="Op"> The public step name reported to clients. </param>
/// <param name="Phase"> The final phase reached by the step. </param>
/// <param name="Applied"> Whether the step has been applied. </param>
/// <param name="Changed"> Whether the step produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
public sealed record IpcExecuteOperationResult (
    string OpId,
    string Op,
    string Phase,
    bool Applied,
    bool Changed,
    IReadOnlyList<IpcExecuteTouchedResource> Touched)
{
    /// <summary> Gets the optional query result payload produced by the step. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }
}
