using MackySoft.Ucli.Application.Diagnostics;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_OrdersDescriptorsByCode ()
    {
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => descriptor.Code.Value)
            .ToArray();
        var expectedCodes = actualCodes
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCodes, actualCodes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, actualCodes);
        Assert.Contains(ProjectContextErrorCodes.ProjectPathNotFound.Value, actualCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_ContainsEveryApplicationErrorCodeDefinition ()
    {
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();
        var expectedCodes = StaticFieldValueReader.ReadFromStaticClasses<UcliCode>(
            typeof(ApplicationErrorCodeDescriptors).Assembly,
            "ErrorCodes");

        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_ConvertsErrorsToErrorKind ()
    {
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();

        foreach (var descriptor in catalog.Descriptors)
        {
            Assert.Equal(CodeCatalogKind.Error, descriptor.Kind);
            Assert.Contains("errors[].code", descriptor.AppearsIn);
            ErrorInspectTargetAssert.DoesNotUseBroadOrSensitiveTargets(descriptor.Inspect);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithReadyContributor_RegistersReadyClaimCodes ()
    {
        var catalog = new CodeCatalogModel(
            [
                new ReadyCodeCatalogContributor(),
            ]);

        Assert.True(catalog.TryFind(ReadyClaimCodes.UnityReadyExecution, out var descriptor));
        Assert.Equal(CodeCatalogKind.Claim, descriptor.Kind);
        Assert.Equal("ready", descriptor.Category);
        Assert.Contains(UcliCommandIds.Ready, descriptor.AppliesTo);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithProductionContributors_RegistersReadyAndModeDecisionCodes ()
    {
        var catalog = CodeCatalogTestSupport.CreateProductionCatalog();

        Assert.True(catalog.TryFind(ReadyClaimCodes.UnityReadyReadIndex, out var readyDescriptor));
        Assert.Equal(CodeCatalogKind.Claim, readyDescriptor.Kind);
        Assert.Contains(UcliCommandIds.Ready, readyDescriptor.AppliesTo);

        Assert.True(catalog.TryFind(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, out var daemonNotRunningDescriptor));
        Assert.Contains(UcliCommandIds.Ready, daemonNotRunningDescriptor.AppliesTo);
        Assert.Equal(
            daemonNotRunningDescriptor.AppliesTo.Count,
            daemonNotRunningDescriptor.AppliesTo.Distinct().Count());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_RegistersBuildErrorCodes ()
    {
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();

        foreach (var code in BuildErrorCodes.All)
        {
            Assert.True(catalog.TryFind(code, out var descriptor));
            Assert.Equal(CodeCatalogKind.Error, descriptor.Kind);
            Assert.Equal("build", descriptor.Category);
            Assert.Contains(UcliCommandIds.BuildRun, descriptor.AppliesTo);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_RegistersProjectContextCodesForTheirResolutionBoundaries ()
    {
        UcliCommand[] projectResolvingCommands =
        [
            UcliCommandIds.Status,
            UcliCommandIds.Ready,
            UcliCommandIds.Compile,
            UcliCommandIds.BuildRun,
            UcliCommandIds.Verify,
            UcliCommandIds.DaemonStart,
            UcliCommandIds.DaemonStop,
            UcliCommandIds.DaemonCleanup,
            UcliCommandIds.DaemonStatus,
            UcliCommandIds.DaemonList,
            UcliCommandIds.LogsDaemonRead,
            UcliCommandIds.LogsUnityRead,
            UcliCommandIds.LogsUnityClear,
            UcliCommandIds.Screenshot,
            UcliCommandIds.Play,
            UcliCommandIds.Validate,
            UcliCommandIds.Plan,
            UcliCommandIds.Call,
            UcliCommandIds.Eval,
            UcliCommandIds.Resolve,
            UcliCommandIds.Query,
            UcliCommandIds.Refresh,
            UcliCommandIds.Ops,
            UcliCommandIds.TestRun,
        ];
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();

        UcliCode[] projectPathCodes =
        [
            ProjectContextErrorCodes.ProjectPathInvalidFormat,
            ProjectContextErrorCodes.ProjectPathNotFound,
            ProjectContextErrorCodes.UnityProjectMarkerMissing,
        ];
        foreach (var code in projectPathCodes)
        {
            Assert.True(catalog.TryFind(code, out var descriptor));
            Assert.Equal(projectResolvingCommands, descriptor.AppliesTo);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithProductionContributors_DoesNotExposePolicyReasonCodes ()
    {
        var catalog = CodeCatalogTestSupport.CreateProductionCatalog();

        foreach (var descriptor in catalog.Descriptors)
        {
            Assert.DoesNotContain("policyReason", descriptor.Code.Value, StringComparison.Ordinal);
            Assert.DoesNotContain(descriptor.AppearsIn, static fieldPath => fieldPath.Contains("policyReason", StringComparison.Ordinal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CodeCatalogKind_HasStableLiteralsAndInvalidDefault ()
    {
        Assert.Equal(
            [
                "error",
                "diagnostic",
                "reason",
                "claim",
                "risk",
                "unknown",
            ],
            TextVocabulary.GetTexts<CodeCatalogKind>());
        Assert.False(TextVocabulary.IsDefined(default(CodeCatalogKind)));
    }
}
