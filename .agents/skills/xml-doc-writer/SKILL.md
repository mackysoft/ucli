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

## 3.XMLドキュメントを記述する
- `authoring-rules.md` のルールに従って、全ての対象に対して必要なXMLドキュメントを記述する。

## 7. 最終整合性チェックを行う
- 対象メンバのXMLドキュメントの内容が、`authoring-rules.md`のルールに従っているかを確認する。
- ドキュメントが実装と矛盾していないか確認する。

# Definition of Done
- 対象メンバに対して、契約を表すXMLドキュメントが追加または更新されている。
- XMLドキュメントが `authoring-rules.md` のルールに従った内容になっている。
- ドキュメントが実装と矛盾しない。
- `assets/self-review-checklist.md` の項目を満たしている。
