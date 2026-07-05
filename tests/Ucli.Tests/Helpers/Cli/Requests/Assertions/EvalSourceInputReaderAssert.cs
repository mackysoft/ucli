namespace MackySoft.Tests;

internal static class EvalSourceInputReaderAssert
{
    public static void SnippetRead (
        RecordingEvalSourceInputReader sourceReader,
        string expectedSource,
        CancellationToken expectedCancellationToken)
    {
        var invocation = Assert.Single(sourceReader.Invocations);
        Assert.Equal(expectedSource, invocation.Source);
        Assert.Null(invocation.File);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
    }

    public static void FileRead (
        RecordingEvalSourceInputReader sourceReader,
        string expectedFile)
    {
        var invocation = Assert.Single(sourceReader.Invocations);
        Assert.Null(invocation.Source);
        Assert.Equal(expectedFile, invocation.File);
    }

}
