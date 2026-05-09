namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Contracts.Storage;

public sealed class DaemonStartupFailureLogClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenCompilerDiagnosticExists_ReturnsStructuredCompilerDiagnostic ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Assets/Foo.cs(74,17): error CS1739: Missing parameter\nSafe Mode enabled\n",
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification!.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.FixCompileErrors, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisStartupPhaseValues.ScriptCompilation, classification.StartupPhase);
        Assert.NotNull(classification.PrimaryDiagnostic);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, classification.PrimaryDiagnostic!.Kind);
        Assert.Equal("CS1739", classification.PrimaryDiagnostic.Code);
        Assert.Equal("Assets/Foo.cs", classification.PrimaryDiagnostic.File);
        Assert.Equal(74, classification.PrimaryDiagnostic.Line);
        Assert.Equal(17, classification.PrimaryDiagnostic.Column);
        Assert.Equal("Missing parameter", classification.PrimaryDiagnostic.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenCompilerAndSafeModeMarkersExist_PrioritizesCompilerError ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Safe Mode is active\nAssets/Foo.cs(10,1): error CS0246: MissingType\n",
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification!.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenPackageResolutionFails_ReturnsPackageResolutionReason ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.unity.test-framework: Package cannot be found
            """,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityPackageResolutionFailed, classification!.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.ResolvePackages, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenSafeModeMarkerExists_ReturnsUserActionRequiredReason ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Unity Editor entered Safe Mode and is waiting for user action.\n",
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorUserActionRequired, classification!.Reason);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.ResolveUnityDialog, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.UnityDialog, classification.PrimaryDiagnostic!.Kind);
    }
}
