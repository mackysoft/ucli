namespace MackySoft.Ucli.Tests;

public sealed class VerifyCliOutputContractTests
{
    [Theory]
    [InlineData("default-success.json")]
    [InlineData("mutation-from-success.json")]
    [InlineData("script-compile-focused.json")]
    [InlineData("file-profile-with-test.json")]
    [Trait("Size", "Medium")]
    public void VerifyGolden_SuccessPayload_UsesSuccessfulCommandEnvelope (string fileName)
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("verify", fileName);
        var root = document.RootElement;

        Assert.Equal(
            TextVocabulary.GetText(CommandResultStatus.Ok),
            root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Pass),
            root.GetProperty("payload").GetProperty("verdict").GetString());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void VerifyGolden_ProfileConflict_IsCommandFailure ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("verify", "profile-conflict-error.json");
        var root = document.RootElement;

        Assert.Equal(
            TextVocabulary.GetText(CommandResultStatus.Error),
            root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("INVALID_ARGUMENT", root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(root.GetProperty("payload").TryGetProperty("project", out _));
    }

}
