---
name: xml-doc-writer
description: XMLドキュメントコメントを契約ベースで執筆・更新する。公開APIまたは重要な非公開APIの入力制約、戻り値保証、例外条件、非同期完了条件を明文化したいときに使う。曖昧語や命名の言い換えを避け、実装と整合する記述を作成する。
---

# 目的
- 対象APIの契約を、呼び出し側が誤解しない粒度でXMLドキュメントとして明文化する。
- 実装と矛盾しない `summary/param/returns/exception` を作成し、利用時の前提を固定する。
- 同じルールで再現できる執筆手順を提供し、品質のばらつきを抑える。

# 非目的
- `dotnet format` やテスト実行など、検証の自動化。
- 実装コードの機能変更や設計変更。
- 命名の言い換えだけで終わる説明文の追加。

# 入力
- 対象メンバの署名（型、メソッド、プロパティ、イベント）。
- 対象実装の実際の挙動。
- 呼び出し側が依存する契約情報（入力制約、戻り値保証、例外条件、非同期完了条件）。

# 出力
- 対象コードに追加または更新されたXMLドキュメントコメント（タグ本文はEnglish）。
- 実装契約と整合した差分。

# 参照
- 執筆規約: [references/authoring-rules.md](references/authoring-rules.md)
- 対象選定規則: [references/target-selection.md](references/target-selection.md)
- コメントテンプレート: [assets/comment-templates.md](assets/comment-templates.md)
- 自己レビュー項目: [assets/self-review-checklist.md](assets/self-review-checklist.md)

# 手順
## 1. 対象メンバを選定する
- まず公開API（`public` / `protected`）を対象にする。
- 非公開メンバは、境界処理、変換、永続化、非同期、例外契約など誤用リスクが高いものだけ対象にする。
- 生成コードは対象外とする。

## 2. 契約情報を抽出する
- 署名と実装から、入力制約、戻り値保証、副作用、例外条件、完了条件を整理する。
- 曖昧な点は推測で埋めず、未確定として扱う。

## 3. `<summary>` を記述する
- 1文で「何をするか」を誤解なく書く。
- 重要点が複数ある場合は `<para>` で分割する。

## 4. 契約タグを埋める
- 入力契約は原則 `<param>` / `<typeparam>` に書く。
- 戻り値契約は `<returns>` に書く。
- 送出条件がある例外は `<exception>` に列挙する。
- `CancellationToken` を受け取るメンバでは、キャンセル時の `OperationCanceledException` を `<exception>` に記載しない。

## 5. 補助タグを必要時のみ追加する
- `<remarks>` は命名以上の補足が必要な場合のみ使う。
- `<example>` は誤用が多いAPIに限定し、最小例にする。

## 6. 省略ルールを適用する
- 命名以上の情報を提供できない `<summary>` は省略する。
- 契約がある `<param>` / `<returns>` / `<exception>` は省略しない。
- 例外として、`cancellationToken` がキャンセル用途のみで追加契約を持たない場合は `<param>` を省略してよい。

## 7. 最終整合性チェックを行う
- ドキュメントが実装と矛盾していないか確認する。
- タグ本文の前後スペース、`<para>` ルール、曖昧語禁止を確認する。

# Definition of Done
- 対象メンバに対して、契約を表すXMLドキュメントが追加または更新されている。
- ドキュメントはEnglishで、実装と矛盾しない。
- `assets/self-review-checklist.md` の項目を満たしている。
