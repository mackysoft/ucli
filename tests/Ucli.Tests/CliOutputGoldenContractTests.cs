using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

public sealed class CliOutputGoldenContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static readonly string CliOutputGoldenRoot = Path.Combine(
        RepositoryRoot,
        "tests",
        "Ucli.Tests",
        "GoldenFiles",
        "Json",
        "CliOutput");

    [Theory]
    [MemberData(nameof(GetCliOutputGoldenFiles))]
    [Trait("Size", "Small")]
    public void AssuranceCommandGoldens_WithOkStatus_SatisfyCommonShapeAndSemanticInvariants (string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, relativePath)));
        var root = document.RootElement;
        var command = ReadRequiredString(root, "command", "$.command");
        if (!IsAssuranceCommand(command)
            || !string.Equals(ReadRequiredString(root, "status", "$.status"), IpcProtocol.StatusOk, StringComparison.Ordinal))
        {
            return;
        }

        var payload = ReadRequiredObject(root, "payload", "$.payload");
        AssertPropertyKind(payload, "verdict", JsonValueKind.String, "$.payload.verdict");
        AssertPropertyKind(payload, "project", JsonValueKind.Object, "$.payload.project");
        AssertPropertyKind(payload, "verifiers", JsonValueKind.Array, "$.payload.verifiers");
        AssertPropertyKind(payload, "claims", JsonValueKind.Array, "$.payload.claims");
        AssertPropertyKind(payload, "reports", JsonValueKind.Object, "$.payload.reports");
        AssertPropertyKind(payload, "residualRisks", JsonValueKind.Array, "$.payload.residualRisks");

        var result = CreateAssuranceValidator().Validate(payload);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Theory]
    [MemberData(nameof(GetCliOutputGoldenFiles))]
    [Trait("Size", "Small")]
    public void CliOutputGoldens_UcliOwnedCodesResolveToCodeCatalog (string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, relativePath)));
        var root = document.RootElement;
        var catalog = CreateCodeCatalog();
        var command = ReadRequiredString(root, "command", "$.command");

        ValidateEnvelopeErrorCodes(catalog, root);
        if (!TryGetProperty(root, "payload", JsonValueKind.Object, out var payload))
        {
            return;
        }

        ValidateOperationDiagnosticCodes(catalog, payload);
        ValidateAssuranceCodeFields(catalog, payload);
        ValidateCodesPayloadCodes(catalog, command, payload);
    }

    [Theory]
    [MemberData(nameof(GetCliOutputGoldenFiles))]
    [Trait("Size", "Small")]
    public void OpsDescribeGoldens_AssuranceContractsExposeLatestShape (string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, relativePath)));
        var root = document.RootElement;
        if (!string.Equals(ReadRequiredString(root, "command", "$.command"), "ops.describe", StringComparison.Ordinal)
            || !TryGetProperty(root, "payload", JsonValueKind.Object, out var payload)
            || !TryGetProperty(payload, "operation", JsonValueKind.Object, out var operation))
        {
            return;
        }

        var assurance = ReadRequiredObject(operation, "assurance", "$.payload.operation.assurance");
        AssertPropertyKind(assurance, "sideEffects", JsonValueKind.Array, "$.payload.operation.assurance.sideEffects");
        AssertBooleanProperty(assurance, "mayDirty", "$.payload.operation.assurance.mayDirty");
        AssertBooleanProperty(assurance, "mayPersist", "$.payload.operation.assurance.mayPersist");
        AssertPropertyKind(assurance, "touchedKinds", JsonValueKind.Array, "$.payload.operation.assurance.touchedKinds");
        AssertPropertyKind(assurance, "planMode", JsonValueKind.String, "$.payload.operation.assurance.planMode");
        AssertPropertyKind(assurance, "planSemantics", JsonValueKind.String, "$.payload.operation.assurance.planSemantics");
        AssertPropertyKind(assurance, "callSemantics", JsonValueKind.String, "$.payload.operation.assurance.callSemantics");
        AssertPropertyKind(assurance, "touchedContract", JsonValueKind.String, "$.payload.operation.assurance.touchedContract");
        AssertPropertyKind(assurance, "readPostconditionContract", JsonValueKind.String, "$.payload.operation.assurance.readPostconditionContract");
        AssertPropertyKind(assurance, "failureSemantics", JsonValueKind.String, "$.payload.operation.assurance.failureSemantics");
        AssertPropertyKind(assurance, "dangerousNotes", JsonValueKind.Array, "$.payload.operation.assurance.dangerousNotes");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RepositoryArtifacts_DoNotReferenceErrorsCommand ()
    {
        string[] prohibitedCommandFragments =
        [
            "ucli errors",
            "errors describe",
            "errors list",
            "errors explain",
        ];
        var violations = EnumerateDocumentedArtifactPaths()
            .SelectMany(path => FindProhibitedCommandFragments(path, prohibitedCommandFragments))
            .ToArray();

        Assert.Empty(violations);
    }

    public static IEnumerable<object[]> GetCliOutputGoldenFiles ()
    {
        return Directory
            .EnumerateFiles(CliOutputGoldenRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => new object[]
            {
                Path.GetRelativePath(RepositoryRoot, path),
            });
    }

    private static bool IsAssuranceCommand (string command)
    {
        return string.Equals(command, "ready", StringComparison.Ordinal)
            || string.Equals(command, "compile", StringComparison.Ordinal)
            || string.Equals(command, "verify", StringComparison.Ordinal);
    }

    private static AssuranceSemanticInvariantValidator CreateAssuranceValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            CreateCodeCatalog(),
            [
                new ReadyAssuranceSemanticInvariantRule(),
                new CompileAssuranceSemanticInvariantRule(),
                new VerifyAssuranceSemanticInvariantRule(),
            ]);
    }

    private static CodeCatalog CreateCodeCatalog ()
    {
        return new CodeCatalog(
        [
            new ContractsCodeCatalogContributor(),
            new ApplicationCodeCatalogContributor(),
            new ReadyCodeCatalogContributor(),
            new CompileCodeCatalogContributor(),
            new VerifyCodeCatalogContributor(),
        ]);
    }

    private static void ValidateEnvelopeErrorCodes (
        ICodeCatalog catalog,
        JsonElement root)
    {
        if (!TryGetProperty(root, "errors", JsonValueKind.Array, out var errors))
        {
            return;
        }

        var index = 0;
        foreach (var error in errors.EnumerateArray())
        {
            if (error.ValueKind == JsonValueKind.Object && TryGetString(error, "code", out var code))
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
        if (!TryGetProperty(payload, "opResults", JsonValueKind.Array, out var opResults))
        {
            return;
        }

        var opResultIndex = 0;
        foreach (var opResult in opResults.EnumerateArray())
        {
            if (opResult.ValueKind != JsonValueKind.Object
                || !TryGetProperty(opResult, "diagnostics", JsonValueKind.Array, out var diagnostics))
            {
                opResultIndex++;
                continue;
            }

            var diagnosticIndex = 0;
            foreach (var diagnostic in diagnostics.EnumerateArray())
            {
                if (diagnostic.ValueKind == JsonValueKind.Object && TryGetString(diagnostic, "code", out var code))
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
        if (TryGetProperty(payload, "claims", JsonValueKind.Array, out var claims))
        {
            var claimIndex = 0;
            foreach (var claim in claims.EnumerateArray())
            {
                if (claim.ValueKind != JsonValueKind.Object)
                {
                    claimIndex++;
                    continue;
                }

                if (TryGetString(claim, "id", out var claimId))
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
        if (!TryGetProperty(owner, "residualRisks", JsonValueKind.Array, out var residualRisks))
        {
            return;
        }

        var index = 0;
        foreach (var residualRisk in residualRisks.EnumerateArray())
        {
            if (residualRisk.ValueKind == JsonValueKind.Object && TryGetString(residualRisk, "code", out var code))
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

        if (TryGetString(payload, "code", out var code))
        {
            AssertCatalogCode(catalog, code, expectedKind: null, "$.payload.code");
        }

        if (!TryGetProperty(payload, "codes", JsonValueKind.Array, out var codes))
        {
            return;
        }

        var index = 0;
        foreach (var codeEntry in codes.EnumerateArray())
        {
            if (codeEntry.ValueKind == JsonValueKind.Object && TryGetString(codeEntry, "code", out var codeValue))
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

    private static JsonElement ReadRequiredObject (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(JsonValueKind.Object, property.ValueKind);
        return property;
    }

    private static string ReadRequiredString (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(JsonValueKind.String, property.ValueKind);
        return property.GetString() ?? string.Empty;
    }

    private static void AssertPropertyKind (
        JsonElement owner,
        string propertyName,
        JsonValueKind expectedKind,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.Equal(expectedKind, property.ValueKind);
    }

    private static void AssertBooleanProperty (
        JsonElement owner,
        string propertyName,
        string path)
    {
        Assert.True(owner.TryGetProperty(propertyName, out var property), $"{path} is missing.");
        Assert.True(property.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{path} must be boolean.");
    }

    private static bool TryGetProperty (
        JsonElement owner,
        string propertyName,
        JsonValueKind expectedKind,
        out JsonElement property)
    {
        if (owner.ValueKind == JsonValueKind.Object
            && owner.TryGetProperty(propertyName, out property)
            && property.ValueKind == expectedKind)
        {
            return true;
        }

        property = default;
        return false;
    }

    private static bool TryGetString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        if (owner.ValueKind == JsonValueKind.Object
            && owner.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateDocumentedArtifactPaths ()
    {
        var readmePath = Path.Combine(RepositoryRoot, "README.md");
        if (File.Exists(readmePath))
        {
            yield return readmePath;
        }

        string[] roots =
        [
            Path.Combine(RepositoryRoot, "skills", "definitions"),
            Path.Combine(RepositoryRoot, "skills", "generated"),
            CliOutputGoldenRoot,
        ];
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> FindProhibitedCommandFragments (
        string path,
        IReadOnlyList<string> prohibitedCommandFragments)
    {
        var text = File.ReadAllText(path);
        for (var i = 0; i < prohibitedCommandFragments.Count; i++)
        {
            if (text.Contains(prohibitedCommandFragments[i], StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{Path.GetRelativePath(RepositoryRoot, path)} contains '{prohibitedCommandFragments[i]}'.";
            }
        }
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
