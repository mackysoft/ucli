# Test Size Policy

## 定義
- Small: 1プロセス内で完結する。
- Medium: 1マシン内だが複数プロセスまたは境界連携を含む。
- Large: ネットワーク越し、または1マシン外に依存する。

## 必須ルール
- 全ての新規/更新テストへサイズ属性を付与する。
- テスト名やXMLドキュメントにサイズを埋め込まない。
- Large を選ぶ場合は、Small/Medium で代替できない理由を明記する。

## 付与方法
### xUnit
```csharp
[Trait("Size", "Small")]
```
許容値は `Small` / `Medium` / `Large` のみ。

### NUnit
```csharp
[Category("Size.Small")]
```
許容値は `Size.Small` / `Size.Medium` / `Size.Large` のみ。

## 判定の目安
- デフォルトは Small。
- Medium と Large は必要性を契約単位で説明する。
