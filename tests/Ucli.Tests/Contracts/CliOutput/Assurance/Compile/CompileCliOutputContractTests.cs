namespace MackySoft.Ucli.Tests;

public sealed class CompileCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_PassNoReloadPayload_KeepsDomainReloadGenerationStable ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("compile", "pass-no-reload.json");
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal(
            ["verdict", "project", "lifecycleExecutionRef", "compile"],
            payload.EnumerateObject().Select(static property => property.Name));
        var domainReload = payload.GetProperty("compile").GetProperty("domainReload");
        Assert.False(domainReload.GetProperty("reloadRequired").GetBoolean());
        Assert.False(domainReload.GetProperty("reloadObserved").GetBoolean());
        Assert.Equal(
            domainReload.GetProperty("generationBefore").GetInt64(),
            domainReload.GetProperty("generationAfter").GetInt64());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_CompileErrorPayload_IsVerifierFailureNotCommandFailure ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("compile", "compile-error.json");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.Equal(
            TextVocabulary.GetText(CommandResultStatus.Ok),
            root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Fail),
            payload.GetProperty("verdict").GetString());
        Assert.Equal(
            TextVocabulary.GetText(ExecutionLifecycle.Terminal),
            payload
                .GetProperty("lifecycleExecutionRef")
                .GetProperty("lifecycle")
                .GetString());
        Assert.Equal(1, payload
            .GetProperty("compile")
            .GetProperty("scriptCompilation")
            .GetProperty("diagnostics")
            .GetProperty("errorCount")
            .GetInt32());
    }
}
