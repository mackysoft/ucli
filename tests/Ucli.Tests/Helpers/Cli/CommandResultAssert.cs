namespace MackySoft.Tests;

using System.Text.Json;
using MackySoft.Ucli.Cli;

internal static class CommandResultAssert
{
    public static void HasStandardEnvelope (
        JsonElement root,
        string command,
        string status,
        int exitCode)
    {
        JsonAssert.For(root)
            .HasInt32("protocolVersion", CliProtocol.CurrentVersion)
            .HasString("command", command)
            .HasString("status", status)
            .HasInt32("exitCode", exitCode)
            .HasValueKind("message", JsonValueKind.String)
            .HasValueKind("payload", JsonValueKind.Object)
            .HasValueKind("errors", JsonValueKind.Array);
    }

    public static void HasNoErrors (JsonElement root)
    {
        JsonAssert.For(root)
            .HasArrayLength("errors", 0);
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
