# Diff Budget thresholds（初期値）

この閾値は誤った巨大PRを止めるためのガード。
正当な例外がある場合は Issue に Split waived（理由付き）を明記する。

## Small（Micro）
- 変更ファイル数: 2 以下
- 変更規模: 概ね 50 行程度以下

## Medium（Normal）
- 変更ファイル数: 10 以下
- 変更規模: 概ね 400 行程度以下
- レイヤー跨ぎ: 原則 1 回以内（UI↔Domain↔Infra 等）

## Large（Large）
- 上記を超える
- または 新規概念導入 / レイヤー跨ぎ複数 / 公開APIやデータ形式へ影響
  - 原則: 分割して子Issue/子PRへ落とす
