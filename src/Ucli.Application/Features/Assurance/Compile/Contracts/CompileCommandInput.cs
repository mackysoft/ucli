namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;

/// <summary> Represents normalized inputs for the <c>compile</c> assurance command. </summary>
internal sealed record CompileCommandInput (
    AbsolutePath? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds);
