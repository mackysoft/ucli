namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents script compilation evidence grouped under <c>payload.compile.scriptCompilation</c>. </summary>
internal sealed record CompileScriptCompilationOutput (
    bool Started,
    bool Completed,
    string CompileGenerationBefore,
    string CompileGenerationAfter,
    CompileDiagnosticsOutput Diagnostics);
