> [!NOTE]
> この文書は、`uni-test-hub` の元READMEを移設したアーカイブです。  
> `uni-test-hub` の機能は、`ucli test` として uCLI へ統合予定です（現時点では未実装）。  
> 統合仕様案は `docs/uCLI.md` の「test コマンド（統合仕様）」を参照してください。

# UniTestHub - Integrated Unity Test Runner CLI

`uni-test-hub` is a CLI tool that unifies execution of the Unity Test Framework and collects and normalizes test results. It standardizes the verification process and quality gates.

- 複数のUnityバージョンのテストを統一されたCLIで実行
- Unity Editorとバージョンの自動検出
- テストプロファイルJSONでテスト構成を定義

## Installation

Install as a global .NET tool (requires .NET 8.0 or later):

```bash
dotnet tool install --global MackySoft.UniTestHub
uni-test-hub --version
```

Update:

```bash
dotnet tool update --global MackySoft.UniTestHub
```

## Get Started

最初に、Unityのテストを実行する

```bash
uni-test-hub run \
  --projectPath ./UnityProject \
  --mode editmode \
  --assemblyName MyGame.Tests.EditMode \
  --outputDir ./artifacts/test-results
```

Run PlayMode tests with build target:

```bash
uni-test-hub run \
  --projectPath ./UnityProject \
  --mode playmode \
  --buildTarget StandaloneWindows64 \
  --assemblyName MyGame.Tests.PlayMode
```

## Profile JSON

プロファイルJSONファイルは、テスト実行の構成を定義できます。基本的にはCLIオプションと同じフィールドを持ち、CLIオプションを指定する代わりにJSONとしてプリセットを保持することが出来ます。

プロファイルJSONのテンプレートは、次のコマンドで生成できます。

```bash
uni-test-hub profile init
```

Generated:

```json
{
  "schemaVersion": 1,
  "projectPath": ".",
  "unityVersion": null,
  "unityEditorPath": null,
  "mode": "editmode",
  "buildTarget": null,
  "testFilter": null,
  "testCategories": [],
  "assemblyNames": [],
  "testSettingsPath": null,
  "outputDir": "./artifacts/uni-test-hub",
  "timeoutSeconds": 1800
}
```

プロファイルの設定がCLIオプションと競合する場合は、CLIオプションが優先されます。
`CLI options > profile.json > defaults`

```bash
uni-test-hub profile init --outputPath ./profile.json
uni-test-hub run --projectPath ./UnityProject --profilePath ./profile.json
```

## Commands

- `uni-test-hub run`: Executes Unity tests and emits a JSON result to stdout.
- `uni-test-hub profile init`: Generates a `profile.json` template.

### `run` options

| Option | Short | Description |
| --- | --- | --- |
| `--projectPath <string?>` | `-p` | Unity project root path |
| `--profilePath <string?>` | `-c` | Profile configuration path |
| `--unityVersion <string?>` | `-u` | Unity editor version |
| `--unityEditorPath <string?>` | - | Unity editor executable path or editor directory path |
| `--mode <string?>` | `-m` | `editmode` or `playmode` |
| `--buildTarget <string?>` | `-t` | Build target used when `mode=playmode` |
| `--testFilter <string?>` | `-f` | Test name filter pattern |
| `--testCategory <string?>` | - | Comma-separated test categories |
| `--assemblyName <string?>` | `-a` | Comma-separated assembly names |
| `--testSettingsPath <string?>` | `-s` | Path to `TestSettings.json` |
| `--outputDir <string?>` | `-o` | Artifact output root directory |
| `--timeoutSeconds <int?>` | - | Timeout in seconds (`1..86400`) |

### `profile init` options

| Option | Short | Description |
| --- | --- | --- |
| `--outputPath <string?>` | `-o` | Output path for profile JSON (default: `profile.json`) |
| `--force` | `-f` | Overwrite existing file |


## Unity version and editor resolution

Unityバージョンは基本的に、Unityプロジェクトの `ProjectSettings/ProjectVersion.txt` に記載されたバージョンから自動的に解決されます。

`unityVersion` is resolved in this order:

1. `--unityVersion`
2. `profile.json` `unityVersion`
3. `ProjectSettings/ProjectVersion.txt` (`m_EditorVersion`)

`unityEditorPath` is resolved in this order:

1. `--unityEditorPath`
2. `profile.json` `unityEditorPath`
3. Search default installation roots for the resolved `unityVersion`

Default search roots include:

- Windows: `%ProgramFiles%/Unity/Hub/Editor`, `%ProgramFiles%/Unity/Editor`, `%ProgramFiles(x86)%/Unity/Hub/Editor`, `%ProgramFiles(x86)%/Unity/Editor`
- macOS: `/Applications/Unity/Hub/Editor`, `/Applications/Unity/Editor`
- Linux: `/opt/Unity/Hub/Editor`, `/opt/unity/hub/editor`, `$HOME/Unity/Hub/Editor`

## Artifacts layout

For each run, artifacts are written to:

`<outputDir>/<runId>/`

```text
<outputDir>/<runId>/
  meta.json
  results.xml
  editor.log
  results.json
  summary.json
```

File purpose:

- `meta.json`: resolved configuration snapshot and run metadata
- `results.xml`: raw Unity Test Framework output
- `editor.log`: Unity editor log for the run
- `results.json`: normalized per-test result
- `summary.json`: compact status, counts, and top failures

## JSON output contracts

### `run` stdout JSON

`run` always emits a single JSON object to stdout:

```json
{
  "status": "pass|fail|error",
  "errorKind": "invalidInput|infraError|toolError|null",
  "exitCode": 0,
  "message": "...",
  "runId": "...",
  "artifactsDir": "...",
  "summaryJsonPath": "..."
}
```

### `profile init` stdout JSON

`profile init` always emits a single JSON object to stdout:

```json
{
  "status": "success|error",
  "errorKind": "invalidInput|infraError|null",
  "exitCode": 0,
  "message": "...",
  "profilePath": "..."
}
```

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | pass |
| `1` | fail |
| `2` | infraError |
| `3` | invalidInput |
| `4` | toolError |

## Common failure cases and fixes

| Symptom | Cause | Fix |
| --- | --- | --- |
| `InvalidInput: projectPath does not exist` | `--projectPath` is wrong | Pass a valid Unity project root |
| `InvalidInput: mode must be editmode or playmode` | Unsupported mode string | Use `editmode` or `playmode` |
| `InvalidInput: buildTarget is not allowed when mode=editmode` | `buildTarget` used in EditMode | Remove `--buildTarget` or switch to `playmode` |
| `EditorNotInstalled: Unity Editor is not installed for unityVersion ...` | Matching Unity version was not found in search roots | Install that editor version or specify `--unityEditorPath` |
| `InvalidInput: timeoutSeconds must be in range 1..86400` | Timeout out of allowed range | Use a value between `1` and `86400` |
