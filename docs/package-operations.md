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

## Release Workflow
- `contracts-package-verify`: `src/Ucli.Contracts` または workflow 自体に変更がある PR で、restore/build/pack を検証する。
- `contracts-package-publish`: `contracts/<major>.<minor>.<patch>` タグ push、または `workflow_dispatch` の `package_version` 指定で GitHub Packages へ公開する。
- タグは `v` プレフィックスを付けない（例: `contracts/0.2.0`）。

```bash
git tag contracts/0.2.0
git push origin contracts/0.2.0
```
