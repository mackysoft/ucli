# Package Operations

## Shared Contracts Ownership
- 共通DTOは `src/Ucli.Contracts` で定義する。
- CLI（`src/Ucli`）は `ProjectReference` で `Ucli.Contracts` を参照する。
- Unityプラグイン（`src/Ucli.Unity`）は NuGetForUnity と `packages.config` で `MackySoft.Ucli.Contracts` を参照する。

## Unity Dependency Restore
- `src/Ucli.Unity/Assets/NuGet.config` で以下のソースを利用する。
  - `PrivateNuGet`: `https://nuget.pkg.github.com/mackysoft/index.json`
  - `nuget.org`: `https://api.nuget.org/v3/index.json`
- NuGetForUnity の復元成果物は生成物として扱う。
  - `src/Ucli.Unity/Assets/Packages/`
  - `src/Ucli.Unity/.nuget-cache/`
  - `src/Ucli.Unity/.nuget-packages/`

## GitHub Packages Authentication
- 認証情報はリポジトリに含めない。
- NuGetForUnity は `ApplicationData/NuGet/NuGet.Config` を参照する。
  - macOS/Linux: `~/.config/NuGet/NuGet.Config`
  - Windows: `%AppData%\NuGet\NuGet.Config`
- `dotnet` CLI は通常 `~/.nuget/NuGet/NuGet.Config` を参照する。
- Unity と CLI の設定を揃えるため、`~/.config/NuGet/NuGet.Config` と `~/.nuget/NuGet/NuGet.Config` は同一内容に維持する。
- `packageSourceCredentials` のキー名は、`NuGet.config` の source key と一致させる（`PrivateNuGet`）。
- GitHub Packages 読み取りには `read:packages` 権限が必要。private repository 配下のパッケージは `repo` も必要。

```bash
dotnet nuget add source "https://nuget.pkg.github.com/mackysoft/index.json" \
  --name "PrivateNuGet" \
  --username "<github-user>" \
  --password "<github-pat>" \
  --store-password-in-clear-text \
  --configfile "$HOME/.config/NuGet/NuGet.Config"
```

上記設定後に Unity を batchmode で開くと、NuGetForUnity が `packages.config` の依存を GitHub Packages から復元する。

## NuGetForUnity パッケージ解決手順
### 標準フロー（Unity エディタ起動）
1. `src/Ucli.Unity/Assets/packages.config` を更新する。
2. Unity プロジェクト（`src/Ucli.Unity`）を起動する。
3. 起動時に NuGetForUnity が `packages.config` を読み取り、`Assets/Packages/` を更新する。
4. Unity コンソールにコンパイルエラーがないことを確認する。

### 契約パッケージ更新フロー（`MackySoft.Ucli.Contracts`）
`MackySoft.Ucli.Contracts` のソースを変更した場合は、同じバージョン番号でもローカル nupkg を再生成しないと Unity 側が古い契約を参照し続ける。

標準手順は以下のスクリプトを使用する。

```bash
./scripts/update-local-contracts-package.sh
```

必要に応じて `--prune`（生成物整理を有効化）または `--repo-root <path>`（リポジトリ位置を明示）を指定する。

内部で実行している手順は次のとおり。

1. `src/Ucli.Contracts` からローカル nupkg を再生成する。
```bash
dotnet pack "src/Ucli.Contracts/Ucli.Contracts.csproj" \
  -c Release \
  -o "src/Ucli.Unity/Packages/nuget-local-source" \
  --no-restore
```
2. Unity 側の `MackySoft.Ucli.Contracts` 展開先を消して再復元する。
```bash
rm -rf src/Ucli.Unity/Assets/Packages/MackySoft.Ucli.Contracts.*
nuget restore "src/Ucli.Unity/Assets/packages.config" \
  -PackagesDirectory "src/Ucli.Unity/Assets/Packages" \
  -ConfigFile "src/Ucli.Unity/Assets/NuGet.config" \
  -NonInteractive
```
3. `nuget restore` が生成した package 配下の簡易 `.meta` を削除し、次回 Unity 起動で importer 設定を再生成させる。
```bash
find "src/Ucli.Unity/Assets/Packages" -type f -name '*.meta' -delete
```

### バッチモード運用での注意
`nuget restore` を直接使うと、同一アセンブリ名の複数ターゲットフレームワーク DLL が同時に import され、`Multiple precompiled assemblies with the same name ...` で失敗することがある。

また、fresh worktree では `nuget restore` が置いた簡易 `.meta` のままだと `MackySoft.Ucli.Contracts.dll` の importer 設定が不足し、Unity が共有契約 DLL を解決できずコンパイルエラーになることがある。`scripts/update-local-contracts-package.sh` は復元後にその `.meta` を削除し、次回 Unity 起動で Unity 正規の `.meta` を再生成させる。

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
- `verify`: PR、`master` push、`workflow_dispatch` で起動する統一検証 workflow。変更差分に応じて `.NET`、Unity、contracts pack を job 単位で分岐し、最終的な必須判定は `required` job で集約する。
- `contracts-package-publish` の workflow 自体を変更した PR でも `verify` は `.NET` と contracts pack を起動し、公開フロー変更を無検証のまま通さない。
- `verify` は workflow-level `concurrency` で同一 PR または同一 branch の古い run を自動キャンセルする。`workflow_dispatch` のみ明示比較用途のため自動キャンセルしない。
- `pull_request` では変更差分を merge base 起点で判定し、必要な job だけを `Linux`、`Windows`、`macOS` の 3 OS matrix で実行する。外部 contributor の PR では `access-guard` job が失敗し、Unity job は実行しない。
- `push` to `master` では変更差分を判定しつつ、実行 OS は `Linux` のみに絞って post-merge 検証を軽量化する。
- `workflow_dispatch` は差分判定を使わず、`.NET`、Unity、contracts pack を常に `Linux`、`Windows`、`macOS` の 3 OS でフル検証する。
- Unity 検証は `src/Ucli`、`src/Ucli.Unity`、`src/Ucli.Contracts`、`scripts/update-local-contracts-package.sh`、`verify` 自体の変更時に動く。`buildalon/unity-setup` と `buildalon/activate-unity-license` で各 OS の Unity Editor を用意した後、`ucli test run --mode oneshot` を使って `EditMode` テストアセンブリを明示指定して実行する。workflow はプロセス終了コードだけでなく `command-result.json` の `status` / `exitCode` / `payload.result` も検証し、`pass` 以外を失敗として扱う。
- `contracts-package-publish`: `contracts/<major>.<minor>.<patch>` タグ push、または `workflow_dispatch` の `package_version` 指定で GitHub Packages へ公開する。
- `contracts-package-publish` は公開後に `src/Ucli.Contracts/Ucli.Contracts.csproj` と `src/Ucli.Unity/Assets/packages.config` の `MackySoft.Ucli.Contracts` バージョンを同一値へ自動同期する。
- タグは `v` プレフィックスを付けない（例: `contracts/x.y.z`）。

```bash
git tag contracts/x.y.z
git push origin contracts/x.y.z
```
