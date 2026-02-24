---
name: push
description: push 指示を受けたときに、未コミット差分があれば `$commit` で責務単位コミットを作成し、現在ブランチを upstream 優先で安全に push する。upstream が無い場合は `origin/<current_branch>` を `-u` で設定して push する。
---

# 目的
- push 手順を固定し、コミット漏れ・push 先誤り・危険な push の再発を防ぐ。
- プッシュ依頼に対して、同じ安全基準で実行できる状態を作る。

# 使うタイミング
- 「pushして」「プッシュして」と指示されたとき
- コミットが未作成の可能性を含む状態で、現在ブランチを確実にリモート反映したいとき

# 入力（任意。無くても推定して進める）
- push 対象ブランチ（未指定時は現在ブランチ）
- push 先リモート（未指定時は upstream 優先）

# 出力
- `$commit` 実行有無（実行時は作成コミット情報を含む）
- push 実行結果
- push 対象なしで停止した場合の理由

# 手順

## 1. 前提を確認する
1. `git symbolic-ref --quiet HEAD` で detached HEAD でないことを確認する。detached HEAD なら停止する。
2. 現在ブランチ名を取得する。
3. `origin` が存在することを確認する。存在しない場合は停止する。

## 2. 変更状態を確認する
1. `git status --porcelain` を確認する。
2. 未コミット差分がある場合は `$commit` を呼び、責務単位でコミットを作成する。

## 3. push 対象の有無を判定する
1. 現在ブランチの upstream があるかを確認する。
2. upstream がある場合は `git rev-list --count @{upstream}..HEAD` で ahead 件数を確認する。
3. upstream があり ahead が `0` なら、push 対象なしとして停止する。

## 4. push を実行する
1. upstream がある場合は `git push` を実行する。
2. upstream が無い場合は `git push -u origin <current_branch>` を実行する。

## 5. 安全規約
1. `--force` / `--force-with-lease` は使用しない。
2. push 失敗時は再試行前に失敗理由を確認し、同条件での無限再試行をしない。

# Definition of Done
- 未コミット差分がある場合に `$commit` が実行されている
- upstream 優先で push が実行されている
- upstream が無い場合に `origin/<current_branch>` が `-u` で設定されている
- push 対象が無い場合に理由付きで停止している
