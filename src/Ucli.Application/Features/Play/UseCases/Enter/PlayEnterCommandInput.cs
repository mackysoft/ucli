namespace MackySoft.Ucli.Application.Features.Play.UseCases.Enter;

/// <summary> Represents normalized CLI input values for one Play Mode enter execution. </summary>
/// <param name="ProjectPath"> The optional Unity project path. </param>
/// <param name="TimeoutMilliseconds"> The optional normalized timeout in milliseconds. </param>
internal sealed record PlayEnterCommandInput (
    string? ProjectPath,
    int? TimeoutMilliseconds);
