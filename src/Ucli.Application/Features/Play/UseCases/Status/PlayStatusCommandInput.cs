namespace MackySoft.Ucli.Application.Features.Play.UseCases.Status;

/// <summary> Represents normalized CLI input values for one Play Mode status execution. </summary>
/// <param name="ProjectPath"> The optional Unity project path. </param>
/// <param name="TimeoutMilliseconds"> The optional normalized timeout in milliseconds. </param>
internal sealed record PlayStatusCommandInput (
    string? ProjectPath,
    int? TimeoutMilliseconds);
