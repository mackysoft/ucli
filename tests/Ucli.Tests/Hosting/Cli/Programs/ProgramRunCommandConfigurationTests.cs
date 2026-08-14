using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Programs;

namespace MackySoft.Ucli.Tests;

public sealed class ProgramRunCommandConfigurationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateEffectiveConfiguration_WhenEvalIsEnabled_MatchesTheUnityProgramSnapshotAndCallBinding ()
    {
        var project = new ProjectContext(
            ResolvedUnityProjectContextTestFactory.Create(),
            UcliConfig.CreateDefault() with { EvalEnabled = true },
            ConfigSource.Default);
        var capturedAtUtc = new DateTimeOffset(2026, 8, 13, 1, 2, 3, TimeSpan.Zero);

        var cliConfiguration = ProgramRunCommand.CreateEffectiveConfiguration(project, capturedAtUtc);
        var unityConfiguration = new IpcProgramEffectiveConfigurationSnapshot(
            cliConfiguration.SchemaVersion,
            TextVocabulary.GetText(cliConfiguration.OperationPolicy),
            TextVocabulary.GetText(cliConfiguration.PlanTokenMode),
            TextVocabulary.GetText(cliConfiguration.ReadIndexDefaultMode),
            cliConfiguration.OperationAllowlist,
            cliConfiguration.IpcDefaultTimeoutMilliseconds,
            cliConfiguration.IpcTimeoutMillisecondsByCommand,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                cliConfiguration.SchemaVersion,
                TextVocabulary.GetText(cliConfiguration.OperationPolicy),
                TextVocabulary.GetText(cliConfiguration.PlanTokenMode),
                TextVocabulary.GetText(cliConfiguration.ReadIndexDefaultMode),
                cliConfiguration.OperationAllowlist,
                cliConfiguration.IpcDefaultTimeoutMilliseconds,
                cliConfiguration.IpcTimeoutMillisecondsByCommand));
        Assert.Equal(unityConfiguration.Digest, cliConfiguration.Digest);

        var requestDigest = Sha256Digest.Compute("program-call"u8);
        var callBinding = new IpcProgramRequestExecutionBinding(
            new UnityProjectIdentity(project.UnityProject.UnityProjectRoot.Value, project.UnityProject.ProjectFingerprint, project.UnityProject.UnityVersion),
            new LifecycleExecutionHostRegistration(new ProcessIdentity(41, 1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new UnityEditorGenerationSnapshot(0, 0, 0, 0),
            capturedAtUtc.AddMinutes(1),
            requestDigest,
            requestDigest,
            planTokenDigest: null,
            [requestDigest],
            requestDigest,
            cliConfiguration.Digest);
        var start = new IpcProgramRequestStartRequest(
            Guid.NewGuid(),
            callBinding,
            new IpcExecuteRequest(UcliCommandIds.Call.Name, IpcPayloadCodec.SerializeToElement(new { protocolVersion = IpcProtocol.CurrentVersion, steps = Array.Empty<object>() })));

        Assert.Equal(unityConfiguration.Digest, start.Binding.ConfigurationDigest);
    }
}
