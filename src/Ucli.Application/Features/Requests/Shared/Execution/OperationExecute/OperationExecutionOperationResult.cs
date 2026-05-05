using System.Text.Json;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;

/// <summary> Represents one operation execution step result. </summary>
/// <param name="OpId"> The public step identifier. </param>
/// <param name="Op"> The public step operation name. </param>
/// <param name="Phase"> The final phase reached by the step. </param>
/// <param name="Applied"> Whether the step has been applied. </param>
/// <param name="Changed"> Whether the step produced persistent changes. </param>
/// <param name="Touched"> The touched persistence-unit resources. </param>
internal sealed record OperationExecutionOperationResult (
    string OpId,
    string Op,
    string Phase,
    bool Applied,
    bool Changed,
    IReadOnlyList<OperationExecutionTouchedResource> Touched)
{
    /// <summary> Gets the optional query result payload produced by the step. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Result { get; init; }
}
