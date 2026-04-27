namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the JSON schema for <c>ucli.resolve</c> operation arguments. </summary>
public static class IpcResolveSelectorArgsSchema
{
    /// <summary> Gets the JSON schema text for <c>ucli.resolve</c> operation arguments. </summary>
    public const string Json =
        @"{
          ""type"": ""object"",
          ""additionalProperties"": false,
          ""properties"": {
            """ + IpcResolveSelectorPropertyNames.GlobalObjectId + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.AssetGuid + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.AssetPath + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.ProjectAssetPath + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.Scene + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.Prefab + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.HierarchyPath + @""": { ""type"": ""string"", ""minLength"": 1 },
            """ + IpcResolveSelectorPropertyNames.ComponentType + @""": { ""type"": ""string"", ""minLength"": 1 }
          },
          ""oneOf"": [
            { ""required"": [""" + IpcResolveSelectorPropertyNames.GlobalObjectId + @"""] },
            { ""required"": [""" + IpcResolveSelectorPropertyNames.AssetGuid + @"""] },
            { ""required"": [""" + IpcResolveSelectorPropertyNames.AssetPath + @"""] },
            { ""required"": [""" + IpcResolveSelectorPropertyNames.ProjectAssetPath + @"""] },
            { ""required"": [""" + IpcResolveSelectorPropertyNames.Scene + @""", """ + IpcResolveSelectorPropertyNames.HierarchyPath + @"""] },
            { ""required"": [""" + IpcResolveSelectorPropertyNames.Prefab + @""", """ + IpcResolveSelectorPropertyNames.HierarchyPath + @"""] }
          ],
          ""allOf"": [
            {
              ""if"": { ""required"": [""" + IpcResolveSelectorPropertyNames.ComponentType + @"""] },
              ""then"": {
                ""oneOf"": [
                  { ""required"": [""" + IpcResolveSelectorPropertyNames.Scene + @""", """ + IpcResolveSelectorPropertyNames.HierarchyPath + @"""] }
                ]
              }
            }
          ]
        }";
}