using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Reads a file after proving that its physical node remains within a lexical boundary. </summary>
internal interface IProgramDefinitionFileReader
{
    /// <summary> Reads one contained document and returns its confirmed physical path. </summary>
    ValueTask<ProgramDefinitionFileReadResult> ReadAsync (ContainedPath path, CancellationToken cancellationToken = default);
}

/// <summary> Represents the closed outcome of a Program definition document read. </summary>
internal abstract record ProgramDefinitionFileReadResult;

/// <summary> Represents bytes read from a confirmed physical file within the requested boundary. </summary>
internal sealed record ProgramDefinitionFileReadSuccess (
    byte[] Content,
    AbsolutePath PhysicalPath) : ProgramDefinitionFileReadResult;

/// <summary> Represents a physical containment failure. </summary>
internal sealed record ProgramDefinitionFileReadOutsideBoundary : ProgramDefinitionFileReadResult;

/// <summary> Represents an unavailable, non-regular, or unreadable file. </summary>
internal sealed record ProgramDefinitionFileReadUnavailable (string Message) : ProgramDefinitionFileReadResult;

/// <summary> Represents a read discarded because a path or node identity changed during the operation. </summary>
internal sealed record ProgramDefinitionFileReadChangedDuringRead : ProgramDefinitionFileReadResult;
