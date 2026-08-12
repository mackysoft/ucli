using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.Programs.Parsing;

/// <summary> Represents one validated, closed Program definition. </summary>
internal sealed record ProgramDefinition (
    IReadOnlyList<ProgramStep> Steps,
    JsonElement RootDocument);

/// <summary> Represents one Program step selected by its fixed command. </summary>
internal abstract record ProgramStep (int? TimeoutMilliseconds);

/// <summary> Invokes exactly one request represented inline or by a referenced request document. </summary>
internal abstract record CallProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Invokes a request defined directly in the Program document. </summary>
internal sealed record InlineCallProgramStep (
    int? TimeoutMilliseconds,
    ValidateRequest Request) : CallProgramStep(TimeoutMilliseconds);

/// <summary> Invokes a request read from a path relative to the Program reference root. </summary>
internal sealed record ReferencedCallProgramStep (
    int? TimeoutMilliseconds,
    RootRelativePath RequestPath) : CallProgramStep(TimeoutMilliseconds);

/// <summary> Observes that the project is ready. </summary>
internal sealed record ReadyProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Refreshes the project. </summary>
internal sealed record RefreshProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Compiles the project. </summary>
internal sealed record CompileProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Enters Play Mode. </summary>
internal sealed record PlayEnterProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Exits Play Mode. </summary>
internal sealed record PlayExitProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);

/// <summary> Captures the Game view. </summary>
internal sealed record ScreenshotGameProgramStep (
    int? TimeoutMilliseconds,
    int? Width,
    int? Height) : ProgramStep(TimeoutMilliseconds);

/// <summary> Captures the Scene view. </summary>
internal sealed record ScreenshotSceneProgramStep (int? TimeoutMilliseconds) : ProgramStep(TimeoutMilliseconds);
