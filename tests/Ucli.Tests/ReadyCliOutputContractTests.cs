using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ReadyGolden_AutoOneshotPayload_SatisfiesSemanticInvariants ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("ready", "auto-oneshot-success.json"))));
        var payload = document.RootElement.GetProperty("payload");

        var result = CreateValidator().Validate(payload);

        Assert.True(result.IsValid);
        var claim = Assert.Single(payload.GetProperty("claims").EnumerateArray());
        var validity = claim.GetProperty("validity");
        Assert.Equal(ReadyValidityKindValues.ProbeOnly, validity.GetProperty("kind").GetString());
        Assert.False(validity.GetProperty("guaranteesReusableSession").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyGolden_ReadIndexPayload_UsesArtifactOnlySession ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            CliOutputGoldenFiles.GetPath("ready", "read-index-success.json"))));
        var payload = document.RootElement.GetProperty("payload");

        var result = CreateValidator().Validate(payload);

        Assert.True(result.IsValid);
        Assert.Equal("readIndex", payload.GetProperty("target").GetString());
        Assert.Equal(AssuranceExecutionModeCodec.NotApplicable, payload.GetProperty("resolvedMode").GetString());
        Assert.Equal(AssuranceSessionKindValues.ArtifactOnly, payload.GetProperty("sessionKind").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("lifecycle").ValueKind);
        Assert.Equal(JsonValueKind.Object, payload.GetProperty("readIndex").ValueKind);
        Assert.Equal(3, payload.GetProperty("readIndex").GetProperty("artifacts").GetArrayLength());
    }

    private static AssuranceSemanticInvariantValidator CreateValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
                new ReadyCodeCatalogContributor(),
            ]),
            [new ReadyAssuranceSemanticInvariantRule()]);
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
