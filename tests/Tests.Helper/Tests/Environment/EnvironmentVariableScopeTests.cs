namespace MackySoft.Ucli.Tests;

using MackySoft.Tests;

[Collection(EnvironmentStateTestCollection.Name)]
public sealed class EnvironmentVariableScopeTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void Dispose_RestoresExistingEnvironmentVariableValue ()
    {
        var variableName = CreateVariableName();
        Environment.SetEnvironmentVariable(variableName, "before");

        try
        {
            using (new EnvironmentVariableScope(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [variableName] = "during",
            }))
            {
                Assert.Equal("during", Environment.GetEnvironmentVariable(variableName));
            }

            Assert.Equal("before", Environment.GetEnvironmentVariable(variableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Dispose_RemovesEnvironmentVariable_WhenOriginallyUnset ()
    {
        var variableName = CreateVariableName();
        Environment.SetEnvironmentVariable(variableName, null);

        using (new EnvironmentVariableScope(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [variableName] = "during",
        }))
        {
            Assert.Equal("during", Environment.GetEnvironmentVariable(variableName));
        }

        Assert.Null(Environment.GetEnvironmentVariable(variableName));
    }

    private static string CreateVariableName ()
    {
        return "UCLI_TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
    }
}
