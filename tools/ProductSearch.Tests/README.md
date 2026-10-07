# Product catalogue search regression checks

Run from `AssistFlow-BE`:

```powershell
dotnet run --project tools/ProductSearch.Tests/ProductSearch.Tests.csproj -p:OutputPath=bin/product-search-check/
```

Requires SQL Server LocalDB. The runner creates and removes its own `MgsProductSearchTests_<GUID>` database. It never reads application connection strings.

Checks the real `ProductService` queries for four tenant catalogues, metadata and system name/code matching (including legacy records), price precedence, numeric formats, nulls, deleted and unavailable products, pagination, the general catalogue, purchase products and the customerless fallback.
