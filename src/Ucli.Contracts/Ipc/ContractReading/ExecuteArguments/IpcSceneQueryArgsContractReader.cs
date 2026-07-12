using System.Text.Json;
namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Provides strict readers for public <c>ucli.scene.query</c> argument objects. </summary>
internal static class IpcSceneQueryArgsContractReader
{
    /// <summary>
    /// Parses one edit-local scene-query argument object.
    /// </summary>
    /// <param name="argsElement"> The source JSON argument object. </param>
    /// <param name="contract"> The parsed argument contract when parsing succeeds. </param>
    /// <param name="errorMessage"> The validation error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the argument object matches the edit-local scene-query contract; otherwise <see langword="false" />. </returns>
    public static bool TryReadForEditSelection (
        JsonElement argsElement,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        return TryRead(
            argsElement,
            propertyRoot: "step.select.from.args",
            contextLabel: "Edit step property",
            allowScene: false,
            requireScene: false,
            out contract,
            out errorMessage);
    }

    /// <summary>
    /// Parses one public <c>ucli.scene.query</c> operation argument object.
    /// </summary>
    /// <param name="argsElement"> The source JSON argument object. </param>
    /// <param name="contract"> The parsed argument contract when parsing succeeds. </param>
    /// <param name="errorMessage"> The validation error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the argument object matches the public operation contract; otherwise <see langword="false" />. </returns>
    public static bool TryReadForOperation (
        JsonElement argsElement,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        return TryRead(
            argsElement,
            propertyRoot: "args",
            contextLabel: "Operation",
            allowScene: true,
            requireScene: true,
            out contract,
            out errorMessage);
    }

    private static bool TryRead (
        JsonElement argsElement,
        string propertyRoot,
        string contextLabel,
        bool allowScene,
        bool requireScene,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        contract = default!;
        if (argsElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}' must be an object.";
            return false;
        }

        var state = new IpcSceneQueryArgsReadState(propertyRoot, contextLabel, allowScene);
        foreach (var property in argsElement.EnumerateObject())
        {
            if (!state.TryRead(property, out errorMessage))
            {
                return false;
            }
        }

        return state.TryBuild(requireScene, out contract, out errorMessage);
    }
}
