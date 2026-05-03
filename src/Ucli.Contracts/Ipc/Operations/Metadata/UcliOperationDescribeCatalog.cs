using MackySoft.Ucli.Contracts.Index;
using C = MackySoft.Ucli.Contracts.Ipc.UcliOperationInputConstraintContract;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides built-in operation describe contracts shared by Unity discovery and CLI projection. </summary>
public static class UcliOperationDescribeCatalog
{
    private const string ArrayValueType = "array";
    private const string IntegerValueType = "integer";
    private const string ObjectValueType = "object";
    private const string StringValueType = "string";

    private const string AssetKindAsset = "asset";
    private const string AssetKindPrefab = "prefab";
    private const string AssetKindProjectSettings = "projectSettings";
    private const string AssetKindScene = "scene";

    private const string TargetKindAsset = "asset";
    private const string TargetKindComponent = "component";
    private const string TargetKindGameObject = "gameObject";

    private const string TypeKindComponent = "component";

    private const string AccessWrite = "write";

    private const string OpensSceneSideEffect = "opensSceneInEditor";
    private const string OpensPrefabSideEffect = "opensPrefabStage";
    private const string RefreshesAssetDatabaseSideEffect = "refreshesAssetDatabase";
    private const string WritesAssetSideEffect = "writesAsset";
    private const string WritesSceneSideEffect = "writesScene";
    private const string WritesPrefabSideEffect = "writesPrefab";
    private const string WritesProjectSideEffect = "writesProjectSettings";

    /// <summary> Gets the built-in describe contract for one primitive operation. </summary>
    /// <param name="operationName"> The primitive operation name. </param>
    /// <returns> The describe contract. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="operationName" /> is unsupported. </exception>
    public static UcliOperationDescribeContract Get (string operationName)
    {
        return operationName switch
        {
            UcliPrimitiveOperationNames.Resolve => Resolve(),
            UcliPrimitiveOperationNames.AssetCreate => AssetCreate(),
            UcliPrimitiveOperationNames.AssetSchema => AssetSchema(),
            UcliPrimitiveOperationNames.AssetSet => AssetSet(),
            UcliPrimitiveOperationNames.AssetsFind => AssetsFind(),
            UcliPrimitiveOperationNames.CompEnsure => CompEnsure(),
            UcliPrimitiveOperationNames.CompSchema => CompSchema(),
            UcliPrimitiveOperationNames.CompSet => CompSet(),
            UcliPrimitiveOperationNames.GoCreate => GoCreate(),
            UcliPrimitiveOperationNames.GoDelete => GoDelete(),
            UcliPrimitiveOperationNames.GoDescribe => GoDescribe(),
            UcliPrimitiveOperationNames.GoReparent => GoReparent(),
            UcliPrimitiveOperationNames.PrefabCreate => PrefabCreate(),
            UcliPrimitiveOperationNames.PrefabOpen => PrefabOpen(),
            UcliPrimitiveOperationNames.PrefabSave => PrefabSave(),
            UcliPrimitiveOperationNames.ProjectRefresh => ProjectRefresh(),
            UcliPrimitiveOperationNames.ProjectSave => ProjectSave(),
            UcliPrimitiveOperationNames.SceneOpen => SceneOpen(),
            UcliPrimitiveOperationNames.SceneQuery => SceneQuery(),
            UcliPrimitiveOperationNames.SceneSave => SceneSave(),
            UcliPrimitiveOperationNames.SceneTree => SceneTree(),
            _ => throw new ArgumentException("Operation describe contract is not registered.", nameof(operationName)),
        };
    }

