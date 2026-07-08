---
name: "commit"
description: "変更を責務単位で分割し、Conventional Commits形式の包括的なメッセージでコミットを作成する。"
---

# commit

## 目的
未コミット差分を責務単位に分け、Conventional Commits 形式でコミットする。

## フロー

### Phase 1: 作業状態を確認する
`git status --porcelain` を確認する。
変更がなければコミットを作成しない。
detached HEAD なら停止する。

### Phase 2: コミット単位を決める
1コミットは1つの責務、1つの変更理由に揃える。
変更ファイルと差分を読み、1コミット1責務になるように分ける。
異なる type、異なる scope、異なる目的の変更は分割を優先する。
分割できない場合は理由を残す。

type は次から選ぶ。

| 変更の性質 | Conventional Commit type |
| --- | --- |
| 利用者に見える機能追加 | `feat` |
| バグ修正 | `fix` |
| 振る舞いを変えない再構成 | `refactor` |
| 性能改善 | `perf` |
| テスト | `test` |
| ドキュメント | `docs` |
| CI 設定 | `ci` |
| build、依存、リリース設定 | `build` |
| 書式だけの変更 | `style` |
| その他 | `chore` |

### Phase 3: メッセージを作る
件名は `type(scope): <subject>` 形式にする。
scope は主要なモジュール名、または横断変更の対象領域にする。
件名と本文は `$writing` で文章前提を固定してから書く。
`type` と `scope` は Conventional Commits の規約語彙として小文字にする。
`<subject>` は件名の言語、固有名詞、識別子、既存履歴の表記に合わせ、prefix 後の先頭も自然な表記を保つ。
既存コミット履歴がある場合は粒度を合わせる。

本文には `Why`、`What Changed`、`Impact` を入れる。
本文の見出しはこの3つに固定し、本文の言語に合わせて翻訳しない。
作業ログや検討過程を並べない。

### Phase 4: コミットする
コミット単位ごとに対象ファイルだけを stage する。
`git commit -m "<subject>" -m "<body>"` で作成する。

### Phase 5: 状態を残す
作成したコミット、残った未コミット差分、分割できなかった理由を残す。
