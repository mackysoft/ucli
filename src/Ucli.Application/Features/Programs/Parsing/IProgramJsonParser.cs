namespace MackySoft.Ucli.Application.Features.Programs.Parsing;

/// <summary> Parses Program JSON through its closed runtime contract. </summary>
internal interface IProgramJsonParser
{
    /// <summary> Parses one Program document. </summary>
    ProgramJsonParseResult Parse (string json);

    /// <summary> Parses one strictly encoded UTF-8 Program document. </summary>
    ProgramJsonParseResult Parse (ReadOnlySpan<byte> utf8Json);
}
