# AGENTS.md

## 原則
- ユーザーに対しては **日本語** で応答する
- ユーザーに対して応答する際は、一般的ではない略語や語彙の圧縮を避け、明瞭で具体性のある表現を用いる
- 成果物に含める各記述は「成果物の目的・読者・利用シーン」に対して必要性を説明できるものに限り、説明できない要素（生成過程の都合・会話の流れ・仮定・自己言及・一般論の飾り）は出力しない。

## リポジトリ構成
- `src/`：製品コード
- `tests/`：.NET テスト
- `scripts/`：検証、パッケージ生成、リリースの自動化
- `schemas/`：公開スキーマ成果物
- `skills/`：公式 SKILL のバンドルと生成済み成果物

## 仕様文書
- uCLI の製品仕様を確認または変更するときは、`$HOME/Repositories/mackysoft/engineering-principles/ucli/` を参照する。
- 公開コマンド、JSON 契約、スキーマ、公式 SKILL の意味を変更する場合は、実装と対応する仕様文書を同じ変更で整合させる。

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

### スクリーンショット取得の専用検証

GameView または SceneView の取得面、画像の正規化、PNG 符号化を変更する場合は、[tests/System/ScreenshotFidelity/README.md](tests/System/ScreenshotFidelity/README.md) の前提条件を満たし、macOS の GUI システムテストを Linear と Gamma の両方で実行する。

```bash
bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "<UNITY_EDITOR>" \
  --color-space linear \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/linear"

bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "<UNITY_EDITOR>" \
  --color-space gamma \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/gamma"
```

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

## パッケージ復元とリリース

- uCLI は `MackySoft.FileSystem` の standalone NuGet パッケージの生成、版管理、公開を所有せず、固定版 `0.1.0` を外部依存として利用する。CLI tool の成果物には runtime dependency assembly が含まれ得るが、Unity plugin は assembly を内包せず exact dependency を宣言する。
- 公開前の `MackySoft.FileSystem` を検証する場合は、`MackySoft.FileSystem.0.1.0.nupkg` を含むディレクトリを `scripts/update-local-shared-packages.sh --filesystem-package-source <dir>` へ渡す。この経路は空の隔離 global package cache と一時 NuGet.config を使い、候補パッケージを永続的なローカル feed へ残さない。
- GitHub Actions の新規 runner と uCLI のリリースワークフローは `MackySoft.FileSystem` を生成しないため、実行前に固定版 `0.1.0` が nuget.org で取得可能であることを前提とする。
- `Ucli.Contracts`、`Ucli.Infrastructure`、または Unity 側の NuGet 依存を変更した場合は、`scripts/update-local-shared-packages.sh` で uCLI 内部のローカルパッケージを再生成して復元する。

```bash
bash scripts/update-local-shared-packages.sh
```

- 直接実行では、重複アセンブリを除く必要がある場合だけ `--prune` を指定する。`scripts/test-unity.sh` は既定で不要な対象フレームワーク向けファイルを除去し、`--no-prune` で無効にする。
- リリース前に、GitHub Actions の `verify` ワークフローが現在の `master` 先頭コミットで成功していることを確認する。リリースは最新の `master` から、`v` を付けない `<major>.<minor>.<patch>` を指定して `package-publish` を起動する。

```bash
gh workflow run package-publish.yaml --ref master -f release_tag=1.2.3
```

- パッケージや GitHub Release を手動で公開せず、ワークフローの完了と公開結果を確認する。

## 作業規則
- ユーザー明示が無い限り、uCLI は開発中のアプリケーションとして扱い、互換性維持のためだけのコードや説明を追加しない
- 変更を加えた場合は、影響範囲を確認し、必要な修正を同じ作業範囲で行う

## コマンド運用
- GitHub操作（接続・起票・閲覧）には必ず `gh` コマンドを使用すること。
  - URL：`https://github.com/mackysoft`
- コマンドの接続失敗やライセンス問題は多くの場合権限不足が原因であるため、権限昇格すること
