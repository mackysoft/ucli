using System.Text.Json;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class CliOutputGoldenCodeCatalogContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void CliOutputGoldens_UcliOwnedCodesResolveToCodeCatalog ()
    {
        var catalog = CliAssuranceSemanticInvariantValidatorFactory.CreateAllAssuranceCommandCodeCatalog();
        foreach (CliOutputGoldenFiles.GoldenDocument golden in CliOutputGoldenFiles.ReadAllDocuments())
        {
            CliOutputGoldenContractTestSupport.AssertGolden(
                golden,
                root => AssertGoldenCodesResolveToCodeCatalog(catalog, root));
        }
    }

    private static void AssertGoldenCodesResolveToCodeCatalog (
        ICodeCatalog catalog,
        JsonElement root)
    {
        var command = CliOutputGoldenContractTestSupport.ReadRequiredString(root, "command", "$.command");

        ValidateEnvelopeErrorCodes(catalog, root);
        if (!CliOutputGoldenContractTestSupport.TryGetProperty(root, "payload", JsonValueKind.Object, out var payload))
        {
            return;
        }

        ValidateOperationDiagnosticCodes(catalog, payload);
        ValidateAssuranceCodeFields(catalog, payload);
        ValidateCodesPayloadCodes(catalog, command, payload);
    }

    private static void ValidateEnvelopeErrorCodes (
        ICodeCatalog catalog,
        JsonElement root)
    {
        if (!CliOutputGoldenContractTestSupport.TryGetProperty(root, "errors", JsonValueKind.Array, out var errors))
        {
            return;
        }

        var index = 0;
        foreach (var error in errors.EnumerateArray())
        {
            if (error.ValueKind == JsonValueKind.Object && CliOutputGoldenContractTestSupport.TryGetString(error, "code", out var code))
            {
                AssertCatalogCode(catalog, code, CodeCatalogKindValues.Error, $"$.errors[{index}].code");
            }

            index++;
        }
    }

    private static void ValidateOperationDiagnosticCodes (
        ICodeCatalog catalog,
        JsonElement payload)
    {
        if (!CliOutputGoldenContractTestSupport.TryGetProperty(payload, "opResults", JsonValueKind.Array, out var opResults))
        {
            return;
        }

        var opResultIndex = 0;
        foreach (var opResult in opResults.EnumerateArray())
        {
            if (opResult.ValueKind != JsonValueKind.Object
                || !CliOutputGoldenContractTestSupport.TryGetProperty(opResult, "diagnostics", JsonValueKind.Array, out var diagnostics))
            {
                opResultIndex++;
                continue;
            }

            var diagnosticIndex = 0;
            foreach (var diagnostic in diagnostics.EnumerateArray())
            {
                if (diagnostic.ValueKind == JsonValueKind.Object
                    && CliOutputGoldenContractTestSupport.TryGetString(diagnostic, "code", out var code))
                {
                    AssertCatalogCode(catalog, code, expectedKind: null, $"$.payload.opResults[{opResultIndex}].diagnostics[{diagnosticIndex}].code");
                }

                diagnosticIndex++;
            }

            opResultIndex++;
        }
    }

    private static void ValidateAssuranceCodeFields (
        ICodeCatalog catalog,
        JsonElement payload)
    {
        if (CliOutputGoldenContractTestSupport.TryGetProperty(payload, "claims", JsonValueKind.Array, out var claims))
        {
            var claimIndex = 0;
            foreach (var claim in claims.EnumerateArray())
            {
                if (claim.ValueKind != JsonValueKind.Object)
                {
                    claimIndex++;
                    continue;
                }

                if (CliOutputGoldenContractTestSupport.TryGetString(claim, "id", out var claimId))
                {
                    AssertCatalogCode(catalog, claimId, CodeCatalogKindValues.Claim, $"$.payload.claims[{claimIndex}].id");
                }

                ValidateResidualRiskCodes(
                    catalog,
                    claim,
                    $"$.payload.claims[{claimIndex}].residualRisks");
                claimIndex++;
            }
        }

        ValidateResidualRiskCodes(catalog, payload, "$.payload.residualRisks");
    }

    private static void ValidateResidualRiskCodes (
        ICodeCatalog catalog,
        JsonElement owner,
        string path)
    {
        if (!CliOutputGoldenContractTestSupport.TryGetProperty(owner, "residualRisks", JsonValueKind.Array, out var residualRisks))
        {
            return;
        }

        var index = 0;
        foreach (var residualRisk in residualRisks.EnumerateArray())
        {
            if (residualRisk.ValueKind == JsonValueKind.Object && CliOutputGoldenContractTestSupport.TryGetString(residualRisk, "code", out var code))
            {
                AssertCatalogCode(catalog, code, CodeCatalogKindValues.Risk, $"{path}[{index}].code");
            }

            index++;
        }
    }

    private static void ValidateCodesPayloadCodes (
        ICodeCatalog catalog,
        string command,
        JsonElement payload)
    {
        if (!string.Equals(command, "codes.describe", StringComparison.Ordinal)
            && !string.Equals(command, "codes.list", StringComparison.Ordinal))
        {
            return;
        }

        if (CliOutputGoldenContractTestSupport.TryGetString(payload, "code", out var code))
        {
            AssertCatalogCode(catalog, code, expectedKind: null, "$.payload.code");
        }

        if (!CliOutputGoldenContractTestSupport.TryGetProperty(payload, "codes", JsonValueKind.Array, out var codes))
        {
            return;
        }

        var index = 0;
        foreach (var codeEntry in codes.EnumerateArray())
        {
            if (codeEntry.ValueKind == JsonValueKind.Object && CliOutputGoldenContractTestSupport.TryGetString(codeEntry, "code", out var codeValue))
            {
                AssertCatalogCode(catalog, codeValue, expectedKind: null, $"$.payload.codes[{index}].code");
            }

            index++;
        }
    }

    private static void AssertCatalogCode (
        ICodeCatalog catalog,
        string code,
        string? expectedKind,
        string path)
    {
        Assert.True(UcliCode.TryCreate(code, out var codeValue), $"{path} contains an invalid uCLI code value '{code}'.");
        Assert.True(catalog.TryFind(codeValue, out var descriptor), $"{path} code '{code}' is not registered in the production code catalog.");
        if (expectedKind != null)
        {
            Assert.Equal(expectedKind, descriptor.Kind);
        }
    }
}
