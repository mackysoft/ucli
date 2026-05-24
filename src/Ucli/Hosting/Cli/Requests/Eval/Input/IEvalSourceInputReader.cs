namespace MackySoft.Ucli.Hosting.Cli.Requests.Eval.Input;

/// <summary> Reads C# source text for the <c>eval</c> command. </summary>
internal interface IEvalSourceInputReader
{
    /// <summary> Reads source text from a direct option, a file, or redirected standard input. </summary>
    /// <param name="source"> The optional direct source option. </param>
    /// <param name="file"> The optional source file path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The source read result. </returns>
    ValueTask<EvalSourceInputReadResult> ReadAsync (
        string? source,
        string? file,
        CancellationToken cancellationToken = default);
}
