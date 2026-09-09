# Collection offline checks

Run from the backend root:

```powershell
dotnet run --project tools/Collections.Tests/Collections.Tests.csproj
dotnet build AssistFlow-BE.sln --no-restore
```

This executable follows the repository's existing tools-based smoke-check pattern.
It compiles the production CollectionPeriodRules source directly and exercises it
without starting WebAPI, opening a database connection or running a migration.
It is not a dotnet test-discovered project; failures throw and exit nonzero.
The solution build separately verifies integration into the Business project.

Coverage includes all eight legacy month intervals, leap dates, anchored window
queries, half-open ends, invalid inputs, DateOnly limits, duplicate source keys,
adjacent/nested/open rate periods. The overlap result contains one witness per
conflicting row, not all overlapping pairs.

Query checks cover the default page, maximum page size, offset overflow, invalid
sort/view enum values, complete/ordered date ranges, customer identifiers and
search length. There are 34 checks across the calendar and query primitives.

The calendar utility does not decide whether a customer is eligible for billing,
whether a frozen subscription is billable, how exchange rates apply, or which
historical periods should migrate. Those rules remain outside this primitive.
Supporting the 4/36-month cadence does not activate those options in the UI.

No endpoint or UI uses this utility yet. Finishing these checks is not evidence
of production financial reconciliation, database pagination or load performance.
