namespace MackySoft.Ucli.Application.Features.Play.UseCases.Exit;

/// <summary> Represents normalized CLI input values for one Play Mode exit execution. </summary>
/// <param name="ProjectPath"> The optional Unity project path. </param>
/// <param name="TimeoutMilliseconds"> The optional normalized timeout in milliseconds. </param>
internal sealed record PlayExitCommandInput (
    string? ProjectPath,
    int? TimeoutMilliseconds);
