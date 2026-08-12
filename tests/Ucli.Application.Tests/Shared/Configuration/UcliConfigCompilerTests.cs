using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Configuration;

public sealed class UcliConfigCompilerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithUnsupportedLiteral_ReturnsDiagnostic ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "unsupported",
          "planTokenMode": "optional",
          "operationAllowlist": ["^ucli\\."]
        }
        """;

        var result = Compile(json);

        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral");
        Assert.Equal(UcliConfigJsonPropertyNames.OperationPolicy, diagnostic.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithEmptyAllowlistPattern_ReturnsDiagnostic ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": [" "]
        }
        """;

        var result = Compile(json);

        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.emptyAllowlistPattern");
        Assert.Equal("operationAllowlist[0]", diagnostic.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithInvalidRegex_ReturnsDiagnostic ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": ["["]
        }
        """;

        var result = Compile(json);

        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidRegexPattern");
        Assert.Equal("operationAllowlist[0]", diagnostic.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithInvalidTimeout_ReturnsDiagnostic ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "ipcDefaultTimeoutMilliseconds": 0,
          "operationAllowlist": ["^ucli\\."]
        }
        """;

        var result = Compile(json);

        var diagnostic = AssertSingleDiagnostic(result.Diagnostics, "config.semantic.invalidTimeout");
        Assert.Equal(UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds, diagnostic.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithMultipleSemanticErrors_PreservesAllDiagnostics ()
    {
        const string json = """
        {
          "schemaVersion": 2,
          "operationPolicy": "unsupported",
          "planTokenMode": "never",
          "readIndexDefaultMode": "bad",
          "ipcDefaultTimeoutMilliseconds": 0,
          "ipcTimeoutMillisecondsByCommand": {
            "status": 0,
            "unknown": 3000
          },
          "operationAllowlist": [" ", "["]
        }
        """;

        var result = Compile(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Config);
        AssertDiagnostic(result.Diagnostics, "config.semantic.unsupportedSchemaVersion", UcliConfigJsonPropertyNames.SchemaVersion);
        AssertDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral", UcliConfigJsonPropertyNames.OperationPolicy);
        AssertDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral", UcliConfigJsonPropertyNames.PlanTokenMode);
        AssertDiagnostic(result.Diagnostics, "config.semantic.unsupportedLiteral", UcliConfigJsonPropertyNames.ReadIndexDefaultMode);
        AssertDiagnostic(result.Diagnostics, "config.semantic.invalidTimeout", UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds);
        AssertDiagnostic(result.Diagnostics, "config.semantic.invalidTimeout", "ipcTimeoutMillisecondsByCommand.status");
        AssertDiagnostic(result.Diagnostics, "config.semantic.unsupportedTimeoutCommand", "ipcTimeoutMillisecondsByCommand.unknown");
        AssertDiagnostic(result.Diagnostics, "config.semantic.emptyAllowlistPattern", "operationAllowlist[0]");
        AssertDiagnostic(result.Diagnostics, "config.semantic.invalidRegexPattern", "operationAllowlist[1]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithValidConfig_NormalizesEffectiveValues ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "dangerous",
          "planTokenMode": "required",
          "readIndexDefaultMode": "allowStale",
          "ipcDefaultTimeoutMilliseconds": 4500,
          "ipcTimeoutMillisecondsByCommand": {
            "status": null,
            "call": 15000
          },
          "operationAllowlist": [" ^ucli\\. ", "^mylab\\."]
        }
        """;

        var result = Compile(json);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Equal(OperationPolicy.Dangerous, config.OperationPolicy);
        Assert.Equal(PlanTokenMode.Required, config.PlanTokenMode);
        Assert.Equal(ReadIndexMode.AllowStale, config.ReadIndexDefaultMode);
        Assert.Equal(4500, config.IpcDefaultTimeoutMilliseconds);
        Assert.Equal(["^ucli\\.", "^mylab\\."], config.OperationAllowlist);
        Assert.Null(config.IpcTimeoutMillisecondsByCommand["status"]);
        Assert.Equal(15000, config.IpcTimeoutMillisecondsByCommand["call"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithScreenshotTimeoutOverride_AcceptsRootCommandKey ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "ipcTimeoutMillisecondsByCommand": {
            "screenshot": 7000
          },
          "operationAllowlist": ["^ucli\\."]
        }
        """;

        var result = Compile(json);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var config = Assert.IsType<UcliConfig>(result.Config);
        Assert.Equal(7000, config.IpcTimeoutMillisecondsByCommand[UcliCommandIds.Screenshot.Name]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Compile_WithProgramPreset_NormalizesRegistration ()
    {
        const string json = """
        {
          "schemaVersion": 1,
          "operationPolicy": "safe",
          "planTokenMode": "optional",
          "operationAllowlist": [],
          "programPresets": {
            "smoke": {
              "description": "Runs smoke checks.",
              "programPath": "programs/smoke.json"
            }
          }
        }
        """;

        var result = Compile(json);

        Assert.True(result.IsSuccess);
        var preset = Assert.Single(result.Config!.ProgramPresets);
        Assert.Equal("smoke", preset.Key);
        Assert.Equal("Runs smoke checks.", preset.Value.Description);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDocument_WithInvalidProgramPresets_ReturnsLoadEquivalentDiagnostics ()
    {
        var config = CreateConfigWithProgramPresets(
            new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["Invalid"] = new ProgramPresetRegistration(new string('a', 1025), RootRelativePath.Parse("smoke.json")),
            });

        var result = UcliConfigCompiler.CreateDefault().CreateDocument(config, "config.json");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "config.save.invalidProgramPresetId");
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Code == "config.save.invalidProgramPresetDescription");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDocument_WithUnorderedProgramPresets_WritesOrdinalAscendingKeys ()
    {
        var config = CreateConfigWithProgramPresets(
            new Dictionary<string, ProgramPresetRegistration>(StringComparer.Ordinal)
            {
                ["zeta"] = new ProgramPresetRegistration("Zeta.", RootRelativePath.Parse("zeta.json")),
                ["alpha"] = new ProgramPresetRegistration("Alpha.", RootRelativePath.Parse("alpha.json")),
            });

        var result = UcliConfigCompiler.CreateDefault().CreateDocument(config, "config.json");

        Assert.True(result.IsSuccess);
        Assert.Equal(["alpha", "zeta"], result.Document!.ProgramPresets!.Keys);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDocument_WithValidConfig_ReturnsSerializableDocument ()
    {
        var config = new UcliConfig(
            SchemaVersion: UcliConfig.CurrentSchemaVersion,
            OperationPolicy: OperationPolicy.Dangerous,
            PlanTokenMode: PlanTokenMode.Required,
            ReadIndexDefaultMode: ReadIndexMode.AllowStale,
            OperationAllowlist: ["^ucli\\."])
        {
            IpcDefaultTimeoutMilliseconds = 4500,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                ["status"] = null,
                ["call"] = 15000,
            },
        };

        var result = UcliConfigCompiler.CreateDefault().CreateDocument(config, "config.json");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics);
        var document = Assert.IsType<UcliConfigDocument>(result.Document);
        Assert.Equal(UcliConfig.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal("dangerous", document.OperationPolicy);
        Assert.Equal("required", document.PlanTokenMode);
        Assert.Equal("allowStale", document.ReadIndexDefaultMode);
        Assert.Equal(["^ucli\\."], document.OperationAllowlist);
        Assert.Equal(4500, document.IpcDefaultTimeoutMilliseconds);
        Assert.NotNull(document.IpcTimeoutMillisecondsByCommand);
        var timeoutOverrides = document.IpcTimeoutMillisecondsByCommand!;
        Assert.Null(timeoutOverrides["status"]);
        Assert.Equal(15000, timeoutOverrides["call"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateDocument_WithInvalidConfig_PreservesAllDiagnostics ()
    {
        var config = new UcliConfig(
            SchemaVersion: 2,
            OperationPolicy: (OperationPolicy)999,
            PlanTokenMode: (PlanTokenMode)999,
            ReadIndexDefaultMode: (ReadIndexMode)999,
            OperationAllowlist: [" ", "["])
        {
            IpcDefaultTimeoutMilliseconds = 0,
            IpcTimeoutMillisecondsByCommand = new Dictionary<string, int?>(StringComparer.Ordinal)
            {
                ["status"] = 0,
                ["unknown"] = 3000,
            },
        };

        var result = UcliConfigCompiler.CreateDefault().CreateDocument(config, "config.json");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Document);
        AssertDiagnostic(result.Diagnostics, "config.save.unsupportedSchemaVersion", UcliConfigJsonPropertyNames.SchemaVersion);
        AssertDiagnostic(result.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.OperationPolicy);
        AssertDiagnostic(result.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.PlanTokenMode);
        AssertDiagnostic(result.Diagnostics, "config.save.unsupportedEnum", UcliConfigJsonPropertyNames.ReadIndexDefaultMode);
        AssertDiagnostic(result.Diagnostics, "config.save.emptyAllowlistPattern", "operationAllowlist[0]");
        AssertDiagnostic(result.Diagnostics, "config.save.invalidRegexPattern", "operationAllowlist[1]");
        AssertDiagnostic(result.Diagnostics, "config.save.invalidTimeout", UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds);
        AssertDiagnostic(result.Diagnostics, "config.save.invalidTimeout", "ipcTimeoutMillisecondsByCommand.status");
        AssertDiagnostic(result.Diagnostics, "config.save.unsupportedTimeoutCommand", "ipcTimeoutMillisecondsByCommand.unknown");
    }

    private static UcliConfigBuildResult Compile (string json)
    {
        using var document = JsonDocument.Parse(json);
        return UcliConfigCompiler.CreateDefault().Compile(document.RootElement, "config.json");
    }

    private static UcliConfig CreateConfigWithProgramPresets (IReadOnlyDictionary<string, ProgramPresetRegistration> programPresets)
    {
        return new UcliConfig(
            SchemaVersion: UcliConfig.CurrentSchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist: [])
        {
            IpcDefaultTimeoutMilliseconds = 3000,
            IpcTimeoutMillisecondsByCommand = IpcTimeoutDefaults.CreateDefaultTimeoutOverrides(),
            ProgramPresets = programPresets,
        };
    }

    private static UcliConfigDiagnostic AssertSingleDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        return diagnostic;
    }

    private static void AssertDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode,
        string expectedPropertyPath)
    {
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Code == expectedCode
                && diagnostic.PropertyPath == expectedPropertyPath);
    }
}
