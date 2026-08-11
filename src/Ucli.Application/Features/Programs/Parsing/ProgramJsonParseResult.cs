namespace MackySoft.Ucli.Application.Features.Programs.Parsing;

/// <summary> Represents a Program parse result and its structured diagnostics. </summary>
internal sealed record ProgramJsonParseResult (
    ProgramDefinition? Program,
    IReadOnlyList<ProgramDiagnostic> Diagnostics)
{
    /// <summary> Gets whether parsing succeeded. </summary>
    public bool IsSuccess => Program is not null && Diagnostics.Count == 0;

    /// <summary> Creates a successful result. </summary>
    public static ProgramJsonParseResult Success (ProgramDefinition program)
    {
        ArgumentNullException.ThrowIfNull(program);
        return new ProgramJsonParseResult(program, Array.Empty<ProgramDiagnostic>());
    }

    /// <summary> Creates a failed result. </summary>
    public static ProgramJsonParseResult Failure (IReadOnlyList<ProgramDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new ProgramJsonParseResult(null, diagnostics);
    }
}

/// <summary> Identifies one structural Program input violation. </summary>
internal sealed record ProgramDiagnostic (string Code, string? InstancePath, string Message);
