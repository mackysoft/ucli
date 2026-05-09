namespace MackySoft.Ucli.Contracts;

/// <summary> Describes one recommended follow-up action for an error code. </summary>
/// <param name="When"> The optional condition that selects this action. </param>
/// <param name="Action"> The action text intended for machine-readable catalog output. </param>
public sealed record UcliErrorNextActionDescriptor (
    string? When,
    string Action);
