namespace MackySoft.Ucli.Application.Tests.Daemon;

using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Startup;
using MackySoft.Ucli.Contracts.Storage;

public sealed class DaemonStartupFailureLogClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenCompilerDiagnosticExistsInBatchmode_ReturnsStructuredCompilerDiagnostic ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Assets/Foo.cs(74,17): error CS1739: Missing parameter\nSafe Mode enabled\n",
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, classification.RetryDisposition);
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
    public void TryClassifyFailure_WhenGuiSafeModeAndCompilerMarkersExist_PrioritizesSafeModeUserAction ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Unity Editor entered Safe Mode and is waiting for user action.\nAssets/Foo.cs(10,1): error CS0246: MissingType\n",
            DaemonStartupFailureClassificationContext.Gui,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.SafeMode, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorUserActionRequired, classification.Reason);
        Assert.Equal("editorUserActionRequired", classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.ManualActionRequired, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.ResolveUnityDialog, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.UnityDialog, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenBatchmodeSafeModeMarkerExists_ReturnsCompileBlocker ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Unity Editor entered Safe Mode and is waiting for user action.\n",
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.Compile, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityScriptCompilationFailed, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, classification.RetryDisposition);
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
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityPackageResolutionFailed, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.ResolvePackages, classification.ActionRequired);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
        Assert.Null(classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenNuGetForUnityRestoreFails_ReturnsNuGetDiagnosticCode ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            [NuGetForUnity] Restoring packages
            Failed to restore package MackySoft.Ucli.Contracts because the source is unavailable.
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityPackageResolutionFailed, classification.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
        Assert.Equal("NUGET_FOR_UNITY_RESTORE_FAILED", classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenNuGetForUnityIsMentionedWithoutSpecificRestoreFailure_DoesNotReturnNuGetDiagnosticCode ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            [NuGetForUnity] Starting package restore
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            Failed to restore package com.example.unrelated because the source is unavailable.
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
        Assert.NotEqual("NUGET_FOR_UNITY_RESTORE_FAILED", classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenNuGetForUnityMentionIsNotRestoreContext_DoesNotReturnNuGetDiagnosticCode ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            [NuGetForUnity] Package cache is ready
            Failed to restore package com.example.unrelated because the source is unavailable.
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
        Assert.NotEqual("NUGET_FOR_UNITY_RESTORE_FAILED", classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenNuGetForUnityRestoreFailureMentionsUcliPackage_ReturnsNuGetPackageResolution ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            [NuGetForUnity] Restoring packages
            Failed to restore package MackySoft.Ucli.Contracts because the package was not found.
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityPackageResolutionFailed, classification.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PackageResolution, classification.PrimaryDiagnostic!.Kind);
        Assert.Equal("NUGET_FOR_UNITY_RESTORE_FAILED", classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenUcliDependencyIsMissingWithoutNuGetRestoreLog_DoesNotReturnNuGetDiagnosticCode ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            "Library/PackageCache/MackySoft.Ucli.Unity/Editor/Bootstrap.cs(1,1): error CS0234: The type or namespace name 'Contracts' does not exist in the namespace 'MackySoft.Ucli'\n",
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.UcliPlugin, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UcliPluginDependencyMissing, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, classification.RetryDisposition);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.PluginDependency, classification.PrimaryDiagnostic!.Kind);
        Assert.NotEqual("NUGET_FOR_UNITY_RESTORE_FAILED", classification.PrimaryDiagnostic.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenPrecompiledAssemblyConflictAndCompileErrorExist_PrioritizesPrecompiledAssemblyConflict ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            Assets/Foo.cs(10,1): error CS0246: MissingType
            Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PrecompiledAssemblyConflict, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.PrecompiledAssemblyConflict, classification.Reason);
        Assert.Equal(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, classification.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenGuiSafeModeAndAllLowerPrioritySignalsExist_PrioritizesSafeMode ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.
            Could not load file or assembly 'MackySoft.Ucli.Infrastructure'
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            Assets/Foo.cs(10,1): error CS0246: MissingType
            Unity Editor entered Safe Mode and is waiting for user action.
            """,
            DaemonStartupFailureClassificationContext.Gui,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.SafeMode, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorUserActionRequired, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.ManualActionRequired, classification.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenGuiModalDialogAndAllLowerPrioritySignalsExist_PrioritizesModalDialog ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.
            Could not load file or assembly 'MackySoft.Ucli.Infrastructure'
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            Assets/Foo.cs(10,1): error CS0246: MissingType
            Unity Editor is blocked by a modal dialog and waiting for user action.
            """,
            DaemonStartupFailureClassificationContext.Gui,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.ModalDialog, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.EditorUserActionRequired, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.ManualActionRequired, classification.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenPrecompiledConflictAndUcliDependencyExist_PrioritizesPrecompiledAssemblyConflict ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            Could not load file or assembly 'MackySoft.Ucli.Infrastructure'
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            Multiple precompiled assemblies with the same name Newtonsoft.Json.dll included on the current platform.
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PrecompiledAssemblyConflict, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.PrecompiledAssemblyConflict, classification.Reason);
        Assert.Equal(DaemonStartupRetryDispositionValues.RetryAfterFix, classification.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenUcliDependencyAndPackageResolutionExist_PrioritizesUcliPlugin ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            Could not load file or assembly 'MackySoft.Ucli.Infrastructure'
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.UcliPlugin, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UcliPluginDependencyMissing, classification.Reason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryClassifyFailure_WhenPackageResolutionAndCompileErrorExist_PrioritizesPackageResolution ()
    {
        var result = DaemonStartupFailureLogClassifier.TryClassifyFailure(
            """
            Assets/Foo.cs(10,1): error CS0246: MissingType
            An error occurred while resolving packages:
              Project has invalid dependencies:
                com.example.missing: Package cannot be found
            """,
            DaemonStartupFailureClassificationContext.Batchmode,
            out var classification);

        Assert.True(result);
        Assert.NotNull(classification);
        Assert.Equal(DaemonStartupBlockingReasonValues.PackageResolution, classification!.StartupBlockingReason);
        Assert.Equal(DaemonDiagnosisReasonValues.UnityPackageResolutionFailed, classification.Reason);
    }
}