    private static UcliOperationDescribeContract Resolve ()
    {
        return Describe(
            "Resolves an asset, scene object, prefab object, or component reference to a Unity GlobalObjectId.",
            new[] { Input("selector", ObjectValueType, "Reference to resolve.", NoConstraints(), "$", ResolveVariants("$")) },
            UcliOperationResultContract.One<IpcResolveOperationResult>("Resolved Unity object reference."),
            Assurance(Array.Empty<string>(), false, false, Array.Empty<string>(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract AssetCreate ()
    {
        return Describe(
            "Creates a Unity asset at a project-relative path.",
            new[] { TypeInput(), AssetPathInput("path", "Project-relative path to create.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesAssetSideEffect }, false, true, new[] { IpcExecuteTouchedResourceKindNames.Asset }, UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract AssetSchema ()
    {
        return Describe(
            "Returns the serialized schema for an asset type or existing asset target.",
            new[] { Input("subject", ObjectValueType, "Asset type or asset target to inspect.", NoConstraints(), "$", AssetSchemaSubjectVariants()) },
            UcliOperationResultContract.One<IndexSchemaEntryJsonContract>("Serialized property schema for the requested asset subject."),
            Assurance(Array.Empty<string>(), false, false, Array.Empty<string>(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract AssetSet ()
    {
        return Describe(
            "Assigns serialized property values on an asset or project asset target.",
            new[] { AssetReferenceInput(), SetsInput() },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesAssetSideEffect }, true, false, new[] { IpcExecuteTouchedResourceKindNames.Asset, IpcExecuteTouchedResourceKindNames.ProjectSettings }, UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract AssetsFind ()
    {
        return Describe(
            "Finds project assets by type, path prefix, or name substring.",
            new[]
            {
                Input("type", StringValueType, "Optional asset type identifier filter.", Constraints(C.TypeExists())),
                Input("pathPrefix", StringValueType, "Optional project-relative asset path prefix.", Constraints(C.NonEmpty(), C.ProjectRelativePath())),
                Input("nameContains", StringValueType, "Optional case-sensitive asset name substring.", Constraints(C.NonEmpty())),
            },
            UcliOperationResultContract.One<AssetsFindResult>("Asset search result containing matching assets."),
            Assurance(Array.Empty<string>(), false, false, Array.Empty<string>(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract CompEnsure ()
    {
        return Describe(
            "Ensures that a GameObject has a component of the requested type.",
            new[] { GameObjectReferenceInput(), TypeInput() },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect, WritesPrefabSideEffect }, true, false, SceneAndPrefabTouchedKinds(), UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract CompSchema ()
    {
        return Describe(
            "Returns the serialized schema for a component type.",
            new[] { TypeInput() },
            UcliOperationResultContract.One<IndexSchemaEntryJsonContract>("Serialized property schema for the requested component type."),
            Assurance(Array.Empty<string>(), false, false, Array.Empty<string>(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract CompSet ()
    {
        return Describe(
            "Assigns serialized property values on a component target.",
            new[] { ComponentReferenceInput(), SetsInput() },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect, WritesPrefabSideEffect }, true, false, SceneAndPrefabTouchedKinds(), UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract GoCreate ()
    {
        return Describe(
            "Creates a GameObject in a scene or under an existing parent.",
            new[] { Input("name", StringValueType, "Name assigned to the created GameObject.", Constraints(C.NonEmpty())), GoCreateDestinationInput() },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect, WritesPrefabSideEffect }, true, false, SceneAndPrefabTouchedKinds(), UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract GoDelete ()
    {
        return Describe(
            "Deletes a GameObject from a scene or prefab hierarchy.",
            new[] { GameObjectReferenceInput() },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect, WritesPrefabSideEffect }, true, false, SceneAndPrefabTouchedKinds(), UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract GoDescribe ()
    {
        return Describe(
            "Returns a GameObject description including components and child hierarchy.",
            new[] { GameObjectReferenceInput(), DepthInput("Maximum child hierarchy depth to include.") },
            UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description with components and child objects."),
            Assurance(Array.Empty<string>(), false, false, Array.Empty<string>(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract GoReparent ()
    {
        return Describe(
            "Moves a GameObject under a new parent GameObject.",
            new[] { GameObjectReferenceInput(), GameObjectReferenceInput("parent") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect, WritesPrefabSideEffect }, true, false, SceneAndPrefabTouchedKinds(), UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract PrefabCreate ()
    {
        return Describe(
            "Creates a prefab asset from a scene GameObject.",
            new[] { SceneGameObjectReferenceInput(), PrefabCreatablePathInput("path", "Project-relative prefab asset path to create.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesPrefabSideEffect }, false, true, new[] { IpcExecuteTouchedResourceKindNames.Prefab }, UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract PrefabOpen ()
    {
        return Describe(
            "Opens a prefab asset in the Unity editor.",
            new[] { PrefabPathInput("path", "Project-relative prefab asset path to open.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { OpensPrefabSideEffect }, false, false, new[] { IpcExecuteTouchedResourceKindNames.Prefab }, UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract PrefabSave ()
    {
        return Describe(
            "Saves an opened or previewed prefab asset.",
            new[] { PrefabPathInput("path", "Project-relative prefab asset path to save.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesPrefabSideEffect }, false, true, new[] { IpcExecuteTouchedResourceKindNames.Prefab }, UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract ProjectRefresh ()
    {
        return Describe(
            "Refreshes Unity AssetDatabase and reports resources changed by import.",
            Array.Empty<UcliOperationInputContract>(),
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { RefreshesAssetDatabaseSideEffect }, false, false, AllPersistentTouchedKinds(), UcliOperationPlanModeValues.ValidationOnly));
    }

    private static UcliOperationDescribeContract ProjectSave ()
    {
        return Describe(
            "Saves dirty project assets, scenes, and prefab stages known to uCLI.",
            Array.Empty<UcliOperationInputContract>(),
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesAssetSideEffect, WritesSceneSideEffect, WritesPrefabSideEffect, WritesProjectSideEffect }, false, true, AllPersistentTouchedKinds(), UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract SceneOpen ()
    {
        return Describe(
            "Opens a Unity scene asset in the editor.",
            new[] { ScenePathInput("path", "Project-relative scene asset path to open.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { OpensSceneSideEffect }, false, false, new[] { IpcExecuteTouchedResourceKindNames.Scene }, UcliOperationPlanModeValues.MayCreatePreviewState));
    }

    private static UcliOperationDescribeContract SceneQuery ()
    {
        return Describe(
            "Finds objects or components in a scene by hierarchy path prefix and component type.",
            new[]
            {
                ScenePathInput("scene", "Scene asset path to query."),
                Input("pathPrefix", StringValueType, "Optional hierarchy path prefix filter.", Constraints(C.NonEmpty(), C.HierarchyPath())),
                Input("componentType", StringValueType, "Optional component type identifier filter.", Constraints(C.TypeAssignableTo(TypeKindComponent))),
            },
            UcliOperationResultContract.One<SceneQueryResult>("Scene query result containing matching objects or components."),
            Assurance(Array.Empty<string>(), false, false, new[] { IpcExecuteTouchedResourceKindNames.Scene }, UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract SceneSave ()
    {
        return Describe(
            "Saves a loaded or previewed Unity scene asset.",
            new[] { ScenePathInput("path", "Project-relative scene asset path to save.") },
            UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
            Assurance(new[] { WritesSceneSideEffect }, false, true, new[] { IpcExecuteTouchedResourceKindNames.Scene }, UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract SceneTree ()
    {
        return Describe(
            "Returns the hierarchy tree for a Unity scene.",
            new[] { ScenePathInput("path", "Scene asset path to inspect."), DepthInput("Maximum hierarchy depth to include.") },
            UcliOperationResultContract.One<SceneTreeResult>("Scene hierarchy tree result."),
            Assurance(Array.Empty<string>(), false, false, new[] { IpcExecuteTouchedResourceKindNames.Scene }, UcliOperationPlanModeValues.ObservesLiveUnity));
    }

    private static UcliOperationDescribeContract Describe (
        string description,
        IReadOnlyList<UcliOperationInputContract> inputs,
        UcliOperationResultContract resultContract,
        UcliOperationAssuranceContract assurance)
    {
        return new UcliOperationDescribeContract(
            description,
            inputs,
            resultContract,
            assurance);
    }

    private static UcliOperationAssuranceContract Assurance (
        IReadOnlyList<string> sideEffects,
        bool mayDirty,
        bool mayPersist,
        IReadOnlyList<string> touchedKinds,
        string planMode)
    {
        return new UcliOperationAssuranceContract(
            sideEffects,
            mayDirty,
            mayPersist,
            touchedKinds,
            planMode);
    }

    private static UcliOperationInputContract Input (
        string name,
        string valueType,
        string description,
        IReadOnlyList<UcliOperationInputConstraintContract> constraints,
        string? argsPath = null,
        IReadOnlyList<UcliOperationInputVariantContract>? variants = null)
    {
        return new UcliOperationInputContract(
            name,
            valueType,
            description,
            constraints,
            argsPath,
            variants);
    }

    private static UcliOperationInputContract ScenePathInput (
        string name,
        string description)
    {
        return Input(name, StringValueType, description, Constraints(C.NonEmpty(), C.ProjectRelativePath(), C.AssetExists(AssetKindScene)));
    }

    private static UcliOperationInputContract PrefabPathInput (
        string name,
        string description)
    {
        return Input(name, StringValueType, description, Constraints(C.NonEmpty(), C.ProjectRelativePath(), C.AssetExists(AssetKindPrefab)));
    }

    private static UcliOperationInputContract PrefabCreatablePathInput (
        string name,
        string description)
    {
        return Input(name, StringValueType, description, Constraints(C.NonEmpty(), C.ProjectRelativePath(), C.AssetCreatable(AssetKindPrefab)));
    }

    private static UcliOperationInputContract AssetPathInput (
        string name,
        string description)
    {
        return Input(name, StringValueType, description, Constraints(C.NonEmpty(), C.ProjectRelativePath(), C.AssetCreatable(AssetKindAsset)));
    }

    private static UcliOperationInputContract TypeInput ()
    {
        return Input("type", StringValueType, "Unity type identifier.", Constraints(C.NonEmpty(), C.TypeExists()));
    }

    private static UcliOperationInputContract DepthInput (string description)
    {
        return Input("depth", IntegerValueType, description, Constraints(C.Range(0, null)));
    }

    private static UcliOperationInputContract SetsInput ()
    {
        return Input("sets", ArrayValueType, "Serialized property assignments to apply.", Constraints(C.NonEmpty(), C.SerializedProperty(AccessWrite)));
    }

    private static UcliOperationInputContract AssetReferenceInput ()
    {
        return Input("target", ObjectValueType, "Asset or project asset reference.", Constraints(C.ReferenceResolvable(TargetKindAsset)), variants: AssetReferenceVariants("$.target"));
    }

    private static UcliOperationInputContract ComponentReferenceInput ()
    {
        return Input("target", ObjectValueType, "Component reference.", Constraints(C.ReferenceResolvable(TargetKindComponent)), variants: ComponentReferenceVariants("$.target"));
    }

    private static UcliOperationInputContract GameObjectReferenceInput ()
    {
        return GameObjectReferenceInput("target");
    }

    private static UcliOperationInputContract GameObjectReferenceInput (string name)
    {
        var argsPath = "$." + name;
        return Input(name, ObjectValueType, "GameObject reference.", Constraints(C.ReferenceResolvable(TargetKindGameObject)), variants: GameObjectReferenceVariants(argsPath));
    }

    private static UcliOperationInputContract SceneGameObjectReferenceInput ()
    {
        return Input("target", ObjectValueType, "Scene GameObject reference.", Constraints(C.ReferenceResolvable(TargetKindGameObject)), variants: SceneGameObjectReferenceVariants("$.target"));
    }

    private static UcliOperationInputContract GoCreateDestinationInput ()
    {
        return Input(
            "destination",
            ObjectValueType,
            "Scene or parent reference that receives the new GameObject.",
            NoConstraints(),
            argsPath: "$",
            variants: new[]
            {
                Variant("byScene", "Create as a scene root object.", new[] { "$.scene" }, Constraints(C.AssetExists(AssetKindScene))),
                Variant("byParent", "Create under an existing parent GameObject.", new[] { "$.parent" }, Constraints(C.ReferenceResolvable(TargetKindGameObject))),
            });
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> ResolveVariants (string prefix)
    {
        return new[]
        {
            Variant("byGlobalObjectId", "Use an existing Unity GlobalObjectId.", Paths(prefix, "globalObjectId"), Constraints(C.GlobalObjectId())),
            Variant("byAssetGuid", "Use a Unity asset GUID.", Paths(prefix, "assetGuid"), NoConstraints()),
            Variant("byAssetPath", "Use a project-relative asset path.", Paths(prefix, "assetPath"), Constraints(C.ProjectRelativePath(), C.AssetExists(AssetKindAsset))),
            Variant("byProjectAssetPath", "Use a project-scoped asset path.", Paths(prefix, "projectAssetPath"), Constraints(C.ProjectRelativePath(), C.AssetExists(AssetKindProjectSettings))),
            Variant("bySceneHierarchyPath", "Use a scene path and hierarchy path.", Paths(prefix, "scene", "hierarchyPath"), Constraints(C.AssetExists(AssetKindScene), C.HierarchyPath())),
            Variant("byPrefabHierarchyPath", "Use a prefab path and hierarchy path.", Paths(prefix, "prefab", "hierarchyPath"), Constraints(C.AssetExists(AssetKindPrefab), C.HierarchyPath())),
        };
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> GameObjectReferenceVariants (string prefix)
    {
        return new[]
        {
            Variant("byGlobalObjectId", "Use an existing Unity GlobalObjectId.", Paths(prefix, "globalObjectId"), Constraints(C.GlobalObjectId())),
            Variant("bySceneHierarchyPath", "Use a scene path and hierarchy path.", Paths(prefix, "scene", "hierarchyPath"), Constraints(C.AssetExists(AssetKindScene), C.HierarchyPath())),
            Variant("byPrefabHierarchyPath", "Use a prefab path and hierarchy path.", Paths(prefix, "prefab", "hierarchyPath"), Constraints(C.AssetExists(AssetKindPrefab), C.HierarchyPath())),
        };
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> SceneGameObjectReferenceVariants (string prefix)
    {
        return new[]
        {
            Variant("byGlobalObjectId", "Use an existing Unity GlobalObjectId.", Paths(prefix, "globalObjectId"), Constraints(C.GlobalObjectId())),
            Variant("bySceneHierarchyPath", "Use a scene path and hierarchy path.", Paths(prefix, "scene", "hierarchyPath"), Constraints(C.AssetExists(AssetKindScene), C.HierarchyPath())),
        };
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> ComponentReferenceVariants (string prefix)
    {
        return new[]
        {
            Variant("byGlobalObjectId", "Use an existing Unity GlobalObjectId.", Paths(prefix, "globalObjectId"), Constraints(C.GlobalObjectId())),
            Variant("bySceneHierarchyPath", "Use scene path, hierarchy path, and component type.", Paths(prefix, "scene", "hierarchyPath", "componentType"), Constraints(C.AssetExists(AssetKindScene), C.HierarchyPath(), C.TypeAssignableTo(TypeKindComponent))),
            Variant("byPrefabHierarchyPath", "Use prefab path, hierarchy path, and component type.", Paths(prefix, "prefab", "hierarchyPath", "componentType"), Constraints(C.AssetExists(AssetKindPrefab), C.HierarchyPath(), C.TypeAssignableTo(TypeKindComponent))),
        };
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> AssetReferenceVariants (string prefix)
    {
        return new[]
        {
            Variant("byGlobalObjectId", "Use an existing Unity GlobalObjectId.", Paths(prefix, "globalObjectId"), Constraints(C.GlobalObjectId())),
            Variant("byAssetGuid", "Use a Unity asset GUID.", Paths(prefix, "assetGuid"), NoConstraints()),
            Variant("byAssetPath", "Use a project-relative asset path.", Paths(prefix, "assetPath"), Constraints(C.ProjectRelativePath(), C.AssetExists(AssetKindAsset))),
            Variant("byProjectAssetPath", "Use a project-scoped asset path.", Paths(prefix, "projectAssetPath"), Constraints(C.ProjectRelativePath(), C.AssetExists(AssetKindProjectSettings))),
        };
    }

    private static IReadOnlyList<UcliOperationInputVariantContract> AssetSchemaSubjectVariants ()
    {
        return new[]
        {
            Variant("byType", "Inspect an asset type without selecting an existing asset.", new[] { "$.type" }, Constraints(C.TypeExists())),
            Variant("byTarget", "Inspect an existing asset target.", new[] { "$.target" }, Constraints(C.ReferenceResolvable(TargetKindAsset))),
        };
    }

    private static UcliOperationInputVariantContract Variant (
        string name,
        string description,
        IReadOnlyList<string> argsPaths,
        IReadOnlyList<UcliOperationInputConstraintContract> constraints)
    {
        return new UcliOperationInputVariantContract(
            name,
            description,
            argsPaths,
            constraints);
    }

    private static IReadOnlyList<UcliOperationInputConstraintContract> NoConstraints ()
    {
        return Array.Empty<UcliOperationInputConstraintContract>();
    }

    private static IReadOnlyList<UcliOperationInputConstraintContract> Constraints (params UcliOperationInputConstraintContract[] constraints)
    {
        return constraints;
    }

    private static IReadOnlyList<string> Paths (
        string prefix,
        params string[] fields)
    {
        if (string.Equals(prefix, "$", StringComparison.Ordinal))
        {
            var rootPaths = new string[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                rootPaths[i] = "$." + fields[i];
            }

            return rootPaths;
        }

        var paths = new string[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            paths[i] = prefix + "." + fields[i];
        }

        return paths;
    }

    private static IReadOnlyList<string> SceneAndPrefabTouchedKinds ()
    {
        return new[] { IpcExecuteTouchedResourceKindNames.Scene, IpcExecuteTouchedResourceKindNames.Prefab };
    }

    private static IReadOnlyList<string> AllPersistentTouchedKinds ()
    {
        return new[]
        {
            IpcExecuteTouchedResourceKindNames.Scene,
            IpcExecuteTouchedResourceKindNames.Prefab,
            IpcExecuteTouchedResourceKindNames.Asset,
            IpcExecuteTouchedResourceKindNames.ProjectSettings,
        };
    }
}
