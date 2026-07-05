using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCliOutputContractTests
{
    [Theory]
    [InlineData("default-success.json")]
    [InlineData("mutation-from-success.json")]
    [InlineData("script-compile-focused.json")]
    [InlineData("file-profile-with-test.json")]
    [Trait("Size", "Medium")]
    public void VerifyGolden_SuccessPayload_SatisfiesSemanticInvariants (string fileName)
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("verify", fileName);
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateVerifyValidator().Validate(payload);

        Assert.Equal(IpcProtocol.StatusOk, root.GetProperty("status").GetString());
        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void VerifyGolden_ProfileConflict_IsCommandFailure ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("verify", "profile-conflict-error.json");
        var root = document.RootElement;

        Assert.Equal(IpcProtocol.StatusError, root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("INVALID_ARGUMENT", root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(root.GetProperty("payload").TryGetProperty("project", out _));
    }

}
