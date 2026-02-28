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
- ドキュメント：`docs/`
- エージェント用作業ドキュメント：`docs/agents/`

## コーディング規約
- SOLID原則を遵守し、高凝集・低結合を前提にモジュール分割する
- DRY原則を遵守し、本質的な重複を避ける
- 関心事を分離し、単一責任の原則を守る
- データ構造を意識した設計を行う（操作の計算量、割り当て回数、局所性を考慮）
- 割り当て削減を優先し、可能な範囲でモダン実装を行う。
- 例外処理は契約として設計し、入力検証は早期に弾く。例外を制御フローに使わない。
- レイヤーを適切に分離し、境界の明確化を行う。ユニットテスト可能な設計（副作用は境界へ）を優先する。
- 可読性を重視し、意味のあるコメントやXMLドキュメントを充実させる
- 背景が重要な処理には、`NOTE`コメントで意図を明示して残すこと
- 処理ブロックで波括弧（`{}`）を省略しない
- 命名規則は既存コードに合わせる
- 以下の場合、メンバ上部に１行の空行を設ける
  - ドキュメント・コメント付き
  - 属性付き
  - 責務・ブロックのグルーピングの切れ目

## フィールド宣言規則
基本的には責務・性質ごとに改行で区切る。
静的、インスタンスの順にまとめる。
1. `const`
2. `static`
3. `SerializeField`（Unityインスペクターでの適切なレイアウトを考慮）
4. コンストラクタで割り当てられるフィールド
5. `readonly`
6. その他のインスタンスフィールド

## 非同期規約
- 非同期関数は必ず`CancellationToken`を伝搬させる。`CancellationToken`を渡す場合は、原則として引数の末尾。特別な理由がない限りは、公開APIの引数に`default`パラメーターを設定する。
- 同期関数に`CancellationToken`を定義しない。命名プレフィックスに`Async`を用いない。
- 非同期関数は必ず、適切な箇所で`ThrowIfCancellationRequested`を呼び出し、キャンセル要求を尊重すること。

## DI規約
- 原則としてコンストラクタインジェクションを用いる
- 依存性の注入を行うメンバには、注入を行う属性を明示的に付与する（DIライブラリに属性が存在する場合）

## Unity特有の規約
- `UnityEngine.Object` から派生するオブジェクト・コンポーネントに対しては、`null`評価系の演算子を用いないこと（`??`、`?.`、`?:`、`is null`、`is not null`、etc）
- Unityオブジェクトの生存判定は `== null` / `!= null` のみを使用する
- `RequireComponent` を使用する場合、対象コンポーネント参照フィールドには `SerializeField` を付けない（`GetComponent` で解決する）
- Unityにコードファイルを追加・削除した場合は、実装後にbatchmodeで起動して、`meta`を更新すること。Unity正規の生成手順を踏まずに`meta`を触らないこと。

## テスト実行
Unityのテストは `-runTests` を使い、`-testPlatform` と `-assemblyNames` を必ず明示する。

汎用コマンド（PlayMode）
```bash
"<UNITY_BIN>" -batchmode -nographics -projectPath "<PROJECT_PATH>" -runTests -testPlatform PlayMode -assemblyNames "<TEST_ASSEMBLY>" -testResults "<RESULT_XML>" -logFile "<LOG_FILE>"
```

汎用コマンド（EditMode）
```bash
"<UNITY_BIN>" -batchmode -nographics -projectPath "<PROJECT_PATH>" -runTests -testPlatform EditMode -assemblyNames "<TEST_ASSEMBLY>" -testResults "<RESULT_XML>" -logFile "<LOG_FILE>"
```

- `-quit` 併用で結果XMLが出力されない場合は、`-quit` を外して再実行する

## コードフォーマット
コードフォーマットは `.editorconfig` を正として `dotnet format` を使用する。

標準手順
```bash
dotnet restore "<SOLUTION_OR_PROJECT>"
dotnet format "<SOLUTION_OR_PROJECT>" --verbosity minimal --no-restore
dotnet format "<SOLUTION_OR_PROJECT>" --verbosity minimal --verify-no-changes --no-restore
```

`dotnet format` が停止・ハングした場合
```bash
dotnet build-server shutdown
dotnet format "<SOLUTION_OR_PROJECT>" --verbosity diagnostic --no-restore
dotnet format "<SOLUTION_OR_PROJECT>" --verbosity diagnostic --include "<TARGET_PATH>" --no-restore
```

- フォーマット後は対象のテストを実行し、回帰がないことを確認すること。

## 作業規則
- 変更を加える場合、後方互換性ではなく新たなコードを含む全体の整然性を優先する。互換目的のコードの温存は原則禁止。
- 変更を加えた場合、依存関係のある全てのコードに対して影響範囲を考慮し、必要に応じて修正を行うこと。
- コードは責務単位で整理して配置し、関係の薄い型を同一階層へ直置きしない。レイヤー境界を跨ぐ責務を同一ディレクトリへ混在させず、上位レイヤーの契約と下位レイヤーの実装詳細を分離して配置する

## コマンド運用
- GitHub操作（接続・起票・閲覧）には必ず `gh` コマンドを使用すること。
  - URL：`https://github.com/mackysoft`
- コマンドの接続失敗やライセンス問題は多くの場合権限不足が原因であるため、権限昇格すること