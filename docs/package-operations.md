# Package Operations

## Shared Packages Ownership
- 共有契約は `src/Ucli.Contracts` で定義する。
- 外部環境に触れる共有実装は `src/Ucli.Infrastructure` で定義し、`Ucli.Contracts` のみを参照する。
- CLI（`src/Ucli`）は `ProjectReference` で `Ucli.Contracts` と `Ucli.Infrastructure` を参照する。
- Unity開発プロジェクト（`src/Ucli.Unity`）は NuGetForUnity と `packages.config` で `MackySoft.Ucli.Contracts` と `MackySoft.Ucli.Infrastructure` を参照する。
- 配布用Unityプラグインは `MackySoft.Ucli.Unity` nupkg として生成し、NuGetForUnity で導入する。

Project 責務境界、依存方向、公開パッケージにしない内部 project の扱いは [uCLI-architecture.md](uCLI-architecture.md) を参照する。`Ucli.Application` は追加後も `MackySoft.Ucli` の内部 assembly として扱い、単独の NuGet package にはしない。`Ucli.Skills` も単独の NuGet package にはせず、`Ucli` から参照する場合は `MackySoft.Ucli` package 内部の assembly として扱う。SKILL 配布物は `MackySoft.Ucli` package、release artifact、または `ucli skills install/export` の出力として扱う。

## Unity Dependency Restore
- `src/Ucli.Unity/Assets/NuGet.config` で以下のソースを利用する。
  - `LocalNuGet`: `../Packages/nuget-local-source`
  - `nuget.org`: `https://api.nuget.org/v3/index.json`
- NuGetForUnity の復元成果物は生成物として扱う。
  - `src/Ucli.Unity/Assets/Packages/`
  - `src/Ucli.Unity/.nuget-cache/`
  - `src/Ucli.Unity/.nuget-packages/`
- `src/Ucli.Unity/Assets/NuGet.config` は package source mapping で `MackySoft.Ucli.Contracts` と `MackySoft.Ucli.Infrastructure` を `LocalNuGet` に固定し、開発中の共有パッケージが公開済み版で上書きされることを防ぐ。

## NuGet Source Policy
- 公開パッケージは `MackySoft.Ucli`、`MackySoft.Ucli.Contracts`、`MackySoft.Ucli.Infrastructure`、`MackySoft.Ucli.Unity` のいずれも nuget.org へ公開する。
- 利用者側の Unity project は `nuget.org` source だけで `MackySoft.Ucli.Unity` と推移依存を復元できる。
- 開発用 project は `MackySoft.Ucli.Contracts` と `MackySoft.Ucli.Infrastructure` を `LocalNuGet` から復元し、`Microsoft.*` / `System.*` の外部依存を `nuget.org` から復元する。
- 認証付き feed は標準導入手順に含めない。

## Unity Plugin Distribution
`MackySoft.Ucli.Unity` は NuGetForUnity 用の nupkg として nuget.org へ公開し、同じ `.nupkg` を GitHub Releases へミラーする。Unityプラグイン本体、`ucli-plugin.json`、README、LICENSE を package root に含め、復元後の配置は次の形になる。

```text
Assets/Packages/MackySoft.Ucli.Unity.<version>/ucli-plugin.json
Assets/Packages/MackySoft.Ucli.Unity.<version>/Editor/MackySoft.Ucli.Unity.Editor.asmdef
```

