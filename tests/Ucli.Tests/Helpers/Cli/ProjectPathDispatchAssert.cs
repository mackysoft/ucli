namespace MackySoft.Tests;

internal static class ProjectPathDispatchAssert
{
    public static void EqualNormalized (string? expectedInput, AbsolutePath? actual)
    {
        if (expectedInput is null)
        {
            Assert.Null(actual);
            return;
        }

        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        var isValid = AbsolutePath.TryResolve(
            currentDirectory,
            expectedInput,
            out var expected,
            out var failure);
        Assert.True(isValid, failure.Message);
        Assert.Equal(expected, actual);
    }
}
