# AGENTS.md

## 原則
- ユーザーに対しては **日本語** で応答する
- ユーザーに対して応答する際は、一般的ではない略語や語彙の圧縮を避け、明瞭で具体性のある表現を用いる
- 成果物に含める各記述は「成果物の目的・読者・利用シーン」に対して必要性を説明できるものに限り、説明できない要素（生成過程の都合・会話の流れ・仮定・自己言及・一般論の飾り）は出力しない。

## プロジェクト構成
- ソースコード：`src/`
  - `uCLI`：CLIコア
  - `uCLI.Unity`：Unity IPCクライアント
  - `uCLI.Contracts`：IPC共有契約パッケージ
- 仕様文書（Mac）：`$HOME/Repositories/assurance/ucli/`

## テスト実行
.NET の変更中確認
```bash
bash scripts/test-dotnet.sh
```

.NET の最終確認
```bash
bash scripts/verify.sh
```

Unity を含む変更
```bash
bash scripts/test-unity.sh \
  --project-path "src/Ucli.Unity" \
  --assembly-name "MackySoft.Ucli.Unity.Tests.Editor"
```

.NET と Unity の一括確認
```bash
bash scripts/verify.sh --include-unity \
  --project-path "src/Ucli.Unity" \
  --assembly-name "MackySoft.Ucli.Unity.Tests.Editor"
```

Unity Editor のインストールとライセンス有効化は実行環境の前提とし、スクリプト内では隠蔽しない。

## コードフォーマット
コードフォーマットは `.editorconfig` と `scripts/code-quality.sh` を正とする。`.editorconfig` は Unity コードベースにも適用する。

標準手順
```bash
bash scripts/code-quality.sh format
bash scripts/code-quality.sh verify
bash scripts/verify.sh
```

対象ファイルまたはディレクトリのみを整形・確認したい場合は、`--include` に対象パスの一覧を指定してもよい。
```bash
bash scripts/code-quality.sh format --include "<TARGET_PATH>" ["<TARGET_PATH>"...]
bash scripts/code-quality.sh verify --include "<TARGET_PATH>" ["<TARGET_PATH>"...]
```

- フォーマット後は対象のテストを実行し、回帰がないことを確認すること。

## 作業規則
- ユーザー明示が無い限り、uCLI は開発中のアプリケーションとして扱い、互換性維持のためだけのコードや説明を追加しない
- 変更を加えた場合は、影響範囲を確認し、必要な修正を同じ作業範囲で行う

## コマンド運用
- GitHub操作（接続・起票・閲覧）には必ず `gh` コマンドを使用すること。
  - URL：`https://github.com/mackysoft`
- コマンドの接続失敗やライセンス問題は多くの場合権限不足が原因であるため、権限昇格すること
