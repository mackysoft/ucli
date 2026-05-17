using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCliOutputContractTests
{
    [Theory]
    [InlineData("default-success.json")]
    [InlineData("mutation-from-success.json")]
    [InlineData("script-compile-focused.json")]
    [InlineData("file-profile-with-test.json")]
    [Trait("Size", "Small")]
    public void VerifyGolden_SuccessPayload_SatisfiesSemanticInvariants (string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("verify", fileName))));
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        var result = CreateValidator().Validate(payload);

        Assert.Equal(IpcProtocol.StatusOk, root.GetProperty("status").GetString());
        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
        Assert.Equal(
            payload.GetProperty("profile").GetProperty("digest").GetString(),
            payload.GetProperty("profileDigest").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifyGolden_ProfileConflict_IsCommandFailure ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("verify", "profile-conflict-error.json"))));
        var root = document.RootElement;

        Assert.Equal(IpcProtocol.StatusError, root.GetProperty("status").GetString());
        Assert.Equal(3, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("INVALID_ARGUMENT", root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(root.GetProperty("payload").TryGetProperty("project", out _));
    }

    private static AssuranceSemanticInvariantValidator CreateValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
                new CompileCodeCatalogContributor(),
                new VerifyCodeCatalogContributor(),
            ]),
            [
                new ReadyAssuranceSemanticInvariantRule(),
                new CompileAssuranceSemanticInvariantRule(),
            ]);
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }
}
