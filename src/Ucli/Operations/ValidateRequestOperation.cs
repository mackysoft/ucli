using System.Text.Json;

namespace MackySoft.Ucli.Operations;

/// <summary> Represents one operation element in a normalized validation request. </summary>
/// <param name="OpId"> The operation identifier. </param>
/// <param name="Op"> The operation name. </param>
/// <param name="Args"> The operation arguments. </param>
internal sealed record ValidateRequestOperation (
    string? OpId,
    string? Op,
    JsonElement Args);