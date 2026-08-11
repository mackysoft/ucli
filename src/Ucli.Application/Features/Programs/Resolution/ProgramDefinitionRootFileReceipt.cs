using System.Text;

namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Captures one root Program file read and the path facts derived from that same read. </summary>
internal sealed class ProgramDefinitionRootFileReceipt
{
    private ProgramDefinitionRootFileReceipt (
        string json,
        ContainedPath requestedPath,
        AbsolutePath physicalPath,
        AbsolutePath requestedParent,
        AbsolutePath physicalParent)
    {
        Json = json;
        RequestedPath = requestedPath;
        PhysicalPath = physicalPath;
        RequestedParent = requestedParent;
        PhysicalParent = physicalParent;
    }

    /// <summary> Gets the strict UTF-8 decoded root Program JSON. </summary>
    public string Json { get; }

    /// <summary> Gets the lexical path requested from the reader. </summary>
    public ContainedPath RequestedPath { get; }

    /// <summary> Gets the physical path returned by the successful reader call. </summary>
    public AbsolutePath PhysicalPath { get; }

    /// <summary> Gets the parent of the requested Program file. </summary>
    public AbsolutePath RequestedParent { get; }

    /// <summary> Gets the parent of the physical Program file. </summary>
    public AbsolutePath PhysicalParent { get; }

    /// <summary> Reads and validates one root Program file exactly once. </summary>
    public static async ValueTask<ProgramDefinitionRootFileReceiptResult> ReadAsync (
        IProgramDefinitionFileReader reader,
        ContainedPath requestedPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(requestedPath);

        var read = await reader.ReadAsync(requestedPath, cancellationToken).ConfigureAwait(false);
        if (read is not ProgramDefinitionFileReadSuccess success)
        {
            return new ProgramDefinitionRootFileReceiptReadFailure(read);
        }

        string json;
        try
        {
            json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(success.Content);
        }
        catch (DecoderFallbackException exception)
        {
            return new ProgramDefinitionRootFileReceiptInvalidUtf8(exception.Message);
        }

        if (!requestedPath.Target.TryGetParent(out var requestedParent)
            || !success.PhysicalPath.TryGetParent(out var physicalParent))
        {
            return new ProgramDefinitionRootFileReceiptInvalidParent();
        }

        return new ProgramDefinitionRootFileReceiptSuccess(new ProgramDefinitionRootFileReceipt(
            json,
            requestedPath,
            success.PhysicalPath,
            requestedParent,
            physicalParent));
    }
}

/// <summary> Represents the closed outcome of creating a root Program file receipt. </summary>
internal abstract record ProgramDefinitionRootFileReceiptResult;

/// <summary> Represents a usable root Program file receipt. </summary>
internal sealed record ProgramDefinitionRootFileReceiptSuccess (
    ProgramDefinitionRootFileReceipt Receipt) : ProgramDefinitionRootFileReceiptResult;

/// <summary> Represents a failed underlying file read. </summary>
internal sealed record ProgramDefinitionRootFileReceiptReadFailure (
    ProgramDefinitionFileReadResult ReadResult) : ProgramDefinitionRootFileReceiptResult;

/// <summary> Represents bytes that cannot be decoded as strict UTF-8. </summary>
internal sealed record ProgramDefinitionRootFileReceiptInvalidUtf8 (
    string Message) : ProgramDefinitionRootFileReceiptResult;

/// <summary> Represents a requested or physical root file without a parent directory. </summary>
internal sealed record ProgramDefinitionRootFileReceiptInvalidParent : ProgramDefinitionRootFileReceiptResult;
