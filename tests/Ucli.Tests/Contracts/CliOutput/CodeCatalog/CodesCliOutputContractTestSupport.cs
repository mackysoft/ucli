using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Codes;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

internal static class CodesCliOutputContractTestSupport
{
    private static readonly Lazy<ServiceProvider> SharedCodesServiceProvider = new(UcliServiceProviderTestFactory.CreateApplication);

    public static Task<CommandExecutionResult> RunCodesListCommandAsync (
        string? kind = null,
        string? command = null)
    {
        return CommandResultCapture.ExecuteSynchronousCommandAsync(() =>
            ActivatorUtilities.CreateInstance<CodesListCommand>(
                    SharedCodesServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .List(kind, command));
    }

    public static Task<CommandExecutionResult> RunCodesDescribeCommandAsync (
        string code,
        bool requireKnown = false)
    {
        return CommandResultCapture.ExecuteSynchronousCommandAsync(() =>
            ActivatorUtilities.CreateInstance<CodesDescribeCommand>(
                    SharedCodesServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .Describe(code, requireKnown));
    }

    public static CodesListCommandFilterCase[] GetCodesListCommandFilterCases ()
    {
        return
        [
            new(
                "query.assets.find leaf",
                Kind: null,
                Command: "query.assets.find",
                ExpectedCodes:
                [
                    IpcTransportErrorCodes.IpcTimeout.Value,
                ],
                RejectedCodes:
                [
                    PlanTokenErrorCodes.PlanTokenExpired.Value,
                ]),
            new(
                "eval execution",
                CodeCatalogKindValues.Error,
                UcliCommandNames.Eval,
                ExpectedCodes:
                [
                    OperationAuthorizationErrorCodes.OperationNotAllowed.Value,
                    IpcTransportErrorCodes.IpcTimeout.Value,
                    EditorLifecycleErrorCodes.EditorCompiling.Value,
                    ExecuteRequestErrorCodes.OperationContractViolation.Value,
                ],
                RejectedCodes: []),
            new(
                "daemon logs read",
                CodeCatalogKindValues.Error,
                UcliCommandNames.LogsDaemonRead,
                ExpectedCodes:
                [
                    DaemonErrorCodes.DaemonSessionNotAvailable.Value,
                    IpcTransportErrorCodes.IpcTimeout.Value,
                ],
                RejectedCodes: []),
            new(
                "play family",
                CodeCatalogKindValues.Error,
                "play",
                ExpectedCodes:
                [
                    PlayModeErrorCodes.PlayModeSessionNotAvailable.Value,
                    PlayModeErrorCodes.PlayModeTransitionTimeout.Value,
                    PlayModeErrorCodes.PlayModeTransitionBlocked.Value,
                    PlayModeErrorCodes.PlayModeAlreadyChanging.Value,
                    PlayModeErrorCodes.PlayModeEnterRejected.Value,
                    PlayModeErrorCodes.PlayModeExitRejected.Value,
                    PlayModeErrorCodes.PlayModeStateUnknown.Value,
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor.Value,
                ],
                RejectedCodes:
                [
                    PlayModeErrorCodes.PlayModeNotActive.Value,
                    PlayModeErrorCodes.PlayModePersistenceForbidden.Value,
                ]),
            new(
                "play enter leaf",
                CodeCatalogKindValues.Error,
                "play.enter",
                ExpectedCodes:
                [
                    PlayModeErrorCodes.PlayModeSessionNotAvailable.Value,
                    PlayModeErrorCodes.PlayModeTransitionTimeout.Value,
                    PlayModeErrorCodes.PlayModeTransitionBlocked.Value,
                    PlayModeErrorCodes.PlayModeAlreadyChanging.Value,
                    PlayModeErrorCodes.PlayModeEnterRejected.Value,
                    PlayModeErrorCodes.PlayModeStateUnknown.Value,
                    PlayModeErrorCodes.PlayModeRequiresGuiEditor.Value,
                ],
                RejectedCodes:
                [
                    PlayModeErrorCodes.PlayModeExitRejected.Value,
                    PlayModeErrorCodes.PlayModeNotActive.Value,
                    PlayModeErrorCodes.PlayModePersistenceForbidden.Value,
                ]),
        ];
    }

    public static async Task AssertCodesListCommandFilterCaseAsync (CodesListCommandFilterCase testCase)
    {
        var result = await RunCodesListCommandAsync(
            kind: testCase.Kind,
            command: testCase.Command);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesList);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codeItems = payload.GetProperty("codes").EnumerateArray().ToArray();
        var codes = codeItems.Select(static code => code.GetProperty("code").GetString()!).ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codeItems, AssertListCodeItemShape);
        foreach (string expectedCode in testCase.ExpectedCodes)
        {
            Assert.True(
                codes.Contains(expectedCode, StringComparer.Ordinal),
                $"{testCase.Name} must include `{expectedCode}`.");
        }

        foreach (string rejectedCode in testCase.RejectedCodes)
        {
            Assert.False(
                codes.Contains(rejectedCode, StringComparer.Ordinal),
                $"{testCase.Name} must not include `{rejectedCode}`.");
        }
    }

    public static string[] GetCodes (JsonElement root)
    {
        return root
            .GetProperty("payload")
            .GetProperty("codes")
            .EnumerateArray()
            .Select(static code => code.GetProperty("code").GetString()!)
            .ToArray();
    }

    public static void AssertListCodeItemShape (JsonElement code)
    {
        var propertyNames = code
            .EnumerateObject()
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["category", "code", "kind", "summary"], propertyNames);
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("kind").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("category").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("summary").GetString()));
    }

    public sealed record CodesListCommandFilterCase (
        string Name,
        string? Kind,
        string Command,
        string[] ExpectedCodes,
        string[] RejectedCodes);
}
