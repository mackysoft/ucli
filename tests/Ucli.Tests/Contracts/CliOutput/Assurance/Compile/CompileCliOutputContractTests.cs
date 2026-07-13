using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_PassNoReloadPayload_SatisfiesSemanticInvariants ()
    {
        using var document = CliOutputGoldenFiles.ReadJsonDocument("compile", "pass-no-reload.json");
        var payload = document.RootElement.GetProperty("payload");

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateCompileValidator().Validate(payload);

        Assert.True(result.IsValid);
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

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateCompileValidator().Validate(payload);

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

    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_WhenErrorCountConflictsWithPassedClaim_ReturnsSemanticViolation ()
    {
        var payloadNode = LoadGoldenPayloadNode("pass-no-reload.json");
        payloadNode["compile"]!["scriptCompilation"]!["diagnostics"]!["errorCount"] = 1;

        var result = Validate(payloadNode);

        AssertViolationPath(result, "$.claims[0].status");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_WhenRefreshOriginIsUnknown_ReturnsSemanticViolation ()
    {
        var payloadNode = LoadGoldenPayloadNode("pass-no-reload.json");
        payloadNode["compile"]!["refresh"]!["origin"] = "unknown";

        var result = Validate(payloadNode);

        AssertViolationPath(result, "$.compile.refresh.origin");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_WhenDomainReloadUnsettledConflictsWithPassedClaim_ReturnsSemanticViolation ()
    {
        var payloadNode = LoadGoldenPayloadNode("pass-no-reload.json");
        payloadNode["compile"]!["domainReload"]!["settled"] = false;

        var result = Validate(payloadNode);

        AssertViolationPath(result, "$.claims[1].status");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void CompileGolden_WhenLifecycleNotReadyConflictsWithPassedClaim_ReturnsSemanticViolation ()
    {
        var payloadNode = LoadGoldenPayloadNode("pass-no-reload.json");
        payloadNode["compile"]!["lifecycle"]!["canAcceptExecutionRequests"] = false;

        var result = Validate(payloadNode);

        AssertViolationPath(result, "$.claims[2].status");
    }

    private static AssuranceSemanticInvariantValidationResult Validate (JsonNode payloadNode)
    {
        using var document = JsonDocument.Parse(payloadNode.ToJsonString());
        return CliAssuranceSemanticInvariantValidatorFactory.CreateCompileValidator().Validate(document.RootElement);
    }

    private static JsonNode LoadGoldenPayloadNode (string fileName)
    {
        var rootNode = CliOutputGoldenFiles.ReadJsonNode("compile", fileName);
        return rootNode["payload"] ?? throw new InvalidOperationException("Compile golden payload is missing.");
    }

    private static void AssertViolationPath (
        AssuranceSemanticInvariantValidationResult result,
        string path)
    {
        Assert.Contains(result.Violations, violation => string.Equals(violation.Path, path, StringComparison.Ordinal));
    }

}
