# Tenant dashboards

The ADMIN home page shows an operation summary for currently permitted modules only. Multiple modules add a total tab and tenant tabs; one module renders without tabs. Non-ADMIN home behavior remains on the legacy dashboard.

Four tenant menu pages expose their operation dashboard when the user has the corresponding `ServiceDashboard`, `YkbDashboard`, `EkbDashboard` or `QnbDashboard` view grant. Technician-only grants continue to show assigned requests, never global operations. The individual tenant page retains its detailed financial/product/geographic dashboard.

Operation totals count non-deleted requests using one Istanbul calendar date range. Completed, pending and cancelled counts are separate. Stage counts include pending requests only. No financial aggregation is introduced.

## Deployment

Deploy frontend and backend together. `TenantDashboardSeed` is registered in startup, after EKB seeding. No schema migration is needed. It creates menu records and migrates legacy `Dashboard` grants to the three APIs those grants already allowed (individual/YKB/EKB). Legacy ADMIN also receives the new QNB operation grant. Explicit existing denied grants are respected. A versioned `SeedHistory` entry ensures revoked grants are not restored on later runs. Roles without legacy Dashboard grants receive no new general data access.

The former YKB/EKB KPI endpoints now require their own tenant view grants. The aggregate endpoint additionally requires active ADMIN assignment. API scope is resolved from database grants, not tenant codes supplied by the client. Users should refresh their session after rollout so the frontend receives the new menu list.

## Verification

From backend root:

```powershell
dotnet restore tools/TenantDashboard.Tests/TenantDashboard.Tests.csproj
dotnet build tools/TenantDashboard.Tests/TenantDashboard.Tests.csproj -m:1 -p:OutputPath=bin/tenant-dashboard-build/
dotnet tools/TenantDashboard.Tests/bin/tenant-dashboard-build/TenantDashboard.Tests.dll
```

The executable creates a unique `MgsTenantDashboardTests_<guid>` LocalDB database and deletes only that database in `finally`. It never reads production connection settings. It checks actual EF SQL queries for all four modules, shared date boundaries/status semantics, permission isolation, immediate revocation, admin checks and seed idempotency.

From frontend root:

```powershell
node --test tools/tenantDashboard.test.mjs
npm run build
npm run lint
```

Manual checks: ADMIN four tenant tabs, restricted ADMIN two/one/no tenant permissions, QNB standalone page, technician-only page, denied tenant URL, common date filters, zero counts, error/retry display, mobile layout. No live rollout is performed by these commands.

Verified locally: frontend/backend builds; 28 isolated SQL/permission/seed checks and 5 frontend checks; browser checks using mocked API data for four/two/one/no permissions, QNB operations, assigned technician requests, individual detailed dashboard, invalid dates, all-time clearing, empty periods and retry after a failed response. Mobile viewport verification was not performed. Existing whole-project lint/TypeScript diagnostics remain; HEAD comparison found no new diagnostics (TypeScript 133 current versus 134 baseline).