利用者側は `Assets/NuGet.config` に `nuget.org` を設定し、NuGetForUnity で `MackySoft.Ucli.Unity` を導入する。`MackySoft.Ucli.Unity` は `MackySoft.Ucli.Contracts`、`MackySoft.Ucli.Infrastructure`、`System.Text.Json`、`Microsoft.Extensions.DependencyInjection` などを NuGet 依存として定義する。

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="MackySoft.Ucli.Unity" version="<version>" manuallyInstalled="true" targetFramework="netstandard2.1" />
</packages>
```

NuGetForUnity のUIで導入した場合は依存パッケージも `packages.config` に追加される。`nuget restore` をCIなどで直接使うプロジェクトでは、NuGetForUnity が生成した依存行を含む `packages.config` をコミットする。

配布パッケージは次のコマンドで生成・検証する。

```bash
./scripts/pack-unity-plugin.sh --version <version> --output "artifacts/packages"
./scripts/verify-unity-plugin-package.sh "artifacts/packages" <version>
```

## Release Artifact Mirror
各 publish workflow は、nuget.org への公開が成功した後に同じ `.nupkg` を GitHub Releases へミラーする。配布の正は nuget.org とし、GitHub Releases はタグごとの公開成果物を確認する監査場所として扱う。

- `cli/<version>`: `MackySoft.Ucli.<version>.nupkg`
- `shared/<version>`: `MackySoft.Ucli.Contracts.<version>.nupkg` / `MackySoft.Ucli.Infrastructure.<version>.nupkg`
- `unity/<version>`: `MackySoft.Ucli.Unity.<version>.nupkg`

## Agent Skill Distribution
uCLI 公式 SKILL は agent 向け workflow 配布物であり、operation contract の正本ではない。正本は `Ucli.Contracts`、operation metadata、`ucli ops describe`、[json-request-spec.md](json-request-spec.md) に置く。

SKILL の詳細な仕様、生成方針、責務境界は [uCLI-skills.md](uCLI-skills.md) を参照する。

配布上の役割は次のとおり。

- `src/Ucli.Skills/SkillDefinitions/`: 人間が編集する template と metadata の定義
- `skills/`: canonical generated output。CLI package、release artifact、install/export の配布元
- host install target: `.claude/skills/`、`.github/skills/`、`.agents/skills/` など host ごとの配置先

`MackySoft.Ucli` CLI package は `skills/**` を同梱する。`ucli skills list/export/install/doctor` は canonical `ucli-skill.json` を使って SKILL 配布物を扱う。公式 SKILL は選択した host 向けに一括で install / export し、SKILL ごとの host allowlist や個別導入 metadata は持たない。

## CLI Global Tool Distribution
`MackySoft.Ucli` は nuget.org へ .NET global tool として公開する。利用者は追加 NuGet source を設定せず、次のコマンドで導入する。

```bash
dotnet tool install --global MackySoft.Ucli --version <version>
```

既存インストールの更新は次のコマンドで行う。

```bash
dotnet tool update --global MackySoft.Ucli --version <version>
```

CLI パッケージは `src/Ucli/Ucli.csproj` を正として `dotnet pack` で生成する。公開 workflow は release tag の version を `Version` / `PackageVersion` に渡し、`ucli --version` が公開 version と一致することを検証してから nuget.org へ公開し、同じ `.nupkg` を GitHub Releases へミラーする。

```bash
dotnet pack "src/Ucli/Ucli.csproj" \
  -c Release \
  -p:Version=<version> \
  -p:PackageVersion=<version> \
  -o "artifacts/packages"
```

nuget.org への公開は GitHub Actions の Trusted Publishing を使用する。nuget.org 側で repository owner `mackysoft`、repository `ucli`、workflow file `cli-package-publish.yaml` / `shared-package-publish.yaml` / `unity-package-publish.yaml`、environment 未指定の policy を作成し、GitHub repository variable `NUGET_USER` に nuget.org profile name を設定する。

## NuGetForUnity パッケージ解決手順
### 標準フロー（Unity エディタ起動）
1. `src/Ucli.Unity/Assets/packages.config` を更新する。
2. Unity プロジェクト（`src/Ucli.Unity`）を起動する。
3. 起動時に NuGetForUnity が `packages.config` を読み取り、`Assets/Packages/` を更新する。
4. Unity コンソールにコンパイルエラーがないことを確認する。

### Shared パッケージ更新フロー（`MackySoft.Ucli.Contracts` / `MackySoft.Ucli.Infrastructure`）
`MackySoft.Ucli.Contracts` または `MackySoft.Ucli.Infrastructure` のソースを変更した場合は、同じバージョン番号でもローカル nupkg を再生成しないと Unity 側が古い shared package を参照し続ける。

標準手順は以下のスクリプトを使用する。

```bash
./scripts/update-local-shared-packages.sh
```

必要に応じて `--prune`（生成物整理を有効化）または `--repo-root <path>`（リポジトリ位置を明示）を指定する。

内部で実行している手順は次のとおり。

1. `src/Ucli.Contracts` と `src/Ucli.Infrastructure` から同一バージョンのローカル nupkg を再生成する。
```bash
dotnet pack "src/Ucli.Contracts/Ucli.Contracts.csproj" \
  -c Release \
  -o "src/Ucli.Unity/Packages/nuget-local-source" \
  --no-restore
dotnet pack "src/Ucli.Infrastructure/Ucli.Infrastructure.csproj" \
  -c Release \
  -o "src/Ucli.Unity/Packages/nuget-local-source" \
  --no-restore
```
2. Unity 側の NuGet 復元成果物を消して再復元する。
```bash
rm -rf src/Ucli.Unity/Assets/Packages
mkdir -p src/Ucli.Unity/Assets/Packages
rm -rf src/Ucli.Unity/.nuget-cache
rm -rf src/Ucli.Unity/.nuget-packages
nuget restore "src/Ucli.Unity/Assets/packages.config" \
  -PackagesDirectory "src/Ucli.Unity/Assets/Packages" \
  -ConfigFile "src/Ucli.Unity/Assets/NuGet.config" \
  -NoCache \
  -NonInteractive
```
3. `nuget restore` が生成した package 配下の簡易 `.meta` を削除し、次回 Unity 起動で importer 設定を再生成させる。
```bash
find "src/Ucli.Unity/Assets/Packages" -type f -name '*.meta' -delete
```

### バッチモード運用での注意
`nuget restore` を直接使うと、同一アセンブリ名の複数ターゲットフレームワーク DLL が同時に import され、`Multiple precompiled assemblies with the same name ...` で失敗することがある。

また、fresh worktree では `nuget restore` が置いた簡易 `.meta` のままだと shared package DLL の importer 設定が不足し、Unity が `MackySoft.Ucli.Contracts.dll` または `MackySoft.Ucli.Infrastructure.dll` を解決できずコンパイルエラーになることがある。`scripts/update-local-shared-packages.sh` は復元後にその `.meta` を削除し、次回 Unity 起動で Unity 正規の `.meta` を再生成させる。

その場合は生成物を整理してから batchmode を実行する。

```bash
ROOT="src/Ucli.Unity/Assets/Packages"
find "$ROOT" -type d -name analyzers -prune -exec rm -rf {} +
find "$ROOT" -type d -name runtimes -prune -exec rm -rf {} +
find "$ROOT" -type d \( -name build -o -name buildMultiTargeting -o -name buildTransitive \) -prune -exec rm -rf {} +
for pkg in "$ROOT"/*; do
  [ -d "$pkg/lib" ] || continue
  keep=""
  for tfm in netstandard2.1 netstandard2.0 net462 net461; do
    if [ -d "$pkg/lib/$tfm" ]; then
      keep="$tfm"
      break
    fi
  done
  [ -n "$keep" ] && find "$pkg/lib" -mindepth 1 -maxdepth 1 -type d ! -name "$keep" -exec rm -rf {} +
done
```

### 検証
復元後は Unity batchmode でコンパイルとテストを確認する。

```bash
"/Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit \
  -projectPath "src/Ucli.Unity" \
  -runTests -testPlatform EditMode \
  -assemblyNames "MackySoft.Ucli.Unity.Tests.Editor" \
  -testResults "/tmp/ucli-unity-editmode-results.xml" \
  -logFile "/tmp/ucli-unity-editmode.log"
```

失敗時はログの `Scripts have compiler errors.` の直前にあるエラーで原因を判定する。

## CI / Release Workflow
- `verify`: PR、`master` push、`workflow_dispatch` で起動する統一検証 workflow。変更差分に応じて `.NET`、Unity、shared pack、CLI pack を job 単位で分岐し、最終的な必須判定は `required` job で集約する。
- `shared-package-publish` の workflow 自体を変更した PR でも `verify` は `.NET` と shared pack を起動し、公開フロー変更を無検証のまま通さない。
- `cli-package-publish` の workflow 自体を変更した PR では `verify` は `.NET` と CLI pack を起動し、global tool packaging の回帰を検出する。
- `unity-package-publish` の workflow、Unity package nuspec、pack/verify script、Unityプラグイン本体、`packages.config` を変更した PR では `verify` は Unity package pack を起動し、NuGetForUnity配布物の回帰を検出する。
- `verify` は workflow-level `concurrency` で同一 PR または同一 branch の古い run を自動キャンセルする。`workflow_dispatch` のみ明示比較用途のため自動キャンセルしない。
- `pull_request` では変更差分を merge base 起点で判定し、必要な job だけを `Linux`、`Windows`、`macOS` の 3 OS matrix で実行する。外部 contributor の PR では `access-guard` job が失敗し、Unity job は実行しない。
- `push` to `master` では変更差分を判定しつつ、実行 OS は `Linux` のみに絞って post-merge 検証を軽量化する。
- `workflow_dispatch` は差分判定を使わず、`.NET`、Unity、shared pack、CLI pack、Unity package pack をフル検証する。`.NET` と Unity は `Linux`、`Windows`、`macOS` の 3 OS で実行し、package 検証は `Linux` で実行する。
- Unity 検証は `src/Ucli`、`src/Ucli.Unity`、`src/Ucli.Contracts`、`src/Ucli.Infrastructure`、`scripts/test-unity.sh`、`scripts/update-local-shared-packages.sh`、`verify` 自体の変更時に動く。`buildalon/unity-setup` と `buildalon/activate-unity-license` で各 OS の Unity Editor を用意した後、CI とローカル共通の `scripts/test-unity.sh` から `ucli test run --mode oneshot` を使って `EditMode` テストアセンブリを明示指定して実行する。workflow はプロセス終了コードだけでなく `command-result.json` の `status` / `exitCode` / `payload.result` も検証し、`pass` 以外を失敗として扱う。
- CLI pack 検証は `src/Ucli`、`src/Ucli.Contracts`、`src/Ucli.Infrastructure`、`README.md`、`LICENSE`、`cli-package-publish`、`verify` 自体の変更時に動く。`dotnet pack` 後にローカル tool install、`ucli --version`、`ucli --help`、nupkg 内の `DotnetToolSettings.xml` / `README.md` / `LICENSE` を検証する。
- Unity package pack 検証は `scripts/pack-unity-plugin.sh` で `MackySoft.Ucli.Unity` nupkg を作成し、`scripts/verify-unity-plugin-package.sh` で必須ファイル、依存定義、ローカル復元後の `ucli-plugin.json` 配置を検証する。
- `shared-package-publish`: `shared/<major>.<minor>.<patch>` タグを公開の起点とする。`workflow_dispatch` は `package_version` から同名タグを先に作成して push し、その同一 workflow run の中で nuget.org publish / GitHub Release mirror / repository version sync PR 作成まで継続する。
- `shared-package-publish` は公開後に `chore/shared-sync-<version>` ブランチを作成し、`src/Ucli.Contracts/Ucli.Contracts.csproj`、`src/Ucli.Infrastructure/Ucli.Infrastructure.csproj`、`src/Ucli.Unity/Assets/packages.config`、`src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec` の shared package version を同一値へ同期する PR を作成する。同期 PR に対しては `verify` workflow を明示的に dispatch する。
- `cli-package-publish`: `cli/<major>.<minor>.<patch>` タグを公開の起点とする。`workflow_dispatch` は `package_version` から同名タグを先に作成して push し、同一 workflow run の中で pack / smoke test / nuget.org publish / GitHub Release mirror まで継続する。
- `cli-package-publish` は公開後に `chore/cli-sync-<version>` ブランチを作成し、`src/Ucli/Ucli.csproj` の `MackySoft.Ucli` バージョンを公開 version へ同期する PR を作成する。同期 PR に対しては `verify` workflow を明示的に dispatch する。
- `unity-package-publish`: `unity/<major>.<minor>.<patch>` タグを公開の起点とする。`workflow_dispatch` は `package_version` から同名タグを先に作成して push し、同一 workflow run の中で pack / package verify / nuget.org publish / GitHub Release mirror / repository version sync PR 作成まで継続する。
- `unity-package-publish` は公開後に `chore/unity-sync-<version>` ブランチを作成し、`src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec` の `MackySoft.Ucli.Unity` バージョンを公開 version へ同期する PR を作成する。同期 PR に対しては `verify` workflow を明示的に dispatch する。
- タグは `v` プレフィックスを付けない（例: `shared/x.y.z`、`cli/x.y.z`、`unity/x.y.z`）。

```bash
git tag shared/x.y.z
git push origin shared/x.y.z

git tag cli/x.y.z
git push origin cli/x.y.z

git tag unity/x.y.z
git push origin unity/x.y.z
```
