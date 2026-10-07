# Service notification mail regression checks

Run from `AssistFlow-BE`:

```powershell
dotnet build tools/ServiceNotificationMail.Tests/ServiceNotificationMail.Tests.csproj -m:1 -p:OutputPath=bin/service-mail-check/
dotnet tools/ServiceNotificationMail.Tests/bin/service-mail-check/ServiceNotificationMail.Tests.dll
```

Runs the four tenants' actual technician/warehouse mail builders and the SLA dispatcher with an in-memory SQLite database and the real mail outbox queue. Covers subscriber versus contact names, Turkish characters, multiline and missing names, tenant-specific customer selection, and duplicate SLA prevention for old subjects and renamed customers. No SMTP dispatcher, application configuration or live database is used.
