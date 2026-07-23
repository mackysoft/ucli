namespace MackySoft.Tests;

using System.Text.Json;

internal static class CommandResultAssert
{
    public static void HasStandardEnvelope (
        JsonElement root,
        string command,
        string status,
        int exitCode)
    {
        JsonAssert.For(root)
            .HasInt32("protocolVersion", 1)
            .HasString("command", command)
            .HasString("status", status)
            .HasInt32("exitCode", exitCode)
            .HasValueKind("message", JsonValueKind.String)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("errors", JsonValueKind.Array);
    }

    public static void HasInvalidArgumentOutput (
        string standardOutput,
        string command)
    {
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        HasInvalidArgumentError(outputJson.RootElement, command);
    }

    public static void HasPreDispatchInvalidArgumentFailure<TInvocation> (
        CommandExecutionResult result,
        IReadOnlyCollection<TInvocation> serviceInvocations,
        string command)
    {
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(serviceInvocations);
        HasInvalidArgumentOutput(result.StdOut, command);
    }

    public static void HasPreDispatchInvalidArgumentFailureWithEmptyStandardError<TInvocation> (
        CommandExecutionResult result,
        IReadOnlyCollection<TInvocation> serviceInvocations,
        string command)
    {
        HasPreDispatchInvalidArgumentFailure(result, serviceInvocations, command);
        Assert.Equal(string.Empty, result.StdErr);
    }

    public static void HasInvalidArgumentEnvelope (
        JsonElement root,
        string command)
    {
        HasStandardEnvelope(
            root,
            command,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.InvalidArgument);
    }

    public static void HasInvalidArgumentError (
        JsonElement root,
        string command)
    {
        HasInvalidArgumentEnvelope(root, command);
        HasSingleError(root, UcliCoreErrorCodes.InvalidArgument);
    }

    public static void HasSuccessEnvelope (
        JsonElement root,
        string command)
    {
        HasStandardEnvelope(
            root,
            command,
            TextVocabulary.GetText(CommandResultStatus.Ok),
            (int)CliExitCode.Success);
    }

    public static void HasNoErrors (JsonElement root)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 0);
    }

    public static void ReportsUnrecognizedArgument (
        string? text,
        string argument)
    {
        Assert.Contains(
            FormatUnrecognizedArgumentMessage(argument),
            text,
            StringComparison.Ordinal);
    }

    public static void DoesNotReportUnrecognizedArguments (
        string standardError,
        params string[] arguments)
    {
        foreach (string argument in arguments)
        {
            Assert.DoesNotContain(
                FormatUnrecognizedArgumentMessage(argument),
                standardError,
                StringComparison.Ordinal);
        }
    }

    private static string FormatUnrecognizedArgumentMessage (string argument)
    {
        return $"Argument '{argument}' is not recognized.";
    }

    public static void HasSingleError (JsonElement root, UcliCode expectedCode)
    {
        HasSingleError(root, expectedCode.Value);
    }

    public static void HasSingleError (JsonElement root, string expectedCode)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", expectedCode)
                .HasValueKind("message", JsonValueKind.String)
                .IsNull("opId"));
    }
}
