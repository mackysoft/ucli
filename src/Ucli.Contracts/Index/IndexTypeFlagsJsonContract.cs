namespace MackySoft.Ucli.Contracts.Index;

/// <summary> Represents one type-entry flags contract in <c>types.catalog.json</c>. </summary>
/// <param name="IsAbstract"> Whether type is abstract. </param>
/// <param name="IsGenericDefinition"> Whether type is one open generic type definition. </param>
/// <param name="IsUnityObject"> Whether type derives from <c>UnityEngine.Object</c>. </param>
/// <param name="IsComponent"> Whether type derives from <c>UnityEngine.Component</c>. </param>
/// <param name="IsScriptableObject"> Whether type derives from <c>UnityEngine.ScriptableObject</c>. </param>
/// <param name="IsSerializeReferenceCandidate"> Whether type is one valid SerializeReference candidate. </param>
internal sealed record IndexTypeFlagsJsonContract (
    bool IsAbstract,
    bool IsGenericDefinition,
    bool IsUnityObject,
    bool IsComponent,
    bool IsScriptableObject,
    bool IsSerializeReferenceCandidate);