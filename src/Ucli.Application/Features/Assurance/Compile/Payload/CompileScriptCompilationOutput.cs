namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents script compilation evidence grouped under <c>payload.compile.scriptCompilation</c>. </summary>
internal sealed record CompileScriptCompilationOutput (
    bool Started,
    bool Completed,
    long? CompileGenerationBefore,
    long? CompileGenerationAfter,
    CompileDiagnosticsOutput Diagnostics);
