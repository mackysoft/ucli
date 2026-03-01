using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents one parsed request operation contract. </summary>
/// <param name="Id"> The operation identifier. </param>
/// <param name="Name"> The operation name. </param>
/// <param name="Args"> The operation arguments object. </param>
/// <param name="Alias"> The optional alias for operation output. </param>
/// <param name="Expectation"> The optional expectation constraints. </param>
internal sealed record IpcRequestContractOperation (
    string? Id,
    string? Name,
    JsonElement Args,
    string? Alias,
    ExpectationConstraints? Expectation);