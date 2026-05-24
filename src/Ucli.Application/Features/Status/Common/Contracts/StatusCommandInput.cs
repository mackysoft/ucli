namespace MackySoft.Ucli.Application.Features.Status.Common.Contracts;

/// <summary> Represents normalized CLI input values for one status execution. </summary>
/// <param name="ProjectPath"> The optional Unity project path. </param>
/// <param name="TimeoutMilliseconds"> The optional normalized timeout in milliseconds. </param>
internal sealed record StatusCommandInput (
    string? ProjectPath,
    int? TimeoutMilliseconds);
