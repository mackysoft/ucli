using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents an operation args contract with no accepted public properties. </summary>
[Description("No operation arguments are accepted.")]
public sealed record UcliEmptyArgs;
