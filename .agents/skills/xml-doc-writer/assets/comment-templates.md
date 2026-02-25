# コメントテンプレート

## 同期メソッド
```csharp
/// <summary> 
/// Performs the operation and updates the target state.
/// </summary>
/// <param name="input"> Non-empty input value used by the operation. </param>
/// <returns> The computed result. Never <c>null</c>. </returns>
/// <exception cref="ArgumentException"> Thrown when <paramref name="input" /> is empty. </exception>
public Result Execute (string input)
{
}
```

## 非同期メソッド（CancellationTokenあり）
```csharp
/// <summary>
/// Loads the catalog into memory.
/// </summary>
/// <returns> A task that completes when all catalog entries are available for lookup. </returns>
/// <exception cref="HttpRequestException"> Thrown when the download request fails. </exception>
public Task LoadCatalogAsync (CancellationToken cancellationToken = default)
{
}
```

## 複数行 summary
```csharp
/// <summary>
/// <para> Loads all definitions from disk and updates the cache. </para>
/// <para> The operation completes when lookups return the new definitions. </para>
/// </summary>
public void Reload ()
{
}
```

## プロパティ
```csharp
/// <summary>
/// Gets the current balance snapshot.
/// </summary>
/// <returns> The latest balance value. Never <c>null</c>. </returns>
public Money Balance { get; }
```
