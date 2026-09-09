# Collection model checks

Latest total: 42 checks, including actual AutofacBusinessModule IServiceCollection registration, scoped read-service lifetime and configuration binding/default-disabled options. Business sources are now consumed by project reference; controller and authorization sources remain linked. No hosted services are started by the registration tests.

Current suite: 39 model/query/API checks. Controller actions are called directly; authorization metadata is inspected, not exercised through an HTTP server. Production controller, read-service and authorization sources are linked into this test assembly without running WebAPI Program/startup seeds. Default-disabled actions and missing-model service guards are verified without database access. SQL ToQueryString checks verify translation, not performance or database constraint enforcement.

Run from the backend directory:

```powershell
dotnet run --project tools/Collections.Model.Tests/Collections.Model.Tests.csproj
```

Framework-free console checks, matching the existing tools convention; not a `dotnet test` discovery project.

Uses the actual AppDataContext model and a test-only derived context applying CollectionContractConfiguration. SQL Server provides conventions without a connection string. No API host, connection opening, migration, seed, EnsureCreated or SaveChanges is invoked.

19 checks cover contract and payment-frequency schemas, identity, required shared references with NoAction deletion, no inverse navigation, rowversion, dates, reference lengths, indexes, draft isolation and a comparison of existing column/index metadata. The test-only context applies both collection configurations. No frequency seed or automatic activation is applied. The comparison is intentionally limited to the attributes selected in Shape; it is not a complete migration-diff or database schema validation. Constraint presence is checked, not database enforcement. SQL execution plans, concurrent writes and actual rowversion updates require a separately authorized isolated database test.

The test project explicitly references EF Relational 9.0.5 to match Data's compiled dependency; no production package versions were changed.
