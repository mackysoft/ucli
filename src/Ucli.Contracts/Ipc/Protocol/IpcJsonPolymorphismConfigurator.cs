using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Routes effective IPC contracts to the configurator that owns their tagged union.</summary>
internal static class IpcJsonPolymorphismConfigurator
{
    public static void Configure (JsonTypeInfo typeInfo)
    {
        if (ArtifactRefJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || ExecutionRefJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || UcliRequestJsonPolymorphismConfigurator.TryConfigure(typeInfo)
            || UcliReferenceJsonPolymorphismConfigurator.TryConfigure(typeInfo))
        {
            return;
        }

        UcliOperationJsonPolymorphismConfigurator.TryConfigure(typeInfo);
    }
}
