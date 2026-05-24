using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an operation args contract with no accepted public properties. </summary>
[UcliDescription("No operation arguments are accepted.")]
public sealed record UcliEmptyArgs;
