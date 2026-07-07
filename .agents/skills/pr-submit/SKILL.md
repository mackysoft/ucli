---
name: "pr-submit"
description: "現在作業を PR として作成または既存 open PR へ更新するときに、`$commit`、`$sync-latest`、検証、`$push`、本文作成、PR 作成または更新までを一気通貫で実行する。Issue がある場合は Issue を優先して本文と base を決定し、PR タイトルと本文の言語は `$writing` で揃える。"
---

# pr-submit

## 目的
現在作業を PR として作成または既存 open PR へ更新する。

PR の base を決め、作業差分を `$commit` で保護し、`$sync-latest`、検証、`$push`、本文作成、PR 作成または既存 open PR の更新まで進める。

## フロー

### Phase 1: 作業状態を確認する
- `gh auth status` を確認する。
- 現在ブランチ、未コミット差分、既存 open PR の有無を確認する。
- detached HEAD で PR 対象を確定できない場合は停止する。

### Phase 2: Issue と base を決める
Issue は明示入力、ブランチ名の `issue-<N>`、既存 PR の関連情報の順に解決する。
Issue が解決できた場合は、`gh issue view` で本文、状態、受け入れ条件、base 指定を取得する。
取得できなければ停止する。
取得した Issue は、PR 本文末尾の `Closes #<N>` で Development へ紐付ける。
Issue が解決できない場合は、Issue なしで進める。

base branch は明示入力、既存 PR の base、Issue 側の指定、リポジトリの既定ブランチの順に決める。

### Phase 3: 作業差分をコミットする
未コミット差分がある場合は `$commit` を使い、PR 対象の変更を責務単位でコミットする。
コミット後も PR 対象差分がなく、未コミット差分もない場合は PR 対象なしとして停止する。

### Phase 4: PR 対象を最新化する
検証前に `$sync-latest` で base branch を現在ブランチへ同期する。
同期で衝突した場合は検証や PR 作成へ進まない。

base との差分がなく、未コミット差分もない場合は PR 対象なしとして停止する。

### Phase 5: 検証する
`$verification-gate` を PR 前用途で実行する。
失敗した場合は PR を作成または更新しない。
検証が成果物更新や整形で未完了になった場合は、Phase 3 に戻って変更をコミットしてから再検証する。
未検証範囲が残る場合は、PR を作成または更新しない。

### Phase 6: push する
`$push` を使い、必要な最新化と push を行う。
push の過程で、同期、追加コミット、生成物更新などにより検証後の内容が変わった場合は、PR タイトルと本文を書く前に Phase 5 の検証を再実行する。

### Phase 7: PR タイトルと本文を書く
差分、コミット履歴、検証結果、Issue 本文から PR タイトルと本文を作る。

PR タイトルと本文の文章前提は `$writing` に従う。
PR タイトルと本文は同じ言語で書く。
PR タイトルに Issue 番号、scope、`type(scope):` 風の prefix を付ける場合も、prefix 後のタイトル本文は本文言語、固有名詞、識別子、既存 PR の表記に合わせる。
本文は `##` 見出しと短い箇条書きを基本にする。
本文の基本順は `Summary`、`Scope`、`Verification` にする。
任意セクションは、必要な場合だけ次の表の順に差し込む。
本文の見出しは本文の言語に合わせて翻訳しない。
`Closes #<N>` のような GitHub の構文は原文のまま置く。

| 見出し | 必須 | 内容 |
| --- | --- | --- |
| `Summary` | 必須 | 変更の要点。レビュー前に把握すべき結論を短く書く。 |
| `Changelog` | 任意 | 利用判断に使う変更説明が必要な場合に置く。項目は `$changelog` で作り、見出しはこのスキルで置く。 |
| `Scope` | 必須 | 変更した範囲、生成物、影響する機能や文書を列挙する。 |
| `Verification` | 必須 | 実行した検証、確認できた結果、実行できなかった検証を書く。 |
| `References` | 任意 | レビュー時に確認すべき資料、仕様、外部URL、リポジトリ内の相対パスを書く。ローカル環境の絶対パスは書かない。 |
| `Risks / Rollback` | 任意 | レビュー判断に必要な具体的なリスク、未確認事項、通常のrevertだけでは足りない戻し方を書く。 |

`Changelog` の本文は `$changelog` の埋め込み用エントリとして作る。
`## Changelog` はこのスキルで置き、`$changelog` 側では重複させない。
カテゴリ見出しが必要な場合は、`## Changelog` の下に `### Added` のような1段下の見出しを置く。
空の任意セクションは置かない。
長い補足やログを本文に含める必要がある場合だけ `<details>` を使い、通常の説明は箇条書きまたは短い段落で書く。

本文の形は次を基準にし、任意セクションは必要なものだけ残す。

```md
## Summary
- <summary item>

## Changelog
<$changelog が作る埋め込み用エントリ>

## Scope
- <scope item>

## Verification
- `<command or check>`

## References
- <repository-relative path or URL>

## Risks / Rollback
- <risk or rollback item>
```

### Phase 8: PR を作成または更新する
同じブランチの open PR がある場合は、その PR を更新する。
open PR がない場合は `gh pr create` で作成する。

Issue があり、受け入れ条件が未完了または未確認なら Draft PR にする。
Issue があり、受け入れ条件が完了している場合は通常 PR にする。
Issue がない場合は、検証が成功していれば通常 PR にする。

### Phase 9: 状態を残す
PR URL、base branch、検証結果、push 結果、Draft 状態、Issue 連携、停止理由を残す。
