using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CompileGolden_PassNoReloadPayload_SatisfiesSemanticInvariants ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("compile", "pass-no-reload.json"))));
        var payload = document.RootElement.GetProperty("payload");

        var result = CreateValidator().Validate(payload);

        Assert.True(result.IsValid);
        var domainReload = payload.GetProperty("compile").GetProperty("domainReload");
        Assert.False(domainReload.GetProperty("reloadRequired").GetBoolean());
        Assert.False(domainReload.GetProperty("reloadObserved").GetBoolean());
        Assert.Equal(
            domainReload.GetProperty("generationBefore").GetString(),
            domainReload.GetProperty("generationAfter").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileGolden_CompileErrorPayload_IsVerifierFailureNotCommandFailure ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("compile", "compile-error.json"))));
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        var result = CreateValidator().Validate(payload);

        Assert.True(result.IsValid);
        Assert.Equal(IpcProtocol.StatusOk, root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal(CompileVerdictValues.Fail, payload.GetProperty("verdict").GetString());
        Assert.Equal(1, payload
            .GetProperty("compile")
            .GetProperty("scriptCompilation")
            .GetProperty("diagnostics")
            .GetProperty("errorCount")
            .GetInt32());
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
