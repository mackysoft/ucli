using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Routes effective IPC contracts to the configurator that owns their tagged union.</summary>
internal static class IpcJsonPolymorphismConfigurator
{
    public static void Configure (JsonTypeInfo typeInfo)
    {
        if (ArtifactRefJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || ExecutionRefJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || GameViewRecordingJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || IpcGameViewRecordingJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || LifecycleExecutionTerminalRecordJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || UcliRequestJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || UcliReferenceJsonPolymorphismConfigurator.TryConfigure(typeInfo))
        {
            return;
        }

        UcliOperationJsonPolymorphismConfigurator.TryConfigure(typeInfo);
    }
}
