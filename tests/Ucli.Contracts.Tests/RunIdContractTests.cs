using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Contracts.Tests;

public sealed class RunIdContractTests
{
    private const string EmptyRunIdJson = "{\"runId\":\"00000000-0000-0000-0000-000000000000\"}";

    public static TheoryData<Type> RunIdContractTypes => new()
    {
        typeof(BuildDiagnosticEntry),
        typeof(BuildLogEntry),
        typeof(BuildProgressEntry),
        typeof(TestCaseStartedEntry),
        typeof(TestCaseFinishedEntry),
        typeof(TestRunStartedEntry),
        typeof(TestRunDiagnosticEntry),
        typeof(IpcBuildRunRequest),
        typeof(IpcBuildRunResponse),
        typeof(IpcTestRunRequest),
    };

    [Theory]
    [MemberData(nameof(RunIdContractTypes))]
    [Trait("Size", "Small")]
    public void JsonDeserialize_WhenRunIdIsMissingOrEmpty_ThrowsArgumentException (Type contractType)
    {
        foreach (var json in new[] { "{}", EmptyRunIdJson })
        {
            var exception = Record.Exception(
                () => JsonSerializer.Deserialize(json, contractType, IpcJsonSerializerOptions.Default));
            var argumentException = FindArgumentException(exception);

            Assert.NotNull(argumentException);
            Assert.Equal("RunId", argumentException.ParamName);
        }
    }

    private static ArgumentException? FindArgumentException (Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is ArgumentException argumentException)
            {
                return argumentException;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
