using System.Text.Json;

namespace MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one detailed operation payload entry. </summary>
/// <param name="Name"> The operation name. </param>
/// <param name="Kind"> The operation kind literal. </param>
/// <param name="Policy"> The operation policy literal. </param>
/// <param name="ArgsSchema"> The JSON schema object for operation arguments. </param>
internal sealed record OpsOperationDetail (
    string Name,
    string Kind,
    string Policy,
    JsonElement ArgsSchema);
