using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents the primary machine-readable diagnostic attached to one daemon diagnosis. </summary>
/// <param name="Kind"> The normalized diagnostic kind value. </param>
/// <param name="Code"> The diagnostic code when available. </param>
/// <param name="File"> The diagnostic file path when available. </param>
/// <param name="Line"> The one-based diagnostic line number when available. </param>
/// <param name="Column"> The one-based diagnostic column number when available. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record DaemonDiagnosisPrimaryDiagnosticJsonContract
{
    /// <summary> Initializes persisted primary diagnostic fields. </summary>
    [JsonConstructor]
    public DaemonDiagnosisPrimaryDiagnosticJsonContract (
        DaemonDiagnosisPrimaryDiagnosticKind? Kind,
        string? Code,
        string? File,
        int? Line,
        int? Column,
        string? Message)
    {
        if (Kind.HasValue && !TextVocabulary.IsDefined(Kind.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported primary diagnostic kind.");
        }

        this.Kind = Kind;
        this.Code = Code;
        this.File = File;
        this.Line = Line;
        this.Column = Column;
        this.Message = Message;
    }

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisPrimaryDiagnosticKind? Kind { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? Code { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? File { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public int? Line { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public int? Column { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? Message { get; private init; }
}
