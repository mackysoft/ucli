using System.Text.Json;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Assurance;

public sealed class CompileProgressExecutionIdContractTests
{
    private const string EmptyExecutionIdJson =
        "{\"executionId\":\"00000000-0000-0000-0000-000000000000\"}";

    public static TheoryData<Type> CompileProgressTypes => new()
    {
        typeof(CompileStartedEntry),
        typeof(CompileCompletedEntry),
        typeof(CompileDiagnosticEntry),
        typeof(CompileRecoveredEntry),
        typeof(CompileRefreshStartedEntry),
    };

    [Theory]
    [MemberData(nameof(CompileProgressTypes))]
    [Trait("Size", "Small")]
    public void JsonDeserialize_WhenExecutionIdIsMissingOrEmpty_ThrowsArgumentException (
        Type contractType)
    {
        foreach (var json in new[] { "{}", EmptyExecutionIdJson })
        {
            var exception = Record.Exception(
                () => JsonSerializer.Deserialize(
                    json,
                    contractType,
                    IpcJsonSerializerOptions.Default));
            var argumentException = FindArgumentException(exception);

            Assert.NotNull(argumentException);
            Assert.Equal("ExecutionId", argumentException.ParamName);
        }
    }

    private static ArgumentException? FindArgumentException (
        Exception? exception)
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
