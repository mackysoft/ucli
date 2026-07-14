using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        typeof(CompileStartedEntry),
        typeof(CompileCompletedEntry),
        typeof(CompileDiagnosticEntry),
        typeof(CompileRecoveredEntry),
        typeof(CompileRefreshStartedEntry),
        typeof(TestCaseStartedEntry),
        typeof(TestCaseFinishedEntry),
        typeof(TestRunStartedEntry),
        typeof(TestRunDiagnosticEntry),
        typeof(IpcBuildRunRequest),
        typeof(IpcBuildRunResponse),
        typeof(IpcCompileRequest),
        typeof(IpcCompileSummary),
        typeof(IpcTestRunRequest),
    };

    [Theory]
    [MemberData(nameof(RunIdContractTypes))]
    [Trait("Size", "Small")]
    public void Constructor_WhenRunIdIsEmpty_ThrowsArgumentException (Type contractType)
    {
        var constructor = Assert.Single(contractType.GetConstructors());
        Assert.NotNull(constructor.GetCustomAttribute<JsonConstructorAttribute>());
        var parameters = constructor.GetParameters();
        var runIdParameter = Assert.Single(parameters, static parameter => parameter.Name == "RunId");
        var arguments = parameters
            .Select(static parameter => parameter.HasDefaultValue
                ? parameter.DefaultValue
                : CreateDefaultValue(parameter.ParameterType))
            .ToArray();
        arguments[Array.IndexOf(parameters, runIdParameter)] = Guid.Empty;

        var exception = Assert.Throws<TargetInvocationException>(() => constructor.Invoke(arguments));
        var argumentException = Assert.IsType<ArgumentException>(exception.InnerException);

        Assert.Equal("RunId", argumentException.ParamName);
    }

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

    private static object? CreateDefaultValue (Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
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
