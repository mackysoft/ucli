using MackySoft.Ucli.Application.Features.Programs.Parsing;

namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Represents a resolved Program definition or its diagnostics. </summary>
internal sealed record ProgramDefinitionResolutionResult (
    ResolvedProgramDefinition? Definition,
    IReadOnlyList<ProgramDiagnostic> Diagnostics)
{
    /// <summary> Gets whether resolution succeeded. </summary>
    public bool IsSuccess => Definition is not null && Diagnostics.Count == 0;

    /// <summary> Creates a successful result. </summary>
    public static ProgramDefinitionResolutionResult Success (ResolvedProgramDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ProgramDefinitionResolutionResult(definition, Array.Empty<ProgramDiagnostic>());
    }

    /// <summary> Creates a failed result. </summary>
    public static ProgramDefinitionResolutionResult Failure (IReadOnlyList<ProgramDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new ProgramDefinitionResolutionResult(null, diagnostics);
    }
}

/// <summary> Represents the immutable, fully resolved Program definition. </summary>
internal sealed record ResolvedProgramDefinition (
    ProgramDefinition Program,
    IReadOnlyList<ResolvedProgramSource> Sources,
    ProgramSourceManifest SourceManifest,
    string DefinitionDigest);

/// <summary> Represents one resolved request source. </summary>
internal sealed record ResolvedProgramSource (
    string InstancePath,
    string Path,
    string DocumentDigest,
    int ByteLength,
    string DocumentJson);

/// <summary> Represents the Program source manifest fixed alongside a resolved definition. </summary>
internal sealed record ProgramSourceManifest (
    string Digest,
    ProgramRootSource RootSource,
    string? RootPath,
    string? PresetId,
    string ProgramDigest,
    IReadOnlyList<ProgramSourceManifestEntry> Sources);

/// <summary> Represents one request document recorded by a Program source manifest. </summary>
internal sealed record ProgramSourceManifestEntry (
    string InstancePath,
    string Role,
    string Path,
    string DocumentDigest,
    int ByteLength);
