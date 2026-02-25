# uCLI - CLI workflow for Unity

uCLI は Unity プロジェクトを安全に自動化するための CLI です。  
Unity Editor API を経由した編集操作を中心に、Unity Test Framework 実行の統合も進めています。

## 主な機能
- JSON リクエストによる Unity 編集オペレーション（`validate` / `plan` / `call`）
- デーモン実行と oneshot 実行の切り替え
- Unity テスト実行と結果の正規化（`ucli test`、統合予定）

## Get Started

### 1. プロジェクト設定を初期化する
```bash
ucli init
```

### 2. 変更前に計画を確認する
```bash
ucli plan < request.json
```

### 3. 変更を適用する
```bash
ucli call --planToken "<PLAN_TOKEN>" < request.json
```

### 4. Unity テストを実行する（統合後の予定）
※ `ucli test` は現時点では未実装です。次は統合仕様の利用例です。

```bash
ucli test run \
  --projectPath ./UnityProject \
  --testPlatform editmode \
  --assemblyName MyGame.Tests.EditMode
```

テスト成果物は既定で `./.ucli/local/artifacts/` 配下に出力されます。

## Contracts / NuGetForUnity 運用
- 共通DTOは `src/Ucli.Contracts` で定義します。
- CLI（`src/Ucli`）は `ProjectReference` で `Ucli.Contracts` を参照します。
- Unityプラグイン（`src/Ucli.Unity`）は NuGetForUnity + `packages.config` で `MackySoft.Ucli.Contracts` を参照します。
- NuGetForUnity の復元成果物 `src/Ucli.Unity/Assets/Packages/` は生成物として Git 追跡しません。

### GitHub Packages 認証
`src/Ucli.Unity/Assets/NuGet.config` は `https://nuget.pkg.github.com/mackysoft/index.json` を参照します。  
認証情報はリポジトリに含めず、各開発環境の `~/.nuget/NuGet/NuGet.Config` に設定してください。

### 未公開版をローカルで使う手順
`MackySoft.Ucli.Contracts` の公開前バージョンを Unity で検証する場合のみ、ローカルフィードへ pack します。

```bash
dotnet pack src/Ucli.Contracts/Ucli.Contracts.csproj \
  --configuration Release \
  -p:PackageVersion=1.0.0 \
  --output src/Ucli.Unity/Packages/nuget-local-source
```

その後、Unity を batchmode で開くと NuGetForUnity が `packages.config` を復元します。

### パッケージ公開（CI）
`contracts/<major>.<minor>.<patch>` タグを push すると、`contracts-publish` workflow が GitHub Packages へ公開します。

```bash
git tag contracts/1.0.0
git push origin contracts/1.0.0
```

## Commands
- `ucli init`
- `ucli validate`
- `ucli plan`
- `ucli call`
- `ucli resolve`
- `ucli query`
- `ucli refresh`
- `ucli ops`
- `ucli status`
- `ucli daemon`
- `ucli test run`（予定）
- `ucli test profile init`（予定）

詳細仕様は `docs/uCLI.md` を参照してください。  
`uni-test-hub` 旧READMEは `docs/uni-test-hub.md` にアーカイブしています。
