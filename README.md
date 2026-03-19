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

詳細仕様の入口は `docs/uCLI.md` です。  
JSON リクエスト仕様は `docs/json-request-spec.md`、設計原則は `docs/uCLI-design-principles.md` を参照してください。
コマンド別の option table は `docs/uCLI-command-reference.md`、JSON プロパティ定義は `docs/uCLI-property-reference.md` を参照してください。
