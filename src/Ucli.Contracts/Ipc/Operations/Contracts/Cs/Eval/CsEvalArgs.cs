using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation arguments.")]
public sealed record CsEvalArgs
{
    [JsonConstructor]
    public CsEvalArgs (
        string source,
        string entryPoint)
    {
        Source = source;
        EntryPoint = entryPoint;
    }

    [UcliRequired]
    [UcliDescription("Complete C# compilation unit to compile in memory.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Source { get; init; }

    [UcliRequired]
    [UcliDescription("Fully qualified public static entry point in Namespace.Type.Method form.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string EntryPoint { get; init; }
}
