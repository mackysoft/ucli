namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

internal static class BuildProfileResolverTestSupport
{
    public const string ValidExplicitProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "explicit",
            "buildTarget": "standaloneLinux64",
            "scenes": {
              "source": "explicit",
              "paths": [
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Bootstrap.unity"
              ]
            },
            "options": {
              "development": false
            }
          },
          "runner": {
            "kind": "buildPipeline"
          },
          "policy": {
            "runtime": {
              "allowedExecutionModes": [
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

    public static string CreateProfileJson (string runnerJson)
    {
        return $$"""
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": false
                }
              },
              "runner": {{runnerJson}},
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
    }
}
