# Package Operations

## Shared Contracts Ownership
- 共通DTOは `src/Ucli.Contracts` で定義する。
- CLI（`src/Ucli`）は `ProjectReference` で `Ucli.Contracts` を参照する。
- Unityプラグイン（`src/Ucli.Unity`）は NuGetForUnity と `packages.config` で `MackySoft.Ucli.Contracts` を参照する。

## Unity Dependency Restore
- `src/Ucli.Unity/Assets/NuGet.config` で以下のソースを利用する。
  - `https://nuget.pkg.github.com/mackysoft/index.json`
  - `../Packages/nuget-local-source`
  - `https://api.nuget.org/v3/index.json`
- NuGetForUnity の復元成果物は生成物として扱う。
  - `src/Ucli.Unity/Assets/Packages/`
  - `src/Ucli.Unity/.nuget-cache/`
  - `src/Ucli.Unity/.nuget-packages/`
  - `src/Ucli.Unity/Packages/nuget-local-source/`

## GitHub Packages Authentication
- 認証情報はリポジトリに含めない。
- 各開発環境の `~/.nuget/NuGet/NuGet.Config` に credentials を設定する。

## Local Pre-Release Validation
公開前バージョンを Unity で検証する場合は、ローカルフィードへ pack する。

```bash
dotnet pack src/Ucli.Contracts/Ucli.Contracts.csproj \
  --configuration Release \
  -p:PackageVersion=1.0.0 \
  --output src/Ucli.Unity/Packages/nuget-local-source
```

その後、Unity を batchmode で開くと NuGetForUnity が `packages.config` の依存を復元する。

## Release Workflow
`contracts/<major>.<minor>.<patch>` タグを push すると、`contracts-publish` workflow が GitHub Packages へ公開する。

```bash
git tag contracts/1.0.0
git push origin contracts/1.0.0
```
